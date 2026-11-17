using SONORA.Client.Models;

namespace SONORA.Client.Services;

/// <summary>
/// Reglas reales de la sonificación, en palabras. Es la única fuente de estos textos: la pestaña «Qué estás escuchando»,
/// las explicaciones de la guía hablada y la demostración por capas los toman de aquí. Deben coincidir con
/// <c>playStep</c> y <c>playYearTick</c> de <c>wwwroot/js/sonora.js</c>.
/// </summary>
public static class SoundRules
{
    /// <summary>Aviso que distingue la sonificación de una grabación ambiental.</summary>
    public const string Sonification =
        "Es una sonificación: los sonidos se generan a partir de los datos. No es una grabación del lugar ni sonidos de la naturaleza.";

    /// <summary>Cómo cambia el sonido de una capa con sus datos (sonora.js, playStep).</summary>
    public static string Role(string role, ScaleInfo? scale = null) => role switch
    {
        "melody" => $"Más intensidad = nota más aguda y más fuerte (escala {scale?.Name.ToLowerInvariant() ?? "pentatónica menor"}, en do).",
        "rhythm" => "Más intensidad = más golpes por paso (1, 2 o 4) y bombo cuando es muy densa.",
        "bass" => "Suena cuando la capa aparece; su intensidad controla el volumen.",
        "pad" => $"Acordes {(scale is null ? "Do menor, La♭, Mi♭ y Si♭" : scale.Chords)} a lo largo del recorrido; su cantidad controla el volumen.",
        _ => ""
    };

    /// <summary>Destellos: puntos brillantes aislados (sonora.js, pointsFor).</summary>
    public static string Points(bool radar) =>
        "Los destellos (puntos brillantes aislados) suenan con marimba si son rojos, naranjas o amarillos y con glockenspiel si son azules o blancos. " +
        $"Cada uno suena cuando el barrido pasa por él; {(radar ? "más lejos del centro, más agudo" : "más arriba, más agudo")}.";

    /// <summary>Serie año por año (sonora.js, playYearTick).</summary>
    public const string Years =
        "Cada año dura unos segundos y empieza con una campanita. La melodía llega a la nota de ese año desde abajo si la capa creció " +
        "respecto al año anterior, desde arriba si disminuyó, y ronda la nota si casi no cambió.";

    /// <summary>Cómo se recorre lo que suena.</summary>
    public static string Scan(AnalysisInfo a) => a.Scan switch
    {
        "years" => $"Se recorre año por año, en {a.Steps} pasos: cada paso es un año.",
        "radar" => $"Se recorre como un radar: una línea gira desde las 12 alrededor de un centro, en {a.Steps} pasos; la distancia al centro decide el tono.",
        "time" => $"Se recorre en el tiempo del video, en {a.Steps} fotogramas.",
        _ => a.Box is not null
            ? $"El mapa se recorre de oeste a este, en {a.Steps} pasos."
            : $"La imagen se recorre de izquierda a derecha, en {a.Steps} columnas."
    };

    /// <summary>Cómo se convirtieron los colores en capas, según la fuente.</summary>
    public static string Colors(AnalysisInfo a) => a.Source switch
    {
        "upload" or "eic" => "Los colores se agruparon automáticamente en 4 familias (k-means): la más extensa suena como armonía de fondo y las demás como melodía, ritmo y bajo.",
        "sample" => "Cada píxel se compara con los colores de la barra oficial de la NASA.",
        _ => "El mapa usa 4 colores exactos, uno por capa, así que cada capa suena con su nombre real."
    };
}
