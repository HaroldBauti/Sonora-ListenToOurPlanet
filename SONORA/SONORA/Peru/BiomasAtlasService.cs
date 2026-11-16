using System.Collections.Concurrent;
using System.Diagnostics;
using BiomasPeru;
using Hba.NeuralNet;

namespace SONORA.Peru;

/// <summary>
/// Atlas de biomas de Perú en memoria. Al arrancar el servidor (en segundo plano) carga los datos NASA GIBS
/// —descargándolos la primera vez, unos 38 MB— y el modelo entrenado, y precalcula 2001–2024.
/// Después cada mapa se dibuja en milisegundos y se guarda en caché.
/// Configuración: <c>Biomas:DataDir</c> (carpeta de datos) y <c>Biomas:ModelPath</c> (modelo_biomas.json).
/// </summary>
public sealed class BiomasAtlasService(IConfiguration config, IWebHostEnvironment env, ILogger<BiomasAtlasService> log) : BackgroundService
{
    const int MaxCached = 400;
    readonly ConcurrentDictionary<string, (byte[] Png, MapaRender Map)> _cache = new();
    volatile Atlas? _atlas;

    public string Estado { get; private set; } = "calentando";
    public string? Error { get; private set; }
    public Atlas? Atlas => _atlas;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            await Task.Yield();
            string dataDir = Resolve(config["Biomas:DataDir"] is { Length: > 0 } dd ? dd : Path.Combine("App_Data", "biomas"));
            string modelPath = Resolve(config["Biomas:ModelPath"] is { Length: > 0 } mp ? mp : Path.Combine(AppContext.BaseDirectory, "Biomas", "modelo_biomas.json"));
            if (!File.Exists(modelPath)) throw new FileNotFoundException("No se encontró el modelo entrenado.", modelPath);

            var sw = Stopwatch.StartNew();
            Estado = "descargando datos NASA";
            await Datos.DownloadAsync(dataDir, ct);

            Estado = "calculando atlas";
            _atlas = await Task.Run(() =>
            {
                var d = Datos.Load(dataDir);
                var b = new Biomas(d);
                return BiomasPeru.Atlas.Build(Perceptron.Load(modelPath), b, d.Peru, BiomasPeru.Atlas.RegionMasks(dataDir));
            }, ct);
            GC.Collect(); // los datos crudos (unos 250 MB) ya no se necesitan
            Estado = "listo";
            log.LogInformation("Atlas de biomas listo en {Seconds:F1} s (datos: {DataDir})", sw.Elapsed.TotalSeconds, dataDir);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            Estado = "error";
            Error = ex.Message;
            log.LogError(ex, "No se pudo preparar el atlas de biomas");
        }
    }

    string Resolve(string path) => Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(env.ContentRootPath, path));

    /// <summary>PNG del mapa pedido (desde caché si ya se dibujó). Requiere el atlas listo.</summary>
    public (byte[] Png, MapaRender Map) Get(string tipo, int anio, string region, bool sonora)
    {
        var atlas = _atlas ?? throw new InvalidOperationException("El atlas aún no está listo.");
        string key = $"{tipo}|{anio}|{region}|{(sonora ? "s" : "v")}";
        if (_cache.TryGetValue(key, out var hit)) return hit;
        if (_cache.Count >= MaxCached) _cache.Clear();
        var map = atlas.Render(tipo, anio, region, sonora);
        return _cache[key] = (Png.Encode(map.W, map.H, map.Rgba), map);
    }
}
