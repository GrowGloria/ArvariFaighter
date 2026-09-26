using DndFighter.Combat;
using DndFighter.Combat.Effects;
using DndFighter.Data;
using DndFighter.Engine;
using DndFighter.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DndFighter.Diagnostics;

/// <summary>
/// Отладочный лист боксов: для каждого персонажа — стойки и все приёмы на ударном кадре
/// с хёртбоксами (синие), хитбоксами (красные), пушбоксом (жёлтый) и снарядами.
/// Запуск: DndFighter.exe --boxsheet папка
/// </summary>
public static class BoxSheet
{
    private const int CellW = 170, CellH = 84, OriginX = 50, OriginY = 74, Cols = 5, Scale = 2;

    public static void Save(GraphicsDevice device, SpriteBatch batch, Draw d, ContentLoader loader, string dir)
    {
        Directory.CreateDirectory(dir);
        foreach (var def in loader.Characters)
        {
            var cells = BuildCells(def);
            int rows = (cells.Count + Cols - 1) / Cols;
            using var target = new RenderTarget2D(device, Cols * CellW * Scale, rows * CellH * Scale);
            device.SetRenderTarget(target);
            device.Clear(new Color(40, 40, 52));
            batch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: Matrix.CreateScale(Scale));
            for (int i = 0; i < cells.Count; i++)
                DrawCell(d, def, cells[i], i % Cols * CellW, i / Cols * CellH);
            batch.End();
            device.SetRenderTarget(null);
            using var fs = File.Create(Path.Combine(dir, $"boxes_{def.Id}.png"));
            target.SaveAsPng(fs, target.Width, target.Height);
        }
    }

    private sealed record Cell(string Label, string Anim, int Frame, Stance Stance, MoveDef? Move);

    private static List<Cell> BuildCells(CharacterDef def)
    {
        var cells = new List<Cell>
        {
            new("STAND idle", "idle", 0, Stance.Stand, null),
            new("CROUCH", "crouch", 0, Stance.Crouch, null),
            new("AIR jump", "jump", 1, Stance.Air, null),
        };
        foreach (var m in def.Moves)
        {
            var anim = def.GetAnimation(m.AnimationName);
            int frame = anim == null ? 0 : anim.FrameForMove(m.Startup, m.Startup, m.Active, m.Recovery);
            var stance = m.Command.Air ? Stance.Air : m.Command.IsCrouching ? Stance.Crouch : Stance.Stand;
            cells.Add(new($"{m.Id} {m.Input}", m.AnimationName, frame, stance, m));
        }
        return cells;
    }

    private static void DrawCell(Draw d, CharacterDef def, Cell c, int x0, int y0)
    {
        d.Frame(x0, y0, CellW, CellH, new Color(70, 70, 90));
        int ox = x0 + OriginX, oy = y0 + OriginY;
        d.Rect(x0 + 2, oy, CellW - 4, 1, new Color(90, 140, 90)); // пол

        var anim = def.GetAnimation(c.Anim);
        if (anim != null) WorldRenderer.DrawFrame(d, def, anim, c.Frame, ox, oy, 1, Color.White);

        void Box(BoxDef b, Color col)
        {
            d.Rect(ox + b.X, oy + b.Y, b.W, b.H, col * 0.2f);
            d.Frame(ox + b.X, oy + b.Y, b.W, b.H, col);
        }

        d.Frame(ox + def.Pushbox.X, oy + def.Pushbox.Y, def.Pushbox.W, def.Pushbox.H, Color.Yellow * 0.5f);
        foreach (var h in def.GetHurtboxes(c.Stance)) Box(h, new Color(80, 160, 255));

        if (c.Move is { } m)
        {
            foreach (var h in m.Hurtboxes) Box(h, new Color(120, 220, 255));
            foreach (var h in m.Hitboxes) Box(h, Color.Red);
            if (m.Type == MoveType.Throw)
                d.Rect(ox, oy + 2, m.ThrowRange, 2, Color.Magenta);
            foreach (var e in m.Effects.Where(e => e.Type == "projectile"))
            {
                var p = e.GetParams<ProjectileParams>();
                float px = ox + p.OffsetX, py = oy + p.OffsetY;
                var pa = def.GetAnimation(p.Animation);
                if (pa != null) WorldRenderer.DrawFrame(d, def, pa, 0, px, py, 1, Color.White);
                Box(new BoxDef { X = p.OffsetX + p.Box.X, Y = p.OffsetY + p.Box.Y, W = p.Box.W, H = p.Box.H }, Color.Orange);
            }
            d.Text($"{m.Startup}/{m.Active}/{m.Recovery}", x0 + 3, y0 + CellH - 9, new Color(170, 170, 190), shadow: false);
        }
        d.Rect(ox - 1, oy - 1, 3, 3, Color.White);
        d.Text(c.Label, x0 + 3, y0 + 2, Color.White, shadow: false);
    }
}
