using System.IO.Compression;

namespace SpriteGen;

public readonly record struct Rgba(byte R, byte G, byte B, byte A = 255)
{
    public static readonly Rgba Clear = new(0, 0, 0, 0);

    public Rgba Shade(float k) => new(
        (byte)Math.Clamp(R * k, 0, 255), (byte)Math.Clamp(G * k, 0, 255), (byte)Math.Clamp(B * k, 0, 255), A);

    public Rgba WithAlpha(float a) => this with { A = (byte)Math.Clamp(A * a, 0, 255) };

    public static Rgba Lerp(Rgba a, Rgba b, float t) => new(
        (byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t),
        (byte)(a.A + (b.A - a.A) * t));
}

/// <summary>Простой RGBA-холст с примитивами для пиксель-арта и записью в PNG.</summary>
public sealed class Canvas
{
    public int W { get; }
    public int H { get; }
    public Rgba[] Px { get; }

    public Canvas(int w, int h)
    {
        W = w;
        H = h;
        Px = new Rgba[w * h];
    }

    public Rgba Get(int x, int y) => x < 0 || y < 0 || x >= W || y >= H ? Rgba.Clear : Px[y * W + x];

    public void Set(int x, int y, Rgba c)
    {
        if (x < 0 || y < 0 || x >= W || y >= H || c.A == 0) return;
        ref var d = ref Px[y * W + x];
        if (c.A == 255 || d.A == 0) { d = c; return; }
        float a = c.A / 255f;
        d = new Rgba((byte)(d.R + (c.R - d.R) * a), (byte)(d.G + (c.G - d.G) * a), (byte)(d.B + (c.B - d.B) * a),
            (byte)Math.Max(d.A, c.A));
    }

    public void Rect(int x, int y, int w, int h, Rgba c)
    {
        for (int j = y; j < y + h; j++)
            for (int i = x; i < x + w; i++) Set(i, j, c);
    }

    public void Disc(float cx, float cy, float r, Rgba c)
    {
        int x0 = (int)MathF.Floor(cx - r), x1 = (int)MathF.Ceiling(cx + r);
        int y0 = (int)MathF.Floor(cy - r), y1 = (int)MathF.Ceiling(cy + r);
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                if (dx * dx + dy * dy <= r * r) Set(x, y, c);
            }
    }

    /// <summary>Толстая линия: штампуем круги вдоль отрезка.</summary>
    public void Line(float x0, float y0, float x1, float y1, float thickness, Rgba c)
    {
        float len = MathF.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
        int steps = Math.Max(1, (int)(len * 2));
        float r = Math.Max(0.5f, thickness / 2f);
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            float x = x0 + (x1 - x0) * t, y = y0 + (y1 - y0) * t;
            if (thickness <= 1) Set((int)MathF.Floor(x), (int)MathF.Floor(y), c);
            else Disc(x, y, r, c);
        }
    }

    public void Polygon(IReadOnlyList<(float X, float Y)> pts, Rgba c)
    {
        float minY = pts.Min(p => p.Y), maxY = pts.Max(p => p.Y);
        for (int y = (int)MathF.Floor(minY); y <= (int)MathF.Ceiling(maxY); y++)
        {
            float sy = y + 0.5f;
            var xs = new List<float>();
            for (int i = 0; i < pts.Count; i++)
            {
                var a = pts[i];
                var b = pts[(i + 1) % pts.Count];
                if ((a.Y <= sy && b.Y > sy) || (b.Y <= sy && a.Y > sy))
                    xs.Add(a.X + (sy - a.Y) / (b.Y - a.Y) * (b.X - a.X));
            }
            xs.Sort();
            for (int i = 0; i + 1 < xs.Count; i += 2)
                for (int x = (int)MathF.Round(xs[i]); x < (int)MathF.Round(xs[i + 1]); x++)
                    Set(x, y, c);
        }
    }

    /// <summary>Однопиксельная тёмная обводка вокруг непрозрачных пикселей.</summary>
    public void Outline(Rgba color)
    {
        var copy = (Rgba[])Px.Clone();
        bool Opaque(int x, int y) => x >= 0 && y >= 0 && x < W && y < H && copy[y * W + x].A > 100;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                if (copy[y * W + x].A > 100) continue;
                if (Opaque(x - 1, y) || Opaque(x + 1, y) || Opaque(x, y - 1) || Opaque(x, y + 1))
                    Px[y * W + x] = color;
            }
    }

    public (int MinX, int MinY, int MaxX, int MaxY)? Bounds()
    {
        int minX = W, minY = H, maxX = -1, maxY = -1;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                if (Px[y * W + x].A > 0)
                {
                    minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                    minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                }
        return maxX < 0 ? null : (minX, minY, maxX, maxY);
    }

    public void Blit(Canvas src, int dx, int dy, float alpha = 1f)
    {
        for (int y = 0; y < src.H; y++)
            for (int x = 0; x < src.W; x++)
            {
                var c = src.Px[y * src.W + x];
                if (c.A == 0) continue;
                Set(dx + x, dy + y, alpha >= 1 ? c : c.WithAlpha(alpha));
            }
    }

    public void SavePng(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var fs = File.Create(path);
        fs.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });

        var ihdr = new byte[13];
        WriteBE(ihdr, 0, W);
        WriteBE(ihdr, 4, H);
        ihdr[8] = 8;  // бит на канал
        ihdr[9] = 6;  // RGBA
        Chunk(fs, "IHDR", ihdr);

        using var raw = new MemoryStream();
        using (var z = new ZLibStream(raw, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            var row = new byte[1 + W * 4];
            for (int y = 0; y < H; y++)
            {
                row[0] = 0;
                for (int x = 0; x < W; x++)
                {
                    var c = Px[y * W + x];
                    int o = 1 + x * 4;
                    row[o] = c.R; row[o + 1] = c.G; row[o + 2] = c.B; row[o + 3] = c.A;
                }
                z.Write(row);
            }
        }
        Chunk(fs, "IDAT", raw.ToArray());
        Chunk(fs, "IEND", Array.Empty<byte>());
    }

    private static void Chunk(Stream s, string type, byte[] data)
    {
        var len = new byte[4];
        WriteBE(len, 0, data.Length);
        s.Write(len);
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        s.Write(typeBytes);
        s.Write(data);
        uint crc = Crc32(typeBytes, 0xFFFFFFFF);
        crc = Crc32(data, crc) ^ 0xFFFFFFFF;
        var crcBytes = new byte[4];
        WriteBE(crcBytes, 0, (int)crc);
        s.Write(crcBytes);
    }

    private static void WriteBE(byte[] b, int o, int v)
    {
        b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v;
    }

    private static readonly uint[] CrcTable = Enumerable.Range(0, 256).Select(n =>
    {
        uint c = (uint)n;
        for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
        return c;
    }).ToArray();

    private static uint Crc32(byte[] data, uint crc)
    {
        foreach (var b in data) crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc;
    }
}
