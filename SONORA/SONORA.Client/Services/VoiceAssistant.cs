using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SONORA.Client.Models;

namespace SONORA.Client.Services;

/// <summary>Orden entendida por las reglas: frase inicial opcional y acciones que ejecuta <c>Home.razor</c>.</summary>
public sealed record VoiceIntent(string? Reply, List<AssistantAction> Actions);

/// <summary>
/// Asistente de voz. Las órdenes habituales se entienden aquí con reglas (al instante y sin conexión);
/// lo que no encaja se envía con <see cref="Context"/> a <c>POST /api/assistant</c>, que responde con un modelo de OpenAI.
/// </summary>
public static class VoiceAssistant
{
    public const string Help =
        "Puedes decir: haz sonar el Sahara, busca donde hay más humo, lo más tranquilo, reproduce, pausa, siguiente, paso 10, " +
        "más rápido, tempo 100, sube el volumen, escala blues, silencia el polvo, solo la melodía, activa todas las capas, " +
        "más científico, más musical, modo radar, centra el radar en el huracán, habla más lento, activa la vibración, " +
        "alto contraste, describe, resumen o repite. Mapas de Perú: muéstrame la deforestación en Madre de Dios, " +
        "biomas de Perú en 2010, evolución de los biomas en Ucayali o anomalía del verdor en Piura en 2017. " +
        "Para no tener que pulsar nada, di activa manos libres. También puedes pedírmelo con tus palabras.";

    static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-ES");

    /// <summary>Minúsculas, sin tildes ni signos: "¿Dónde está el Kīlauea?" → "donde esta el kilauea".</summary>
    public static string Normalize(string s)
    {
        var sb = new StringBuilder();
        foreach (var ch in s.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : ' ');
        }
        return Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
    }

    /// <summary>Respuesta sí / no (en español o inglés). null si no se entiende o no hay respuesta.</summary>
    public static bool? YesNo(string? text)
    {
        var t = Normalize(text ?? "");
        if (t.Length == 0) return null;
        // "no" primero: "no, gracias", "ahora no", "sí… mejor no" cuentan como no
        if (Has(t, "no", "nop", "nope", "nah", "ahora no", "despues", "luego", "mejor no", "no gracias", "not now", "no thanks", "later"))
            return false;
        if (Has(t, "si", "sip", "claro", "dale", "ok", "okay", "vale", "bueno", "de acuerdo", "por supuesto", "por favor", "activalo", "activa",
                "adelante", "correcto", "yes", "yeah", "yep", "sure", "please", "of course", "go ahead"))
            return true;
        return null;
    }

    // Frase completa como palabras sueltas / alguna palabra que empieza por la raíz.
    static bool Has(string t, params string[] phrases) => phrases.Any(p => $" {t} ".Contains($" {p} "));
    static bool Starts(string t, params string[] stems) => t.Split(' ').Any(w => stems.Any(w.StartsWith));

    public static VoiceIntent? Parse(string text, AnalysisInfo? a, IReadOnlyList<ScaleInfo> scales)
    {
        var t = Normalize(text);
        if (t.Length == 0) return null;
        static VoiceIntent Do(string? reply, params AssistantAction[] acts) => new(reply, [.. acts]);
        static AssistantAction Act(string type, string value = "") => new(type, value);

        string OnOff() => Starts(t, "desactiv", "apag", "quit", "ocult", "sin") || Has(t, "no quiero", "normal") ? "off" : "on";

        // Navegación por voz (la lectura y el "ve a…" los entiende antes lector.js) y modo manos libres
        if (Has(t, "como navego", "ayuda de navegacion", "como recorro la pagina", "como uso la pagina", "ayuda de la pagina", "como me muevo"))
            return Do(null, Act("page_help"));
        if (Has(t, "manos libres", "escucha continua", "escuchame siempre", "escucha siempre", "modo conversacion", "modo voz"))
            return Do(null, Act("hands_free", OnOff()));
        if (Has(t, "deja de escuchar", "no me escuches", "dejame en paz", "adios", "hasta luego", "eso es todo"))
            return Do(null, Act("hands_free", "off"));
        if (Has(t, "ayuda", "que puedo decir", "que puedes hacer", "comandos")) return Do(null, Act("help"));
        if (Starts(t, "repit", "repet") || t is "otra vez" or "que dijiste") return Do(null, Act("repeat"));

        // Voz del asistente y narración ("habla más rápido" no es el tempo de la música)
        bool voice = Has(t, "habla", "hablas", "voz", "narracion", "narrador", "lee", "lees");
        if (voice && Has(t, "rapido", "deprisa")) return Do(null, Act("set_speech_rate", "+0.2"));
        if (voice && Has(t, "lento", "despacio")) return Do(null, Act("set_speech_rate", "-0.2"));
        if (Has(t, "narracion", "narrador")) return Do(null, Act("narration", OnOff()));
        if (Has(t, "ejemplo", "nasa") && Starts(t, "carg", "abr", "pon", "vuelv", "usa")) return Do(null, Act("load_sample"));
        if (PeruMap(t) is { } map) return Do(null, Act("load_map", map));

        // Accesibilidad visual y táctil, descarga
        if (Starts(t, "vibra")) return Do(null, Act("haptics", OnOff()));
        if (Has(t, "contraste")) return Do(null, Act("high_contrast", OnOff()));
        if (Has(t, "texto grande", "letra grande", "texto mas grande", "letra mas grande", "texto normal", "letra normal"))
            return Do(null, Act("large_text", OnOff()));
        if (Starts(t, "descarg") || Has(t, "transcripcion")) return Do(null, Act("download_transcript"));
        if (a is null) return null;

        // Volumen general ("más bajo" va antes que la capa "bajo")
        if (Has(t, "volumen") || (Has(t, "mas fuerte", "mas alto", "mas bajo", "mas suave", "mas flojo") && !Has(t, "donde", "busca", "llevame")))
        {
            if (FirstNumber(t) is int vol) return Do(null, Act("set_volume", vol.ToString(Es)));
            return Do(null, Act("set_volume", Starts(t, "baj", "menos", "reduc", "suave", "flojo") ? "-20" : "+20"));
        }

        // Equilibrio ciencia ↔ música (frente de Pareto: 0 = lo más musical, 100 = lo más científico)
        if (Has(t, "lo mas musical", "maxima musicalidad", "totalmente musical")) return Do(null, Act("set_balance", "0"));
        if (Has(t, "lo mas cientifico", "lo mas fiel", "maxima fidelidad", "totalmente cientifico")) return Do(null, Act("set_balance", "100"));
        if (Has(t, "mas musical", "mas musicalidad")) return Do(null, Act("set_balance", "-25"));
        if (Has(t, "mas cientifico", "mas fiel", "mas fidelidad", "mas preciso")) return Do(null, Act("set_balance", "+25"));
        if (Has(t, "equilibrio", "equilibrado", "punto medio", "intermedio"))
            return Do(null, Act("set_balance", (FirstNumber(t) ?? 50).ToString(Es)));
        if (Has(t, "parametros manuales", "manual", "manuales")) return Do(null, Act("manual_params"));

        if (FindScale(t) is { } scaleId) return Do(null, Act("set_scale", scaleId));
        if (Has(t, "escala")) return Do(null, Act("set_scale", "next"));

        if (Has(t, "mas rapido", "mas deprisa", "acelera", "sube el tempo", "aumenta el tempo")) return Do(null, Act("set_tempo", "+20"));
        if (Has(t, "mas lento", "mas despacio", "desacelera", "baja el tempo", "reduce el tempo", "frena")) return Do(null, Act("set_tempo", "-20"));
        if (Has(t, "tempo", "bpm", "pulsos") && FirstNumber(t) is int bpm) return Do(null, Act("set_tempo", bpm.ToString(Es)));

        // Recorrido y centro del radar ("centra el radar en el huracán")
        if (Has(t, "radar"))
        {
            if (Starts(t, "centr"))
            {
                if (FindPlace(t, a) is int pc)
                    return Do(null, Act("set_center", ((pc + 0.5) / a.Steps).ToString("0.###", CultureInfo.InvariantCulture) + ",0.5"));
                return Do(null, Act("set_center", "0.5,0.5"));
            }
            return Do(null, Act("scan_radar"));
        }
        if (Has(t, "lineal")) return Do(null, Act("scan_linear"));
        if (Has(t, "capas detectadas", "ver capas", "ver las capas", "muestra las capas", "oculta las capas", "colores de las capas"))
            return Do(null, Act("show_layers", OnOff()));

        // Capas
        var layer = FindLayer(t, a);
        if (layer is not null && Starts(t, "solo", "solament", "unicament")) return Do(null, Act("solo", layer));
        if (Has(t, "todas las capas", "todas", "todos los instrumentos") && Starts(t, "activ", "enciend", "pon", "devuelv", "restaur"))
            return Do(null, Act("unmute_all"));
        if (layer is not null && (Starts(t, "silenci", "quit", "apag", "mute", "calla") || Has(t, "sin el", "sin la", "sin los", "sin las")))
            return Do(null, Act("mute", layer));
        if (layer is not null && (Starts(t, "activ", "enciend", "devuelv", "agreg", "anad", "restaur") || Has(t, "pon el", "pon la", "pon los", "pon las", "vuelve a poner")))
            return Do(null, Act("unmute", layer));

        // Buscar y hacer sonar: máximos y mínimos de una capa, momento más intenso o tranquilo, lugares destacados
        bool where = Has(t, "donde", "llevame", "lleva", "ve", "vamos", "busca", "buscame", "encuentra", "salta", "ir", "muestrame",
                         "cuando", "en que paso", "en que parte", "quiero escuchar", "quiero oir", "haz sonar", "hazme oir")
                     || Starts(t, "reproduc", "toca", "pon", "suena", "sonar", "escuch");
        if (where && layer is not null)
        {
            bool least = Has(t, "menos", "minimo", "menor");
            if (least || Has(t, "mas", "maximo", "mayor") || Starts(t, "intens", "fuert"))
            {
                double[] v = layer == "points" ? a.Points!.PerStep.Select(x => (double)x).ToArray() : a.Col[layer];
                int s = ArgBest(v, !least);
                return Do($"{(least ? "Mínimo" : "Máximo")} de {LayerName(a, layer).ToLower(Es)}.", Act("play_at", (s + 1).ToString(Es)));
            }
        }
        if (where || Has(t, "momento", "parte", "zona", "lo mas"))
        {
            var total = Enumerable.Range(0, a.Steps).Select(i => a.Layers.Sum(l => a.Col[l.Role][i])).ToArray();
            if (Starts(t, "tranquil", "silencios", "calm", "suave", "vaci"))
                return Do("Lo más tranquilo.", Act("play_at", (ArgBest(total, false) + 1).ToString(Es)));
            if (Starts(t, "intens", "fuert", "ruidos", "animad"))
                return Do("Lo más intenso.", Act("play_at", (ArgBest(total, true) + 1).ToString(Es)));
        }
        if ((where || Has(t, "donde esta")) && FindPlace(t, a) is int place)
            return Do(null, Act("play_at", (place + 1).ToString(Es)));

        // Navegación por pasos
        bool andPlay = Starts(t, "reproduc", "toca", "play");
        if (NumberAfter(t, "paso", "columna", "fotograma", "sector", "posicion") is int n)
            return andPlay ? Do(null, Act("goto_step", n.ToString(Es)), Act("play")) : Do(null, Act("goto_step", n.ToString(Es)));
        if (Has(t, "al inicio", "al principio", "al comienzo", "primer paso", "desde el inicio", "desde el principio"))
            return andPlay ? Do(null, Act("goto_step", "1"), Act("play")) : Do(null, Act("goto_step", "1"));
        if (Has(t, "al final", "ultimo paso")) return Do(null, Act("goto_step", a.Steps.ToString(Es)));
        if (Starts(t, "siguient", "adelant", "avanz")) return Do(null, Act("next"));
        if (Starts(t, "anterior", "atras", "retroced", "regres")) return Do(null, Act("prev"));

        // Reproducción
        if (Starts(t, "paus", "deten", "stop") || t is "para" or "alto" or "callate" || Has(t, "para la musica", "para ya"))
            return Do(null, Act("pause"));
        if (andPlay || Starts(t, "continu", "empiez", "comienz", "arranc", "dale") || t is "sigue" or "seguir")
            return Do(null, Act("play"));

        // Descripciones
        if (Starts(t, "resum") || Has(t, "de que trata", "que es esto", "que estoy viendo", "que muestra", "que representa")
            || (Starts(t, "describ") && Has(t, "imagen", "mapa", "video")))
            return Do(null, Act("summary"));
        if (Starts(t, "describ") || Has(t, "que suena", "que hay aqui", "donde estoy", "que estoy escuchando", "que escucho", "que se oye"))
            return Do(null, Act("describe"));

        return null; // pregunta libre → modelo de OpenAI
    }

    public static string LayerName(AnalysisInfo a, string role) =>
        role == "points" ? "Destellos" : a.Layers.FirstOrDefault(l => l.Role == role)?.Name ?? Roles.Es(role);

    static int ArgBest(double[] v, bool max)
    {
        int best = 0;
        for (int i = 1; i < v.Length; i++) if (max ? v[i] > v[best] : v[i] < v[best]) best = i;
        return best;
    }

    // ---------- Escalas ----------
    // Ids de sonora.js. "mayor" y "menor" solo cuentan si se dice "escala" (también aparecen en "mayor intensidad").
    static readonly (string Id, string[] Keys, string[] WithScaleWord)[] ScaleKeys =
    [
        ("pent-minor", ["pentatonica menor"], ["pentatonica"]),
        ("pent-major", ["pentatonica mayor"], []),
        ("whole-tone", ["tonos enteros"], []),
        ("blues", ["blues"], []),
        ("dorian", ["dorica"], []),
        ("hirajoshi", ["hirajoshi", "japonesa"], []),
        ("major", ["jonica"], ["mayor"]),
        ("minor", ["eolica"], ["menor", "menor natural"]),
    ];

    static string? FindScale(string t)
    {
        bool scaleWord = Has(t, "escala");
        foreach (var (id, keys, withScale) in ScaleKeys)
            if (keys.Any(k => Has(t, k)) || (scaleWord && withScale.Any(k => Has(t, k)))) return id;
        return null;
    }

    // ---------- Capas y lugares ----------
    static List<(string Key, string Role)> LayerKeys(AnalysisInfo a)
    {
        var keys = new List<(string Key, string Role)>();
        foreach (var l in a.Layers)
        {
            var n = Normalize(l.Name);
            keys.Add((n, l.Role));
            if (n.EndsWith('s') && n.Length > 4) keys.Add((n[..^1], l.Role)); // "sulfatos" → "sulfato"
            keys.Add((Normalize(Roles.Es(l.Role)), l.Role));
        }
        if (a.Points is { Count: > 0 }) keys.AddRange([("destellos", "points"), ("destello", "points"), ("puntos", "points")]);
        return keys.OrderByDescending(k => k.Key.Length).ToList();
    }

    static string? FindLayer(string t, AnalysisInfo a) => LayerKeys(a).FirstOrDefault(k => Has(t, k.Key)).Role;

    static readonly HashSet<string> PlaceStop = ["hacia", "cruzando", "sobre", "desde", "donde", "aqui", "entre"];

    /// <summary>Paso central del lugar destacado que se nombra ("llévame al Etna"). Si hay varios, gana el nombre más corto.</summary>
    static int? FindPlace(string t, AnalysisInfo a)
    {
        var layerWords = LayerKeys(a).Select(k => k.Key).ToHashSet();
        (int Step, int Len)? best = null;
        foreach (var lm in a.Landmarks.OfType<string>().Distinct())
        {
            var place = Normalize(lm.Split(':')[0]);
            var words = place.Split(' ').Where(w => w.Length >= 4 && !layerWords.Contains(w) && !PlaceStop.Contains(w));
            if (!words.Any(w => Has(t, w))) continue;
            var steps = Enumerable.Range(0, a.Steps).Where(i => a.Landmarks[i] == lm).ToList();
            int len = place.Split(' ').Length;
            if (best is null || len < best.Value.Len) best = (steps[steps.Count / 2], len);
        }
        return best?.Step;
    }

    // ---------- Números ("paso 5", "paso cinco", "tempo 100") ----------
    static readonly Dictionary<string, int> NumberWords = new()
    {
        ["un"] = 1, ["uno"] = 1, ["una"] = 1, ["primer"] = 1, ["primero"] = 1, ["dos"] = 2, ["tres"] = 3, ["cuatro"] = 4, ["cinco"] = 5,
        ["seis"] = 6, ["siete"] = 7, ["ocho"] = 8, ["nueve"] = 9, ["diez"] = 10, ["once"] = 11, ["doce"] = 12, ["trece"] = 13,
        ["catorce"] = 14, ["quince"] = 15, ["dieciseis"] = 16, ["diecisiete"] = 17, ["dieciocho"] = 18, ["diecinueve"] = 19,
        ["veinte"] = 20, ["veintiuno"] = 21, ["veintiun"] = 21, ["veintidos"] = 22, ["veintitres"] = 23, ["veinticuatro"] = 24,
        ["veinticinco"] = 25, ["veintiseis"] = 26, ["veintisiete"] = 27, ["veintiocho"] = 28, ["veintinueve"] = 29,
        ["treinta"] = 30, ["cuarenta"] = 40, ["cincuenta"] = 50, ["sesenta"] = 60, ["setenta"] = 70, ["ochenta"] = 80,
        ["noventa"] = 90, ["cien"] = 100, ["ultimo"] = int.MaxValue,
    };

    static int? ParseNumber(string tok, string? unit)
    {
        if (int.TryParse(tok, out var d)) return d;
        if (!NumberWords.TryGetValue(tok, out var n)) return null;
        return unit is not null && NumberWords.TryGetValue(unit, out var u) && n % 10 == 0 && n >= 30 && u < 10 ? n + u : n;
    }

    static int? NumberAfter(string t, params string[] keys)
    {
        var m = Regex.Match(t, $@"\b(?:{string.Join('|', keys)}) (?:numero )?(\w+)(?: y (\w+))?");
        return m.Success ? ParseNumber(m.Groups[1].Value, m.Groups[2].Success ? m.Groups[2].Value : null) : null;
    }

    static int? FirstNumber(string t) => Regex.Match(t, @"\d+") is { Success: true } m ? int.Parse(m.Value, Es) : null;

    // ---------- Mapas de Perú (acción load_map) ----------

    static readonly (string Region, string[] Words)[] PeruRegions =
    [
        ("madre-de-dios", ["madre de dios", "la pampa", "puerto maldonado", "interoceanica"]),
        ("ucayali", ["ucayali", "pucallpa", "aguaytia", "huanuco"]),
        ("san-martin", ["san martin", "tarapoto", "moyobamba"]),
        ("cordillera-blanca", ["cordillera blanca", "huascaran", "huaraz", "glaciar", "glaciares", "nevados"]),
        ("costa-norte", ["costa norte", "piura", "tumbes", "bosque seco", "sechura"]),
        ("altiplano", ["altiplano", "puno", "titicaca", "juliaca"]),
        ("loreto", ["loreto", "iquitos"]),
    ];

    /// <summary>
    /// "muéstrame la deforestación en Madre de Dios" → "cambio_bosque;;madre-de-dios";
    /// "evolución de los biomas en Ucayali" → "serie;biomas;ucayali". Null si la frase no pide un mapa de Perú.
    /// </summary>
    public static string? PeruMap(string t)
    {
        string? tipo =
            Has(t, "deforestacion", "deforestado", "deforestada", "perdida de bosque", "perdida de selva", "cambio de bosque", "tala", "mineria") ? "cambio_bosque" :
            Has(t, "anomalia", "el nino", "sequia", "sequias") ? "anomalia_ndvi" :
            Has(t, "verdor", "ndvi", "reverdecimiento", "tendencia de la vegetacion") ? "tendencia_ndvi" :
            Has(t, "bioma", "biomas", "ecosistemas", "ecorregiones") ? "biomas" : null;
        string? region = PeruRegions.FirstOrDefault(r => Has(t, r.Words)).Region ?? (Has(t, "peru") ? "peru" : null);
        bool show = Starts(t, "muestr", "ensen", "genera", "carg", "abr", "dibuj") || Has(t, "dame", "quiero ver", "mapa", "mapas");
        if (tipo is null && !(show && region is not null)) return null;

        tipo ??= "biomas";
        region ??= "peru";
        bool serie = Has(t, "evolucion", "ano por ano", "a lo largo", "serie", "animacion", "todos los anos", "en el tiempo", "con los anos");
        if (serie && tipo is "biomas" or "anomalia_ndvi") return $"serie;{tipo};{region}";
        int? year = Regex.Matches(t, @"\b(20[0-2]\d)\b").Select(m => int.Parse(m.Value, Es)).FirstOrDefault(y => y is >= 2001 and <= 2024) is int y and > 0 ? y : null;
        return $"{tipo};{year?.ToString(Es) ?? ""};{region}";
    }

    // ---------- Contexto para el modelo ----------
    /// <summary>Todo lo que el modelo necesita saber de lo que suena: descripción, resumen, estado y valores por paso.</summary>
    public static string Context(AnalysisInfo a, IReadOnlyList<string> summary, StepInfo? cur, int tempo,
                                 ScaleInfo? scale, IReadOnlyList<ScaleInfo> scales, IEnumerable<string> muted, string settings)
    {
        string P(double v) => Math.Round(v * 100).ToString(Es);
        bool pts = a.Points is { Count: > 0 };
        var sb = new StringBuilder();
        sb.AppendLine($"Título: {a.Title}");
        string source = a.Source switch
        {
            "sample" => "imagen de la NASA",
            "biomas" => a.Scan == "years" ? "serie anual de mapas de Perú generada por SONORA (red neuronal + NASA)" : "mapa de Perú generado por SONORA (red neuronal + NASA)",
            _ => $"{(a.Mode == "video" ? "video" : "imagen")} subida por la persona",
        };
        sb.AppendLine($"Fuente: {source}; recorrido {a.Scan}; {a.Steps} pasos.");
        if (a.Box is { } box)
            sb.AppendLine($"Mapa de longitud {box.LonMin.ToString("0.##", CultureInfo.InvariantCulture)} a {box.LonMax.ToString("0.##", CultureInfo.InvariantCulture)} " +
                          $"y latitud {box.LatMin.ToString("0.##", CultureInfo.InvariantCulture)} a {box.LatMax.ToString("0.##", CultureInfo.InvariantCulture)} (play_lon funciona dentro de ese rango).");
        sb.AppendLine($"Descripción: {a.Alt}");
        if (a.Context is not null) sb.AppendLine($"Contexto científico: {a.Context}");
        sb.AppendLine("Capas: " + string.Join("; ", a.Layers.Select(l => $"{l.Name} → {Roles.Es(l.Role)} con {Roles.Instrument(l.Role)} (rol {l.Role})"))
                      + (pts ? "; Destellos → marimba o glockenspiel (rol points)" : ""));
        sb.AppendLine("Resumen:");
        foreach (var line in summary) sb.AppendLine($"- {line}");
        var mutedNames = muted.Select(r => LayerName(a, r)).ToList();
        sb.AppendLine($"Estado: paso {(cur?.Step ?? 0) + 1} de {a.Steps}{(cur is null ? "" : $" ({cur.StepLabel})")}; " +
                      $"{(cur?.Playing == true ? "reproduciendo" : "en pausa")}; tempo {tempo} BPM; escala {scale?.Name} (id {scale?.Id}); " +
                      $"capas silenciadas: {(mutedNames.Count == 0 ? "ninguna" : string.Join(", ", mutedNames))}; {settings}.");
        sb.AppendLine("Escalas disponibles (id = nombre): " + string.Join("; ", scales.Select(s => $"{s.Id} = {s.Name}")));
        sb.AppendLine("Pasos (número | posición | intensidad relativa 0-100 por capa | lugar destacado):");
        for (int i = 0; i < a.Steps; i++)
        {
            sb.Append($"{i + 1} | {a.StepLabels[i]} | ");
            sb.Append(string.Join(", ", a.Layers.Select(l => $"{l.Name} {P(a.Col[l.Role][i])}")));
            if (pts) sb.Append($", destellos {a.Points!.PerStep[i]}");
            if (a.Landmarks[i] is { } lm) sb.Append($" | {lm}");
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
