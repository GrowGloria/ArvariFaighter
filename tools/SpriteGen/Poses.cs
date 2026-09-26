namespace SpriteGen;

/// <summary>
/// Стандартная раскладка листа (строка = анимация). Совпадает с разделом "animations" в character.json.
/// Художник может нарисовать свой sprites.png в той же сетке 64x64 — и просто заменить файл.
/// </summary>
public static class Poses
{
    public static readonly Pose Idle = new();

    public static readonly Pose CrouchLegs = Idle with
    {
        Lean = 20, FrontThigh = 80, FrontShin = -8, BackThigh = 30, BackShin = -85,
        FrontUpper = 40, FrontFore = 100,
    };

    public static readonly Pose BlockArms = Idle with
    {
        Lean = 2, FrontUpper = 70, FrontFore = 170, Weapon = 172, BackUpper = 70, BackFore = 100,
    };

    public static Pose[] Build(string anim, bool mage) => anim switch
    {
        "idle" => new[] { Idle, Idle with { Bob = 1 }, Idle with { Bob = 1 }, Idle },
        "walk" => Walk(),
        "crouch" => new[] { CrouchLegs },
        "jump" => new[]
        {
            Idle with { FrontThigh = 45, FrontShin = -20, BackThigh = 5, BackShin = -40, FrontUpper = 60, FrontFore = 150, Weapon = 170 },
            Idle with { FrontThigh = 85, FrontShin = -30, BackThigh = 60, BackShin = -45, FrontUpper = 50, FrontFore = 120 },
            Idle with { FrontThigh = 30, FrontShin = 10, BackThigh = -10, BackShin = -25, FrontUpper = 35, FrontFore = 80 },
        },
        "hit" => new[]
        {
            Idle with { Lean = -20, FrontUpper = -20, FrontFore = 20, BackUpper = -30, BackFore = 0, Weapon = 110, FrontThigh = 25, BackThigh = -25, BackShin = -15 },
            Idle with { Lean = -12, FrontUpper = -5, FrontFore = 50, BackUpper = -15, BackFore = 20, Weapon = 120 },
        },
        "block" => new[] { BlockArms },
        "crouch_block" => new[] { CrouchLegs with { FrontUpper = 70, FrontFore = 170, Weapon = 172, BackUpper = 70, BackFore = 100, Lean = 12 } },
        "air_hit" => new[]
        {
            Idle with { OffsetX = 6, Lean = -60, FrontThigh = 50, FrontShin = 30, BackThigh = 20, BackShin = 0, FrontUpper = 150, FrontFore = 170, BackUpper = -120, BackFore = -150, Weapon = 200 },
            Idle with { OffsetX = 8, Lean = -75, FrontThigh = 70, FrontShin = 50, BackThigh = 40, BackShin = 20, FrontUpper = 120, FrontFore = 150, BackUpper = -150, BackFore = -170, Weapon = 230 },
        },
        "knockdown" => new[]
        {
            Idle with { OffsetX = 10, Lean = -70, FrontThigh = 60, FrontShin = 60, BackThigh = 45, BackShin = 45, FrontUpper = 120, FrontFore = 140, BackUpper = -100, BackFore = -120, Weapon = 160 },
            Idle with { OffsetX = 6, Lean = -90, FrontThigh = 88, FrontShin = 92, BackThigh = 80, BackShin = 100, FrontUpper = 80, FrontFore = 95, BackUpper = 95, BackFore = 90, Weapon = 95 },
        },
        "getup" => new[]
        {
            Idle with { OffsetX = 4, Lean = -35, FrontThigh = 85, FrontShin = 90, BackThigh = 80, BackShin = 30, FrontUpper = 20, FrontFore = 10, BackUpper = -20, BackFore = -10, Weapon = 60 },
            CrouchLegs with { Lean = 10 },
            Idle with { Lean = 12, FrontThigh = 45, FrontShin = -10, BackThigh = -5, BackShin = -40 },
        },
        "victory" => mage
            ? new[] { Idle with { Lean = 0, FrontUpper = 160, FrontFore = 175, Weapon = 180, Cast = true }, Idle with { Lean = 0, FrontUpper = 160, FrontFore = 175, Weapon = 180, Glow = true, Bob = 1 } }
            : new[] { Idle with { Lean = 0, FrontUpper = 165, FrontFore = 180, Weapon = 180 }, Idle with { Lean = 0, FrontUpper = 165, FrontFore = 180, Weapon = 180, Glow = true, Bob = 1 } },

        "5L" => new[]
        {
            Idle with { FrontUpper = 20, FrontFore = 60, Weapon = 110, Lean = 5 },
            Idle with { FrontUpper = 85, FrontFore = 90, Weapon = 100, Lean = 18, FrontThigh = 28 },
            Idle with { FrontUpper = 50, FrontFore = 80, Weapon = 120, Lean = 10 },
        },
        "5M" => new[]
        {
            Idle with { FrontUpper = 10, FrontFore = 150, Weapon = 195, Lean = 2 },
            Idle with { FrontUpper = 85, FrontFore = 90, Weapon = 90, Lean = 16, FrontThigh = 30 },
            Idle with { FrontUpper = 100, FrontFore = 65, Weapon = 55, Lean = 16, FrontThigh = 30 },
            Idle with { FrontUpper = 45, FrontFore = 80, Weapon = 110, Lean = 10 },
        },
        "5H" => new[]
        {
            Idle with { FrontUpper = 150, FrontFore = 190, Weapon = 210, Lean = -5 },
            Idle with { FrontUpper = 170, FrontFore = 200, Weapon = 225, Lean = -10, FrontThigh = 25 },
            Idle with { FrontUpper = 105, FrontFore = 95, Weapon = 85, Lean = 25, FrontThigh = 38, FrontShin = 5 },
            Idle with { FrontUpper = 60, FrontFore = 40, Weapon = 30, Lean = 25, FrontThigh = 38, FrontShin = 5 },
            Idle with { FrontUpper = 40, FrontFore = 70, Weapon = 100, Lean = 15 },
        },
        "2L" => new[]
        {
            CrouchLegs with { FrontUpper = 40, FrontFore = 70, Weapon = 100 },
            CrouchLegs with { FrontUpper = 85, FrontFore = 92, Weapon = 95, Lean = 28 },
            CrouchLegs with { FrontUpper = 55, FrontFore = 85, Weapon = 105 },
        },
        "2M" => new[]
        {
            CrouchLegs with { FrontUpper = 20, FrontFore = 60, Weapon = 95, Lean = 15 },
            CrouchLegs with { FrontUpper = 92, FrontFore = 92, Weapon = 94, Lean = 30 },
            CrouchLegs with { FrontUpper = 80, FrontFore = 85, Weapon = 94, Lean = 28 },
            CrouchLegs with { FrontUpper = 45, FrontFore = 80, Weapon = 105, Lean = 20 },
        },
        "2H" => new[]
        {
            CrouchLegs with { Lean = 5, FrontUpper = -10, FrontFore = 40, Weapon = 60 },
            CrouchLegs with { Lean = -10, FrontThigh = 86, FrontShin = 90, FrontUpper = 70, FrontFore = 80, Weapon = 80 },
            CrouchLegs with { Lean = -8, FrontThigh = 86, FrontShin = 90, FrontUpper = 90, FrontFore = 80, Weapon = 70 },
            CrouchLegs with { Lean = 0, FrontThigh = 75, FrontShin = 30, FrontUpper = 60, FrontFore = 80, Weapon = 90 },
            CrouchLegs,
        },
        "jL" => new[]
        {
            Idle with { FrontThigh = 60, FrontShin = -20, BackThigh = 20, BackShin = -40, FrontUpper = 40, FrontFore = 90 },
            Idle with { FrontThigh = 95, FrontShin = 30, BackThigh = 20, BackShin = -40, FrontUpper = 40, FrontFore = 90, Lean = -5 },
        },
        "jM" => new[]
        {
            Idle with { FrontThigh = 60, FrontShin = -20, BackThigh = 20, BackShin = -40, FrontUpper = 120, FrontFore = 170, Weapon = 190 },
            Idle with { FrontThigh = 60, FrontShin = -20, BackThigh = 20, BackShin = -40, FrontUpper = 90, FrontFore = 60, Weapon = 45, Lean = 20 },
            Idle with { FrontThigh = 50, FrontShin = -10, BackThigh = 10, BackShin = -30, FrontUpper = 50, FrontFore = 60, Weapon = 80 },
        },
        "jH" => new[]
        {
            Idle with { FrontThigh = 50, FrontShin = -30, BackThigh = 0, BackShin = -60, FrontUpper = 170, FrontFore = 200, Weapon = 220, Lean = -10 },
            Idle with { FrontThigh = 60, FrontShin = 0, BackThigh = 20, BackShin = -40, FrontUpper = 95, FrontFore = 45, Weapon = 25, Lean = 30 },
            Idle with { FrontThigh = 50, FrontShin = 0, BackThigh = 10, BackShin = -30, FrontUpper = 50, FrontFore = 30, Weapon = 20, Lean = 20 },
        },
        "throw" => new[]
        {
            Idle with { FrontUpper = 70, FrontFore = 85, BackUpper = 70, BackFore = 85, Weapon = 150, Lean = 12 },
            Idle with { FrontUpper = 92, FrontFore = 92, BackUpper = 90, BackFore = 90, Weapon = 150, Lean = 20, FrontThigh = 30 },
            Idle with { FrontUpper = 30, FrontFore = 120, BackUpper = 30, BackFore = 110, Weapon = 160, Lean = -15 },
            Idle with { FrontUpper = 130, FrontFore = 160, BackUpper = 100, BackFore = 140, Weapon = 190, Lean = 5 },
        },
        _ => mage ? MageSpecial(anim) : KnightSpecial(anim),
    };

    private static Pose[] Walk()
    {
        var frames = new Pose[6];
        for (int i = 0; i < 6; i++)
        {
            float ph = i / 6f * MathF.Tau;
            float s = MathF.Sin(ph), c = MathF.Cos(ph);
            frames[i] = Idle with
            {
                FrontThigh = 5 + 25 * s,
                FrontShin = 5 + 25 * s - 10 - 20 * Math.Max(0, c),
                BackThigh = -5 - 25 * s,
                BackShin = -5 - 25 * s - 10 - 20 * Math.Max(0, -c),
                Bob = Math.Abs(s) > 0.5f ? 0 : 1,
            };
        }
        return frames;
    }

    private static Pose[] KnightSpecial(string anim) => anim switch
    {
        // 236L — натиск щитом
        "s1" => new[]
        {
            Idle with { Lean = 0, BackUpper = 20, BackFore = 50, FrontUpper = 10, FrontFore = 60, Weapon = 120 },
            Idle with { Lean = 30, BackUpper = 90, BackFore = 90, FrontThigh = 40, FrontShin = 0, BackThigh = -40, BackShin = -30, FrontUpper = 20, FrontFore = 60, Weapon = 120 },
            Idle with { Lean = 26, BackUpper = 85, BackFore = 95, FrontThigh = 35, FrontShin = 0, BackThigh = -35, BackShin = -25, FrontUpper = 20, FrontFore = 60, Weapon = 120 },
            Idle with { Lean = 15, BackUpper = 60, BackFore = 80 },
            Idle,
        },
        // 236H — божественная кара
        "s2" => new[]
        {
            Idle with { FrontUpper = 150, FrontFore = 190, Weapon = 210, Lean = -5, Glow = true },
            Idle with { FrontUpper = 170, FrontFore = 200, Weapon = 225, Lean = -10, Glow = true, FrontThigh = 30 },
            Idle with { FrontUpper = 105, FrontFore = 90, Weapon = 75, Lean = 30, Glow = true, FrontThigh = 42, FrontShin = 5, BackThigh = -35 },
            Idle with { FrontUpper = 60, FrontFore = 35, Weapon = 20, Lean = 28, Glow = true, FrontThigh = 42, FrontShin = 5, BackThigh = -35 },
            Idle with { FrontUpper = 40, FrontFore = 60, Weapon = 90, Lean = 15 },
        },
        // 214M — возложение рук
        "s3" => new[]
        {
            CrouchLegs with { Lean = 10, FrontUpper = 30, FrontFore = 50, Weapon = 10 },
            CrouchLegs with { Lean = 15, FrontUpper = 60, FrontFore = 60, BackUpper = 60, BackFore = 60, Weapon = 5, Cast = true },
            CrouchLegs with { Lean = 15, FrontUpper = 60, FrontFore = 60, BackUpper = 60, BackFore = 60, Weapon = 5, Cast = true, Ring = 16 },
            CrouchLegs with { Lean = 12, FrontUpper = 50, FrontFore = 60, BackUpper = 50, BackFore = 60, Weapon = 5, Cast = true },
            CrouchLegs,
        },
        // 623H — восходящий удар
        _ => new[]
        {
            CrouchLegs with { FrontUpper = 10, FrontFore = 30, Weapon = 60, Lean = 25 },
            Idle with { Lean = 10, FrontUpper = 110, FrontFore = 140, Weapon = 150, Glow = true, FrontThigh = 30, FrontShin = -10 },
            Idle with { Lean = -5, FrontUpper = 170, FrontFore = 178, Weapon = 180, Glow = true, FrontThigh = 60, FrontShin = -20, BackThigh = 10, BackShin = -10 },
            Idle with { Lean = -5, FrontUpper = 165, FrontFore = 175, Weapon = 185, FrontThigh = 40, FrontShin = 0, BackThigh = 0, BackShin = -20 },
            Idle with { FrontThigh = 30, FrontShin = 10, BackThigh = -10, BackShin = -25, FrontUpper = 60, FrontFore = 110 },
        },
    };

    private static Pose[] MageSpecial(string anim) => anim switch
    {
        // 236L — огненный снаряд
        "s1" => new[]
        {
            Idle with { FrontUpper = -20, FrontFore = 30, Weapon = 170, Cast = true, Lean = 0 },
            Idle with { FrontUpper = 40, FrontFore = 80, Weapon = 170, Cast = true, Lean = 8 },
            Idle with { FrontUpper = 90, FrontFore = 92, Weapon = 150, Cast = true, Lean = 18, FrontThigh = 30 },
            Idle with { FrontUpper = 85, FrontFore = 90, Weapon = 150, Lean = 16, FrontThigh = 30 },
            Idle with { FrontUpper = 40, FrontFore = 90, Lean = 10 },
        },
        // 236H — волшебная стрела
        "s2" => new[]
        {
            Idle with { FrontUpper = 30, FrontFore = 140, Weapon = 180, Glow = true, BackUpper = 20, BackFore = 50 },
            Idle with { FrontUpper = 60, FrontFore = 150, Weapon = 175, Glow = true, BackUpper = 60, BackFore = 80, Lean = 5 },
            Idle with { FrontUpper = 90, FrontFore = 100, Weapon = 130, Cast = true, Glow = true, BackUpper = 88, BackFore = 90, Lean = 15 },
            Idle with { FrontUpper = 88, FrontFore = 95, Weapon = 130, Cast = true, BackUpper = 85, BackFore = 90, Lean = 15 },
            Idle with { FrontUpper = 50, FrontFore = 100, Lean = 10 },
        },
        // 214L — туманный шаг
        "s3" => new[]
        {
            Idle with { Cast = true },
            Idle with { Cast = true, Alpha = 0.6f, Lean = 15 },
            Idle with { Alpha = 0.25f, Lean = 20, Ring = 12 },
            Idle with { Alpha = 0.6f, Lean = 10 },
            Idle,
        },
        // j.236L — огненный снаряд из прыжка (вниз-вперёд)
        "s5" => new[]
        {
            Idle with { FrontThigh = 60, FrontShin = -20, BackThigh = 20, BackShin = -40, FrontUpper = 150, FrontFore = 170, Weapon = 175, Cast = true },
            Idle with { FrontThigh = 60, FrontShin = -20, BackThigh = 20, BackShin = -40, FrontUpper = 55, FrontFore = 50, Weapon = 150, Cast = true, Lean = 15 },
            Idle with { FrontThigh = 55, FrontShin = -15, BackThigh = 15, BackShin = -35, FrontUpper = 55, FrontFore = 55, Weapon = 150, Lean = 12 },
            Idle with { FrontThigh = 45, FrontShin = -10, BackThigh = 5, BackShin = -30, FrontUpper = 40, FrontFore = 90 },
        },
        // 214H — облако кинжалов
        "s6" => new[]
        {
            Idle with { FrontUpper = 60, FrontFore = 120, Weapon = 175, Glow = true },
            Idle with { FrontUpper = 120, FrontFore = 150, BackUpper = 100, BackFore = 140, Weapon = 175, Glow = true, Lean = 2 },
            Idle with { FrontUpper = 150, FrontFore = 165, BackUpper = 130, BackFore = 160, Weapon = 180, Glow = true, Cast = true, Lean = -2 },
            Idle with { FrontUpper = 140, FrontFore = 160, BackUpper = 120, BackFore = 150, Weapon = 180, Cast = true },
            Idle with { FrontUpper = 50, FrontFore = 100 },
        },
        // 623H — волна грома
        _ => new[]
        {
            CrouchLegs with { FrontUpper = 40, FrontFore = 30, BackUpper = 30, BackFore = 20, Weapon = 170, Cast = true },
            Idle with { Lean = 0, FrontUpper = 110, FrontFore = 130, BackUpper = -110, BackFore = -130, Weapon = 170, Cast = true, Ring = 12 },
            Idle with { Lean = -3, FrontUpper = 130, FrontFore = 150, BackUpper = -130, BackFore = -150, Weapon = 170, Cast = true, Glow = true, Ring = 21 },
            Idle with { Lean = 0, FrontUpper = 110, FrontFore = 130, BackUpper = -110, BackFore = -130, Weapon = 170, Ring = 26 },
            Idle with { FrontUpper = 40, FrontFore = 90 },
        },
    };
}
