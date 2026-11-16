using System.Globalization;
using System.Text;
using SONORA.Client.Models;

namespace SONORA.Client.Services;

/// <summary>Genera texto descriptivo en español para lectores de pantalla, narración por voz y la transcripción descargable.</summary>
public static class Describer
{
    static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-ES");

    public static string Pct(double v) => $"{Math.Round(v * 100).ToString(Es)} %";

    static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0], Es) + s[1..];

    public static string LayerDetail(string role, StepInfo s) => role switch
    {
        "melody" => s.Note is null ? "silencio" : $"nota {s.Note}",
        "rhythm" => s.Hits == 0 ? "sin percusión" : $"{s.Hits} golpe{(s.Hits == 1 ? "" : "s")} por paso",
        "bass" => s.Bass ? $"bajo en {s.BassNote}" : "sin bajo",
        "pad" => $"acorde {s.Chord}",
        "points" => PointsText(s),
        _ => ""
    };

    /// <summary>Cambio de una capa respecto al año anterior en puntos porcentuales ("▲3", "▼2", "="), o null si no aplica.</summary>
    public static string? ChangeMark(string role, StepInfo s)
    {
        if (s.Delta is null || !s.Delta.TryGetValue(role, out var d)) return null;
        int p = (int)Math.Round(d * 100);
        return p > 0 ? $"▲{p}" : p < 0 ? $"▼{-p}" : "=";
    }

    /// <summary>Cambios notables (≥ 2 puntos) respecto al año anterior, en frase: "Agricultura y urbano sube 4 puntos".</summary>
    public static string ChangesText(StepInfo s, AnalysisInfo a)
    {
        if (s.Delta is null) return "";
        var parts = a.Layers
            .Select(l => (l.Name, P: (int)Math.Round(s.Delta.GetValueOrDefault(l.Role) * 100)))
            .Where(x => Math.Abs(x.P) >= 2)
            .Select(x => $"{x.Name} {(x.P > 0 ? "sube" : "baja")} {Math.Abs(x.P)} punto{(Math.Abs(x.P) == 1 ? "" : "s")}")
            .ToList();
        return parts.Count == 0 ? " Casi sin cambios respecto al año anterior." : $" Respecto al año anterior: {string.Join(", ", parts)}.";
    }

    public static string PointsText(StepInfo s)
    {
        if (s.Points == 0) return "sin destellos";
        int cool = s.Points - s.PointsWarm;
        var parts = new List<string>();
        if (s.PointsWarm > 0) parts.Add($"{s.PointsWarm} en marimba");
        if (cool > 0) parts.Add($"{cool} en glockenspiel");
        return $"{s.Points} destello{(s.Points == 1 ? "" : "s")} ({string.Join(" y ", parts)})";
    }

    /// <summary>Descripción completa de un paso (o de una celda en modo exploración).</summary>
    public static string Step(StepInfo s, AnalysisInfo a)
    {
        var sb = new StringBuilder();
        string unit = a.Scan == "years" ? "Año" : a.Mode == "video" ? "Fotograma" : a.Scan == "radar" ? "Paso" : "Columna";
        sb.Append($"{unit} {s.Step + 1} de {a.Steps}: {s.StepLabel}");
        if (s.RowLabel is not null) sb.Append($", {s.RowLabel}");
        sb.Append(". ");
        if (s.Landmark is not null) sb.Append($"Aquí: {s.Landmark} ");
        var parts = a.Layers.Select(l =>
            $"{l.Name} ({Roles.Es(l.Role)}) {Pct(s.Values.GetValueOrDefault(l.Role))}, {LayerDetail(l.Role, s)}");
        sb.Append(string.Join(". ", parts)).Append('.');
        if (a.Points is { Count: > 0 }) sb.Append($" Destellos: {PointsText(s)}.");
        var dominant = a.Layers.OrderByDescending(l => s.Values.GetValueOrDefault(l.Role)).First();
        if (s.Values.GetValueOrDefault(dominant.Role) > 0.05)
            sb.Append($" Domina: {dominant.Name.ToLower(Es)}.");
        else
            sb.Append(" Zona casi vacía: casi no suena nada.");
        sb.Append(ChangesText(s, a));
        return sb.ToString();
    }

    /// <summary>Descripción breve para narrar sin saturar (nota y capa dominante).</summary>
    public static string Short(StepInfo s, AnalysisInfo a)
    {
        var dominant = a.Layers.OrderByDescending(l => s.Values.GetValueOrDefault(l.Role)).First();
        var where = s.RowLabel is null ? s.StepLabel : $"{s.StepLabel}, {s.RowLabel}";
        return $"{Cap(where)}. {dominant.Name} {Pct(s.Values.GetValueOrDefault(dominant.Role))}. {(s.Note is null ? "Sin melodía" : $"Nota {s.Note}")}.{ChangesText(s, a)}";
    }

    static string RangeText(AnalysisInfo a, int from, int to) => a.Mode switch
    {
        _ when a.Scan == "years" => from == to ? $"en {a.StepStart[from]}" : $"de {a.StepStart[from]} a {a.StepEnd[to]}",
        "video" => $"del segundo {a.StepStart[from]} al {a.StepEnd[to]}",
        _ when a.Scan == "radar" => $"del rumbo {a.StepStart[from]} al {a.StepEnd[to]}",
        _ when a.Box is not null => $"de {a.StepStart[from]} a {a.StepEnd[to]}",
        _ => $"del {a.StepStart[from]} al {a.StepEnd[to]} del ancho"
    };

    static string PointText(AnalysisInfo a, int i) => a.Mode switch
    {
        _ when a.Scan == "years" => $"en {a.StepStart[i]}",
        "video" => $"entre el segundo {a.StepStart[i]} y el {a.StepEnd[i]}",
        _ when a.Scan == "radar" => $"entre los rumbos {a.StepStart[i]} y {a.StepEnd[i]}",
        _ when a.Box is not null => $"entre {a.StepStart[i]} y {a.StepEnd[i]}",
        _ => $"entre el {a.StepStart[i]} y el {a.StepEnd[i]} del ancho"
    };

    /// <summary>Resumen rico de toda la imagen o video: qué suena, dónde y cuándo.</summary>
    public static List<string> Summary(AnalysisInfo a)
    {
        var lines = new List<string>();
        lines.Add(a.Scan == "years"
            ? $"Serie de {a.Steps} mapas anuales, de {a.StepStart[0]} a {a.StepEnd[^1]}. Cada año es un paso musical: escuchas cómo cambia el territorio con el tiempo."
            : a.Mode == "video"
            ? $"Video de {Math.Round(a.Duration).ToString(Es)} segundos analizado en {a.Steps} fotogramas. Cada fotograma es un paso musical; la música avanza al ritmo del video."
            : a.Scan == "radar"
                ? $"Recorrido tipo radar, como la sonificación Caldwell 73 de la NASA: una línea gira en sentido horario desde las 12 alrededor del centro marcado, en {a.Steps} sectores de 11,25°. Rumbo 0° es arriba y 90°, a la derecha."
            : a.Box is { } box
                ? $"El mapa se recorre de oeste a este en {a.Steps} pasos de {((box.LonMax - box.LonMin) / a.Steps).ToString("0.##", Es)}° de longitud. " +
                  $"Cada paso es una franja vertical {(a.Source == "sample" ? "del planeta, de polo a polo" : "del territorio, de norte a sur")}."
                : $"La imagen se recorre de izquierda a derecha en {a.Steps} columnas. Cada columna es un paso musical.");

        foreach (var l in a.Layers)
        {
            var v = a.Col[l.Role];
            string name = $"{l.Name} ({Roles.Es(l.Role)}, {Roles.Instrument(l.Role)})";
            if (v.Max() < 1e-6) { lines.Add($"{name}: no se detectó en esta imagen; esta capa queda en silencio."); continue; }
            int peak = Array.IndexOf(v, v.Max());
            int active = v.Count(x => x >= 0.35);
            var runs = Runs(v, 0.35).OrderByDescending(r => r.To - r.From).Take(3).OrderBy(r => r.From).ToList();
            var sb = new StringBuilder($"{name}: ");
            if (active == 0) sb.Append("presencia débil en todo el recorrido. ");
            else if (active == v.Length) sb.Append("presente en todo el recorrido. ");
            else sb.Append($"fuerte en {active} de {v.Length} pasos, sobre todo {string.Join("; ", runs.Select(r => RangeText(a, r.From, r.To)))}. ");
            sb.Append($"Máximo {PointText(a, peak)}");
            if (a.Landmarks[peak] is { } lm) sb.Append($" ({lm.TrimEnd('.')})");
            sb.Append('.');
            if (l.Share > 0) sb.Append($" Ocupa cerca del {Pct(l.Share)} {(a.Scan == "years" ? "de la serie" : a.Mode == "video" ? "del video" : "de la imagen")}.");
            lines.Add(sb.ToString());
        }

        if (a.Points is { } pts)
        {
            if (pts.Count == 0)
                lines.Add("Destellos: no se detectaron puntos brillantes aislados.");
            else
            {
                int busiest = Array.IndexOf(pts.PerStep, pts.PerStep.Max());
                string pitch = a.Scan == "radar" ? "la distancia al centro decide el tono: más lejos, más agudo" : "la altura decide el tono: más arriba, más agudo";
                lines.Add($"Destellos (puntos brillantes aislados, como estrellas, incendios o luces de ciudades): se detectaron {pts.Count}. " +
                          $"{pts.Warm} son cálidos (rojos, naranjas o amarillos) y suenan en marimba; {pts.Cool} son fríos o blancos y suenan en glockenspiel. " +
                          $"Cada uno suena cuando el barrido pasa por él, y {pitch}. Donde más destellos hay es {PointText(a, busiest)}" +
                          (a.Landmarks[busiest] is { } lmk ? $" ({lmk.TrimEnd('.')})." : "."));
            }
        }

        var total = Enumerable.Range(0, a.Steps).Select(i => a.Layers.Sum(l => a.Col[l.Role][i])).ToArray();
        int loud = Array.IndexOf(total, total.Max()), quiet = Array.IndexOf(total, total.Min());
        lines.Add($"El momento más intenso suena {PointText(a, loud)}; el más tranquilo, {PointText(a, quiet)}.");
        return lines;
    }

    static IEnumerable<(int From, int To)> Runs(double[] v, double thr)
    {
        int start = -1;
        for (int i = 0; i <= v.Length; i++)
        {
            bool on = i < v.Length && v[i] >= thr;
            if (on && start < 0) start = i;
            if (!on && start >= 0) { yield return (start, i - 1); start = -1; }
        }
    }

    /// <summary>Transcripción completa en texto plano (descargable), paso a paso.</summary>
    public static string Transcript(AnalysisInfo a, IReadOnlyList<StepInfo> steps, ParetoSolution? sol, ScaleInfo? scale = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("SONORA · LISTEN TO OUR PLANET");
        sb.AppendLine($"Descripción sonora de: {a.Title}");
        sb.AppendLine();
        sb.AppendLine("DESCRIPCIÓN VISUAL");
        sb.AppendLine(a.Alt);
        if (a.Context is not null) { sb.AppendLine(); sb.AppendLine("CONTEXTO CIENTÍFICO"); sb.AppendLine(a.Context); }
        sb.AppendLine();
        sb.AppendLine("CÓMO SUENA");
        if (scale is not null) sb.AppendLine($"Escala {scale.Name} ({scale.Mood}): {scale.Notes}. Acordes: {scale.Chords}.");
        foreach (var l in a.Layers) sb.AppendLine($"- {l.Name} → {Roles.Es(l.Role)} ({Roles.Instrument(l.Role)})");
        if (a.Points is { Count: > 0 }) sb.AppendLine("- Destellos (puntos brillantes) → marimba si son cálidos, glockenspiel si son fríos o blancos");
        sb.AppendLine();
        sb.AppendLine("RESUMEN");
        foreach (var line in Summary(a)) sb.AppendLine($"- {line}");
        if (sol is not null)
        {
            sb.AppendLine();
            sb.AppendLine($"Configuración musical: fidelidad {Pct(sol.Fidelity)}, musicalidad {Pct(sol.Musicality)} (NSGA-II, HBA.Evolutionary).");
        }
        sb.AppendLine();
        sb.AppendLine("PASO A PASO");
        foreach (var s in steps) sb.AppendLine($"{s.Step + 1}. {Step(s, a)}");
        return sb.ToString();
    }
}
