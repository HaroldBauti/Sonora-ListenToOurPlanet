using System.Globalization;
using System.Text.RegularExpressions;
using SONORA.Client.Models;

namespace SONORA.Client.Services;

/// <summary>Etapa de la guía hablada.</summary>
public enum GuideStage { Menu, Choose, Loading, Ready, Error, Demo }

/// <summary>Opción que se puede elegir por número, por nombre, con el teclado o tocándola.</summary>
public sealed record GuideOption(string Id, string Label, string? Detail = null, bool Recommended = false);

/// <summary>Visualización del Jukebox tal como la necesita la guía (catálogo del EIC más el ejemplo de la NASA).</summary>
public sealed record GuideEic(string Id, string Titulo, string Tema, string Periodo, string Fuente, string Descripcion, string Contexto = "");

public sealed record GuidePlace(string Id, string Nombre);

/// <summary>Catálogos reales que la guía presenta. Se rellenan cuando la app los descarga.</summary>
public sealed class GuideData
{
    public List<GuideEic> Eic { get; set; } = [];
    public BiomasCatalog? Biomas { get; set; }
    public TendenciasCatalog? Tendencias { get; set; }
    public List<GuidePlace> Places { get; set; } = [];
    public int WorldMin { get; set; } = 2001;
    public int WorldMax { get; set; } = 2024;
}

/// <summary>Lo que hay que cargar cuando la selección está completa.</summary>
public sealed record GuideRequest(string Section, IReadOnlyDictionary<string, string> Values);

public enum GuideCommandKind
{
    None, Select, Ambiguous, Prefilled, More, Less, Back, Menu, Repeat, Where, WhatCanIDo, Change, Retry, Recommend,
    Explain, ExplainMore, Demo
}

/// <summary>Orden de la guía interpretada a partir de lo que dijo o escribió la persona.</summary>
public sealed record GuideCommand(GuideCommandKind Kind, string? Value = null, IReadOnlyList<GuideOption>? Options = null);

/// <summary>
/// Navegación guiada: secciones reales de SONORA → datos que hacen falta (región, año, variables…) → carga → explicación.
/// Presenta las opciones en grupos cortos, reutiliza lo que la persona ya dijo, permite volver o corregir una elección y
/// nunca elige contenido al azar (solo propone algo si se le pide una recomendación).
/// No depende de Blazor ni de JavaScript: Home.razor ejecuta lo que esta clase decide y las pruebas la usan directamente.
/// </summary>
public sealed class GuideFlow
{
    public const int PageSize = 5;
    static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-ES");

    public GuideData Data { get; } = new();
    public GuideStage Stage { get; private set; } = GuideStage.Menu;
    public string? Section { get; private set; }
    public int Page { get; private set; }
    public string? LoadedTitle { get; private set; }
    public string? ErrorMessage { get; private set; }
    /// <summary>Cambia con cada transición (la interfaz lo usa para mover el foco a las opciones nuevas).</summary>
    public int Version { get; private set; }
    public List<GuideOption> DemoLayers { get; private set; } = [];

    readonly Dictionary<string, Dictionary<string, string>> memory = [];
    List<GuideOption>? clarify;
    /// <summary>Hay que aclarar entre varias opciones parecidas.</summary>
    public bool Clarifying => clarify is not null;

    Dictionary<string, string> V => Section is null ? [] : memory.TryGetValue(Section, out var d) ? d : memory[Section] = [];

    /// <summary>Valores elegidos en la sección actual (solo lectura).</summary>
    public IReadOnlyDictionary<string, string> Values => V;

    // ---------- Secciones y datos que pide cada una ----------

    public static readonly IReadOnlyList<(string Id, string Name)> SectionNames =
        [("eic", "Jukebox NASA"), ("archivo", "Tu archivo"), ("biomas", "Biomas de Perú"), ("clima", "Clima de Perú"), ("mundo", "Todo el planeta")];

    public static string SectionName(string? id) => SectionNames.FirstOrDefault(s => s.Id == id).Name ?? "";

    public List<GuideOption> Sections() =>
    [
        new("eic", "Jukebox NASA", Data.Eic.Count > 0 ? $"{Data.Eic.Count} visualizaciones de la Tierra de la NASA" : "Visualizaciones de la Tierra de la NASA"),
        new("archivo", "Tu archivo", "Una imagen o un video tuyo; se procesa en tu navegador"),
        new("biomas", "Biomas de Perú", "Mapas de biomas y del cambio del bosque, hechos con una red neuronal y datos de la NASA"),
        new("clima", "Clima de Perú", "Cómo cambió el clima de una región de Perú, año por año"),
        new("mundo", "Todo el planeta", "Cobertura terrestre de la NASA de un lugar del mundo"),
    ];

    static string SlotName(string slot) => slot switch
    {
        "tema" => "tema", "item" => "visualización", "tipo" => "mapa", "region" => "región", "periodo" => "periodo",
        "anio" => "año", "fuente" => "datos", "vars" => "variables", "pick" => "variables", "lugar" => "lugar", _ => slot
    };

    /// <summary>Pregunta de un dato (sin la lista de opciones).</summary>
    public string Question(string slot) => slot switch
    {
        "tema" => "¿Qué tema te interesa?",
        "item" => "¿Qué visualización quieres escuchar?",
        "tipo" => "¿Qué mapa quieres?",
        "region" => "¿Qué región?",
        "periodo" => "¿Qué periodo?",
        "anio" => $"¿Qué año? Puedes decir o escribir un año de {YearRange().Min} a {YearRange().Max}.",
        "fuente" => "¿Qué datos quieres usar?",
        "vars" => "¿Qué variables quieres escuchar? Suenan hasta 4.",
        "pick" => $"Elige hasta 4 variables; elige otra vez una variable para quitarla.{(Picks.Count > 0 ? $" Elegidas: {string.Join(", ", Picks.Select(VarName))}." : "")}",
        "lugar" => "¿Qué lugar del planeta?",
        _ => ""
    };

    (int Min, int Max) YearRange() => Section == "mundo"
        ? (Data.WorldMin, Data.WorldMax)
        : (Data.Biomas?.AnioMin ?? 2001, Data.Biomas?.AnioMax ?? 2024);

    bool TipoUsaAnio => Data.Biomas?.Tipos.FirstOrDefault(t => t.Id == V.GetValueOrDefault("tipo"))?.UsaAnio ?? true;

    /// <summary>Datos necesarios, en orden. Solo se pide el año si el mapa lo usa, y las variables sueltas si se eligen a mano.</summary>
    public List<string> Required()
    {
        var r = new List<string>();
        switch (Section)
        {
            case "eic": r.AddRange(["tema", "item"]); break;
            case "biomas":
                r.AddRange(["tipo", "region"]);
                if (!V.ContainsKey("tipo") || TipoUsaAnio)
                {
                    r.Add("periodo");
                    if (V.GetValueOrDefault("periodo") == "anio") r.Add("anio");
                }
                break;
            case "clima":
                r.AddRange(["region", "fuente", "vars"]);
                if (V.GetValueOrDefault("vars") == "elegir") r.Add("pick");
                break;
            case "mundo":
                r.AddRange(["lugar", "periodo"]);
                if (V.GetValueOrDefault("periodo") == "anio") r.Add("anio");
                break;
        }
        return r;
    }

    public string? CurrentSlot => Stage == GuideStage.Choose ? Required().FirstOrDefault(s => !V.ContainsKey(s)) : null;

    public bool IsComplete => Section is not null and not "archivo" && Required().All(V.ContainsKey);

    List<string> Picks => V.TryGetValue("picks", out var p) && p.Length > 0 ? [.. p.Split(',')] : [];

    string VarName(string code) =>
        ClimaRegion?.Variables.FirstOrDefault(v => v.Fuente == V.GetValueOrDefault("fuente") && v.Codigo == code)?.Nombre ?? code;

    TendenciasRegion? ClimaRegion => Data.Tendencias?.Regiones.FirstOrDefault(r => r.Id == V.GetValueOrDefault("region"));

    // ---------- Opciones ----------

    /// <summary>Opciones de la etapa actual (todas; la interfaz y la voz las presentan de <see cref="PageSize"/> en <see cref="PageSize"/>).</summary>
    public List<GuideOption> Options()
    {
        if (clarify is not null) return clarify;
        return Stage switch
        {
            GuideStage.Menu => Sections(),
            GuideStage.Demo => [.. DemoLayers, new("__all", "Todas las capas, una por una")],
            GuideStage.Choose when Section == "archivo" => [new("__file", "Elegir un archivo de imagen o video")],
            GuideStage.Choose => SlotOptions(CurrentSlot),
            _ => []
        };
    }

    List<GuideOption> SlotOptions(string? slot)
    {
        var (min, max) = YearRange();
        switch (slot)
        {
            case "tema":
                return [new("todos", "Todos los temas", $"{Data.Eic.Count} visualizaciones"),
                        .. Data.Eic.GroupBy(e => e.Tema).Select(g => new GuideOption(g.Key, g.Key, $"{g.Count()} visualizaci{(g.Count() == 1 ? "ón" : "ones")}"))];
            case "item":
                string tema = V.GetValueOrDefault("tema") ?? "todos";
                return [.. Data.Eic.Where(e => tema == "todos" || e.Tema == tema).Select(e => new GuideOption(e.Id, e.Titulo, $"{e.Periodo} · {e.Fuente}"))];
            case "tipo":
                return [.. (Data.Biomas?.Tipos ?? []).Select(t => new GuideOption(t.Id, t.Nombre, t.Descripcion))];
            case "region" when Section == "biomas":
                return [.. (Data.Biomas?.Regiones ?? []).Select(r => new GuideOption(r.Id, r.Nombre))];
            case "region":
                return [.. (Data.Tendencias?.Regiones ?? []).Select(r => new GuideOption(r.Id, r.Nombre, r.Bioma))];
            case "periodo":
                return [new("ultimo", $"{max}, el año más reciente", null, Recommended: true),
                        new("anio", "Elegir otro año", $"De {min} a {max}"),
                        new("serie", $"De {min} a {max}, año por año", "Una serie: cada paso musical es un año")];
            case "anio":
                return [.. Enumerable.Range(min, max - min + 1).Reverse().Select(y => new GuideOption(y.ToString(Es), y.ToString(Es)))];
            case "fuente":
                return [.. (Data.Tendencias?.Fuentes ?? []).Select(f => new GuideOption(f.Id, f.Nombre, $"{f.AnioMin} a {f.AnioMax}"))];
            case "vars":
                return [new("red", "Las 4 que sugiere la red neuronal", "Las variables que más parecen cambiar en esta región", Recommended: true),
                        new("elegir", "Elegir las variables una por una", "Hasta 4")];
            case "pick":
                var picks = Picks;
                var vars = ClimaRegion?.Variables.Where(v => v.Fuente == V.GetValueOrDefault("fuente")) ?? [];
                var list = vars.Select(v => new GuideOption(v.Codigo, (picks.Contains(v.Codigo) ? "Quitar " : "") + v.Nombre,
                    v.Tendencia switch { "sube" => "los datos suben", "baja" => "los datos bajan", _ => "sin tendencia clara" })).ToList();
                if (picks.Count > 0) list.Insert(0, new("__listo", $"Listo: escuchar {picks.Count} variable{(picks.Count == 1 ? "" : "s")}"));
                return list;
            case "lugar":
                return [.. Data.Places.Select(p => new GuideOption(p.Id, p.Nombre))];
            default:
                return [];
        }
    }

    public int PageCount => Math.Max(1, (Options().Count + PageSize - 1) / PageSize);
    public bool HasMore => Page < PageCount - 1;
    public bool HasLess => Page > 0;

    /// <summary>Opciones del grupo actual con su número (los números son absolutos: «la 7» vale en cualquier grupo).</summary>
    public List<(int Number, GuideOption Option)> PageOptions() =>
        [.. Options().Select((o, i) => (i + 1, o)).Skip(Page * PageSize).Take(PageSize)];

    // ---------- Transiciones ----------

    void Touch() => Version++;

    public string Start()
    {
        Stage = GuideStage.Menu; Section = null; Page = 0; clarify = null; Touch();
        return Prompt();
    }

    public string Menu() => Start();

    string EnterSection(string id)
    {
        Section = id; Page = 0; clarify = null;
        if (id != "archivo" && IsComplete)
        {
            // Volver a una sección ya completa: se conservan los datos y se repregunta el último.
            V.Remove(Required()[^1]);
        }
        Stage = GuideStage.Choose;
        Touch();
        return Prompt();
    }

    /// <summary>Elige una opción por su id. Si la selección queda completa pasa a «Cargando» (la app carga <see cref="Request"/>).</summary>
    public string Select(string id)
    {
        clarify = null;
        if (Stage == GuideStage.Menu)
        {
            if (Sections().All(s => s.Id != id)) return $"No encontré esa sección. {Prompt()}";
            return EnterSection(id);
        }
        if (Stage != GuideStage.Choose || Section == "archivo") return Prompt();
        string? slot = CurrentSlot;
        if (slot is null) return Prompt();
        var opts = SlotOptions(slot);
        if (id.StartsWith("year:", StringComparison.Ordinal)) return SelectYear(int.Parse(id[5..], Es));
        if (opts.All(o => o.Id != id)) return $"Esa opción no está aquí. {Prompt()}";

        switch (slot)
        {
            case "pick":
                if (id == "__listo") { V["pick"] = V.GetValueOrDefault("picks") ?? ""; break; }
                var picks = Picks;
                if (!picks.Remove(id))
                {
                    if (picks.Count >= 4) return $"Ya elegiste 4 variables. Quita una o di «listo». {Prompt()}";
                    picks.Add(id);
                }
                V["picks"] = string.Join(',', picks);
                Touch();
                return Prompt();
            case "tema": V["tema"] = id; V.Remove("item"); break;
            case "region" or "fuente" when Section == "clima": V[slot] = id; V.Remove("pick"); V.Remove("picks"); break;
            default: V[slot] = id; break;
        }
        Page = 0;
        return Advance();
    }

    string SelectYear(int y)
    {
        var (min, max) = YearRange();
        if (y < min || y > max) return $"Ese año no está disponible: elige uno de {min} a {max}.";
        V["periodo"] = "anio"; V["anio"] = y.ToString(Es); Page = 0;
        return Advance();
    }

    string Advance()
    {
        Touch();
        if (IsComplete) { Stage = GuideStage.Loading; return $"Has elegido {SelectionSummary()}. Cargando."; }
        Stage = GuideStage.Choose;
        return Prompt();
    }

    /// <summary>Lo que la app debe cargar (null si falta algún dato). El periodo «más reciente» se convierte en su año.</summary>
    public GuideRequest? Request()
    {
        if (!IsComplete) return null;
        var v = new Dictionary<string, string>(V);
        if (v.GetValueOrDefault("periodo") == "ultimo") v["anio"] = YearRange().Max.ToString(Es);
        if (Section == "biomas" && !TipoUsaAnio) { v.Remove("periodo"); v.Remove("anio"); }
        return new(Section!, v);
    }

    public string Loaded(string title)
    {
        Stage = GuideStage.Ready; LoadedTitle = title; ErrorMessage = null; Touch();
        return Prompt();
    }

    /// <summary>La carga falló: se conservan las elecciones para reintentar, corregir o volver.</summary>
    public string Fail(string message)
    {
        Stage = GuideStage.Error; ErrorMessage = message; Touch();
        return Prompt();
    }

    /// <summary>Algo se cargó fuera de la guía (pestañas, botones, órdenes): la guía pasa a «listo» con eso.</summary>
    public void LoadedExternally(string? section, string title)
    {
        if (section is not null) Section = section;
        Stage = GuideStage.Ready; LoadedTitle = title; clarify = null; Touch();
    }

    public string Retry()
    {
        if (Stage != GuideStage.Error || !IsComplete) return Prompt();
        Stage = GuideStage.Loading; Touch();
        return $"Vuelvo a intentarlo: {SelectionSummary()}. Cargando.";
    }

    public string Back()
    {
        if (clarify is not null) { clarify = null; Touch(); return Prompt(); }
        switch (Stage)
        {
            case GuideStage.Menu:
                return $"Ya estás en el menú principal. {Prompt()}";
            case GuideStage.Demo:
                Stage = GuideStage.Ready; Touch();
                return Prompt();
            case GuideStage.Choose:
                var filled = Required().TakeWhile(V.ContainsKey).ToList();
                if (CurrentSlot == "pick" && Picks.Count > 0) { V.Remove("picks"); Touch(); return Prompt(); }
                if (filled.Count == 0) return Start();
                ClearSlot(filled[^1]);
                Page = 0; Touch();
                return Prompt();
            default:
                var req = Required();
                if (Section == "archivo" || req.Count == 0) return Start();
                ClearSlot(req[^1]);
                Stage = GuideStage.Choose; Page = 0; Touch();
                return Prompt();
        }
    }

    void ClearSlot(string slot)
    {
        V.Remove(slot);
        if (slot == "tema") V.Remove("item");
        if (slot is "region" or "fuente" && Section == "clima") { V.Remove("pick"); V.Remove("picks"); }
        if (slot == "vars") { V.Remove("pick"); V.Remove("picks"); }
        if (slot == "pick") V.Remove("picks");
        if (slot == "periodo") V.Remove("anio");
    }

    /// <summary>Corregir una elección concreta sin perder las demás.</summary>
    public string Change(string slot)
    {
        if (Section is null) return $"Primero elige una sección. {Prompt()}";
        if (slot == "anio" && Required().Contains("periodo")) slot = "periodo";
        if (!Required().Contains(slot)) return $"Aquí no hay {SlotName(slot)} que cambiar. {Prompt()}";
        ClearSlot(slot);
        Stage = GuideStage.Choose; Page = 0; clarify = null; Touch();
        return Prompt();
    }

    public string More()
    {
        if (Options().Count == 0) return Prompt();
        if (!HasMore) return $"No hay más opciones. {ListPage()}";
        Page++; Touch();
        return ListPage();
    }

    public string Less()
    {
        if (Options().Count == 0) return Prompt();
        if (!HasLess) return $"Estas son las primeras opciones. {ListPage()}";
        Page--; Touch();
        return ListPage();
    }

    public string EnterDemo(List<GuideOption> layers)
    {
        DemoLayers = layers; Stage = GuideStage.Demo; Page = 0; clarify = null; Touch();
        return Prompt();
    }

    public string Recommend()
    {
        var opts = Options();
        if (opts.Count == 0) return Prompt();
        var rec = opts.FirstOrDefault(o => o.Recommended) ?? opts[0];
        return $"Te propongo: {rec.Label}. " + (Stage == GuideStage.Demo ? "" : Select(rec.Id));
    }

    public string Clarify(IReadOnlyList<GuideOption> candidates)
    {
        clarify = [.. candidates]; Page = 0; Touch();
        return $"Hay varias opciones parecidas. ¿Cuál quieres? {ListPage()}";
    }

    // ---------- Textos ----------

    public string SelectionSummary()
    {
        var parts = Chosen().Select(c => $"{c.SlotName} {c.Value}").ToList();
        return SectionName(Section) + (parts.Count > 0 ? ": " + string.Join(", ", parts) : "");
    }

    /// <summary>Elecciones hechas en la sección actual, para mostrarlas con un botón «Cambiar».</summary>
    public List<(string Slot, string SlotName, string Value)> Chosen()
    {
        var res = new List<(string, string, string)>();
        if (Section is null) return res;
        foreach (var slot in Required().Where(V.ContainsKey))
        {
            string val = V[slot];
            string label = slot switch
            {
                "periodo" => val switch { "ultimo" => $"el más reciente ({YearRange().Max})", "serie" => $"año por año ({YearRange().Min}–{YearRange().Max})", _ => "un año concreto" },
                "pick" => string.Join(", ", val.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(VarName)),
                "tema" when val == "todos" => "todos",
                _ => SlotOptions(slot).FirstOrDefault(o => o.Id == val)?.Label ?? val
            };
            if (slot == "periodo" && val == "anio") continue; // se dice con el año
            res.Add((slot, SlotName(slot), label));
        }
        return res;
    }

    string ListPage()
    {
        var all = Options();
        if (all.Count == 0) return "";
        var page = PageOptions();
        bool details = Stage == GuideStage.Menu || CurrentSlot is "tipo" or "periodo" or "vars";
        string items = string.Join(" ", page.Select(p => $"{p.Number}, {p.Option.Label}{(details && p.Option.Detail is { Length: > 0 } d ? $": {d}" : "")}."));
        string head = all.Count <= PageSize ? "" : $"Opciones {page[0].Number} a {page[^1].Number} de {all.Count}. ";
        string tail = HasMore ? " Di «más opciones» para escuchar más." : "";
        return head + items + tail;
    }

    /// <summary>Lo que la guía dice en la etapa actual (también se muestra en pantalla).</summary>
    public string Prompt()
    {
        if (clarify is not null) return $"¿Cuál quieres? {ListPage()}";
        switch (Stage)
        {
            case GuideStage.Menu:
                return $"Menú principal. ¿Qué quieres escuchar? {ListPage()} Elige con el número, el nombre, el teclado o tocando la opción.";
            case GuideStage.Choose when Section == "archivo":
                return "Tu archivo. Pulsa «Elegir un archivo de imagen o video» y selecciónalo; se procesa en tu navegador y no se sube a ningún servidor. Para volver, di «volver».";
            case GuideStage.Choose:
                var chosen = Chosen();
                string have = chosen.Count > 0 ? $"Tienes: {string.Join(", ", chosen.Select(c => $"{c.SlotName} {c.Value}"))}. " : "";
                return $"{SectionName(Section)}. {have}{Question(CurrentSlot ?? "")} {ListPage()}";
            case GuideStage.Loading:
                return $"Cargando {SelectionSummary()}.";
            case GuideStage.Ready:
                return $"Listo: {LoadedTitle}. Puedes reproducir, pedir una explicación sencilla o detallada, aprender a escuchar, cambiar el contenido o volver al menú.";
            case GuideStage.Error:
                return $"No se pudo cargar {SelectionSummary()}: {ErrorMessage}. Tus elecciones se conservan: puedes reintentar, cambiar una elección o volver al menú.";
            case GuideStage.Demo:
                return $"Aprender a escuchar. Elige una capa para escucharla sola. {ListPage()}";
        }
        return "";
    }

    /// <summary>«¿Dónde estoy?»: sección, selección y estado actual.</summary>
    public string Where(string stateLabel, string? fallbackSection, string? loadedTitle, string? stepLabel)
    {
        string sec = SectionName(Section ?? fallbackSection);
        string where = Stage switch
        {
            GuideStage.Menu => "Estás en el menú principal de la guía.",
            GuideStage.Choose => $"Estás en {sec}, eligiendo {SlotName(CurrentSlot ?? "")}.",
            GuideStage.Demo => $"Estás en la demostración para aprender a escuchar de {loadedTitle ?? sec}.",
            _ => sec.Length > 0 ? $"Estás en {sec}." : "Estás en SONORA."
        };
        var chosen = Chosen();
        string sel = chosen.Count > 0 ? $" Selección: {string.Join(", ", chosen.Select(c => $"{c.SlotName} {c.Value}"))}." : "";
        string content = loadedTitle is { Length: > 0 } t ? $" Contenido cargado: {t}." : " Todavía no hay contenido cargado.";
        string state = stateLabel.Length > 0 ? $" Estado: {stateLabel.ToLower(Es)}{(stepLabel is { Length: > 0 } s ? $", en {s}" : "")}." : "";
        return where + sel + content + state;
    }

    /// <summary>«¿Qué puedo hacer aquí?»: acciones disponibles en el contexto actual.</summary>
    public string WhatCanIDo(bool hasContent, bool playing, bool micAvailable)
    {
        var acts = new List<string>();
        switch (Stage)
        {
            case GuideStage.Menu:
                acts.Add("elegir una sección por su número o su nombre");
                break;
            case GuideStage.Choose when Section == "archivo":
                acts.Add("pulsar «Elegir un archivo de imagen o video»");
                acts.Add("volver al menú");
                break;
            case GuideStage.Choose or GuideStage.Demo:
                acts.Add("elegir una opción por su número o su nombre");
                if (HasMore) acts.Add("pedir más opciones");
                if (HasLess) acts.Add("volver a las opciones anteriores");
                acts.Add("volver al paso anterior");
                if (Chosen().Count > 0) acts.Add("cambiar una elección, por ejemplo «cambiar " + Chosen()[0].SlotName + "»");
                acts.Add("ir al menú");
                break;
            case GuideStage.Error:
                acts.AddRange(["reintentar", "cambiar una elección", "volver al menú"]);
                break;
            case GuideStage.Loading:
                acts.Add("esperar a que termine la carga");
                break;
        }
        if (hasContent && Stage is GuideStage.Ready or GuideStage.Menu or GuideStage.Error)
        {
            acts.Add(playing ? "pausar" : "reproducir o continuar");
            acts.AddRange(["subir o bajar el volumen", "describir el momento actual", "pedir una explicación sencilla o detallada",
                           "aprender a escuchar cada capa"]);
            if (Stage == GuideStage.Ready) acts.AddRange(["cambiar el contenido", "volver al menú"]);
        }
        acts.Add("repetir o detener la explicación");
        acts.Add(micAvailable ? "usar el micrófono para dar órdenes" : "escribir órdenes (el micrófono no está disponible)");
        return "Aquí puedes: " + string.Join("; ", acts) + ".";
    }

    // ---------- Interpretación de órdenes ----------

    static bool Has(string t, params string[] phrases) => phrases.Any(p => $" {t} ".Contains($" {p} ", StringComparison.Ordinal));

    static readonly Dictionary<string, int> NumWords = new()
    {
        ["uno"] = 1, ["una"] = 1, ["primera"] = 1, ["primero"] = 1, ["dos"] = 2, ["segunda"] = 2, ["segundo"] = 2, ["tres"] = 3, ["tercera"] = 3,
        ["tercero"] = 3, ["cuatro"] = 4, ["cuarta"] = 4, ["cuarto"] = 4, ["cinco"] = 5, ["quinta"] = 5, ["quinto"] = 5, ["seis"] = 6, ["sexta"] = 6,
        ["siete"] = 7, ["septima"] = 7, ["ocho"] = 8, ["octava"] = 8, ["nueve"] = 9, ["novena"] = 9, ["diez"] = 10, ["decima"] = 10, ["once"] = 11,
        ["doce"] = 12, ["trece"] = 13, ["catorce"] = 14, ["quince"] = 15, ["dieciseis"] = 16, ["diecisiete"] = 17, ["dieciocho"] = 18,
        ["diecinueve"] = 19, ["veinte"] = 20, ["veintiuno"] = 21, ["veintidos"] = 22, ["veintitres"] = 23, ["veinticuatro"] = 24,
    };

    /// <summary>"3", "la 3", "opción tres", "la tercera", "número cuatro" → número; null si la frase dice algo más.</summary>
    public static int? OptionNumber(string t)
    {
        var m = Regex.Match(t, @"^(?:(?:la|el|opcion|numero|elijo|elige|escoge|quiero|selecciona|pulsa|marca|ponme)\s+)*(\w+)$");
        if (!m.Success) return null;
        string w = m.Groups[1].Value;
        if (int.TryParse(w, out var n)) return n is > 0 and < 1000 ? n : null;
        return NumWords.TryGetValue(w, out var k) ? k : null;
    }

    static readonly HashSet<string> Stop = ["de", "del", "la", "el", "los", "las", "y", "en", "a", "al", "por", "para", "con", "un", "una", "que",
                                            "frente", "su", "sus", "o", "mas", "quiero", "escuchar", "pon", "ponme", "elige", "elijo", "dame"];

    static List<string> Words(string s) =>
        [.. VoiceAssistant.Normalize(s).Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length >= 3 && !Stop.Contains(w) && !w.All(char.IsDigit))];

    static bool Same(string a, string b) => a == b || (a.Length >= 5 && b.Length >= 5 && (a.StartsWith(b, StringComparison.Ordinal) || b.StartsWith(a, StringComparison.Ordinal)));

    /// <summary>
    /// Opción más parecida a lo dicho. Devuelve una sola si gana con claridad; varias si hay empate (hay que preguntar);
    /// ninguna si nada coincide.
    /// </summary>
    public static List<GuideOption> Match(string text, IEnumerable<GuideOption> options)
    {
        var tw = Words(text);
        if (tw.Count == 0) return [];
        var scored = options.Select(o =>
        {
            var lw = Words(o.Label.Split(" (")[0]);
            int hits = lw.Count(w => tw.Any(x => Same(w, x)));
            return (o, hits, frac: lw.Count == 0 ? 0 : hits / (double)lw.Count, cover: hits / (double)tw.Count);
        }).Where(s => s.hits > 0).OrderByDescending(s => s.hits).ThenByDescending(s => s.frac).ToList();
        if (scored.Count == 0) return [];
        var top = scored[0];
        var rivals = scored.Where(s => s.hits == top.hits && top.frac - s.frac < 0.25).ToList();
        return rivals.Count == 1 ? [top.o] : [.. rivals.Select(s => s.o)];
    }

    static readonly (string Slot, string[] Words)[] SlotWords =
    [
        ("region", ["region"]), ("anio", ["ano", "anio", "fecha"]), ("periodo", ["periodo"]), ("tipo", ["tipo", "mapa"]), ("tema", ["tema"]),
        ("item", ["visualizacion", "video"]), ("lugar", ["lugar", "zona"]), ("fuente", ["datos", "fuente"]), ("vars", ["variables", "variable"]),
    ];

    static readonly (string Section, string[] Words)[] SectionWords =
    [
        ("eic", ["jukebox", "eic", "visualizaciones"]), // "nasa" no: aparece en «NASA POWER», «datos de la NASA»…
        ("archivo", ["archivo", "mi imagen", "mi video", "mi foto", "subir"]),
        ("biomas", ["bioma", "biomas", "deforestacion", "perdida de bosque", "cambio de bosque", "verdor", "ndvi"]),
        ("clima", ["clima", "climaticas", "tendencias del clima", "tendencias"]),
        ("mundo", ["planeta", "mundo", "cobertura terrestre", "todo el planeta"]),
    ];

    static readonly (string Region, string[] Words)[] PeruRegionWords =
    [
        ("madre-de-dios", ["madre de dios", "puerto maldonado", "la pampa"]),
        ("ucayali", ["ucayali", "pucallpa", "aguaytia", "huanuco"]),
        ("san-martin", ["san martin", "tarapoto", "moyobamba"]),
        ("cordillera-blanca", ["cordillera blanca", "huascaran", "huaraz", "glaciares"]),
        ("costa-norte", ["costa norte", "piura", "tumbes"]),
        ("altiplano", ["altiplano", "puno", "titicaca"]),
        ("loreto", ["loreto", "iquitos"]),
        ("peru", ["peru completo", "todo el peru", "todo peru"]),
    ];

    /// <summary>Datos que la frase ya trae ("biomas de Ucayali en 2010" → sección, región y año).</summary>
    public (string? Section, Dictionary<string, string> Values) Extract(string text)
    {
        string t = VoiceAssistant.Normalize(text);
        var vals = new Dictionary<string, string>();
        string? named = SectionWords.FirstOrDefault(s => Has(t, s.Words)).Section;
        string? sec = named ?? (Stage is GuideStage.Choose or GuideStage.Ready or GuideStage.Error ? Section : null);
        if (sec is null) return (null, vals);

        var year = Regex.Matches(t, @"\b(19|20)\d{2}\b").Select(m => int.Parse(m.Value, Es)).FirstOrDefault();
        bool serie = Has(t, "ano por ano", "serie", "todos los anos", "evolucion", "a lo largo del tiempo");
        switch (sec)
        {
            case "biomas":
                string? tipo = Has(t, "deforestacion", "perdida de bosque", "cambio de bosque", "tala", "mineria") ? "cambio_bosque"
                    : Has(t, "anomalia", "el nino", "sequia") ? "anomalia_ndvi"
                    : Has(t, "verdor", "ndvi") ? "tendencia_ndvi"
                    : Has(t, "mapa de biomas") || (Has(t, "bioma", "biomas") && (year > 0 || serie || PeruRegionWords.Any(r => Has(t, r.Words)))) ? "biomas" : null;
                if (tipo is not null && (Data.Biomas is null || Data.Biomas.Tipos.Any(x => x.Id == tipo))) vals["tipo"] = tipo;
                if (PeruRegionWords.FirstOrDefault(r => Has(t, r.Words)).Region is { } reg) vals["region"] = reg;
                break;
            case "clima":
                if (Data.Tendencias is { } tc)
                {
                    var hit = Match(t, tc.Regiones.Select(r => new GuideOption(r.Id, r.Nombre)));
                    if (hit.Count == 1) vals["region"] = hit[0].Id;
                    var f = tc.Fuentes.FirstOrDefault(f => Has(t, f.Id) || Has(t, VoiceAssistant.Normalize(f.Nombre)));
                    if (f is not null) vals["fuente"] = f.Id;
                }
                if (Has(t, "sugiere la red", "sugeridas", "que sugiere", "recomendadas")) vals["vars"] = "red";
                break;
            case "eic":
                // Primero una visualización concreta («lluvia total»); el tema solo si no hay una sola que encaje.
                var items = Match(t, Data.Eic.Select(e => new GuideOption(e.Id, e.Titulo)));
                if (items.Count == 1)
                {
                    vals["item"] = items[0].Id;
                    vals["tema"] = Data.Eic.First(e => e.Id == items[0].Id).Tema;
                }
                else if (Data.Eic.Select(e => e.Tema).Distinct().FirstOrDefault(x => Has(t, VoiceAssistant.Normalize(x))) is { } tema)
                    vals["tema"] = tema;
                break;
            case "mundo":
                var place = Match(t, Data.Places.Select(p => new GuideOption(p.Id, p.Nombre.Split(':')[0])));
                if (place.Count == 1) vals["lugar"] = place[0].Id;
                break;
        }
        if (sec is "biomas" or "mundo")
        {
            var (min, max) = sec == "mundo" ? (Data.WorldMin, Data.WorldMax) : (Data.Biomas?.AnioMin ?? 2001, Data.Biomas?.AnioMax ?? 2024);
            if (year >= min && year <= max) { vals["periodo"] = "anio"; vals["anio"] = year.ToString(Es); }
            else if (serie) vals["periodo"] = "serie";
        }
        return (named ?? (vals.Count > 0 ? sec : null), vals);
    }

    /// <summary>Aplica lo extraído: cambia de sección si hace falta, guarda los datos y pasa al siguiente que falte.</summary>
    public string ApplyExtract(string? section, Dictionary<string, string> vals)
    {
        if (section is null) return Prompt();
        bool switching = section != Section;
        Section = section; clarify = null; Page = 0;
        if (vals.ContainsKey("tema") && !vals.ContainsKey("item")) V.Remove("item");
        if (Section == "clima" && (vals.ContainsKey("region") || vals.ContainsKey("fuente"))) { V.Remove("pick"); V.Remove("picks"); }
        foreach (var (k, v) in vals) V[k] = v;
        if (switching && vals.Count == 0 && IsComplete && section != "archivo") V.Remove(Required()[^1]);
        Stage = GuideStage.Choose;
        if (vals.Count > 0 && IsComplete) return Advance();
        Touch();
        return Prompt();
    }

    /// <summary>
    /// Interpreta una orden según el contexto. «Siguiente» y «anterior» cambian de grupo de opciones mientras se elige
    /// y devuelven None con contenido listo (la app los entiende entonces como paso siguiente o anterior de la música).
    /// </summary>
    public GuideCommand Interpret(string text)
    {
        string t = VoiceAssistant.Normalize(text);
        if (t.Length == 0) return new(GuideCommandKind.None);
        bool choosing = Stage is GuideStage.Menu or GuideStage.Choose or GuideStage.Demo || clarify is not null;

        if (Has(t, "donde estoy", "en donde estoy", "donde me encuentro")) return new(GuideCommandKind.Where);
        if (Has(t, "que puedo hacer", "que puedo decir", "que opciones tengo", "que hago aqui", "que puedo hacer aqui")) return new(GuideCommandKind.WhatCanIDo);
        if (Has(t, "menu", "menu principal", "volver al menu", "ir al menu", "empezar de nuevo")) return new(GuideCommandKind.Menu);
        if (t is "volver" or "vuelve" or "atras" or "regresar" or "regresa" or "volver atras" or "ir atras" or "vuelve atras"
            || Has(t, "volver al paso anterior", "paso anterior de la guia")) return new(GuideCommandKind.Back);
        if (Stage == GuideStage.Error && Has(t, "reintentar", "reintenta", "intentalo de nuevo", "intentar de nuevo", "otra vez", "prueba de nuevo"))
            return new(GuideCommandKind.Retry);
        if (t is "repite" or "repitelo" or "repetir" or "otra vez" or "repite las opciones" || Has(t, "repite las opciones", "repite la pregunta"))
            return new(GuideCommandKind.Repeat);
        if (Has(t, "explicacion detallada", "mas detalle", "mas detalles", "detalle cientifico", "explicacion cientifica", "explicame mas", "explicacion completa"))
            return new(GuideCommandKind.ExplainMore);
        // Solo órdenes cortas: «explícame por qué hay polvo…» es una pregunta libre para el modelo.
        if (t is "explicacion" or "explicame" or "explica" or "explicalo" or "explicamelo" or "explica esto" or "que representa" or "que representa esto"
            or "que significa" or "que significa esto" || Has(t, "explicacion sencilla", "explicacion simple"))
            return new(GuideCommandKind.Explain);
        if (Has(t, "aprender a escuchar", "demostracion", "demo", "aislar una capa", "aislar", "ensename a escuchar"))
            return new(GuideCommandKind.Demo);

        // El nombre exacto de una opción gana a todo lo demás («Elegir otro año» no es «cambiar el año»).
        if (choosing && Options().FirstOrDefault(o => VoiceAssistant.Normalize(o.Label.Split(" (")[0]) == t) is { } exact)
            return new(GuideCommandKind.Select, exact.Id);

        var change = Regex.Match(t, @"\b(?:cambia|cambiar|corrige|corregir|otra|otro|elegir otra|elegir otro)\b");
        if (change.Success && Section is not null)
        {
            var slot = SlotWords.FirstOrDefault(s => Has(t, s.Words)).Slot;
            if (slot is not null) return new(GuideCommandKind.Change, slot);
        }

        if (choosing)
        {
            if (t is "siguiente" or "siguientes" or "mas" or "sigue" or "adelante" || Has(t, "mas opciones", "otras opciones", "siguientes opciones", "siguiente grupo"))
                return new(GuideCommandKind.More);
            if (t is "anterior" or "anteriores" || Has(t, "opciones anteriores", "grupo anterior"))
                return new(GuideCommandKind.Less);
            if (CurrentSlot == "pick" && Picks.Count > 0 && (t is "listo" or "ya" or "ya esta" or "terminar" || Has(t, "eso es todo")))
                return new(GuideCommandKind.Select, "__listo");
            if (Has(t, "recomiendame", "recomienda", "sugiereme", "sugiere", "que me recomiendas", "elige tu", "eligelo tu"))
                return new(GuideCommandKind.Recommend);
            var opts = Options();
            if (OptionNumber(t) is int n)
            {
                if (CurrentSlot == "anio" && n >= 1000) return new(GuideCommandKind.Select, $"year:{n}");
                if (n >= 1 && n <= opts.Count) return new(GuideCommandKind.Select, opts[n - 1].Id);
            }
            var hit = Match(t, opts);
            if (clarify is null && Stage != GuideStage.Demo)
            {
                // La frase trae otra sección o datos que aún faltan («Ucayali en 2010» en la región): se aprovechan todos.
                // Un dato ya elegido (p. ej. el tema) no se cambia de forma implícita: para eso está «cambiar…».
                var (sec, vals) = Extract(t);
                bool newData = vals.Keys.Any(k => !V.ContainsKey(k) && k != CurrentSlot);
                if ((sec is not null && sec != Section) || newData) return new(GuideCommandKind.Prefilled);
                if (hit.Count == 0 && vals.Keys.Any(k => !V.ContainsKey(k))) return new(GuideCommandKind.Prefilled);
            }
            if (hit.Count == 1) return new(GuideCommandKind.Select, hit[0].Id);
            if (hit.Count > 1) return new(GuideCommandKind.Ambiguous, null, hit);
            return new(GuideCommandKind.None);
        }

        if (Stage is GuideStage.Ready or GuideStage.Error)
        {
            var (sec, vals) = Extract(t);
            if (sec is not null && (vals.Count > 0 || sec != Section)) return new(GuideCommandKind.Prefilled);
        }
        return new(GuideCommandKind.None);
    }

    /// <summary>Ejecuta las órdenes que solo cambian el estado de la guía y devuelve lo que hay que decir (null si no es de la guía).</summary>
    public string? Apply(GuideCommand cmd, string text) => cmd.Kind switch
    {
        GuideCommandKind.Select => Select(cmd.Value!),
        GuideCommandKind.Ambiguous => Clarify(cmd.Options!),
        GuideCommandKind.Prefilled => ApplyExtract(Extract(text).Section, Extract(text).Values),
        GuideCommandKind.More => More(),
        GuideCommandKind.Less => Less(),
        GuideCommandKind.Back => Back(),
        GuideCommandKind.Menu => Menu(),
        GuideCommandKind.Repeat => Prompt(),
        GuideCommandKind.Change => Change(cmd.Value!),
        GuideCommandKind.Retry => Retry(),
        GuideCommandKind.Recommend => Recommend(),
        _ => null
    };
}
