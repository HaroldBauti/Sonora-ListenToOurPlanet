using Hba.NeuralNet;

namespace BiomasPeru;

public sealed record GeoBox(double LonMin, double LonMax, double LatMin, double LatMax);
/// <summary>Región del catálogo. Con <paramref name="Departamentos"/>, el recorte sigue el contorno real de esos departamentos
/// (la caja <paramref name="Box"/> solo se usa si no hay contornos); sin ellos, la región es la caja.</summary>
public sealed record Region(string Id, string Nombre, GeoBox Box, string[]? Departamentos = null);
public sealed record Ciudad(string Nombre, double Lon, double Lat);
/// <summary>Color de la paleta "sonora": cada uno es una capa de SONORA (melody, rhythm, bass o pad).</summary>
public sealed record ColorCapa(string Role, string Name, int[] Rgb, double Reach);
public sealed record TipoMapa(string Id, string Nombre, bool UsaAnio, string Descripcion, string Contexto, ColorCapa[] Paleta);
/// <summary>Mapa dibujado. <see cref="Shares"/> = fracción de celdas de Perú del recorte en cada capa de la paleta "sonora".</summary>
public sealed record MapaRender(int W, int H, uint[] Rgba, GeoBox Box, double[] Shares);
public sealed record EstadisticasRegion(string Region, int Celdas, double NdviTendenciaPorDecada, double PctNdviBajando, double PctNdviSubiendo,
                                        double BosqueProb2001, double BosqueProb2024, string BiomaDominante2024);

/// <summary>
/// Atlas precalculado de 2001–2024: probabilidades de bioma por año (red neuronal), NDVI anual y tendencias.
/// Una vez construido no necesita los datos crudos ni la red, y dibuja cualquier mapa en milisegundos.
/// </summary>
public sealed class Atlas
{
    public const int Year0 = Datos.Year0, Years = Datos.Years;
    const int MaxSide = 1110; // lado mayor de los PNG (Perú completo = 780x1110)

    // Colores de fondo con luminancia < 18: SONORA los trata como silencio.
    const uint Background = 0x06080CFF, PeruQuiet = 0x0F1216FF, WaterDark = 0x08122AFF;
    const uint WaterView = 0x2B6CB0FF, NoDataView = 0x3A3F4AFF;

    // ---------- Catálogo ----------

    public static readonly Region[] Regiones =
    [
        new("peru", "Perú completo", new(Grid.LonMin, Grid.LonMin + Grid.W * Grid.Res, Grid.LatMax - Grid.H * Grid.Res, Grid.LatMax)),
        new("madre-de-dios", "Madre de Dios (minería aurífera, Interoceánica)", new(-72.5, -68.6, -13.4, -9.8), ["MADRE DE DIOS"]),
        new("ucayali", "Ucayali y Huánuco (Pucallpa, Aguaytía)", new(-77.0, -70.5, -11.5, -7.3), ["UCAYALI", "HUANUCO"]),
        new("san-martin", "San Martín (Tarapoto, Moyobamba)", new(-77.9, -75.5, -8.8, -5.4), ["SAN MARTIN"]),
        new("cordillera-blanca", "Cordillera Blanca (glaciares)", new(-78.5, -76.8, -10.3, -8.2)),
        new("costa-norte", "Costa norte: Piura y Tumbes (bosque seco, El Niño)", new(-81.4, -79.0, -6.8, -3.3), ["PIURA", "TUMBES"]),
        new("altiplano", "Altiplano de Puno (lago Titicaca)", new(-71.2, -68.6, -17.3, -13.0), ["PUNO"]),
        new("loreto", "Loreto (selva poco intervenida)", new(-77.9, -69.9, -8.8, -0.0), ["LORETO"]),
    ];

    public static readonly Ciudad[] Ciudades =
    [
        new("Tumbes", -80.45, -3.57), new("Piura", -80.63, -5.19), new("Chiclayo", -79.84, -6.77), new("Cajamarca", -78.51, -7.16),
        new("Trujillo", -79.03, -8.11), new("Chimbote", -78.58, -9.07), new("Huaraz", -77.53, -9.53), new("Lima", -77.04, -12.05),
        new("Ica", -75.73, -14.07), new("Arequipa", -71.54, -16.41), new("Moquegua", -70.93, -17.19), new("Tacna", -70.25, -18.01),
        new("Cusco", -71.97, -13.53), new("Puno", -70.02, -15.84), new("Juliaca", -70.13, -15.50), new("Ayacucho", -74.22, -13.16),
        new("Huancayo", -75.21, -12.07), new("Huánuco", -76.24, -9.93), new("Tingo María", -75.99, -9.29), new("Tarapoto", -76.36, -6.49),
        new("Moyobamba", -76.97, -6.03), new("Chachapoyas", -77.87, -6.23), new("Yurimaguas", -76.10, -5.90), new("Iquitos", -73.25, -3.75),
        new("Nauta", -73.55, -4.50), new("Pucallpa", -74.55, -8.38), new("Aguaytía", -75.51, -9.04), new("Atalaya", -73.77, -10.73),
        new("Puerto Maldonado", -69.19, -12.59), new("La Pampa (minería)", -69.95, -12.95), new("Abancay", -72.88, -13.63),
        new("Huancavelica", -74.98, -12.79), new("Cerro de Pasco", -76.26, -10.68), new("Lago Titicaca", -69.50, -15.80),
        new("Nevado Huascarán", -77.60, -9.12),
    ];

    static ColorCapa C(string role, string name, int r, int g, int b) => new(role, name, [r, g, b], 90);
    static readonly int[] Loss = [232, 150, 74], LossStrong = [255, 90, 60], Gain = [79, 195, 176], GainStrong = [139, 224, 78];

    public static readonly TipoMapa[] Tipos =
    [
        new("biomas", "Biomas", true,
            "Bioma más probable de cada celda según la red neuronal, agrupado en 4 familias.",
            "Biomas estimados por una red neuronal (Hba.NeuralNet) entrenada con NDVI y temperatura de superficie MODIS, relieve SRTM " +
            "y cobertura IGBP de la NASA. La selva amazónica ocupa el oriente, los Andes el centro y la costa desértica el occidente. " +
            "Las manchas de agricultura y zona urbana dentro de la selva suelen indicar deforestación.",
            [C("melody", "Selva", 60, 200, 110), C("rhythm", "Agricultura y urbano", 255, 107, 91), C("bass", "Andes", 184, 149, 106), C("pad", "Costa", 242, 211, 91)]),
        new("cambio_bosque", "Cambio de bosque 2001–2024", false,
            "Tendencia de la probabilidad de bosque (Theil-Sen): dónde se pierde y dónde se gana.",
            "Tendencia de 2001 a 2024 de la probabilidad de bosque que estima la red neuronal en cada celda (pendiente de Theil-Sen). " +
            "Las pérdidas fuertes coinciden con focos conocidos de deforestación: minería aurífera en Madre de Dios y frontera agrícola en " +
            "San Martín y Ucayali.",
            [new("melody", "Pérdida de bosque", Loss, 90), new("rhythm", "Pérdida fuerte", LossStrong, 90), new("bass", "Ganancia de bosque", Gain, 90), C("pad", "Bosque estable", 46, 125, 79)]),
        new("tendencia_ndvi", "Tendencia del verdor (NDVI) 2001–2024", false,
            "Solo las celdas con tendencia significativa de NDVI (Mann-Kendall, |Z| ≥ 1,96).",
            "Tendencia del índice de vegetación NDVI de MODIS entre 2001 y 2024, solo donde es estadísticamente significativa " +
            "(Mann-Kendall). El verdor sube en buena parte de los Andes occidentales y baja en zonas deforestadas de la Amazonía.",
            [new("melody", "Verdor bajando", Loss, 90), new("rhythm", "Bajando fuerte", LossStrong, 90), new("bass", "Verdor subiendo", Gain, 90), new("pad", "Subiendo fuerte", GainStrong, 90)]),
        new("anomalia_ndvi", "Anomalía del verdor (NDVI)", true,
            "NDVI de ese año comparado con el promedio 2001–2024 de la misma celda.",
            "Anomalía anual del NDVI frente al promedio 2001–2024. En la costa norte los años de El Niño (1998, 2017, 2023) traen lluvias " +
            "que reverdecen el desierto y el bosque seco; las sequías aparecen como anomalías secas.",
            [new("melody", "Más verde que lo normal", Gain, 90), new("rhythm", "Extremo seco", LossStrong, 90), new("bass", "Más seco que lo normal", Loss, 90), new("pad", "Extremo húmedo", GainStrong, 90)]),
    ];

    public static TipoMapa? Tipo(string id) => Tipos.FirstOrDefault(t => t.Id == id);
    public static Region? RegionPorId(string id) => Regiones.FirstOrDefault(r => r.Id == id);

    // ---------- Estado precalculado ----------

    readonly byte[][] _prob;          // [año][celda*K], 0–255
    readonly float[][] _ndvi;         // [año][celda]
    readonly float[] _ndviMean, _forestSlope, _forestZ, _ndviSlope, _ndviZ;
    readonly bool[] _valid, _water, _peru;
    readonly IReadOnlyDictionary<string, bool[]> _regionMask;
    public int[] Label { get; }

    Atlas(byte[][] prob, float[][] ndvi, float[] ndviMean, float[] forestSlope, float[] forestZ, float[] ndviSlope, float[] ndviZ, bool[] valid, bool[] water, bool[] peru, int[] label,
          IReadOnlyDictionary<string, bool[]> regionMask)
    {
        _prob = prob; _ndvi = ndvi; _ndviMean = ndviMean; _forestSlope = forestSlope; _forestZ = forestZ;
        _ndviSlope = ndviSlope; _ndviZ = ndviZ; _valid = valid; _water = water; _peru = peru; Label = label; _regionMask = regionMask;
    }

    /// <summary>
    /// Máscaras de las regiones con departamentos (contorno real), a partir de <c>peru_departamentos.geo.json</c> de la carpeta de datos.
    /// Si el archivo falta, devuelve un diccionario vacío y las regiones usan su caja.
    /// </summary>
    public static Dictionary<string, bool[]> RegionMasks(string dataDir)
    {
        var path = Path.Combine(dataDir, Datos.DepartamentosFile);
        var masks = new Dictionary<string, bool[]>();
        if (!File.Exists(path)) return masks;
        foreach (var r in Regiones.Where(r => r.Departamentos is { Length: > 0 }))
        {
            var mask = Datos.PolygonMask(path, name => r.Departamentos!.Contains(name, StringComparer.OrdinalIgnoreCase));
            if (mask.Any(v => v)) masks[r.Id] = mask;
        }
        return masks;
    }

    /// <summary>Predice los 24 años con la red y calcula tendencias. Tras esto, <paramref name="b"/> y sus datos crudos se pueden liberar.</summary>
    public static Atlas Build(Perceptron net, Biomas b, bool[] peru, IReadOnlyDictionary<string, bool[]>? regionMasks = null)
    {
        var probs = Mapas.Predict(net, b);
        double Forest(int y, int i) => probs[y][i * Biomas.K + 5] + probs[y][i * Biomas.K + 6];
        var (forestSlope, forestZ) = Mapas.Trend(Forest, b);
        var (ndviSlope, ndviZ) = Mapas.Trend((y, i) => b.NdviAnnual[y][i], b);

        var ndviMean = new float[Grid.N];
        for (int i = 0; i < Grid.N; i++)
        {
            if (!b.Valid[i]) continue;
            double s = 0; for (int y = 0; y < Years; y++) s += b.NdviAnnual[y][i];
            ndviMean[i] = (float)(s / Years);
        }
        var quant = probs.Select(p => p.Select(v => (byte)Math.Clamp(Math.Round(v * 255), 0, 255)).ToArray()).ToArray();
        return new Atlas(quant, b.NdviAnnual, ndviMean, forestSlope, forestZ, ndviSlope, ndviZ,
                         (bool[])b.Valid.Clone(), (bool[])b.Water.Clone(), peru, (int[])b.Label.Clone(),
                         regionMasks ?? new Dictionary<string, bool[]>());
    }

    /// <summary>Celdas de la región (contorno real si existe; si no, Perú dentro de la caja) y la ventana que las contiene.</summary>
    (bool[] Mask, (int X0, int X1, int Y0, int Y1) Win) Area(Region r)
    {
        if (!_regionMask.TryGetValue(r.Id, out var m)) return (_peru, Window(r.Box));
        int x0 = Grid.W, x1 = 0, y0 = Grid.H, y1 = 0;
        for (int y = 0; y < Grid.H; y++)
            for (int x = 0; x < Grid.W; x++)
                if (m[y * Grid.W + x]) { x0 = Math.Min(x0, x); x1 = Math.Max(x1, x + 1); y0 = Math.Min(y0, y); y1 = Math.Max(y1, y + 1); }
        // Un margen de 2 celdas para que el contorno no quede pegado al borde
        return (m, (Math.Max(0, x0 - 2), Math.Min(Grid.W, x1 + 2), Math.Max(0, y0 - 2), Math.Min(Grid.H, y1 + 2)));
    }

    public float Prob(int year, int cell, int k) => _prob[year][cell * Biomas.K + k] / 255f;
    public double Forest(int year, int cell) => Prob(year, cell, 5) + Prob(year, cell, 6);
    public int ArgMax(int year, int cell)
    {
        var p = _prob[year]; int o = cell * Biomas.K, best = 0;
        for (int k = 1; k < Biomas.K; k++) if (p[o + k] > p[o + best]) best = k;
        return best;
    }
    public bool Valid(int cell) => _valid[cell];
    public bool Water(int cell) => _water[cell];
    public float NdviAnomaly(int year, int cell) => _ndvi[year][cell] - _ndviMean[cell];

    // ---------- Dibujo ----------

    /// <summary>
    /// Dibuja un mapa. Estilo "sonora": 4 colores exactos (uno por capa) y fondo silencioso; estilo "vista": paletas detalladas.
    /// </summary>
    public MapaRender Render(string tipoId, int anio, string regionId, bool sonora)
    {
        var tipo = Tipo(tipoId) ?? throw new ArgumentException($"Tipo desconocido: {tipoId}");
        var region = RegionPorId(regionId) ?? throw new ArgumentException($"Región desconocida: {regionId}");
        int y = Math.Clamp(anio - Year0, 0, Years - 1);

        var (inside, (x0, x1, y0, y1)) = Area(region);
        int cw = x1 - x0, ch = y1 - y0;
        int scale = Math.Max(1, MaxSide / Math.Max(cw, ch));
        var counts = new int[4]; int cells = 0;

        // Color y capa (0–3, o -1 si no suena) de cada celda del recorte.
        var cellColor = new uint[cw * ch];
        for (int cy = 0; cy < ch; cy++)
            for (int cx = 0; cx < cw; cx++)
            {
                int i = (y0 + cy) * Grid.W + x0 + cx;
                uint col;
                if (!_peru[i] || !inside[i]) col = Background;
                else if (_water[i]) col = sonora ? WaterDark : WaterView;
                else if (!_valid[i]) col = sonora ? PeruQuiet : NoDataView;
                else
                {
                    cells++;
                    if (sonora)
                    {
                        int layer = Layer(tipo.Id, y, i);
                        if (layer >= 0) counts[layer]++;
                        col = layer < 0 ? PeruQuiet : Rgba(tipo.Paleta[layer].Rgb);
                    }
                    else col = ViewColor(tipo.Id, y, i);
                }
                cellColor[cy * cw + cx] = col;
            }

        int w = cw * scale, h = ch * scale;
        var px = new uint[w * h];
        for (int py = 0; py < h; py++)
        {
            int row = py / scale * cw;
            for (int pxx = 0; pxx < w; pxx++) px[py * w + pxx] = cellColor[row + pxx / scale];
        }
        var box = new GeoBox(Grid.Lon(x0) - Grid.Res / 2, Grid.Lon(x1 - 1) + Grid.Res / 2, Grid.Lat(y1 - 1) - Grid.Res / 2, Grid.Lat(y0) + Grid.Res / 2);
        return new MapaRender(w, h, px, box, counts.Select(c => cells == 0 ? 0 : Math.Round(c / (double)cells, 4)).ToArray());
    }

    /// <summary>Capa SONORA de una celda (índice en la paleta del tipo) o -1 si debe quedar en silencio.</summary>
    int Layer(string tipo, int y, int i)
    {
        switch (tipo)
        {
            case "biomas":
                return ArgMax(y, i) switch { 5 or 6 => 0, 7 => 1, 2 or 3 or 4 => 2, _ => 3 };
            case "cambio_bosque":
            {
                double s = _forestSlope[i];
                if (s <= -0.012) return 1;
                if (s <= -0.004) return 0;
                if (s >= 0.004) return 2;
                return Forest(Years - 1, i) >= 0.5 ? 3 : -1;
            }
            case "tendencia_ndvi":
            {
                if (Math.Abs(_ndviZ[i]) < 1.96) return -1;
                double s = _ndviSlope[i];
                return s <= -0.004 ? 1 : s < 0 ? 0 : s >= 0.004 ? 3 : 2;
            }
            case "anomalia_ndvi":
            {
                double a = NdviAnomaly(y, i);
                return a <= -0.08 ? 1 : a <= -0.03 ? 2 : a >= 0.08 ? 3 : a >= 0.03 ? 0 : -1;
            }
            default: return -1;
        }
    }

    uint ViewColor(string tipo, int y, int i) => tipo switch
    {
        "biomas" => Biomas.Clases[ArgMax(y, i)].Rgba,
        "cambio_bosque" => Mapas.Diverging(_forestSlope[i] / 0.02),
        "tendencia_ndvi" => Mapas.Diverging(Math.Abs(_ndviZ[i]) >= 1.96 ? _ndviSlope[i] / 0.006 : 0),
        "anomalia_ndvi" => Mapas.Diverging(NdviAnomaly(y, i) / 0.12),
        _ => Background,
    };

    static uint Rgba(int[] c) => (uint)(c[0] << 24 | c[1] << 16 | c[2] << 8 | 0xFF);

    /// <summary>Ventana de celdas [x0,x1) × [y0,y1) que cubre la caja geográfica.</summary>
    static (int X0, int X1, int Y0, int Y1) Window(GeoBox b)
    {
        int x0 = Math.Clamp((int)Math.Floor((b.LonMin - Grid.LonMin) / Grid.Res + 1e-9), 0, Grid.W - 1);
        int x1 = Math.Clamp((int)Math.Ceiling((b.LonMax - Grid.LonMin) / Grid.Res - 1e-9), x0 + 1, Grid.W);
        int y0 = Math.Clamp((int)Math.Floor((Grid.LatMax - b.LatMax) / Grid.Res + 1e-9), 0, Grid.H - 1);
        int y1 = Math.Clamp((int)Math.Ceiling((Grid.LatMax - b.LatMin) / Grid.Res - 1e-9), y0 + 1, Grid.H);
        return (x0, x1, y0, y1);
    }

    // ---------- Estadísticas ----------

    public EstadisticasRegion Stats(Region r)
    {
        var (inside, (x0, x1, y0, y1)) = Area(r);
        var cells = new List<int>();
        for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++) { int i = y * Grid.W + x; if (_peru[i] && inside[i] && _valid[i]) cells.Add(i); }
        if (cells.Count == 0) return new(r.Nombre, 0, 0, 0, 0, 0, 0, "");
        return new EstadisticasRegion(
            r.Nombre, cells.Count,
            Math.Round(cells.Average(i => _ndviSlope[i]) * 10, 4),
            Math.Round(100.0 * cells.Count(i => _ndviZ[i] <= -1.96) / cells.Count, 1),
            Math.Round(100.0 * cells.Count(i => _ndviZ[i] >= 1.96) / cells.Count, 1),
            Math.Round(cells.Average(i => Forest(0, i)), 3),
            Math.Round(cells.Average(i => Forest(Years - 1, i)), 3),
            Biomas.Clases[cells.GroupBy(i => ArgMax(Years - 1, i)).MaxBy(g => g.Count())!.Key].Nombre);
    }

    /// <summary>Texto alternativo con las proporciones reales de cada capa del mapa dibujado.</summary>
    public static string AltText(TipoMapa tipo, Region region, int? anio, MapaRender m)
    {
        var parts = tipo.Paleta.Select((c, k) => (c.Name, Share: m.Shares[k])).Where(p => p.Share >= 0.005)
            .OrderByDescending(p => p.Share).Select(p => $"{p.Name.ToLowerInvariant()} {Math.Round(p.Share * 100)} %").ToList();
        string when = anio is int a ? $" en {a}" : "";
        string what = parts.Count > 0 ? string.Join(", ", parts) : "sin cambios destacables";
        return $"{tipo.Nombre} de {region.Nombre}{when}, de oeste a este. Proporción de la superficie: {what}. El resto queda en silencio.";
    }
}
