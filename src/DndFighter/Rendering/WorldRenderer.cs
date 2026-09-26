using DndFighter.Combat;
using DndFighter.Data;
using DndFighter.Engine;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DndFighter.Rendering;

public static class WorldRenderer
{
    public static void DrawWorld(Draw d, FightWorld w, bool showBoxes)
    {
        var stage = w.Stage;
        int cam = (int)MathF.Round(w.CameraX);

        d.Rect(0, 0, 480, 270, ColorUtil.Parse(stage.BackgroundColor));
        if (stage.BackgroundTexture is { } bg)
            d.Batch.Draw(bg, new Vector2(-cam, 0), Color.White);

        // Тени под бойцами.
        foreach (var f in w.Fighters)
        {
            float sx = f.Pos.X - cam;
            float shrink = Math.Clamp(-f.Pos.Y / 80f, 0, 0.6f);
            float width = 26 * (1 - shrink);
            d.Rect(sx - width / 2, stage.FloorY - 1, width, 3, Color.Black * 0.35f);
        }

        // Атакующий рисуется поверх.
        var order = w.Fighters.OrderBy(f => f.IsAttacking ? 1 : 0);
        foreach (var f in order) DrawFighter(d, f, cam, stage.FloorY);
        foreach (var p in w.Projectiles) DrawProjectile(d, p, cam, stage.FloorY);
        foreach (var s in w.Sparks) DrawSpark(d, s, cam, stage.FloorY);

        if (showBoxes) DrawBoxes(d, w, cam, stage.FloorY);

        foreach (var p in w.Popups)
        {
            float y = stage.FloorY + p.Pos.Y - p.Age * 0.4f;
            float alpha = p.Age < 35 ? 1f : 1f - (p.Age - 35) / 15f;
            d.TextCentered(p.Text, p.Pos.X - cam, y, p.Color * alpha);
        }
    }

    public static (string anim, int tick, int total) PickAnimation(Fighter f) => f.State switch
    {
        FighterState.Idle => ("idle", f.AnimClock, 0),
        FighterState.Walk => ("walk", f.AnimClock, 0),
        FighterState.Crouch or FighterState.PreJump or FighterState.Landing => ("crouch", f.AnimClock, 0),
        FighterState.Air => ("jump", 0, 0),
        FighterState.Attack => (f.Move!.AnimationName, f.MoveFrame - 1, f.Move.TotalFrames),
        FighterState.Hitstun => ("hit", f.StateTime, 0),
        FighterState.Blockstun => (f.Stance == Stance.Crouch ? "crouch_block" : "block", 0, 0),
        FighterState.AirHit => ("air_hit", f.AnimClock, 0),
        FighterState.Knockdown or FighterState.KO => ("knockdown", 99, 0),
        FighterState.GetUp => ("getup", f.StateTime, Fighter.GetUpFrames),
        FighterState.Victory => ("victory", f.AnimClock, 0),
        _ => ("idle", 0, 0),
    };

    public static void DrawFighter(Draw d, Fighter f, int cam, int floorY)
    {
        var def = f.Def;
        var (animName, tick, total) = PickAnimation(f);
        var anim = def.GetAnimation(animName) ?? def.GetAnimation("idle");

        float x = f.Pos.X - cam;
        float y = floorY + f.Pos.Y;
        if (f.Hitstop > 0 && f.State is FighterState.Hitstun or FighterState.AirHit or FighterState.Blockstun)
            x += f.Hitstop % 2 == 0 ? 1 : -1;

        var tint = Color.White;
        if (f.State == FighterState.Hitstun && f.StateTime < 4) tint = new Color(255, 170, 170);
        if (f.Invulnerable > 0 && f.State == FighterState.Attack) tint = new Color(210, 230, 255);

        if (anim == null || !def.Textures.TryGetValue(anim.Sheet, out var tex))
        {
            // Нет спрайтов — рисуем пушбокс, чтобы персонаж всё равно был играбелен.
            var pb = f.Pushbox;
            d.Rect(pb.X - cam, floorY + pb.Y, pb.W, pb.H, (f.Index == 0 ? Color.CornflowerBlue : Color.IndianRed) * 0.8f);
            return;
        }

        int frame = animName == "jump" ? JumpFrame(f, anim.Frames)
            : f.State == FighterState.Attack ? anim.FrameForMove(tick, f.Move!.Startup, f.Move.Active, f.Move.Recovery)
            : anim.FrameAt(tick, total);
        DrawFrame(d, def, anim, frame, x, y, f.Facing, tint);
    }

    private static int JumpFrame(Fighter f, int frames)
    {
        if (frames < 3) return 0;
        return f.Vel.Y < -1.5f ? 0 : f.Vel.Y < 1.5f ? 1 : 2;
    }

    public static void DrawFrame(Draw d, CharacterDef def, AnimationDef anim, int frame, float x, float y, int facing, Color tint, int scale = 1)
    {
        if (!def.Sheets.TryGetValue(anim.Sheet, out var sheet) || !def.Textures.TryGetValue(anim.Sheet, out var tex)) return;
        var src = new Rectangle(frame * sheet.FrameWidth, anim.Row * sheet.FrameHeight, sheet.FrameWidth, sheet.FrameHeight);
        int ox = facing > 0 ? sheet.OriginX : sheet.FrameWidth - sheet.OriginX;
        var dest = new Rectangle(
            (int)MathF.Round(x) - ox * scale, (int)MathF.Round(y) - sheet.OriginY * scale,
            sheet.FrameWidth * scale, sheet.FrameHeight * scale);
        d.Batch.Draw(tex, dest, src, tint, 0, Vector2.Zero,
            facing > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally, 0);
    }

    private static void DrawProjectile(Draw d, Projectile p, int cam, int floorY)
    {
        float x = p.Pos.X - cam;
        float y = floorY + p.Pos.Y;
        var anim = p.Owner.Def.GetAnimation(p.Params.Animation);
        if (anim != null)
        {
            DrawFrame(d, p.Owner.Def, anim, anim.FrameAt(p.Age), x, y, p.Facing, Color.White);
            return;
        }
        var b = p.Box;
        d.Rect(b.X - cam, floorY + b.Y, b.W, b.H, Color.Orange);
    }

    private static void DrawSpark(Draw d, Spark s, int cam, int floorY)
    {
        var (core, edge) = s.Kind switch
        {
            SparkKind.Block => (Color.White, new Color(90, 170, 255)),
            SparkKind.Magic => (new Color(230, 220, 255), new Color(160, 110, 255)),
            SparkKind.Throw => (Color.White, new Color(255, 220, 90)),
            SparkKind.Heavy => (Color.White, new Color(255, 120, 40)),
            _ => (Color.White, new Color(255, 190, 60)),
        };
        float t = s.Age / (float)s.Life;
        float cx = s.Pos.X - cam, cy = floorY + s.Pos.Y;
        float radius = 3 + t * (s.Kind == SparkKind.Heavy ? 18 : 12);
        var rng = new Random(s.Seed);
        int rays = s.Kind == SparkKind.Heavy ? 10 : 7;
        for (int i = 0; i < rays; i++)
        {
            float a = (float)(rng.NextDouble() * MathF.Tau);
            float len = radius * (0.6f + (float)rng.NextDouble() * 0.6f);
            for (float r = len * 0.4f; r < len; r += 1.5f)
                d.Rect(cx + MathF.Cos(a) * r, cy + MathF.Sin(a) * r, 2, 2, edge * (1 - t));
        }
        if (t < 0.4f)
        {
            float c = 5 * (1 - t);
            d.Rect(cx - c / 2, cy - c / 2, c, c, core);
        }
    }

    private static void DrawBoxes(Draw d, FightWorld w, int cam, int floorY)
    {
        void Box(RectangleF r, Color c)
        {
            d.Rect(r.X - cam, floorY + r.Y, r.W, r.H, c * 0.25f);
            d.Frame(r.X - cam, floorY + r.Y, r.W, r.H, c);
        }

        foreach (var f in w.Fighters)
        {
            var pb = f.Pushbox;
            d.Frame(pb.X - cam, floorY + pb.Y, pb.W, pb.H, Color.Yellow * 0.6f);
            foreach (var h in f.Hurtboxes()) Box(h, f.Invulnerable > 0 ? Color.White : new Color(80, 160, 255));
            foreach (var (b, _) in f.ActiveHitboxes()) Box(f.ToWorld(b), Color.Red);
            if (f.IsAttacking && f.Move!.Type == MoveType.Throw)
            {
                float range = f.Move.ThrowRange;
                d.Rect(f.Pos.X - cam + (f.Facing > 0 ? 0 : -range), floorY - 2, range, 2, Color.Magenta);
            }
            d.Rect(f.Pos.X - cam - 1, floorY + f.Pos.Y - 1, 3, 3, Color.White);
        }
        foreach (var p in w.Projectiles) Box(p.Box, Color.Red);
    }
}
