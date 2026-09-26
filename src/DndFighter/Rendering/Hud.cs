using DndFighter.Combat;
using DndFighter.Engine;
using Microsoft.Xna.Framework;

namespace DndFighter.Rendering;

public static class Hud
{
    private const int BarW = 190, BarH = 9, BarY = 10;

    /// <param name="seconds">Оставшееся время раунда, -1 — без таймера (тренировка).</param>
    public static void Draw(Draw d, FightWorld w, int[] wins, int seconds)
    {
        for (int i = 0; i < 2; i++) DrawSide(d, w.Fighters[i], i, wins[i]);

        d.Rect(240 - 14, 6, 28, 18, Color.Black * 0.6f);
        d.TextCentered(seconds < 0 ? "--" : Math.Max(0, seconds).ToString("00"), 240, 8, Color.White, 2);
    }

    private static void DrawSide(Draw d, Fighter f, int side, int wins)
    {
        bool left = side == 0;
        int x = left ? 20 : 480 - 20 - BarW;
        float max = f.Def.Stats.Health;

        d.Rect(x - 1, BarY - 1, BarW + 2, BarH + 2, Color.Black);
        d.Rect(x, BarY, BarW, BarH, new Color(50, 20, 25));
        DrawFill(d, x, BarY, BarW, BarH, f.DisplayedHealth / max, left, new Color(220, 50, 40));
        float ratio = f.Health / max;
        var hp = ratio > 0.3f ? new Color(250, 210, 70) : new Color(255, 120, 50);
        DrawFill(d, x, BarY, BarW, BarH, ratio, left, hp);
        d.Rect(x, BarY, BarW, 2, Color.White * 0.25f);

        // Имя и победы в раундах.
        int nameY = BarY + BarH + 3;
        if (left) d.Text(f.Def.Name, x, nameY, Color.White);
        else d.TextRight(f.Def.Name, x + BarW, nameY, Color.White);
        for (int r = 0; r < 2; r++)
        {
            int px = left ? x + BarW - 8 - r * 9 : x + 2 + r * 9;
            d.Rect(px, nameY, 7, 7, Color.Black);
            d.Rect(px + 1, nameY + 1, 5, 5, r < wins ? new Color(255, 210, 60) : new Color(60, 55, 80));
        }

        // Классовые ресурсы.
        int ry = nameY + 11;
        foreach (var res in f.Def.Resources)
        {
            var color = ColorUtil.Parse(res.Color);
            float value = f.GetResource(res.Id);
            int labelW = d.Font.Measure(res.Name);
            if (res.Display == "bar")
            {
                const int w = 70;
                int bx = left ? x : x + BarW - w;
                d.Rect(bx - 1, ry - 1, w + 2, 6, Color.Black);
                DrawFill(d, bx, ry, w, 4, value / res.Max, left, color);
                if (left) d.Text(res.Name, bx + w + 4, ry - 2, color);
                else d.Text(res.Name, bx - 4 - labelW, ry - 2, color);
            }
            else
            {
                int count = (int)MathF.Ceiling(res.Max);
                int pipsW = count * 9;
                int bx = left ? x : x + BarW - pipsW;
                for (int i = 0; i < count; i++)
                {
                    int px = bx + i * 9;
                    d.Rect(px, ry - 1, 7, 7, Color.Black);
                    float fill = Math.Clamp(value - i, 0, 1);
                    if (fill >= 1) d.Rect(px + 1, ry, 5, 5, color);
                    else if (fill > 0) d.Rect(px + 1, ry + 5 - (int)(5 * fill), 5, (int)(5 * fill), color * 0.5f);
                }
                if (left) d.Text(res.Name, bx + pipsW + 3, ry - 1, color);
                else d.Text(res.Name, bx - 3 - labelW, ry - 1, color);
            }
            ry += 10;
        }

        // Счётчик комбо показываем на стороне атакующего.
        var target = f.Opponent;
        if (target.ComboHits >= 2)
        {
            int cx = left ? 60 : 420;
            d.TextCentered($"{target.ComboHits} УДАР.", cx, 80, new Color(255, 230, 120), 2);
            d.TextCentered($"{target.ComboDamage} УРОНА", cx, 98, Color.White);
        }
    }

    private static void DrawFill(Draw d, int x, int y, int w, int h, float ratio, bool fromLeft, Color c)
    {
        int fw = (int)MathF.Round(w * Math.Clamp(ratio, 0, 1));
        if (fw <= 0) return;
        d.Rect(fromLeft ? x + w - fw : x, y, fw, h, c);
    }
}
