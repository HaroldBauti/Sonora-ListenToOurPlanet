using System.Collections.Concurrent;
using System.Globalization;
using System.Xml.Linq;
using BiomasPeru;

namespace SONORA.Mundo;

/// <summary>
/// Cobertura terrestre de cualquier zona del planeta, bajo demanda: pide a NASA GIBS (WMS) el mapa MODIS IGBP
/// (MCD12Q1, anual 2001–2024) del recuadro y lo agrupa en las 4 capas de SONORA con colores exactos.
/// No usa la red neuronal: esa está entrenada solo con Perú.
/// </summary>
public sealed class MundoService(IHttpClientFactory http, ILogger<MundoService> log)
{
    public const int AnioMin = 2001, AnioMax = 2024;
    const string Wms = "https://gibs.earthdata.nasa.gov/wms/epsg4326/best/wms.cgi";
    const string ColorMapUrl = "https://gibs.earthdata.nasa.gov/colormaps/v1.3/MODIS_IGBP_Land_Cover_Type.xml";
    const int MaxSide = 480, MaxCached = 300;
    const uint Background = 0x06080CFF; // agua y sin datos: silencio (luminancia < 18)

    /// <summary>Las 4 capas: color exacto (el cliente las reconoce por color) y clases IGBP que agrupa cada una.</summary>
    public static readonly (string Role, string Name, int[] Rgb, int[] Clases)[] Capas =
    [
        ("melody", "Bosque", [60, 200, 110], [1, 2, 3, 4, 5]),
        ("rhythm", "Agricultura y urbano", [255, 107, 91], [12, 13, 14]),
        ("bass", "Sabana, matorral y pastizal", [184, 149, 106], [6, 7, 8, 9, 10, 11]),
        ("pad", "Desierto y hielo", [242, 211, 91], [15, 16]),
    ];

    public sealed record Render(byte[] Png, int W, int H, double[] Shares);

    readonly ConcurrentDictionary<string, Task<Render>> _cache = new();
    readonly SemaphoreSlim _gate = new(4); // como mucho 4 peticiones a la vez a NASA GIBS
    ColorMap? _cm;

    public Task<Render> GetAsync(GeoBox b, int anio, CancellationToken ct)
    {
        string key = string.Create(CultureInfo.InvariantCulture, $"{b.LonMin}|{b.LonMax}|{b.LatMin}|{b.LatMax}|{anio}");
        if (_cache.Count >= MaxCached) _cache.Clear();
        var task = _cache.GetOrAdd(key, k => RenderAsync(b, anio, k));
        return task.WaitAsync(ct);
    }

    public static (int W, int H) Size(GeoBox b)
    {
        double lw = b.LonMax - b.LonMin, lh = b.LatMax - b.LatMin;
        return lw >= lh ? (MaxSide, Math.Max(8, (int)Math.Round(MaxSide * lh / lw))) : (Math.Max(8, (int)Math.Round(MaxSide * lw / lh)), MaxSide);
    }

    async Task<Render> RenderAsync(GeoBox b, int anio, string key)
    {
        await _gate.WaitAsync();
        try
        {
            var client = http.CreateClient("gibs");
            _cm ??= ColorMap.FromXml(XDocument.Parse(await client.GetStringAsync(ColorMapUrl)));
            var (w, h) = Size(b);
            string url = string.Create(CultureInfo.InvariantCulture,
                $"{Wms}?SERVICE=WMS&REQUEST=GetMap&VERSION=1.3.0&LAYERS=MODIS_Combined_L3_IGBP_Land_Cover_Type_Annual&STYLES=&FORMAT=image/png" +
                $"&CRS=EPSG:4326&BBOX={b.LatMin},{b.LonMin},{b.LatMax},{b.LonMax}&WIDTH={w}&HEIGHT={h}&TIME={anio}-01-01");
            var bytes = await client.GetByteArrayAsync(url);
            if (bytes.Length < 8 || bytes[1] != (byte)'P')
                throw new InvalidDataException("NASA GIBS no devolvió una imagen para esa zona.");
            var (pw, ph, px) = Png.Decode(bytes);
            var classes = _cm.Decode(px);

            var lut = new int[18]; Array.Fill(lut, -1);
            for (int k = 0; k < Capas.Length; k++) foreach (int c in Capas[k].Clases) lut[c] = k;
            var rgba = new uint[pw * ph]; var counts = new int[Capas.Length]; int land = 0;
            for (int i = 0; i < rgba.Length; i++)
            {
                float v = classes[i];
                int cls = float.IsNaN(v) ? -1 : (int)Math.Round(v);
                int k = cls is >= 1 and <= 17 ? lut[cls] : -1;
                if (k < 0) { rgba[i] = Background; continue; }
                land++; counts[k]++;
                var c = Capas[k].Rgb;
                rgba[i] = (uint)(c[0] << 24 | c[1] << 16 | c[2] << 8 | 0xFF);
            }
            return new Render(Png.Encode(pw, ph, rgba), pw, ph, counts.Select(n => land == 0 ? 0 : Math.Round(n / (double)land, 4)).ToArray());
        }
        catch (Exception ex)
        {
            _cache.TryRemove(key, out _); // no guardar errores (p. ej. GIBS caído un momento)
            log.LogWarning(ex, "No se pudo obtener la cobertura IGBP de {Box} ({Anio})", b, anio);
            throw;
        }
        finally { _gate.Release(); }
    }
}
