using Hba.NeuralNet;

namespace BiomasPeru;

/// <summary>Predicción por año, tendencias (Theil-Sen + Mann-Kendall) y mapas de calor PNG listos para SONORA.</summary>
public static class Mapas
{
    public const int Scale = 3; // 260x370 → 780x1110
    const uint Background = 0x0B0E14FF, WaterColor = 0x2B6CB0FF, NoData = 0x3A3F4AFF;

    /// <summary>Probabilidades [año][celda*K] para todas las celdas válidas.</summary>
    public static float[][] Predict(Perceptron net, Biomas b)
    {
        var probs = new float[Datos.Years][];
        var cells = Enumerable.Range(0, Grid.N).Where(i => b.Valid[i]).ToArray();
        for (int y = 0; y < Datos.Years; y++)
        {
            var p = probs[y] = new float[Grid.N * Biomas.K];
            int year = y;
            Parallel.ForEach(cells, i =>
            {
                var o = net.Predict(b.Features_(i, year));
                for (int k = 0; k < Biomas.K; k++) p[i * Biomas.K + k] = (float)o[k];
            });
        }
        return probs;
    }

    public static (double Slope, double Z) TheilSenMannKendall(ReadOnlySpan<double> v)
    {
        int n = v.Length, s = 0;
        var slopes = new double[n * (n - 1) / 2];
        int c = 0;
        for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++) { slopes[c++] = (v[j] - v[i]) / (j - i); s += Math.Sign(v[j] - v[i]); }
        Array.Sort(slopes);
        double med = slopes.Length % 2 == 1 ? slopes[slopes.Length / 2] : (slopes[slopes.Length / 2 - 1] + slopes[slopes.Length / 2]) / 2;
        double var = n * (n - 1) * (2.0 * n + 5) / 18.0;
        double z = s > 0 ? (s - 1) / Math.Sqrt(var) : s < 0 ? (s + 1) / Math.Sqrt(var) : 0;
        return (med, z);
    }

    /// <summary>Tendencia por celda de una serie anual [año][celda].</summary>
    public static (float[] Slope, float[] Z) Trend(Func<int, int, double> series, Biomas b)
    {
        var slope = new float[Grid.N]; var z = new float[Grid.N];
        Parallel.For(0, Grid.N, i =>
        {
            if (!b.Valid[i]) { slope[i] = z[i] = float.NaN; return; }
            Span<double> v = stackalloc double[Datos.Years];
            for (int y = 0; y < Datos.Years; y++) v[y] = series(y, i);
            var (s, zz) = TheilSenMannKendall(v);
            slope[i] = (float)s; z[i] = (float)zz;
        });
        return (slope, z);
    }

    // ---------- Paletas ----------

    /// <summary>Divergente marrón → blanco → verde (BrBG, apta para daltonismo). t en [-1, 1].</summary>
    public static uint Diverging(double t)
    {
        (double, int, int, int)[] stops = [(-1, 0x54, 0x30, 0x05), (-0.5, 0xBF, 0x81, 0x2D), (0, 0xF5, 0xF5, 0xF5), (0.5, 0x35, 0x97, 0x8F), (1, 0x00, 0x3C, 0x30)];
        return Ramp(stops, Math.Clamp(t, -1, 1));
    }

    /// <summary>Secuencial oscuro → verde → amarillo (tipo viridis). t en [0, 1].</summary>
    public static uint Sequential(double t)
    {
        (double, int, int, int)[] stops = [(0, 0x44, 0x01, 0x54), (0.25, 0x3B, 0x52, 0x8B), (0.5, 0x21, 0x91, 0x8C), (0.75, 0x5E, 0xC9, 0x62), (1, 0xFD, 0xE7, 0x25)];
        return Ramp(stops, Math.Clamp(t, 0, 1));
    }

    static uint Ramp((double T, int R, int G, int B)[] s, double t)
    {
        int k = 0; while (k < s.Length - 2 && t > s[k + 1].T) k++;
        double f = (t - s[k].T) / (s[k + 1].T - s[k].T);
        int L(int a, int b) => (int)Math.Round(a + (b - a) * f);
        return (uint)(L(s[k].R, s[k + 1].R) << 24 | L(s[k].G, s[k + 1].G) << 16 | L(s[k].B, s[k + 1].B) << 8 | 0xFF);
    }

    // ---------- Escritura ----------

    public static void Save(string path, Biomas b, Func<int, uint> color)
    {
        int w = Grid.W * Scale, h = Grid.H * Scale;
        var px = new uint[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y / Scale * Grid.W + x / Scale;
                px[y * w + x] = b.Water[i] ? WaterColor : b.Valid[i] ? color(i) : IsPeruNoData(b, i) ? NoData : Background;
            }
        Png.Save(path, w, h, px);
    }

    static bool IsPeruNoData(Biomas b, int i) => b.Label.Length > 0 && !b.Valid[i] && PeruMask is { } m && m[i];
    public static bool[]? PeruMask;

    public static void ColorBar(string path, Func<double, uint> palette, double from, double to)
    {
        const int w = 512, h = 24;
        var px = new uint[w * h];
        for (int x = 0; x < w; x++) { uint c = palette(from + (to - from) * x / (w - 1)); for (int y = 0; y < h; y++) px[y * w + x] = c; }
        Png.Save(path, w, h, px);
    }
}
