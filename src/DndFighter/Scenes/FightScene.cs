using DndFighter.Combat;
using DndFighter.Data;
using DndFighter.Engine;
using DndFighter.Input;
using DndFighter.Rendering;
using DndFighter.Training;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace DndFighter.Scenes;

/// <summary>Бой: раунды, таймер, HUD. В режиме тренировки — манекен, хитбоксы и фреймдата.</summary>
public sealed class FightScene : Scene
{
    private enum Phase { Intro, Fight, RoundOver, MatchOver, Paused }

    public const int RoundsToWin = 2;
    public const int RoundSeconds = 99;

    private readonly GameMode _mode;
    private CharacterDef _def1, _def2;
    private readonly StageDef _stage;
    private FightWorld _world = null!;
    private TrainingDummy? _dummy;

    private Phase _phase;
    private Phase _phaseBeforePause;
    private int _phaseTime;
    private int _round;
    private readonly int[] _wins = new int[2];
    private int _timerTicks;
    private string _banner = "";
    private int _pauseCursor;

    // тренировка
    private bool _showBoxes;
    private bool _infiniteResources = true;
    private string _lastMoveInfo = "";
    private string _lastHitInfo = "";
    private int _idleTicks;

    public FightScene(FighterGame game, GameMode mode, CharacterDef p1, CharacterDef p2, StageDef stage) : base(game)
    {
        _mode = mode;
        _def1 = p1;
        _def2 = p2;
        _stage = stage;
    }

    private bool Training => _mode == GameMode.Training;

    public override void Enter()
    {
        BuildWorld();
        if (Training)
        {
            _showBoxes = false;
            _world.ResetRound();
            _phase = Phase.Fight;
        }
        else StartRound(1);
    }

    private void BuildWorld()
    {
        var c = Game.Controls;
        var f1 = new Fighter(_def1, 0, new KeyboardSource(c.P1));
        IInputSource src2;
        if (Training)
        {
            _dummy ??= new TrainingDummy();
            src2 = _dummy;
        }
        else src2 = new KeyboardSource(c.P2);
        var f2 = new Fighter(_def2, 1, src2);
        if (_dummy != null) _dummy.Self = f2;

        _world = new FightWorld(_stage, f1, f2);
        _world.MoveStarted += OnMoveStarted;
        _world.HitResolved += OnHit;
    }

    private void StartRound(int n)
    {
        _round = n;
        _world.ResetRound();
        _timerTicks = RoundSeconds * 60;
        SetPhase(Phase.Intro);
    }

    private void SetPhase(Phase p)
    {
        _phase = p;
        _phaseTime = 0;
    }

    // ───────────────────────────── обновление ─────────────────────────────

    public override void Update()
    {
        var m = Game.Menu;
        if (m.KeyPressed(Keys.F2)) _showBoxes = !_showBoxes;

        if (_phase == Phase.Paused) { UpdatePause(); return; }
        if (m.KeyPressed(Keys.Escape))
        {
            _phaseBeforePause = _phase;
            _pauseCursor = 0;
            _phase = Phase.Paused;
            return;
        }

        if (Training) { UpdateTraining(); return; }

        _phaseTime++;
        switch (_phase)
        {
            case Phase.Intro:
                _banner = _phaseTime < 60 ? $"РАУНД {_round}" : "БОЙ!";
                _world.Update(inputEnabled: false);
                if (_phaseTime >= 90) { SetPhase(Phase.Fight); }
                break;

            case Phase.Fight:
                _banner = _phaseTime < 30 ? "БОЙ!" : "";
                _world.Update(inputEnabled: true);
                if (_timerTicks > 0 && _world.Fighters.All(f => f.Hitstop == 0)) _timerTicks--;
                CheckRoundEnd();
                break;

            case Phase.RoundOver:
                _world.Update(inputEnabled: false);
                if (_phaseTime == 70) GiveVictoryPoses();
                if (_phaseTime >= 180) NextRoundOrMatchEnd();
                break;

            case Phase.MatchOver:
                _world.Update(inputEnabled: false);
                if (m.AnyPressed(MenuAction.Confirm) && _phaseTime > 60) { _wins[0] = _wins[1] = 0; StartRound(1); }
                if (m.AnyPressed(MenuAction.Back) && _phaseTime > 60) BackToSelect();
                break;
        }
    }

    private void CheckRoundEnd()
    {
        var (a, b) = (_world.Fighters[0], _world.Fighters[1]);
        bool ko = a.IsKo || b.IsKo;
        bool timeout = _timerTicks <= 0;
        if (!ko && !timeout) return;

        if (ko)
        {
            _banner = a.IsKo && b.IsKo ? "ДВОЙНОЙ НОКАУТ" : "НОКАУТ!";
            if (!a.IsKo) _wins[0]++;
            if (!b.IsKo) _wins[1]++;
        }
        else
        {
            _banner = "ВРЕМЯ!";
            float ra = a.Health / a.Def.Stats.Health, rb = b.Health / b.Def.Stats.Health;
            if (ra >= rb) _wins[0]++;
            if (rb >= ra) _wins[1]++;
        }
        SetPhase(Phase.RoundOver);
    }

    private void GiveVictoryPoses()
    {
        var (a, b) = (_world.Fighters[0], _world.Fighters[1]);
        float ra = a.Health / a.Def.Stats.Health, rb = b.Health / b.Def.Stats.Health;
        if (ra > rb && a.Grounded) a.Victory();
        if (rb > ra && b.Grounded) b.Victory();
    }

    private void NextRoundOrMatchEnd()
    {
        if (_wins[0] >= RoundsToWin || _wins[1] >= RoundsToWin)
        {
            _banner = _wins[0] == _wins[1] ? "НИЧЬЯ"
                : $"ПОБЕДА: {(_wins[0] > _wins[1] ? _def1.Name : _def2.Name)}";
            SetPhase(Phase.MatchOver);
        }
        else StartRound(_round + 1);
    }

    private void UpdateTraining()
    {
        var m = Game.Menu;
        if (m.KeyPressed(Keys.F1) && _dummy != null)
            _dummy.Mode = (DummyMode)(((int)_dummy.Mode + 1) % Enum.GetValues<DummyMode>().Length);
        if (m.KeyPressed(Keys.F3)) _world.ResetRound();
        if (m.KeyPressed(Keys.F4)) _infiniteResources = !_infiniteResources;
        if (m.KeyPressed(Keys.F5)) ReloadContent();

        _world.Update(inputEnabled: true);
        _banner = "";

        // Здоровье и ресурсы восстанавливаются, когда оба бойца спокойны.
        bool calm = _world.Fighters.All(f => f.State is FighterState.Idle or FighterState.Walk or FighterState.Crouch);
        _idleTicks = calm ? _idleTicks + 1 : 0;
        foreach (var f in _world.Fighters)
        {
            if (f.IsKo && f.State == FighterState.KO) { f.IsKo = false; f.SetState(FighterState.GetUp); }
            if (_idleTicks > 40) f.Health = f.DisplayedHealth = f.Def.Stats.Health;
            if (_infiniteResources && f.State != FighterState.Attack)
                foreach (var r in f.Def.Resources) f.Resources[r.Id] = r.Max;
        }
    }

    /// <summary>F5 в тренировке: перечитать JSON и спрайты, не выходя из боя. Удобно для настройки приёмов.</summary>
    private void ReloadContent()
    {
        Game.Loader.LoadAll();
        var d1 = Game.Loader.Characters.FirstOrDefault(c => c.Id == _def1.Id);
        var d2 = Game.Loader.Characters.FirstOrDefault(c => c.Id == _def2.Id);
        if (d1 == null || d2 == null)
        {
            _lastHitInfo = "ОШИБКА ЗАГРУЗКИ: " + (Game.Loader.Errors.FirstOrDefault() ?? "персонаж пропал");
            return;
        }
        _def1 = d1;
        _def2 = d2;
        BuildWorld();
        _world.ResetRound();
        _lastMoveInfo = "";
        _lastHitInfo = Game.Loader.Errors.Count > 0 ? "ОШИБКА: " + Game.Loader.Errors[0] : "КОНТЕНТ ПЕРЕЗАГРУЖЕН";
    }

    private void UpdatePause()
    {
        var m = Game.Menu;
        string[] items = PauseItems();
        if (m.AnyPressed(MenuAction.Up)) _pauseCursor = (_pauseCursor + items.Length - 1) % items.Length;
        if (m.AnyPressed(MenuAction.Down)) _pauseCursor = (_pauseCursor + 1) % items.Length;
        if (m.KeyPressed(Keys.Escape)) { _phase = _phaseBeforePause; return; }
        if (!m.AnyPressed(MenuAction.Confirm)) return;
        switch (_pauseCursor)
        {
            case 0: _phase = _phaseBeforePause; break;
            case 1: BackToSelect(); break;
            default: Game.ChangeScene(new TitleScene(Game)); break;
        }
    }

    private static string[] PauseItems() => new[] { "ПРОДОЛЖИТЬ", "ВЫБОР БОЙЦОВ", "ГЛАВНОЕ МЕНЮ" };

    private void BackToSelect()
    {
        var roster = Game.Loader.Characters;
        Game.ChangeScene(new SelectScene(Game, _mode,
            Math.Max(0, roster.FindIndex(c => c.Id == _def1.Id)),
            Math.Max(0, roster.FindIndex(c => c.Id == _def2.Id))));
    }

    // ───────────────────────────── события ─────────────────────────────

    private void OnMoveStarted(Fighter f, MoveDef m)
    {
        if (f.Index != 0) return;
        _lastMoveInfo = $"{m.Name} ({m.Input})  СТАРТ {m.Startup}  АКТИВ {m.Active}  ВОССТ {m.Recovery}";
    }

    private void OnHit(HitReport r)
    {
        if (r.Attacker.Index != 0) return;
        string adv = r.Advantage is int a ? (a >= 0 ? $"+{a}" : a.ToString()) : "НОКДАУН";
        _lastHitInfo = $"{(r.Blocked ? "БЛОК" : "ПОПАДАНИЕ")}  УРОН {r.Damage}  ПРЕИМУЩЕСТВО {adv}";
    }

    // ───────────────────────────── отрисовка ─────────────────────────────

    public override void Draw(Draw d)
    {
        WorldRenderer.DrawWorld(d, _world, _showBoxes);
        Hud.Draw(d, _world, _wins, Training ? -1 : (_timerTicks + 59) / 60);

        if (_banner.Length > 0) d.TextCentered(_banner, 240, 100, new Color(255, 220, 90), 4);

        if (_phase == Phase.MatchOver && _phaseTime > 60)
            d.TextCentered("L: РЕВАНШ   M: ВЫБОР БОЙЦОВ", 240, 140, Color.White);

        if (Training) DrawTrainingInfo(d);
        if (_phase == Phase.Paused) DrawPause(d);
    }

    private void DrawTrainingInfo(Draw d)
    {
        var dim = new Color(200, 195, 230);
        d.Rect(0, 238, 480, 32, Color.Black * 0.55f);
        d.Text(_lastMoveInfo, 6, 241, Color.White);
        d.Text(_lastHitInfo, 6, 251, new Color(255, 220, 120));
        string mode = _dummy != null ? TrainingDummy.ModeName(_dummy.Mode) : "";
        d.Text($"F1 МАНЕКЕН: {mode}  F2 БОКСЫ  F3 СБРОС  F4 РЕСУРСЫ: {(_infiniteResources ? "ВЕЧН" : "ОБЫЧН")}  F5 JSON", 6, 261, dim);
    }

    private void DrawPause(Draw d)
    {
        d.Rect(0, 0, 480, 270, Color.Black * 0.6f);
        d.TextCentered("ПАУЗА", 240, 80, Color.White, 3);
        var items = PauseItems();
        for (int i = 0; i < items.Length; i++)
        {
            bool sel = i == _pauseCursor;
            d.TextCentered(sel ? $"> {items[i]} <" : items[i], 240, 120 + i * 16, sel ? Color.White : Color.Gray, 1);
        }
    }
}
