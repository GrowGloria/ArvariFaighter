namespace SpriteGen;

/// <summary>Фон арены «Перекрёсток»: закатное небо, горы, руины замка, дорога.</summary>
public static class Stage
{
    public const int W = 640, H = 270, Floor = 244;

    public static void Build(string path)
    {
        var c = new Canvas(W, H);
        var rng = new Random(7);

        // Небо с дизерингом между полосами.
        Rgba[] sky =
        {
            new(28, 24, 58), new(46, 36, 86), new(78, 50, 110), new(128, 68, 120),
            new(186, 96, 110), new(226, 140, 104), new(246, 190, 120),
        };
        int skyH = 200;
        for (int y = 0; y < skyH; y++)
        {
            float t = y / (float)skyH * (sky.Length - 1);
            int i = Math.Min((int)t, sky.Length - 2);
            float frac = t - i;
            for (int x = 0; x < W; x++)
            {
                float bayer = Bayer(x, y);
                c.Set(x, y, frac > bayer ? sky[i + 1] : sky[i]);
            }
        }

        for (int i = 0; i < 90; i++)
        {
            int x = rng.Next(W), y = rng.Next(90);
            c.Set(x, y, new Rgba(255, 255, 230, (byte)rng.Next(120, 255)));
        }

        // Луна.
        c.Disc(470, 48, 14, new Rgba(250, 236, 200));
        c.Disc(478, 43, 11, new Rgba(40, 32, 76));
        c.Disc(465, 44, 3, new Rgba(230, 214, 180));
        c.Disc(474, 55, 2, new Rgba(230, 214, 180));

        Ridge(c, 150, 30, 0.012f, 0.031f, new Rgba(96, 62, 112), 1);
        Ridge(c, 175, 22, 0.02f, 0.05f, new Rgba(70, 46, 88), 2);

        // Руины замка на холме.
        var castle = new Rgba(52, 36, 70);
        c.Rect(90, 150, 70, 40, castle);
        c.Rect(80, 128, 18, 62, castle);
        c.Rect(150, 136, 16, 54, castle);
        for (int x = 80; x < 98; x += 6) c.Rect(x, 124, 4, 4, castle);
        for (int x = 150; x < 166; x += 6) c.Rect(x, 132, 4, 4, castle);
        for (int x = 98; x < 150; x += 8) c.Rect(x, 146, 5, 4, castle);
        c.Rect(86, 140, 3, 5, new Rgba(255, 200, 110));
        c.Rect(156, 148, 3, 5, new Rgba(255, 200, 110));

        Ridge(c, 205, 14, 0.03f, 0.07f, new Rgba(48, 58, 60), 3);

        // Деревья вдали.
        for (int i = 0; i < 26; i++)
        {
            int x = rng.Next(W), baseY = 205 + rng.Next(8);
            int h = 12 + rng.Next(10);
            var col = new Rgba(34, 46, 44);
            c.Polygon(new (float, float)[] { (x - 6, baseY), (x + 6, baseY), (x, baseY - h) }, col);
        }

        // Земля и дорога.
        for (int y = 210; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                var grass = (x * 7 + y * 13) % 11 == 0 ? new Rgba(62, 92, 52) : new Rgba(74, 104, 58);
                c.Set(x, y, y > 226 ? new Rgba(56, 80, 46) : grass);
            }
        for (int y = Floor - 12; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float n = Bayer(x, y);
                var dirt = n > 0.8f ? new Rgba(110, 86, 62) : n > 0.2f ? new Rgba(132, 104, 74) : new Rgba(146, 116, 84);
                c.Set(x, y, dirt);
            }
        c.Rect(0, Floor - 12, W, 1, new Rgba(92, 72, 52));
        for (int i = 0; i < 60; i++)
            c.Rect(rng.Next(W), Floor - 8 + rng.Next(30), 2 + rng.Next(3), 1, new Rgba(96, 76, 56));

        // Указатель на перекрёстке.
        var wood = new Rgba(92, 62, 40);
        c.Rect(318, 186, 4, 46, wood);
        c.Rect(300, 190, 26, 7, new Rgba(120, 82, 52));
        c.Rect(318, 202, 26, 7, new Rgba(120, 82, 52));
        c.Polygon(new (float, float)[] { (300, 190), (296, 193.5f), (300, 197) }, new Rgba(120, 82, 52));
        c.Polygon(new (float, float)[] { (344, 202), (348, 205.5f), (344, 209) }, new Rgba(120, 82, 52));

        c.SavePng(path);
        Console.WriteLine($"  {path}");
    }

    private static void Ridge(Canvas c, int baseY, int amp, float f1, float f2, Rgba color, int seed)
    {
        for (int x = 0; x < W; x++)
        {
            float h = MathF.Sin(x * f1 + seed) * amp * 0.6f + MathF.Sin(x * f2 + seed * 3) * amp * 0.4f;
            int top = baseY - (int)MathF.Abs(h);
            for (int y = top; y < H; y++) c.Set(x, y, color);
        }
    }

    private static float Bayer(int x, int y)
    {
        int[] m = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
        return m[(y & 3) * 4 + (x & 3)] / 16f;
    }
}
