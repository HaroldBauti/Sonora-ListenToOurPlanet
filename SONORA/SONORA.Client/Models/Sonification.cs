namespace SONORA.Client.Models;

// ---------- Contratos compartidos entre cliente (Blazor/JS) y servidor (optimizador) ----------

/// <summary>Parámetros del mapeo imagen → música. Los mismos nombres usa <c>wwwroot/js/sonora.js</c>.</summary>
public sealed record MappingParams(
    double SmoothMel, double SmoothRhy, double MelThr, double Gamma, int PitchLow, int PitchSpan,
    double Rhy1, double Rhy2, double Rhy4, double BassThr)
{
    /// <summary>Parámetros elegidos a mano (punto de comparación del optimizador).</summary>
    public static readonly MappingParams Manual = new(0, 0, 0.10, 1.0, 0, 15, 0.12, 0.45, 0.75, 0.12);
}

/// <summary>Intensidad normalizada (0–1) de cada capa en cada paso temporal.</summary>
public sealed record SonificationFeatures(int Steps, Dictionary<string, double[]> Layers);

public sealed record ParetoSolution(MappingParams Params, double Fidelity, double Musicality, int[] Melody, int[] Hits);

public sealed record ParetoResult(
    string Library, int Seed, int Generations, int Evaluations, double Hypervolume, double ElapsedMs,
    ParetoSolution Manual, List<ParetoSolution> Front);

// ---------- Datos que el módulo JS devuelve a Blazor ----------

public sealed record LayerInfo(string Role, string Name, int[] Rgb, double Share);

public sealed record AnalysisInfo(
    string Mode,              // "image" | "video"
    string Title,
    string Source,            // "sample" | "upload"
    int Steps,
    int Rows,
    List<LayerInfo> Layers,
    Dictionary<string, double[]> Col,
    List<string> StepLabels,
    List<string?> Landmarks,
    string? Context,
    string Alt,
    double Duration,
    List<string> StepStart,
    List<string> StepEnd,
    string Scan,              // "linear" | "radar" | "time"
    PointsSummary? Points,
    GeoBox? Box = null,       // caja geográfica si la imagen es un mapa (ejemplo NASA o biomas de Perú)
    string? Event = null);    // suceso del año del mapa de Perú (p. ej. "El Niño costero de 2023")

/// <summary>Destellos: puntos brillantes individuales que suenan como marimba (cálidos) o glockenspiel (fríos).</summary>
public sealed record PointsSummary(int Count, int Warm, int Cool, int[] PerStep);

public sealed record StepInfo(
    int Step, int Row, bool Playing,
    string StepLabel, string? RowLabel, string? Landmark, bool LandmarkEntered,
    Dictionary<string, double> Values,
    string? Note, int Hits, bool Bass, string? BassNote, string Chord,
    int Points, int PointsWarm,
    Dictionary<string, double>? Delta = null); // serie año por año: cambio de cada capa respecto al año anterior

/// <summary>Escala musical disponible (15 grados; el optimizador trabaja con índices, así que vale para todas).</summary>
public sealed record ScaleInfo(string Id, string Name, string Mood, string Notes, string Chords);

// ---------- Asistente de voz (cliente ↔ POST /api/assistant) ----------

/// <summary>Resultado del reconocimiento de voz del navegador: texto oído o código de error de la Web Speech API.</summary>
public sealed record ListenResult(string? Text, string? Error);

public sealed record AssistantTurn(string Role, string Text); // Role: "user" | "assistant"

public sealed record AssistantRequest(string Question, string Context, List<AssistantTurn> History);

/// <summary>Acción sobre la app (play, goto_step, set_tempo…); Value va como texto ("" si no aplica).</summary>
public sealed record AssistantAction(string Type, string Value);

public sealed record AssistantReply(string Reply, List<AssistantAction> Actions);

// ---------- Biomas de Perú (cliente ↔ GET /api/biomas/*) ----------

/// <summary>Caja geográfica en grados (longitud oeste negativa, latitud sur negativa).</summary>
public sealed record GeoBox(double LonMin, double LonMax, double LatMin, double LatMax);

public sealed record GeoPlace(string Name, double Lon, double Lat);

/// <summary>Color exacto de una capa del mapa: SONORA lo usa como paleta conocida (sin k-means).</summary>
public sealed record PaletteColor(string Role, string Name, int[] Rgb, double Reach);

public sealed record BiomasTipo(string Id, string Nombre, bool UsaAnio, string Descripcion, List<PaletteColor> Paleta);

public sealed record BiomasRegion(string Id, string Nombre);

/// <summary>Estado del atlas en el servidor ("calentando", "descargando datos NASA", "calculando atlas", "listo" o "error") y opciones.</summary>
public sealed record BiomasCatalog(string Estado, string? Error, int AnioMin, int AnioMax, List<BiomasTipo> Tipos, List<BiomasRegion> Regiones);

/// <summary>Un año de una serie. <paramref name="Valores"/> (opcional): valor de cada capa ese año, ya con unidad, para la leyenda.</summary>
public sealed record BiomasFrame(int Anio, string Url, List<string>? Valores = null, bool Provisional = false);

/// <summary>Todo lo necesario para escuchar un mapa (o una serie 2001–2024 si <see cref="Frames"/> no es null).
/// <paramref name="Notas"/> (opcional): una nota por capa para la leyenda (p. ej. su tendencia).</summary>
public sealed record BiomasInfo(
    string Tipo, int? Anio, string Region, string Title, string Alt, string Context,
    GeoBox Box, List<GeoPlace> Places, List<PaletteColor> Palette, string ImageUrl, string ViewUrl, List<BiomasFrame>? Frames,
    List<string>? Notas = null);

public static class Roles
{
    public static readonly string[] All = ["melody", "rhythm", "bass", "pad"];

    public static string Es(string role) => role switch
    {
        "melody" => "melodía",
        "rhythm" => "ritmo",
        "bass" => "bajo",
        "pad" => "armonía",
        _ => role
    };

    public static string Instrument(string role) => role switch
    {
        "melody" => "sintetizador brillante",
        "rhythm" => "percusión granular",
        "bass" => "sintetizador grave",
        "pad" => "pad ambiental",
        _ => ""
    };
}
