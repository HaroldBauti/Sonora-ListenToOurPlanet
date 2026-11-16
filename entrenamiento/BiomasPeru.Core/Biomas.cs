namespace BiomasPeru;

public sealed record Bioma(int Id, string Nombre, uint Rgba);

/// <summary>
/// Características por celda y año, y etiquetas de bioma peruano derivadas de MODIS IGBP + altitud + vertiente.
/// </summary>
public sealed class Biomas
{
    public static readonly Bioma[] Clases =
    [
        new(0, "Desierto costero", 0xE9C46AFF),
        new(1, "Bosque seco ecuatorial", 0xB08D3CFF),
        new(2, "Valles y matorral andino", 0xD4845AFF),
        new(3, "Puna y páramo", 0xA3B86CFF),
        new(4, "Nival y glaciar", 0xF1F5F9FF),
        new(5, "Yungas (selva alta)", 0x2FA36BFF),
        new(6, "Selva baja amazónica", 0x0B5D2EFF),
        new(7, "Agricultura y urbano", 0xE76F51FF),
    ];
    public const int K = 8;
    public const int Dim = 29;
    public static readonly string[] Features =
        [.. Enumerable.Range(1, 12).Select(m => $"ndvi_m{m:00}"), .. Enumerable.Range(1, 12).Select(m => $"lst_m{m:00}"),
         "altitud_km", "relieve_km", "latitud", "vertiente_pacifico", "ndvi_vecinos"];

    readonly Datos _d;
    public readonly bool[] Valid = new bool[Grid.N];
    public readonly bool[] West = new bool[Grid.N];
    public readonly bool[] Water = new bool[Grid.N];
    public readonly int[] Label = new int[Grid.N];
    readonly float[][] _ndviClim = new float[12][], _lstClim = new float[12][];
    readonly float[] _relief = new float[Grid.N];
    public readonly float[][] NdviAnnual; // [año][celda], con huecos rellenados

    public Biomas(Datos d)
    {
        _d = d;
        for (int m = 0; m < 12; m++) { _ndviClim[m] = Clim(d.Ndvi, m); _lstClim[m] = Clim(d.LstC, m); }

        // Vertiente del Pacífico: al oeste de la cresta andina (máxima altitud de cada fila dentro de Perú).
        for (int y = 0; y < Grid.H; y++)
        {
            int crest = -1; float best = float.MinValue;
            for (int x = 0; x < Grid.W; x++) { int i = y * Grid.W + x; if (d.Peru[i] && d.ElevM[i] > best) { best = d.ElevM[i]; crest = x; } }
            for (int x = 0; x < crest; x++) West[y * Grid.W + x] = true;
        }

        for (int i = 0; i < Grid.N; i++)
        {
            Label[i] = -1;
            if (!d.Peru[i] || float.IsNaN(d.ElevM[i])) continue;
            int igbp = Mode(i);
            Water[i] = igbp == 17;
            bool ok = !Water[i];
            for (int m = 0; m < 12 && ok; m++) ok = !float.IsNaN(_ndviClim[m][i]) && !float.IsNaN(_lstClim[m][i]);
            Valid[i] = ok;
            if (ok) Label[i] = Rule(igbp, d.ElevM[i], Grid.Lat(i / Grid.W), West[i]);
        }

        for (int i = 0; i < Grid.N; i++)
        {
            if (!Valid[i]) continue;
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (int j in Neighbors(i)) if (!float.IsNaN(d.ElevM[j])) { lo = Math.Min(lo, d.ElevM[j]); hi = Math.Max(hi, d.ElevM[j]); }
            _relief[i] = (hi - lo) / 1000f;
        }

        NdviAnnual = new float[Datos.Years][];
        for (int yr = 0; yr < Datos.Years; yr++)
        {
            var a = NdviAnnual[yr] = new float[Grid.N];
            for (int i = 0; i < Grid.N; i++)
            {
                if (!Valid[i]) { a[i] = float.NaN; continue; }
                double s = 0; for (int m = 0; m < 12; m++) s += Fill(d.Ndvi, _ndviClim, yr, m, i);
                a[i] = (float)(s / 12);
            }
        }
    }

    /// <summary>Reglas de etiquetado: clase IGBP modal 2001–2024 + altitud + vertiente → bioma peruano (o -1 si es ambiguo).</summary>
    static int Rule(int igbp, float elev, double lat, bool west)
    {
        bool forest = igbp is >= 1 and <= 5;
        bool open = igbp is 6 or 7 or 8 or 9 or 10 or 16;
        if (igbp == 15 || (igbp == 16 && elev >= 4800)) return 4;
        if (igbp is 12 or 13 or 14) return 7;
        if (west && lat > -8 && elev < 1500 && (forest || igbp is 6 or 7 or 8 or 9)) return 1;
        if (west && elev < 2000 && igbp is 16 or 7 or 10) return 0;
        if (!west && elev < 800 && (forest || igbp is 8 or 11)) return 6;
        if (!west && elev is >= 800 and < 3500 && (forest || igbp == 8)) return 5;
        if (elev >= 3500 && open) return 3;
        if (elev is >= 1500 and < 3500 && open) return 2;
        return -1;
    }

    public double[] Features_(int cell, int year)
    {
        var f = new double[Dim];
        for (int m = 0; m < 12; m++)
        {
            f[m] = Fill(_d.Ndvi, _ndviClim, year, m, cell);
            f[12 + m] = Fill(_d.LstC, _lstClim, year, m, cell);
        }
        f[24] = _d.ElevM[cell] / 1000.0;
        f[25] = _relief[cell];
        f[26] = Grid.Lat(cell / Grid.W);
        f[27] = West[cell] ? 1 : 0;
        double s = 0; int n = 0;
        foreach (int j in Neighbors(cell)) if (Valid[j]) { s += NdviAnnual[year][j]; n++; }
        f[28] = n > 0 ? s / n : NdviAnnual[year][cell];
        return f;
    }

    static float Fill(float[][] series, float[][] clim, int year, int month, int cell)
    {
        float v = series[year * 12 + month][cell];
        return float.IsNaN(v) ? clim[month][cell] : v;
    }

    float[] Clim(float[][] series, int month)
    {
        var c = new float[Grid.N];
        for (int i = 0; i < Grid.N; i++)
        {
            double s = 0; int n = 0;
            for (int y = 0; y < Datos.Years; y++) { float v = series[y * 12 + month][i]; if (!float.IsNaN(v)) { s += v; n++; } }
            c[i] = n >= 3 ? (float)(s / n) : float.NaN;
        }
        return c;
    }

    int Mode(int cell)
    {
        Span<int> h = stackalloc int[19];
        for (int y = 0; y < Datos.Years; y++) { float v = _d.Igbp[y][cell]; if (!float.IsNaN(v)) h[(int)v]++; }
        int best = 0; for (int k = 1; k < 19; k++) if (h[k] > h[best]) best = k;
        return best;
    }

    public static IEnumerable<int> Neighbors(int i)
    {
        int x0 = i % Grid.W, y0 = i / Grid.W;
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int x = x0 + dx, y = y0 + dy;
                if (x >= 0 && y >= 0 && x < Grid.W && y < Grid.H) yield return y * Grid.W + x;
            }
    }

    /// <summary>Partición espacial por bloques de 0,5° (10x10 celdas): 70 % entrenamiento, 15 % validación, 15 % prueba.</summary>
    public static int Split(int cell, int seed)
    {
        int bx = cell % Grid.W / 10, by = cell / Grid.W / 10;
        uint h = (uint)(bx * 73856093 ^ by * 19349663 ^ seed * 83492791);
        h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
        int r = (int)(h % 100);
        return r < 70 ? 0 : r < 85 ? 1 : 2;
    }

    public sealed record Muestra(List<double[]> X, List<double[]> Y, List<int> Clase);

    /// <summary>Muestra estratificada de pares (celda, año) por partición, con tope por clase para equilibrar.</summary>
    public Muestra[] Sample(int[] capPerClass, int seed)
    {
        var rng = new Random(seed);
        var pools = new List<(int Cell, int Year)>[3, K];
        for (int s = 0; s < 3; s++) for (int k = 0; k < K; k++) pools[s, k] = [];
        for (int i = 0; i < Grid.N; i++)
            if (Valid[i] && Label[i] >= 0)
                for (int y = 0; y < Datos.Years; y++) pools[Split(i, seed), Label[i]].Add((i, y));

        var result = new Muestra[3];
        for (int s = 0; s < 3; s++)
        {
            var picks = new List<(int Cell, int Year, int K)>();
            for (int k = 0; k < K; k++)
            {
                var pool = pools[s, k];
                rng.Shuffle(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(pool));
                picks.AddRange(pool.Take(capPerClass[s]).Select(p => (p.Cell, p.Year, k)));
            }
            rng.Shuffle(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(picks));
            result[s] = new Muestra(
                picks.Select(p => Features_(p.Cell, p.Year)).ToList(),
                picks.Select(p => { var t = new double[K]; t[p.K] = 1; return t; }).ToList(),
                picks.Select(p => p.K).ToList());
        }
        return result;
    }

    public int[,] LabelCounts(int seed)
    {
        var c = new int[3, K];
        for (int i = 0; i < Grid.N; i++) if (Valid[i] && Label[i] >= 0) c[Split(i, seed), Label[i]]++;
        return c;
    }
}
