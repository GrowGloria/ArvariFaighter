namespace SpriteGen;

/// <summary>
/// Поза «куклы». Углы в градусах от направления «вниз», положительные — вперёд (к сопернику).
/// 0 — вниз, 90 — вперёд, 180 — вверх, -90 — назад.
/// </summary>
public sealed record Pose
{
    public float Lean { get; init; } = 8;          // наклон корпуса вперёд от вертикали
    public float FrontThigh { get; init; } = 18;
    public float FrontShin { get; init; } = -2;
    public float BackThigh { get; init; } = -18;
    public float BackShin { get; init; } = -8;
    public float FrontUpper { get; init; } = 25;
    public float FrontFore { get; init; } = 95;
    public float BackUpper { get; init; } = 15;
    public float BackFore { get; init; } = 80;
    public float Weapon { get; init; } = 140;
    public float Bob { get; init; }                // «дыхание» — укорачивает корпус
    public float OffsetX { get; init; }
    public bool Glow { get; init; }                // светящееся оружие
    public bool Cast { get; init; }                // магия в ладони
    public float Alpha { get; init; } = 1;
    public float Ring { get; init; }               // радиус магической волны вокруг
}

public enum WeaponKind { SwordAndShield, Staff }

public sealed class Look
{
    public Rgba Skin = new(232, 190, 150);
    public Rgba Primary = new(170, 180, 200);
    public Rgba Secondary = new(50, 70, 140);
    public Rgba Accent = new(235, 195, 70);
    public Rgba Hair = new(120, 70, 40);
    public Rgba Metal = new(210, 220, 235);
    public Rgba Legs = new(70, 60, 80);
    public Rgba Magic = new(255, 230, 140);
    public Rgba Outline = new(24, 18, 30);
    public WeaponKind Weapon;
    public bool Helmet;
    public bool Robe;
    public bool Hat;
}

public static class Puppet
{
    public const int Frame = 64;
    public const int OriginX = 32, OriginY = 60;

    private const float Thigh = 11, Shin = 11, Torso = 15, Upper = 8, Fore = 8, HeadR = 5;

    private static (float X, float Y) Dir(float deg)
    {
        float a = deg * MathF.PI / 180f;
        return (MathF.Sin(a), MathF.Cos(a));
    }

    private static (float X, float Y) Add((float X, float Y) p, (float X, float Y) d, float k) => (p.X + d.X * k, p.Y + d.Y * k);

    public static Canvas Render(Pose p, Look look)
    {
        const int size = 128;
        var c = new Canvas(size, size);
        var hip = (X: 64f, Y: 64f);

        var up = (X: MathF.Sin(p.Lean * MathF.PI / 180), Y: -MathF.Cos(p.Lean * MathF.PI / 180));
        var fwd = (X: -up.Y, Y: up.X); // «вперёд» для головы, поворачивается вместе с корпусом
        float torso = Torso - p.Bob;
        var shoulder = Add(hip, up, torso - 2);
        var neck = Add(hip, up, torso);
        var head = Add(hip, up, torso + HeadR);

        var fKnee = Add(hip, Dir(p.FrontThigh), Thigh);
        var fFoot = Add(fKnee, Dir(p.FrontShin), Shin);
        var bKnee = Add(hip, Dir(p.BackThigh), Thigh);
        var bFoot = Add(bKnee, Dir(p.BackShin), Shin);
        var fElbow = Add(shoulder, Dir(p.FrontUpper), Upper);
        var fHand = Add(fElbow, Dir(p.FrontFore), Fore);
        var bElbow = Add(shoulder, Dir(p.BackUpper), Upper);
        var bHand = Add(bElbow, Dir(p.BackFore), Fore);

        var dark = 0.72f;

        // Задняя рука и нога — темнее, за корпусом.
        c.Line(shoulder.X, shoulder.Y, bElbow.X, bElbow.Y, 3, look.Primary.Shade(dark));
        c.Line(bElbow.X, bElbow.Y, bHand.X, bHand.Y, 3, look.Primary.Shade(dark));
        c.Disc(bHand.X, bHand.Y, 1.6f, look.Skin.Shade(dark));
        Leg(c, hip, bKnee, bFoot, p.BackShin, look, dark);

        if (look.Hat || !look.Helmet)
        {
            // Длинные волосы за спиной.
            var hairEnd = Add(Add(head, up, -8), fwd, -4);
            c.Line(head.X - fwd.X * 2, head.Y - fwd.Y * 2, hairEnd.X, hairEnd.Y, 4, look.Hair.Shade(0.9f));
        }

        Leg(c, hip, fKnee, fFoot, p.FrontShin, look, 1f);

        if (look.Robe)
        {
            var side = (X: fwd.X * 5, Y: fwd.Y * 5);
            var knees = ((fKnee.X + bKnee.X) / 2, (fKnee.Y + bKnee.Y) / 2);
            c.Polygon(new[]
            {
                (hip.X + side.X, hip.Y + side.Y),
                (Math.Max(fKnee.X, bKnee.X) + 3, Math.Max(fKnee.Y, knees.Item2) + 3),
                (Math.Min(fKnee.X, bKnee.X) - 3, Math.Max(bKnee.Y, knees.Item2) + 3),
                (hip.X - side.X, hip.Y - side.Y),
            }, look.Secondary);
        }
        else
        {
            // Табард рыцаря.
            var bottom = Add(Add(hip, up, -9), fwd, 1);
            c.Line(hip.X, hip.Y, bottom.X, bottom.Y, 5, look.Secondary);
        }

        // Корпус, пояс, голова.
        c.Line(hip.X, hip.Y, neck.X, neck.Y, 8, look.Primary);
        var chest = Add(Add(hip, up, torso - 5), fwd, 2);
        c.Disc(chest.X, chest.Y, 1.5f, look.Primary.Shade(1.15f));
        c.Line(hip.X - fwd.X * 4, hip.Y - fwd.Y * 4, hip.X + fwd.X * 4, hip.Y + fwd.Y * 4, 2, look.Accent);
        Head(c, head, up, fwd, look);

        if (look.Weapon == WeaponKind.SwordAndShield)
        {
            c.Disc(bHand.X, bHand.Y, 6.5f, look.Accent);
            c.Disc(bHand.X, bHand.Y, 5.2f, look.Secondary);
            c.Line(bHand.X, bHand.Y - 3.5f, bHand.X, bHand.Y + 3.5f, 1, look.Accent);
            c.Line(bHand.X - 3, bHand.Y - 0.5f, bHand.X + 3, bHand.Y - 0.5f, 1, look.Accent);
        }

        Weapon(c, fHand, p, look);

        // Передняя рука поверх оружия.
        c.Line(shoulder.X, shoulder.Y, fElbow.X, fElbow.Y, 3, look.Primary.Shade(1.05f));
        c.Line(fElbow.X, fElbow.Y, fHand.X, fHand.Y, 3, look.Primary.Shade(1.05f));
        c.Disc(fHand.X, fHand.Y, 1.7f, look.Skin);

        if (p.Cast)
        {
            c.Disc(fHand.X + 1, fHand.Y, 4f, look.Magic.WithAlpha(0.45f));
            c.Disc(fHand.X + 1, fHand.Y, 2.2f, look.Magic);
            c.Disc(fHand.X + 1, fHand.Y, 1f, new Rgba(255, 255, 255));
        }

        c.Outline(look.Outline);

        if (p.Ring > 0)
        {
            var center = Add(hip, up, 6);
            for (int i = 0; i < 48; i++)
            {
                float a = i / 48f * MathF.Tau;
                c.Disc(center.X + MathF.Cos(a) * p.Ring, center.Y + MathF.Sin(a) * p.Ring, 1.2f, look.Magic.WithAlpha(0.8f));
            }
        }

        return Place(c, hip, p);
    }

    private static void Leg(Canvas c, (float X, float Y) hip, (float X, float Y) knee, (float X, float Y) foot,
        float shinAngle, Look look, float shade)
    {
        c.Line(hip.X, hip.Y, knee.X, knee.Y, 4.5f, look.Legs.Shade(shade));
        c.Line(knee.X, knee.Y, foot.X, foot.Y, 3.5f, look.Legs.Shade(shade * 0.95f));
        // Сапог смотрит вперёд перпендикулярно голени.
        var toe = Dir(shinAngle + 90);
        var boot = look.Helmet ? look.Metal.Shade(0.8f * shade) : new Rgba(90, 60, 40).Shade(shade);
        c.Line(foot.X - toe.X, foot.Y - toe.Y, foot.X + toe.X * 3, foot.Y + toe.Y * 3, 3, boot);
    }

    private static void Head(Canvas c, (float X, float Y) head, (float X, float Y) up, (float X, float Y) fwd, Look look)
    {
        if (look.Helmet)
        {
            c.Disc(head.X, head.Y, HeadR + 0.5f, look.Metal);
            var slit = Add(head, fwd, 2.5f);
            c.Line(slit.X - fwd.X, slit.Y - fwd.Y, slit.X + fwd.X * 2.5f, slit.Y + fwd.Y * 2.5f, 1, look.Outline);
            var crestA = Add(head, up, HeadR);
            var crestB = Add(Add(head, up, HeadR - 1), fwd, -6);
            c.Line(crestA.X, crestA.Y, crestB.X, crestB.Y, 2, look.Accent);
            return;
        }

        c.Disc(head.X - fwd.X * 1.2f, head.Y - fwd.Y * 1.2f, HeadR, look.Hair);
        var face = Add(head, fwd, 1f);
        c.Disc(face.X, face.Y, HeadR - 0.8f, look.Skin);
        var eye = Add(Add(head, fwd, 3f), up, 0.5f);
        c.Set((int)MathF.Floor(eye.X), (int)MathF.Floor(eye.Y), look.Outline);

        if (look.Hat)
        {
            var brim = Add(head, up, 3.5f);
            c.Line(brim.X - fwd.X * 7, brim.Y - fwd.Y * 7, brim.X + fwd.X * 7, brim.Y + fwd.Y * 7, 2, look.Secondary.Shade(0.8f));
            var tip = Add(Add(head, up, 16), fwd, -7);
            var l = Add(brim, fwd, -5);
            var r = Add(brim, fwd, 5);
            c.Polygon(new[] { (l.X, l.Y), (r.X, r.Y), (tip.X, tip.Y) }, look.Secondary);
            var band = Add(head, up, 5f);
            c.Line(band.X - fwd.X * 4, band.Y - fwd.Y * 4, band.X + fwd.X * 4, band.Y + fwd.Y * 4, 1, look.Accent);
        }
    }

    private static void Weapon(Canvas c, (float X, float Y) hand, Pose p, Look look)
    {
        var d = Dir(p.Weapon);
        var perp = (X: -d.Y, Y: d.X);
        if (look.Weapon == WeaponKind.SwordAndShield)
        {
            var blade = p.Glow ? look.Magic : look.Metal;
            var tip = Add(hand, d, 19);
            if (p.Glow)
            {
                for (float t = 2; t <= 19; t += 2)
                {
                    var g = Add(hand, d, t);
                    c.Disc(g.X, g.Y, 3.2f, look.Magic.WithAlpha(0.35f));
                }
            }
            var pommel = Add(hand, d, -3);
            c.Line(pommel.X, pommel.Y, hand.X, hand.Y, 2, new Rgba(90, 60, 40));
            var guard = Add(hand, d, 2);
            c.Line(guard.X - perp.X * 3, guard.Y - perp.Y * 3, guard.X + perp.X * 3, guard.Y + perp.Y * 3, 2, look.Accent);
            var bStart = Add(hand, d, 3);
            c.Line(bStart.X, bStart.Y, tip.X, tip.Y, 2.4f, blade);
            c.Line(bStart.X + perp.X * 0.5f, bStart.Y + perp.Y * 0.5f, tip.X, tip.Y, 1, new Rgba(255, 255, 255, 180));
        }
        else
        {
            var bottom = Add(hand, d, -10);
            var top = Add(hand, d, 17);
            c.Line(bottom.X, bottom.Y, top.X, top.Y, 2, new Rgba(125, 85, 45));
            var orb = Add(hand, d, 19);
            if (p.Glow || p.Cast) c.Disc(orb.X, orb.Y, 5f, look.Magic.WithAlpha(0.35f));
            c.Disc(orb.X, orb.Y, 2.6f, look.Accent);
            c.Set((int)orb.X, (int)orb.Y - 1, new Rgba(255, 255, 255));
        }
    }

    /// <summary>Сдвигает фигуру в кадр 64x64: самая нижняя точка — на линию пола, таз — по центру.</summary>
    private static Canvas Place(Canvas src, (float X, float Y) hip, Pose p)
    {
        var frame = new Canvas(Frame, Frame);
        var b = src.Bounds();
        if (b is not { } bounds) return frame;
        int dx = OriginX + (int)MathF.Round(p.OffsetX) - (int)hip.X;
        int dy = OriginY + 1 - bounds.MaxY - 1;
        frame.Blit(src, dx, dy, p.Alpha);
        return frame;
    }
}
