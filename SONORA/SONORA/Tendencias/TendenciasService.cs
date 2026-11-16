using System.Collections.Concurrent;
using System.Text.Json;
using BiomasPeru;

namespace SONORA.Tendencias;

/// <summary>
/// Tendencias climáticas de Perú exportadas por TrendDetective (<c>tendencias_peru.json</c>): medias anuales sin
/// estacionalidad de 11 regiones (NASA POWER 1981–2025 y Giovanni 2000–2025, más 2026 provisional), su tendencia
/// (Mann-Kendall + Sen) y la sugerencia de la red neuronal de TrendDetective. No descarga nada: los datos ya vienen
/// en el JSON. Configuración: <c>Tendencias:DataPath</c>.
///
/// Cada año se dibuja como un "cuadro" de 4 barras horizontales con colores exactos (una por capa musical): el largo
/// de la barra es el valor de ese año dentro del rango de la serie. Así SONORA lo escucha como cualquier serie de mapas:
/// la variable sube → la barra crece → la melodía sube.
/// </summary>
public sealed class TendenciasService(IConfiguration config, ILogger<TendenciasService> log)
{
    public const int W = 240, H = 160;

    /// <summary>Colores de las 4 capas (roles de SONORA). Separados más de 90 en RGB para que no se confundan.</summary>
    public static readonly (string Role, int[] Rgb)[] Capas =
    [
        ("melody", [86, 204, 242]),
        ("rhythm", [255, 120, 90]),
        ("bass", [150, 220, 110]),
        ("pad", [242, 211, 91]),
    ];
    const uint Fondo = 0x06080CFF, Pista = 0x0C1018FF; // luminancia < 18: silencio

    readonly Lazy<Data?> _data = new(() => Load(config, log), LazyThreadSafetyMode.ExecutionAndPublication);
    readonly ConcurrentDictionary<string, byte[]> _cache = new();

    public string? Error { get; private set; }
    public Data? Datos => _data.Value;

    static Data? Load(IConfiguration config, ILogger log)
    {
        string path = config["Tendencias:DataPath"] is { Length: > 0 } p ? p : Path.Combine(AppContext.BaseDirectory, "Tendencias", "tendencias_peru.json");
        try
        {
            var d = JsonSerializer.Deserialize<Data>(File.ReadAllText(path), new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals,
            });
            log.LogInformation("Tendencias de Perú: {Regiones} regiones desde {Path}", d?.Regiones.Count, path);
            return d;
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "No se pudo leer {Path}", path);
            return null;
        }
    }

    public Serie? Find(string region, string fuente, string variable) =>
        Datos?.Regiones.FirstOrDefault(r => r.Id == region)?.Series.FirstOrDefault(s => s.Fuente == fuente && s.Variable == variable);

    /// <summary>Años comunes a las series elegidas (los pasos musicales).</summary>
    public static List<int> CommonYears(IReadOnlyList<Serie> series) =>
        series.Select(s => s.Anios.AsEnumerable()).Aggregate((a, b) => a.Intersect(b)).Order().ToList();

    /// <summary>PNG de un año: barra k = serie k, largo proporcional a su valor entre el mínimo y el máximo de la serie.</summary>
    public byte[] Frame(string region, string fuente, IReadOnlyList<Serie> series, int anio)
    {
        string key = $"{region}|{fuente}|{string.Join(',', series.Select(s => s.Variable))}|{anio}";
        if (_cache.Count > 2000) _cache.Clear();
        return _cache.GetOrAdd(key, _ =>
        {
            var px = new uint[W * H];
            Array.Fill(px, Fondo);
            int band = H / Capas.Length, margin = 7, x0 = 8, span = W - 16;
            for (int k = 0; k < series.Count && k < Capas.Length; k++)
            {
                var s = series[k];
                int i = s.Anios.IndexOf(anio);
                double frac = 0;
                if (i >= 0 && double.IsFinite(s.Valores[i]))
                {
                    var vals = s.Valores.Where(double.IsFinite).ToList();
                    double min = vals.Min(), max = vals.Max();
                    // Nunca 0: el año más bajo sigue sonando (nota grave), solo un año sin dato queda en silencio.
                    frac = max > min ? 0.08 + 0.92 * (s.Valores[i] - min) / (max - min) : 0.5;
                }
                var c = Capas[k].Rgb;
                uint color = (uint)(c[0] << 24 | c[1] << 16 | c[2] << 8 | 0xFF);
                int len = (int)Math.Round(frac * span);
                for (int y = k * band + margin; y < (k + 1) * band - margin; y++)
                    for (int x = x0; x < x0 + span; x++)
                        px[y * W + x] = x < x0 + len ? color : Pista;
            }
            return Png.Encode(W, H, px);
        });
    }

    // ---------- Formato de tendencias_peru.json (lo escribe TrendDetective.Sonora) ----------
    public sealed record Data(string Origen, string Generado, string Metodo, List<Fuente> Fuentes, List<Variable> Variables, List<Region> Regiones);
    public sealed record Fuente(string Id, string Nombre, int AnioMin, int AnioMax, int? AnioProvisional, Red? Red);
    public sealed record Red(string Nombre, string Entrenada, double Accuracy, double MacroF1, double Top3);
    public sealed record Variable(string Fuente, string Codigo, string Nombre, string Que, string Unidad, string NotaUnidad, string Categoria);
    public sealed record Region(string Id, string Codigo, string Nombre, string Bioma, double Lat, double Lon, Caja Caja, List<Serie> Series);
    public sealed record Caja(double LonMin, double LonMax, double LatMin, double LatMax);
    public sealed record Serie(string Fuente, string Variable, List<int> Anios, List<double> Valores, bool UltimoProvisional,
                               double SenDecada, double SenBajo, double SenAlto, double P, double Z, string Tendencia, double Media, Prob? Red);
    public sealed record Prob(double PSube, double PBaja, double PSinTendencia, bool RegionNoVista);
}
