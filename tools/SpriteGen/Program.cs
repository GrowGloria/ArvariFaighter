using SpriteGen;

// Генератор заглушек: пиксельные спрайтшиты персонажей, эффекты и фон арены.
// Запуск: dotnet run --project tools/SpriteGen [-- путь/к/Content]
var content = args.Length > 0 ? args[0]
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DndFighter/Content"));
Console.WriteLine($"Content: {content}");

// Порядок строк листа — тот же, что в character.json ("row").
string[] rows =
{
    "idle", "walk", "crouch", "jump", "hit", "block", "crouch_block", "air_hit", "knockdown", "getup", "victory",
    "5L", "5M", "5H", "2L", "2M", "2H", "jL", "jM", "jH", "throw", "s1", "s2", "s3", "s4", "s5", "s6",
};

var paladin = new Look
{
    Primary = new(165, 175, 195), Secondary = new(45, 70, 150), Accent = new(235, 195, 70),
    Metal = new(215, 225, 240), Legs = new(95, 100, 120), Magic = new(255, 236, 150),
    Weapon = WeaponKind.SwordAndShield, Helmet = true,
};
var wizard = new Look
{
    Primary = new(120, 70, 170), Secondary = new(80, 45, 125), Accent = new(110, 220, 240),
    Skin = new(236, 196, 165), Hair = new(185, 85, 45), Legs = new(60, 45, 80), Magic = new(180, 150, 255),
    Weapon = WeaponKind.Staff, Robe = true, Hat = true,
};

BuildSheet(paladin, false, Path.Combine(content, "characters/paladin/sprites.png"));
BuildSheet(wizard, true, Path.Combine(content, "characters/wizard/sprites.png"));
BuildWizardFx(Path.Combine(content, "characters/wizard/fx.png"));
Stage.Build(Path.Combine(content, "stages/crossroads/background.png"));
Console.WriteLine("Готово.");

void BuildSheet(Look look, bool mage, string path)
{
    const int cols = 8;
    var sheet = new Canvas(cols * Puppet.Frame, rows.Length * Puppet.Frame);
    for (int r = 0; r < rows.Length; r++)
    {
        var poses = Poses.Build(rows[r], mage);
        if (poses.Length > cols) throw new InvalidOperationException($"{rows[r]}: больше {cols} кадров");
        for (int f = 0; f < poses.Length; f++)
            sheet.Blit(Puppet.Render(poses[f], look), f * Puppet.Frame, r * Puppet.Frame);
    }
    sheet.SavePng(path);
    Console.WriteLine($"  {path}");

    // Необязательное превью x4 первых кадров каждой строки: SpriteGen <content> <папка превью>
    if (args.Length > 1)
    {
        const int s = 2;
        var name = Path.GetFileName(Path.GetDirectoryName(path)!);
        foreach (var (from, to, part) in new[] { (0, 11, "a"), (11, rows.Length, "b") })
        {
            var big = new Canvas(5 * 64 * s, (to - from) * 64 * s);
            for (int y = 0; y < big.H; y++)
                for (int x = 0; x < big.W; x++)
                {
                    var c = sheet.Get(x / s, from * 64 + y / s);
                    bool grid = x % (64 * s) == 0 || y % (64 * s) == 0;
                    big.Px[y * big.W + x] = c.A != 0 ? c : grid ? new Rgba(120, 120, 140) : new Rgba(70, 70, 90);
                }
            big.SavePng(Path.Combine(args[1], $"{name}_{part}.png"));
        }
    }
}

void BuildWizardFx(string path)
{
    // Лист 16x16, origin (8, 8). Строка 0 — огненный снаряд, 1 — волшебная стрела, 2 — луч холода.
    var fx = new Canvas(4 * 16, 3 * 16);
    var outline = new Rgba(40, 10, 10);
    for (int f = 0; f < 4; f++)
    {
        var cell = new Canvas(16, 16);
        float flick = f % 2 == 0 ? 0 : 0.6f;
        for (int t = 0; t < 4; t++)
            cell.Disc(8 - 2.2f * t - f % 2, 8 + (t % 2 == 0 ? 0 : (f % 2 == 0 ? 1 : -1)), 2.4f - t * 0.45f, new Rgba(230, 90, 30, (byte)(220 - t * 40)));
        cell.Disc(9, 8, 4 + flick, new Rgba(240, 120, 30));
        cell.Disc(9.5f, 8, 2.6f, new Rgba(255, 210, 80));
        cell.Disc(10, 7.5f, 1.2f, new Rgba(255, 255, 220));
        cell.Outline(outline);
        fx.Blit(cell, f * 16, 0);

        var dart = new Canvas(16, 16);
        dart.Line(2, 8, 11, 8, 1.5f, new Rgba(150, 110, 255, 150));
        dart.Line(6, 8, 13, 8, 2.5f, new Rgba(190, 160, 255));
        dart.Disc(12.5f, 8, 2 + (f % 2) * 0.5f, new Rgba(235, 225, 255));
        dart.Set(3 + f, 6 + f % 3, new Rgba(200, 180, 255));
        dart.Outline(new Rgba(40, 20, 80));
        fx.Blit(dart, f * 16, 16);

        var frost = new Canvas(16, 16);
        frost.Line(0, 8, 13, 8, 3, new Rgba(120, 200, 255, 160));
        frost.Line(4, 8, 14, 8, 1.5f, new Rgba(220, 245, 255));
        for (int s = 0; s < 3; s++)
        {
            int sx = 3 + (s * 4 + f * 3) % 10, sy = 5 + (s * 5 + f) % 6;
            frost.Set(sx, sy, new Rgba(255, 255, 255));
        }
        frost.Disc(13, 8, 2.2f, new Rgba(200, 240, 255));
        frost.Outline(new Rgba(20, 40, 80));
        fx.Blit(frost, f * 16, 32);
    }
    fx.SavePng(path);
    Console.WriteLine($"  {path}");

    // Облако кинжалов: лист 32x32, origin (16, 16), 4 кадра вращающихся клинков.
    var big = new Canvas(4 * 32, 32);
    for (int f = 0; f < 4; f++)
    {
        var cell = new Canvas(32, 32);
        cell.Disc(16, 16, 13, new Rgba(150, 140, 190, 70));
        cell.Disc(16, 16, 9, new Rgba(170, 160, 210, 60));
        for (int k = 0; k < 6; k++)
        {
            float a = (k / 6f + f / 24f) * MathF.Tau;
            float r = 5 + (k % 3) * 3.5f;
            float cx = 16 + MathF.Cos(a) * r, cy = 16 + MathF.Sin(a) * r * 0.8f;
            float da = a + MathF.PI / 2;
            float dx = MathF.Cos(da) * 3, dy = MathF.Sin(da) * 3;
            cell.Line(cx - dx, cy - dy, cx + dx, cy + dy, 1.5f, new Rgba(225, 230, 245));
            cell.Set((int)(cx - dx), (int)(cy - dy), new Rgba(200, 160, 60));
        }
        big.Blit(cell, f * 32, 0);
    }
    var bigPath = Path.Combine(Path.GetDirectoryName(path)!, "fx_big.png");
    big.SavePng(bigPath);
    Console.WriteLine($"  {bigPath}");
}
