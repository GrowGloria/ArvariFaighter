using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DndFighter.Engine;

/// <summary>Примитивы поверх SpriteBatch: прямоугольники, рамки. Всё в пикселях виртуального экрана.</summary>
public sealed class Draw
{
    public SpriteBatch Batch { get; }
    public Texture2D Pixel { get; }
    public BitmapFont Font { get; }

    public Draw(GraphicsDevice device, SpriteBatch batch)
    {
        Batch = batch;
        Pixel = new Texture2D(device, 1, 1);
        Pixel.SetData(new[] { Color.White });
        Font = new BitmapFont(device);
    }

    public void Rect(float x, float y, float w, float h, Color c) =>
        Batch.Draw(Pixel, new Rectangle((int)MathF.Round(x), (int)MathF.Round(y), (int)MathF.Round(w), (int)MathF.Round(h)), c);

    public void Frame(float x, float y, float w, float h, Color c)
    {
        Rect(x, y, w, 1, c);
        Rect(x, y + h - 1, w, 1, c);
        Rect(x, y, 1, h, c);
        Rect(x + w - 1, y, 1, h, c);
    }

    public void Text(string text, float x, float y, Color c, int scale = 1, bool shadow = true)
    {
        if (shadow) Font.Draw(Batch, text, (int)x + scale, (int)y + scale, Color.Black * 0.8f, scale);
        Font.Draw(Batch, text, (int)x, (int)y, c, scale);
    }

    public void TextCentered(string text, float cx, float y, Color c, int scale = 1, bool shadow = true) =>
        Text(text, cx - Font.Measure(text, scale) / 2f, y, c, scale, shadow);

    public void TextRight(string text, float right, float y, Color c, int scale = 1, bool shadow = true) =>
        Text(text, right - Font.Measure(text, scale), y, c, scale, shadow);
}
