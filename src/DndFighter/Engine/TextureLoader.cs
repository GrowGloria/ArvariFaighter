using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DndFighter.Engine;

public static class TextureLoader
{
    /// <summary>Загружает PNG с диска и переводит в premultiplied alpha (как ждёт SpriteBatch).</summary>
    public static Texture2D Load(GraphicsDevice device, string path)
    {
        using var stream = File.OpenRead(path);
        var tex = Texture2D.FromStream(device, stream);
        var data = new Color[tex.Width * tex.Height];
        tex.GetData(data);
        for (int i = 0; i < data.Length; i++)
        {
            var c = data[i];
            if (c.A == 255) continue;
            data[i] = new Color(c.R * c.A / 255, c.G * c.A / 255, c.B * c.A / 255, c.A);
        }
        tex.SetData(data);
        return tex;
    }
}

public static class ColorUtil
{
    public static Color Parse(string hex, Color fallback = default)
    {
        var s = hex.TrimStart('#');
        try
        {
            if (s.Length == 6)
                return new Color(Convert.ToByte(s[..2], 16), Convert.ToByte(s[2..4], 16), Convert.ToByte(s[4..6], 16));
        }
        catch (FormatException) { }
        return fallback == default ? Color.Magenta : fallback;
    }
}
