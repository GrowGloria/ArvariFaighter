using DndFighter.Combat;
using DndFighter.Data;
using DndFighter.Input;

namespace DndFighter.Diagnostics;

/// <summary>
/// Проверка без окна: грузит весь контент и прогоняет скриптованные бои.
/// Запуск: DndFighter.exe --selftest   (результат также пишется в selftest.log)
/// </summary>
public static class SelfTest
{
    private sealed class Script : IInputSource
    {
        private readonly List<InputFrame> _frames = new();
        private int _i;
        public int Facing = 1;
        public InputFrame Default = InputFrame.Empty;

        public InputFrame Poll() => _i < _frames.Count ? _frames[_i++] : Default;

        /// <summary>Добавить n тиков: направление в нумпад-нотации относительно Facing + кнопки.</summary>
        public Script Add(int numpad, Buttons b = Buttons.None, int n = 1)
        {
            for (int k = 0; k < n; k++) _frames.Add(Raw(numpad, Facing, b));
            return this;
        }

        public Script Wait(int n) => Add(5, Buttons.None, n);

        public static InputFrame Raw(int numpad, int facing, Buttons b)
        {
            int x = (numpad - 1) % 3 - 1;            // -1 назад, 0, +1 вперёд
            int y = (numpad - 1) / 3 - 1;            // -1 вниз, 0, +1 вверх
            int rawX = x * facing;
            return new InputFrame(rawX < 0, rawX > 0, y > 0, y < 0, b);
        }
    }

    private static readonly List<string> Log = new();
    private static int _passed, _failed;

    public static int Run(string contentRoot)
    {
        var loader = new ContentLoader(null, contentRoot);
        loader.LoadAll();
        Write($"Персонажей: {loader.Characters.Count} ({string.Join(", ", loader.Characters.Select(c => c.Id))})");
        Write($"Арен: {loader.Stages.Count}");
        foreach (var e in loader.Errors) Fail($"загрузка: {e}");
        foreach (var c in loader.Characters)
            Write($"  {c.Id}: приёмов {c.Moves.Count}, порядок ввода: {string.Join(" ", c.MovesByPriority.Select(m => m.Input))}");

        var pal = loader.Characters.FirstOrDefault(c => c.Id == "paladin");
        var wiz = loader.Characters.FirstOrDefault(c => c.Id == "wizard");
        var stage = loader.Stages[0];
        if (pal != null && wiz != null) Scenarios(pal, wiz, stage);
        MoveListChecks(loader);

        // Каждый персонаж: каждый приём можно выполнить и он завершается без исключений.
        foreach (var c in loader.Characters) EveryMove(c, loader.Characters[0], stage);

        Write($"\nИТОГ: {_passed} пройдено, {_failed} провалено");
        try { File.WriteAllLines(Path.Combine(AppContext.BaseDirectory, "selftest.log"), Log); } catch (IOException) { }
        return _failed == 0 ? 0 : 1;
    }

    private static (FightWorld w, Fighter a, Fighter b, Script s1, Script s2) Make(CharacterDef d1, CharacterDef d2, StageDef stage, float distance)
    {
        var s1 = new Script { Facing = 1 };
        var s2 = new Script { Facing = -1 };
        var a = new Fighter(d1, 0, s1);
        var b = new Fighter(d2, 1, s2);
        var w = new FightWorld(stage, a, b);
        w.ResetRound();
        float mid = stage.Width / 2f;
        a.Pos.X = mid - distance / 2;
        b.Pos.X = mid + distance / 2;
        return (w, a, b, s1, s2);
    }

    private static void Run(FightWorld w, int ticks)
    {
        for (int i = 0; i < ticks; i++) w.Update(true);
    }

    /// <summary>Страница «Список приёмов»: команды переводятся в стрелки и клавиши правильно, ни один приём не потерян.</summary>
    private static void MoveListChecks(ContentLoader loader)
    {
        Write("\n── Список приёмов ──");
        var keys = new KeyBindings(); // по умолчанию: J K L
        foreach (var (input, expected) in new[]
        {
            ("5L", "J"), ("2M", "↓ + K"), ("6H", "→ + L"), ("j.H", "ПРЫЖОК: L"),
            ("236L", "↓ ↘ → + J"), ("214M", "↓ ↙ ← + K"), ("623H", "→ ↓ ↘ + L"),
            ("j.236L", "ПРЫЖОК: ↓ ↘ → + J"), ("LM", "J+K"),
        })
        {
            var text = Scenes.MoveListScene.FormatCommand(MoveCommand.Parse(input), keys);
            Check($"Команда {input} → {expected}", text == expected, $"получилось '{text}'");
        }

        foreach (var c in loader.Characters)
        {
            var missingDesc = c.Moves.Where(m => string.IsNullOrWhiteSpace(m.Description)).Select(m => m.Id).ToList();
            Check($"{c.Id}: у всех приёмов есть описание для страницы", missingDesc.Count == 0,
                $"без описания: {string.Join(", ", missingDesc)}");
        }

        var wiz = loader.Characters.FirstOrDefault(c => c.Id == "wizard");
        if (wiz != null)
        {
            string Dmg(string id) => Scenes.MoveListScene.DamageText(wiz.Moves.First(m => m.Id == id));
            Check("Урон на странице: снаряд, 3 дротика, удар", Dmg("fire_bolt") == "60" && Dmg("magic_missile") == "3X35" && Dmg("5L") == "25",
                $"{Dmg("fire_bolt")} / {Dmg("magic_missile")} / {Dmg("5L")}");
        }
        var pal = loader.Characters.FirstOrDefault(c => c.Id == "paladin");
        if (pal != null)
        {
            var heal = Scenes.MoveListScene.DamageText(pal.Moves.First(m => m.Id == "lay_on_hands"));
            Check("Урон на странице: лечение показано как +150", heal == "+150", heal);
        }
    }

    private static void Scenarios(CharacterDef pal, CharacterDef wiz, StageDef stage)
    {
        Write("\n── Сценарии ──");

        {
            var (w, a, b, s1, _) = Make(wiz, pal, stage, 32);
            s1.Add(5, Buttons.L);
            Run(w, 40);
            Check("5L попадает по стоящему", b.Health == pal.Stats.Health - 25, $"hp {b.Health}");
        }
        {
            var (w, a, b, s1, s2) = Make(pal, wiz, stage, 34);
            s2.Default = Script.Raw(4, -1, Buttons.None);
            HitReport? rep = null;
            w.HitResolved += r => rep = r;
            s1.Add(5, Buttons.M);
            Run(w, 40);
            Check("5M блокируется стоя (держим назад)", rep is { Blocked: true } && b.Health == wiz.Stats.Health, $"blocked={rep?.Blocked} hp={b.Health}");
        }
        {
            var (w, a, b, s1, s2) = Make(pal, wiz, stage, 24);
            s2.Default = Script.Raw(4, -1, Buttons.None);
            HitReport? rep = null;
            w.HitResolved += r => rep = r;
            s1.Add(2, Buttons.L);
            Run(w, 40);
            Check("2L (низ) пробивает стоячий блок", rep is { Blocked: false }, $"blocked={rep?.Blocked}");
        }
        {
            var (w, a, b, s1, s2) = Make(pal, wiz, stage, 34);
            s2.Default = Script.Raw(1, -1, Buttons.None);
            HitReport? rep = null;
            w.HitResolved += r => rep = r;
            s1.Add(2, Buttons.L);
            Run(w, 40);
            Check("2L блокируется сидя", rep is { Blocked: true }, $"blocked={rep?.Blocked}");
        }
        {
            var (w, a, b, s1, s2) = Make(pal, wiz, stage, 90);
            s2.Default = Script.Raw(1, -1, Buttons.None);
            HitReport? rep = null;
            w.HitResolved += r => rep ??= r;
            // Прыжок вперёд, удар на снижении — классический «прыжок-вход».
            s1.Add(9).Wait(26).Add(5, Buttons.H);
            Run(w, 80);
            Check("j.H (оверхед) пробивает сидячий блок", rep is { Blocked: false }, rep == null ? "не попал" : $"blocked={rep.Blocked}");
        }
        {
            var (w, a, b, s1, _) = Make(pal, wiz, stage, 26);
            s1.Add(5, Buttons.L | Buttons.M);
            Run(w, 10);
            Check("Бросок LM", b.State == FighterState.AirHit && b.Health == wiz.Stats.Health - 110, $"state={b.State} hp={b.Health}");
        }
        {
            var (w, a, b, s1, s2) = Make(pal, wiz, stage, 26);
            s1.Add(5, Buttons.L | Buttons.M);
            s2.Add(5, Buttons.L | Buttons.M);
            Run(w, 10);
            Check("Тех броска (оба нажали бросок)", a.Health == pal.Stats.Health && b.Health == wiz.Stats.Health, $"hp {a.Health}/{b.Health}");
        }
        {
            var (w, a, b, s1, _) = Make(wiz, pal, stage, 140);
            s1.Add(2, n: 2).Add(3, n: 2).Add(6, Buttons.L);
            Run(w, 20);
            bool spawned = w.Projectiles.Count == 1;
            Run(w, 60);
            Check("236L: огненный снаряд вылетает и попадает", spawned && b.Health == pal.Stats.Health - 60, $"spawned={spawned} hp={b.Health}");
        }
        {
            var (w, a, b, s1, _) = Make(wiz, pal, stage, 140);
            b.Pos.X = a.Pos.X + 400; // далеко, чтобы первый снаряд ещё летел
            s1.Add(2, n: 2).Add(3, n: 2).Add(6, Buttons.L).Wait(40).Add(2, n: 2).Add(3, n: 2).Add(6, Buttons.L);
            int casts = 0;
            w.MoveStarted += (f, m) => { if (f == a && m.Id == "fire_bolt") casts++; };
            Run(w, 60);
            Check("Лимит: второй снаряд не выходит, пока летит первый", casts == 1 && w.Projectiles.Count == 1,
                $"casts={casts} projectiles={w.Projectiles.Count}");
        }
        {
            var (w, a, b, s1, _) = Make(wiz, pal, stage, 140);
            s1.Add(2, n: 2).Add(3, n: 2).Add(6, Buttons.H);
            Run(w, 70);
            // 35 + 35 + 32 (третий удар комбо — скейлинг 90%).
            Check("236H: волшебная стрела тратит ячейку и попадает трижды",
                Math.Abs(a.GetResource("slots") - 2) < 0.2f && b.Health == pal.Stats.Health - 102 && b.ComboDamage == 102,
                $"slots={a.GetResource("slots"):0.00} hp={b.Health} comboDamage={b.ComboDamage}");
        }
        {
            var (w, a, b, s1, _) = Make(pal, wiz, stage, 34);
            s1.Add(5, Buttons.M).Wait(4).Add(2, n: 2).Add(3, n: 2).Add(6, Buttons.H);
            string? cancelled = null;
            w.MoveStarted += (f, m) => { if (f == a && m.Id == "smite") cancelled = m.Id; };
            Run(w, 90);
            Check("Отмена 5M → 236H (Божественная кара)", cancelled != null && b.Health < wiz.Stats.Health - 55,
                $"cancel={cancelled ?? "нет"} hp={b.Health} combo={b.ComboHits}");
        }
        {
            var (w, a, b, s1, _) = Make(pal, wiz, stage, 34);
            s1.Add(5, Buttons.M).Wait(3).Add(5, Buttons.H);
            Run(w, 60);
            Check("5M → 5H: обычный удар в обычный (цепочка)", b.Health == wiz.Stats.Health - 55 - 90, $"hp={b.Health}");
        }
        {
            var (w, a, b, s1, _) = Make(pal, wiz, stage, 200);
            a.Health = 500;
            s1.Add(2, n: 2).Add(1, n: 2).Add(4, Buttons.M);
            Run(w, 60);
            Check("214M: возложение рук лечит", a.Health == 650, $"hp={a.Health}");
        }
        {
            var (w, a, b, s1, _) = Make(wiz, pal, stage, 60);
            float x0 = a.Pos.X;
            s1.Add(2, n: 2).Add(1, n: 2).Add(4, Buttons.M);
            Run(w, 30);
            Check("214M: туманный шаг телепортирует за спину", a.Pos.X > b.Pos.X, $"x {x0:0} → {a.Pos.X:0}, соперник {b.Pos.X:0}");
        }
        {
            var (w, a, b, s1, _) = Make(wiz, pal, stage, 40);
            float x0 = a.Pos.X;
            s1.Add(2, n: 2).Add(1, n: 2).Add(4, Buttons.L);
            Run(w, 30);
            Check("214L: туманный шаг назад разрывает дистанцию", b.Pos.X - a.Pos.X > 100, $"дистанция {b.Pos.X - a.Pos.X:0}");
        }
        {
            var (w, a, b, s1, _) = Make(wiz, pal, stage, 140);
            s1.Add(2, n: 2).Add(3, n: 2).Add(6, Buttons.M);
            HitReport? rep = null;
            w.HitResolved += r => rep = r;
            Run(w, 40);
            Check("236M: луч холода быстрый (долетает за 40 тиков)", rep is { Damage: 45 }, $"hp={b.Health}");
        }
        {
            var (w, a, b, s1, _) = Make(wiz, pal, stage, 300);
            b.Pos.X = a.Pos.X + 400;
            s1.Add(2, n: 2).Add(3, n: 2).Add(6, Buttons.L).Wait(4).Add(2, n: 2).Add(3, n: 2).Add(6, Buttons.M);
            int casts = 0;
            w.MoveStarted += (f, m) => { if (f == a) casts++; };
            Run(w, 70);
            Check("Заговоры делят лимит: огонь и холод не летят одновременно", casts == 1, $"casts={casts}");
        }
        {
            var (w, a, b, s1, _) = Make(wiz, pal, stage, 110);
            s1.Add(9).Wait(10).Add(2).Add(3).Add(6, Buttons.L);
            HitReport? rep = null;
            string? started = null;
            w.MoveStarted += (f, m) => { if (f == a) started = m.Id; };
            w.HitResolved += r => rep = r;
            Run(w, 90);
            Check("j.236L: огонь с высоты летит вниз-вперёд и попадает", started == "air_fire_bolt" && rep != null,
                $"move={started ?? "нет"} hit={rep != null}");
        }
        {
            var (w, a, b, s1, _) = Make(wiz, pal, stage, 120);
            s1.Add(2, n: 2).Add(1, n: 2).Add(4, Buttons.H);
            int hits = 0;
            w.HitResolved += r => { if (r.Attacker == a) hits++; };
            Run(w, 200);
            Check("214H: облако кинжалов висит и бьёт несколько раз", hits >= 3, $"hits={hits}");
        }
        {
            var (w, a, b, s1, _) = Make(pal, wiz, stage, 34);
            b.Resources["slots"] = 1;
            s1.Add(2, Buttons.H);
            Run(w, 90);
            Check("Поведение arcane_recovery: после нокдауна +1 ячейка", b.GetResource("slots") >= 1.99f, $"slots={b.GetResource("slots"):0.00}");
        }
        {
            var (w, a, b, s1, s2) = Make(pal, wiz, stage, 34);
            var heavy = pal.Moves.First(m => m.Id == "5H");
            int ticks = 0;
            while (!b.IsKo && ticks < 5000)
            {
                if (a.State == FighterState.Idle && b.State == FighterState.Idle)
                {
                    b.Pos.X = a.Pos.X + 30;
                    a.StartMove(heavy, w);
                }
                w.Update(true);
                ticks++;
            }
            Run(w, 120);
            Check("Нокаут: здоровье до нуля → KO", b.IsKo && b.State == FighterState.KO, $"ticks={ticks} state={b.State}");
        }
        {
            var (w, a, b, s1, s2) = Make(pal, wiz, stage, 34);
            s2.Add(6, Buttons.H); // Элара начинает 5H
            s1.Wait(8).Add(6, n: 1).Add(2, n: 1).Add(3, Buttons.H);  // Торин отвечает 623H с неуязвимостью
            Run(w, 60);
            Check("623H: неуязвимый восходящий удар перебивает атаку", a.Health == pal.Stats.Health && b.Health < wiz.Stats.Health,
                $"hp {a.Health}/{b.Health}");
        }
    }

    private static void EveryMove(CharacterDef def, CharacterDef opp, StageDef stage)
    {
        foreach (var m in def.Moves)
        {
            var (w, a, b, _, _) = Make(def, opp, stage, 40);
            foreach (var r in def.Resources) a.Resources[r.Id] = r.Max;
            try
            {
                if (m.Command.Air)
                {
                    a.Pos.Y = -40;
                    a.Vel.Y = -2;
                    a.SetState(FighterState.Air);
                }
                a.StartMove(m, w);
                Run(w, m.TotalFrames + 80);
                Check($"{def.Id}: {m.Id} ({m.Input}) выполняется", a.State != FighterState.Attack, $"state={a.State}");
            }
            catch (Exception e)
            {
                Fail($"{def.Id}: {m.Id} — исключение {e.GetType().Name}: {e.Message}");
            }
        }
    }

    private static void Check(string name, bool ok, string details)
    {
        if (ok) { _passed++; Write($"  OK    {name}"); }
        else Fail($"{name} ({details})");
    }

    private static void Fail(string msg)
    {
        _failed++;
        Write($"  FAIL  {msg}");
    }

    private static void Write(string s)
    {
        Log.Add(s);
        Console.WriteLine(s);
    }
}
