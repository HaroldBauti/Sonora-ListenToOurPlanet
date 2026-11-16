using System.IO.Compression;
using System.Text;

namespace BiomasPeru;

/// <summary>Lector y escritor PNG mínimo (sin dependencias) para las imágenes de NASA GIBS y los mapas de salida.</summary>
public static class Png
{
    /// <summary>Devuelve cada píxel como 0xRRGGBB, o -1 si es transparente.</summary>
    public static (int W, int H, int[] Pixels) Decode(byte[] data)
    {
        ReadOnlySpan<byte> sig = [137, 80, 78, 71, 13, 10, 26, 10];
        if (data.Length < 8 || !data.AsSpan(0, 8).SequenceEqual(sig)) throw new InvalidDataException("No es un PNG.");

        int pos = 8, w = 0, h = 0, depth = 0, colorType = 0, interlace = 0;
        byte[]? palette = null, trns = null;
        using var idat = new MemoryStream();
        while (pos + 8 <= data.Length)
        {
            int len = BE32(data, pos);
            string type = Encoding.ASCII.GetString(data, pos + 4, 4);
            var chunk = data.AsSpan(pos + 8, len);
            if (type == "IHDR") { w = BE32(data, pos + 8); h = BE32(data, pos + 12); depth = chunk[8]; colorType = chunk[9]; interlace = chunk[12]; }
            else if (type == "PLTE") palette = chunk.ToArray();
            else if (type == "tRNS") trns = chunk.ToArray();
            else if (type == "IDAT") idat.Write(chunk);
            else if (type == "IEND") break;
            pos += 12 + len;
        }
        if (interlace != 0) throw new NotSupportedException("PNG entrelazado no soportado.");

        int channels = colorType switch { 0 => 1, 2 => 3, 3 => 1, 4 => 2, 6 => 4, _ => throw new NotSupportedException($"Tipo de color {colorType}") };
        int bitsPerPixel = channels * depth;
        int stride = (w * bitsPerPixel + 7) / 8;
        int bpp = Math.Max(1, bitsPerPixel / 8);

        var raw = new byte[h * (stride + 1)];
        idat.Position = 0;
        using (var z = new ZLibStream(idat, CompressionMode.Decompress)) z.ReadExactly(raw);

        var img = new byte[h * stride];
        for (int y = 0; y < h; y++)
        {
            int filter = raw[y * (stride + 1)];
            var line = raw.AsSpan(y * (stride + 1) + 1, stride);
            var cur = img.AsSpan(y * stride, stride);
            var prev = y > 0 ? img.AsSpan((y - 1) * stride, stride) : Span<byte>.Empty;
            for (int i = 0; i < stride; i++)
            {
                int a = i >= bpp ? cur[i - bpp] : 0, b = y > 0 ? prev[i] : 0, c = i >= bpp && y > 0 ? prev[i - bpp] : 0;
                cur[i] = (byte)(line[i] + filter switch
                {
                    0 => 0,
                    1 => a,
                    2 => b,
                    3 => (a + b) >> 1,
                    4 => Paeth(a, b, c),
                    _ => throw new InvalidDataException($"Filtro PNG {filter}")
                });
            }
        }

        var px = new int[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var row = img.AsSpan(y * stride, stride);
                int r, g, bl, alpha = 255;
                if (colorType == 3)
                {
                    int idx = depth == 8 ? row[x] : (row[x * depth / 8] >> (8 - depth - (x * depth % 8))) & ((1 << depth) - 1);
                    r = palette![idx * 3]; g = palette[idx * 3 + 1]; bl = palette[idx * 3 + 2];
                    if (trns != null && idx < trns.Length) alpha = trns[idx];
                }
                else if (depth != 8) throw new NotSupportedException($"Profundidad {depth} no soportada.");
                else if (colorType == 2) { r = row[x * 3]; g = row[x * 3 + 1]; bl = row[x * 3 + 2]; }
                else if (colorType == 6) { r = row[x * 4]; g = row[x * 4 + 1]; bl = row[x * 4 + 2]; alpha = row[x * 4 + 3]; }
                else if (colorType == 0) { r = g = bl = row[x]; }
                else { r = g = bl = row[x * 2]; alpha = row[x * 2 + 1]; }
                px[y * w + x] = alpha == 0 ? -1 : (r << 16) | (g << 8) | bl;
            }
        return (w, h, px);
    }

    /// <summary>Escribe un PNG RGBA de 8 bits. <paramref name="rgba"/> = 0xRRGGBBAA por píxel.</summary>
    public static void Save(string path, int w, int h, uint[] rgba) => File.WriteAllBytes(path, Encode(w, h, rgba));

    /// <summary>Codifica un PNG RGBA de 8 bits en memoria. <paramref name="rgba"/> = 0xRRGGBBAA por píxel.</summary>
    public static byte[] Encode(int w, int h, uint[] rgba)
    {
        var raw = new byte[h * (w * 4 + 1)];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                uint v = rgba[y * w + x]; int o = y * (w * 4 + 1) + 1 + x * 4;
                raw[o] = (byte)(v >> 24); raw[o + 1] = (byte)(v >> 16); raw[o + 2] = (byte)(v >> 8); raw[o + 3] = (byte)v;
            }
        using var zs = new MemoryStream();
        using (var z = new ZLibStream(zs, CompressionLevel.Optimal, leaveOpen: true)) z.Write(raw);

        using var f = new MemoryStream();
        f.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        var ihdr = new byte[13];
        WriteBE32(ihdr, 0, w); WriteBE32(ihdr, 4, h); ihdr[8] = 8; ihdr[9] = 6;
        Chunk(f, "IHDR", ihdr);
        Chunk(f, "IDAT", zs.ToArray());
        Chunk(f, "IEND", []);
        return f.ToArray();
    }

    static void Chunk(Stream s, string type, byte[] data)
    {
        var head = new byte[8]; WriteBE32(head, 0, data.Length); Encoding.ASCII.GetBytes(type, 0, 4, head, 4);
        s.Write(head); s.Write(data);
        uint crc = Crc(Crc(0xFFFFFFFF, head.AsSpan(4, 4)), data) ^ 0xFFFFFFFF;
        var c = new byte[4]; WriteBE32(c, 0, (int)crc); s.Write(c);
    }

    static readonly uint[] CrcTable = Enumerable.Range(0, 256).Select(n =>
    {
        uint c = (uint)n; for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1; return c;
    }).ToArray();

    static uint Crc(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (var b in data) crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc;
    }

    static int Paeth(int a, int b, int c)
    {
        int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    static int BE32(byte[] d, int o) => (d[o] << 24) | (d[o + 1] << 16) | (d[o + 2] << 8) | d[o + 3];
    static void WriteBE32(byte[] d, int o, int v) { d[o] = (byte)(v >> 24); d[o + 1] = (byte)(v >> 16); d[o + 2] = (byte)(v >> 8); d[o + 3] = (byte)v; }
}
