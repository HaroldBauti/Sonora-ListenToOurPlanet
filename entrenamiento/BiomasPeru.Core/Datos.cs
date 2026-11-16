using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;

namespace BiomasPeru;

/// <summary>Rejilla de 0,05° (~5,5 km) que cubre Perú.</summary>
public static class Grid
{
    public const double LonMin = -81.5, LatMax = 0.0, Res = 0.05;
    public const int W = 260, H = 370, N = W * H;
    public const string WmsBbox = "-18.5,-81.5,0,-68.5"; // WMS 1.3.0 + EPSG:4326 = lat,lon
    public static double Lon(int x) => LonMin + (x + 0.5) * Res;
    public static double Lat(int y) => LatMax - (y + 0.5) * Res;
}

/// <summary>Paleta de NASA GIBS: color RGB → valor físico (NDVI, K, m o clase IGBP).</summary>
public sealed class ColorMap
{
    readonly Dictionary<int, float> _map = [];
    public int Misses;

    public static ColorMap Load(string path) => FromXml(XDocument.Load(path));

    public static ColorMap FromXml(XDocument xml)
    {
        var cm = new ColorMap();
        var entries = xml.Descendants("ColorMapEntry").ToList();
        var parsed = new List<(int Rgb, double Lo, double Hi, bool NoData)>();
        foreach (var e in entries)
        {
            var c = e.Attribute("rgb")!.Value.Split(',').Select(int.Parse).ToArray();
            int rgb = (c[0] << 16) | (c[1] << 8) | c[2];
            bool noData = (string?)e.Attribute("nodata") == "true" || (string?)e.Attribute("transparent") == "true";
            string? v = (string?)e.Attribute("value") ?? (string?)e.Attribute("sourceValue");
            double lo, hi;
            if (v is null) { parsed.Add((rgb, 0, 0, true)); continue; }
            if (v.StartsWith('[') || v.StartsWith('('))
            {
                var parts = v.Trim('[', ']', '(', ')').Split(',');
                lo = Num(parts[0]); hi = Num(parts[1]);
                if (double.IsInfinity(hi)) hi = lo;
            }
            else lo = hi = Num(v);
            parsed.Add((rgb, lo, hi, noData));
        }
        // Los bins extremos ("[1,10000)" o "[350,652)") son abiertos: se usa su borde interior, no el punto medio.
        var widths = parsed.Where(p => !p.NoData && p.Hi > p.Lo).Select(p => p.Hi - p.Lo).OrderBy(x => x).ToList();
        double typical = widths.Count > 0 ? widths[widths.Count / 2] : 0;
        var data = parsed.Where(p => !p.NoData).ToList();
        for (int i = 0; i < parsed.Count; i++)
        {
            var p = parsed[i];
            float val;
            if (p.NoData) val = float.NaN;
            else if (typical > 0 && p.Hi - p.Lo > 20 * typical) val = (float)(data.IndexOf(p) < data.Count / 2 ? p.Hi : p.Lo);
            else val = (float)((p.Lo + p.Hi) / 2);
            cm._map.TryAdd(p.Rgb, val);
        }
        return cm;

        static double Num(string s) => s.Trim() switch
        {
            "+INF" or "INF" => double.PositiveInfinity,
            "-INF" => double.NegativeInfinity,
            var t => double.Parse(t, CultureInfo.InvariantCulture)
        };
    }

    public float[] Decode(int[] pixels)
    {
        var outp = new float[pixels.Length];
        for (int i = 0; i < pixels.Length; i++)
        {
            if (pixels[i] < 0) { outp[i] = float.NaN; continue; }
            if (_map.TryGetValue(pixels[i], out var v)) outp[i] = v;
            else { outp[i] = float.NaN; Interlocked.Increment(ref Misses); }
        }
        return outp;
    }
}

/// <summary>Todos los datos de entrada sobre la rejilla, ya convertidos a unidades físicas.</summary>
public sealed class Datos
{
    public const int Year0 = 2001, Years = 24; // 2001–2024 (IGBP llega hasta 2024)

    public required float[][] Ndvi { get; init; } // [(año-2001)*12 + mes][celda]
    public required float[][] LstC { get; init; } // °C, mismo índice
    public required float[][] Igbp { get; init; } // [año-2001][celda], clase 1–17
    public required float[] ElevM { get; init; }
    public required bool[] Peru { get; init; }

    const string Wms = "https://gibs.earthdata.nasa.gov/wms/epsg4326/best/wms.cgi";
    const string Colormaps = "https://gibs.earthdata.nasa.gov/colormaps/v1.3/";
    const string PeruGeoJson = "https://raw.githubusercontent.com/johan/world.geo.json/master/countries/PER.geo.json";
    // Límites departamentales (INEI, simplificados) para recortar las regiones por su contorno real.
    const string DepartamentosGeoJson = "https://raw.githubusercontent.com/juaneladio/peru-geojson/master/peru_departamental_simple.geojson";
    public const string DepartamentosFile = "peru_departamentos.geo.json";

    static readonly (string Folder, string Layer, string ColorMap)[] Layers =
    [
        ("ndvi", "MODIS_Terra_L3_NDVI_Monthly", "MODIS_L3_NDVI"),
        ("lst", "MODIS_Terra_L3_Land_Surface_Temp_Monthly_Day", "MODIS_Land_Surface_Temp"),
        ("igbp", "MODIS_Combined_L3_IGBP_Land_Cover_Type_Annual", "MODIS_IGBP_Land_Cover_Type"),
        ("srtm", "SRTM_Color_Index", "SRTM_Color_Index"),
    ];

    static IEnumerable<(string Url, string Path)> Jobs(string dir)
    {
        foreach (var (folder, layer, cmap) in Layers)
        {
            yield return (Colormaps + cmap + ".xml", Path.Combine(dir, "colormaps", cmap + ".xml"));
            IEnumerable<string?> times = folder switch
            {
                "ndvi" or "lst" => from y in Enumerable.Range(Year0, Years) from m in Enumerable.Range(1, 12) select $"{y}-{m:00}-01",
                "igbp" => Enumerable.Range(Year0, Years).Select(y => (string?)$"{y}-01-01"),
                _ => [null]
            };
            foreach (var t in times)
            {
                string url = $"{Wms}?SERVICE=WMS&REQUEST=GetMap&VERSION=1.3.0&LAYERS={layer}&STYLES=&FORMAT=image/png" +
                             $"&CRS=EPSG:4326&BBOX={Grid.WmsBbox}&WIDTH={Grid.W}&HEIGHT={Grid.H}" + (t is null ? "" : $"&TIME={t}");
                yield return (url, Path.Combine(dir, folder, (t?[..7] ?? "srtm") + ".png"));
            }
        }
        yield return (PeruGeoJson, Path.Combine(dir, "peru.geo.json"));
        yield return (DepartamentosGeoJson, Path.Combine(dir, DepartamentosFile));
    }

    /// <summary>Archivos que faltan por descargar y cuántos ya están en caché.</summary>
    public static (int Pending, int Cached) Pending(string dir)
    {
        var all = Jobs(dir).ToList();
        int pending = all.Count(j => !File.Exists(j.Path));
        return (pending, all.Count - pending);
    }

    public static Task DownloadAsync(string dir, CancellationToken ct = default) => DownloadAsync(dir, null, ct);

    /// <param name="onFile">Se llama tras cada archivo descargado con (hechos, total). Si es null, se escribe el avance en la consola.</param>
    public static async Task DownloadAsync(string dir, Action<int, int>? onFile, CancellationToken ct = default)
    {
        var jobs = Jobs(dir).Where(j => !File.Exists(j.Path)).ToList();
        if (onFile is null) Console.WriteLine($"Descargando {jobs.Count} archivos de NASA GIBS (ya en caché: {Jobs(dir).Count() - jobs.Count})…");
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("SONORA-BiomasPeru/1.0");
        int done = 0;
        await Parallel.ForEachAsync(jobs, new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = ct }, async (job, token) =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(job.Path)!);
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    var bytes = await http.GetByteArrayAsync(job.Url, token);
                    if (job.Path.EndsWith(".png") && (bytes.Length < 8 || bytes[1] != (byte)'P'))
                        throw new InvalidDataException("La respuesta no es un PNG: " + System.Text.Encoding.UTF8.GetString(bytes.AsSpan(0, Math.Min(200, bytes.Length))));
                    await File.WriteAllBytesAsync(job.Path, bytes, token);
                    break;
                }
                catch (Exception ex) when (attempt < 4 && ex is not OperationCanceledException)
                {
                    await Task.Delay(1000 * attempt, token);
                }
            }
            int d = Interlocked.Increment(ref done);
            if (onFile is not null) onFile(d, jobs.Count);
            else if (d % 100 == 0 || d == jobs.Count) Console.WriteLine($"  {d}/{jobs.Count}");
        });
    }

    public static Datos Load(string dir)
    {
        ColorMap Cm(string name) => ColorMap.Load(Path.Combine(dir, "colormaps", name + ".xml"));
        var cmNdvi = Cm("MODIS_L3_NDVI"); var cmLst = Cm("MODIS_Land_Surface_Temp");
        var cmIgbp = Cm("MODIS_IGBP_Land_Cover_Type"); var cmSrtm = Cm("SRTM_Color_Index");

        float[] Read(string folder, string name, ColorMap cm)
        {
            var (w, h, px) = Png.Decode(File.ReadAllBytes(Path.Combine(dir, folder, name + ".png")));
            if (w != Grid.W || h != Grid.H) throw new InvalidDataException($"{folder}/{name}: {w}x{h}");
            return cm.Decode(px);
        }

        var months = (from y in Enumerable.Range(Year0, Years) from m in Enumerable.Range(1, 12) select $"{y}-{m:00}").ToArray();
        var ndvi = new float[months.Length][]; var lst = new float[months.Length][];
        Parallel.For(0, months.Length, t =>
        {
            ndvi[t] = Read("ndvi", months[t], cmNdvi);
            lst[t] = Read("lst", months[t], cmLst);
            for (int i = 0; i < Grid.N; i++) lst[t][i] -= 273.15f;
        });
        var igbp = Enumerable.Range(Year0, Years).Select(y => Read("igbp", $"{y}-01", cmIgbp)).ToArray();
        var elev = Read("srtm", "srtm", cmSrtm);

        Console.WriteLine($"Colores sin paleta: NDVI {cmNdvi.Misses}, LST {cmLst.Misses}, IGBP {cmIgbp.Misses}, SRTM {cmSrtm.Misses}");
        return new Datos { Ndvi = ndvi, LstC = lst, Igbp = igbp, ElevM = elev, Peru = PeruMask(Path.Combine(dir, "peru.geo.json")) };
    }

    static bool[] PeruMask(string geojson) => PolygonMask(geojson, null, firstOnly: true);

    /// <summary>
    /// Celdas de la rejilla dentro de los polígonos de un GeoJSON. Con <paramref name="byName"/>, solo las entidades cuyo
    /// nombre (propiedad NOMBDEP, NAME o name) cumple el filtro.
    /// </summary>
    public static bool[] PolygonMask(string geojson, Func<string, bool>? byName, bool firstOnly = false)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(geojson));
        var polys = new List<(List<(double X, double Y)> Ring, double MinX, double MaxX, double MinY, double MaxY)>();
        void AddPolygon(JsonElement poly)
        {
            var ring = poly[0].EnumerateArray().Select(p => (p[0].GetDouble(), p[1].GetDouble())).ToList();
            polys.Add((ring, ring.Min(p => p.Item1), ring.Max(p => p.Item1), ring.Min(p => p.Item2), ring.Max(p => p.Item2)));
        }
        foreach (var f in doc.RootElement.GetProperty("features").EnumerateArray())
        {
            if (byName is not null)
            {
                var props = f.GetProperty("properties");
                string? name = null;
                foreach (var key in new[] { "NOMBDEP", "NAME", "name" })
                    if (props.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String) { name = v.GetString(); break; }
                if (name is null || !byName(name)) continue;
            }
            var geom = f.GetProperty("geometry");
            if (geom.GetProperty("type").GetString() == "Polygon") AddPolygon(geom.GetProperty("coordinates"));
            else foreach (var p in geom.GetProperty("coordinates").EnumerateArray()) AddPolygon(p);
            if (firstOnly) break;
        }

        var mask = new bool[Grid.N];
        for (int y = 0; y < Grid.H; y++)
            for (int x = 0; x < Grid.W; x++)
            {
                double px = Grid.Lon(x), py = Grid.Lat(y);
                bool inside = false;
                foreach (var (ring, minX, maxX, minY, maxY) in polys)
                {
                    if (px < minX || px > maxX || py < minY || py > maxY) continue;
                    for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
                        if ((ring[i].Y > py) != (ring[j].Y > py) && px < (ring[j].X - ring[i].X) * (py - ring[i].Y) / (ring[j].Y - ring[i].Y) + ring[i].X)
                            inside = !inside;
                }
                mask[y * Grid.W + x] = inside;
            }
        return mask;
    }
}
