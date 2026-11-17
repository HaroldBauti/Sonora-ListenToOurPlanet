using System.Globalization;
using SONORA.Client.Models;

namespace SONORA.Client.Services;

/// <summary>Lo que se sabe del contenido elegido antes de analizarlo (catálogo): fuente, periodo, descripción y contexto.</summary>
public sealed record ContentMeta(string? Fuente = null, string? Periodo = null, string? Descripcion = null, string? Contexto = null);

/// <summary>
/// Explicaciones de la guía hablada: una sencilla (qué es, fuente, periodo y cómo suena) y otra con detalle científico.
/// Solo usa los datos del análisis, del catálogo y las reglas reales de <see cref="SoundRules"/>; no inventa relaciones.
/// </summary>
public static class Explainer
{
    static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-ES");

    /// <summary>Fuente por defecto cuando el catálogo no la da.</summary>
    public static string? DefaultSource(AnalysisInfo a) => a.Source switch
    {
        "sample" => "NASA Scientific Visualization Studio 5669, modelo GEOS de la NASA",
        "upload" => "tu propio archivo",
        _ => null
    };

    /// <summary>Periodo deducido del análisis (series año por año y videos).</summary>
    public static string? DefaultPeriod(AnalysisInfo a) => a.Scan switch
    {
        "years" when a.StepLabels.Count > 0 => $"{a.StepLabels[0]} a {a.StepLabels[^1]}",
        "time" when a.Duration > 0 => $"un video de {Math.Round(a.Duration).ToString(Es)} segundos",
        _ => null
    };

    static string FirstSentence(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        int i = s.IndexOf(". ", StringComparison.Ordinal);
        return (i > 0 ? s[..(i + 1)] : s).Trim();
    }

    static string End(string s) => s.Length == 0 || ".!?".Contains(s[^1]) ? s : s + ".";

    /// <summary>Capa → rol: "Selva suena como melodía; Andes, como bajo".</summary>
    public static string Legend(AnalysisInfo a) =>
        string.Join("; ", a.Layers.Select((l, i) => i == 0 ? $"{l.Name} suena como {Roles.Es(l.Role)}" : $"{l.Name}, como {Roles.Es(l.Role)}"));

    /// <summary>Explicación sencilla: qué representa, fuente, periodo, aviso de sonificación y leyenda de capas.</summary>
    public static string Simple(AnalysisInfo a, ContentMeta? m = null)
    {
        var parts = new List<string> { End(a.Title) };
        string what = m?.Descripcion is { Length: > 0 } d ? d : FirstSentence(a.Alt);
        if (what.Length > 0) parts.Add(End(what));
        if ((m?.Fuente ?? DefaultSource(a)) is { Length: > 0 } f) parts.Add($"Fuente: {End(f)}");
        if ((m?.Periodo ?? DefaultPeriod(a)) is { Length: > 0 } p) parts.Add($"Periodo: {End(p)}");
        parts.Add(SoundRules.Sonification);
        if (a.Layers.Count > 0)
            parts.Add($"{Legend(a)}. Cuanto más hay de una capa, más se oye.");
        return string.Join(" ", parts);
    }

    /// <summary>Explicación detallada: contexto científico, recorrido, regla de cada capa, colores, escala y límites de los datos.</summary>
    public static string Detailed(AnalysisInfo a, ContentMeta? m = null, ScaleInfo? scale = null)
    {
        var parts = new List<string>();
        string? ctx = a.Context is { Length: > 0 } c ? c : m?.Contexto;
        if (ctx is { Length: > 0 }) parts.Add(End(ctx));
        parts.Add(SoundRules.Scan(a));
        foreach (var l in a.Layers)
            parts.Add($"{l.Name}: {Roles.Es(l.Role)} con {Roles.Instrument(l.Role)}. {SoundRules.Role(l.Role, scale)}");
        if (a.Points is { Count: > 0 }) parts.Add(SoundRules.Points(a.Scan == "radar"));
        if (a.Scan == "years") parts.Add(SoundRules.Years);
        parts.Add(SoundRules.Colors(a));
        if (scale is not null) parts.Add($"Todas las notas salen de la escala {scale.Name}, {scale.Mood}.");
        parts.Add("Las intensidades son relativas dentro de este contenido: comparan unas partes con otras, no son valores absolutos.");
        return string.Join(" ", parts);
    }

    /// <summary>
    /// Demostración de una capa: qué representa, cómo suena y dónde tiene más y menos datos (pasos reales del análisis).
    /// Devuelve también esos dos pasos para que la app los haga sonar.
    /// </summary>
    public static (string Text, int MaxStep, int MinStep) LayerLesson(AnalysisInfo a, string role, ScaleInfo? scale = null)
    {
        var layer = a.Layers.FirstOrDefault(l => l.Role == role);
        string name = layer?.Name ?? Roles.Es(role);
        if (!a.Col.TryGetValue(role, out var v) || v.Length == 0)
            return ($"{name} no tiene datos en este contenido.", 0, 0);
        int max = 0, min = 0;
        for (int i = 1; i < v.Length; i++) { if (v[i] > v[max]) max = i; if (v[i] < v[min]) min = i; }
        string Lbl(int i) => i < a.StepLabels.Count ? a.StepLabels[i] : $"paso {i + 1}";
        string text =
            $"{name} suena como {Roles.Es(role)}, con {Roles.Instrument(role)}. {SoundRules.Role(role, scale)} " +
            $"En este contenido llega a su máximo en {Lbl(max)}, con {Describer.Pct(v[max])}, y a su mínimo en {Lbl(min)}, con {Describer.Pct(v[min])}. " +
            "Primero la escuchas sola donde hay más y después donde hay menos.";
        return (text, max, min);
    }
}
