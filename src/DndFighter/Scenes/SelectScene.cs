using DndFighter.Data;
using DndFighter.Engine;
using DndFighter.Input;
using DndFighter.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace DndFighter.Scenes;

/// <summary>Выбор персонажей. Список строится из всех загруженных папок — новые персонажи появляются сами.</summary>
public sealed class SelectScene : Scene
{
    private static readonly Color P1Color = new(90, 150, 255);
    private static readonly Color P2Color = new(255, 90, 90);

    private readonly GameMode _mode;
    private readonly int[] _cursor = { 0, 1 };
    private readonly bool[] _locked = new bool[2];
    private int _time;

    public SelectScene(FighterGame game, GameMode mode, int p1 = 0, int p2 = 1) : base(game)
    {
        _mode = mode;
        _cursor[0] = p1;
        _cursor[1] = p2;
    }

    private List<CharacterDef> Roster => Game.Loader.Characters;

    public override void Enter()
    {
        for (int i = 0; i < 2; i++) _cursor[i] = Roster.Count == 0 ? 0 : Math.Min(_cursor[i], Roster.Count - 1);
    }

    public override void Update()
    {
        _time++;
        var m = Game.Menu;
        if (Roster.Count == 0) { if (m.AnyPressed(MenuAction.Back)) Game.ChangeScene(new TitleScene(Game)); return; }

        if (m.KeyPressed(Keys.F5)) { Game.Loader.LoadAll(); Enter(); }

        if (_mode == GameMode.Versus)
        {
            for (int p = 0; p < 2; p++) HandlePlayer(p, p);
        }
        else
        {
            // Тренировка: первый игрок выбирает себя, затем манекен.
            int slot = _locked[0] ? 1 : 0;
            HandlePlayer(0, slot);
        }

        if (_locked[0] && _locked[1])
        {
            var stage = Game.Loader.Stages[Random.Shared.Next(Game.Loader.Stages.Count)];
            Game.ChangeScene(new FightScene(Game, _mode, Roster[_cursor[0]], Roster[_cursor[1]], stage));
        }
    }

    private void HandlePlayer(int player, int slot)
    {
        var m = Game.Menu;
        if (m.Pressed(player, MenuAction.Back))
        {
            if (_locked[slot]) _locked[slot] = false;
            else if (slot == 1 && _mode == GameMode.Training) _locked[0] = false;
            else Game.ChangeScene(new TitleScene(Game));
            return;
        }
        if (_locked[slot]) return;
        int n = Roster.Count;
        if (m.Pressed(player, MenuAction.Left)) _cursor[slot] = (_cursor[slot] + n - 1) % n;
        if (m.Pressed(player, MenuAction.Right)) _cursor[slot] = (_cursor[slot] + 1) % n;
        if (m.Pressed(player, MenuAction.Confirm)) _locked[slot] = true;
    }

    public override void Draw(Draw d)
    {
        d.Rect(0, 0, 480, 270, new Color(22, 18, 36));
        d.TextCentered(_mode == GameMode.Versus ? "ВЫБОР БОЙЦОВ" : "ТРЕНИРОВКА", 240, 10, new Color(255, 210, 90), 2);

        if (Roster.Count == 0)
        {
            d.TextCentered("НЕТ ПЕРСОНАЖЕЙ В CONTENT/CHARACTERS", 240, 130, Color.White);
            return;
        }

        string hint1 = _mode == GameMode.Training ? "ИГРОК" : "ИГРОК 1";
        string hint2 = _mode == GameMode.Training ? "МАНЕКЕН" : "ИГРОК 2";
        DrawPreview(d, 0, 100, hint1);
        DrawPreview(d, 1, 380, hint2);

        // Сетка портретов.
        const int cell = 44;
        int total = Roster.Count * cell;
        int x0 = 240 - total / 2;
        for (int i = 0; i < Roster.Count; i++)
        {
            int x = x0 + i * cell;
            const int y = 206;
            d.Rect(x + 2, y, cell - 4, 48, new Color(40, 34, 60));
            var def = Roster[i];
            var idle = def.GetAnimation("idle");
            if (idle != null) WorldRenderer.DrawFrame(d, def, idle, 0, x + cell / 2, y + 46, 1, Color.White);
            for (int p = 0; p < 2; p++)
            {
                if (_cursor[p] != i) continue;
                bool active = _mode == GameMode.Versus || (p == 0 ? !_locked[0] : _locked[0]);
                if (!active && !_locked[p]) continue;
                var c = p == 0 ? P1Color : P2Color;
                bool blink = !_locked[p] && _time / 15 % 2 == 0;
                d.Frame(x + 2 - p, y - p, cell - 4 + p * 2, 48 + p * 2, blink ? Color.White : c);
            }
        }

        DrawControls(d);
    }

    /// <summary>Подсказки с реальными клавишами каждого игрока (берутся из controls.json).</summary>
    private void DrawControls(Draw d)
    {
        var dim = new Color(150, 145, 180);
        if (_mode == GameMode.Versus)
        {
            DrawKeyHints(d, 0, 12, left: true);
            DrawKeyHints(d, 1, 468, left: false);
            d.TextCentered("ОБА ИГРОКА ВЫБИРАЮТ ОДНОВРЕМЕННО  •  ESC - В МЕНЮ", 240, 260, dim);
        }
        else
        {
            // В тренировке всё выбирает первый игрок: сначала себя, потом манекен.
            DrawKeyHints(d, 0, _locked[0] ? 468 : 12, left: !_locked[0]);
            string step = _locked[0] ? "ШАГ 2 ИЗ 2: ВЫБЕРИ МАНЕКЕН" : "ШАГ 1 ИЗ 2: ВЫБЕРИ СВОЕГО БОЙЦА";
            d.TextCentered(step + "  •  ESC - В МЕНЮ", 240, 260, Color.Yellow);
        }
    }

    private void DrawKeyHints(Draw d, int player, int x, bool left)
    {
        var k = Game.Controls.For(player);
        var color = player == 0 ? P1Color : P2Color;
        int slot = _mode == GameMode.Training ? (_locked[0] ? 1 : 0) : player;
        string[] lines = _locked[slot]
            ? new[] { $"{KeyName(k.M)} - ОТМЕНИТЬ" }
            : new[] { $"{KeyName(k.Left)} {KeyName(k.Right)} - ЛИСТАТЬ", $"{KeyName(k.L)} - ВЫБРАТЬ", $"{KeyName(k.M)} - НАЗАД" };

        int y = 212;
        foreach (var line in lines)
        {
            if (left) d.Text(line, x, y, color);
            else d.TextRight(line, x, y, color);
            y += 11;
        }
    }

    public static string KeyName(Keys key) => key switch
    {
        Keys.Left => "←",
        Keys.Right => "→",
        Keys.Up => "↑",
        Keys.Down => "↓",
        >= Keys.D0 and <= Keys.D9 => ((int)(key - Keys.D0)).ToString(),
        >= Keys.NumPad0 and <= Keys.NumPad9 => "NUM" + (int)(key - Keys.NumPad0),
        Keys.Enter => "ENTER",
        Keys.Space => "ПРОБЕЛ",
        _ => key.ToString().ToUpperInvariant(),
    };

    private void DrawPreview(Draw d, int slot, int cx, string label)
    {
        var def = Roster[_cursor[slot]];
        var color = slot == 0 ? P1Color : P2Color;
        d.TextCentered(label, cx, 34, color);
        var anim = def.GetAnimation(_locked[slot] ? "victory" : "idle") ?? def.GetAnimation("idle");
        if (anim != null)
            WorldRenderer.DrawFrame(d, def, anim, anim.FrameAt(_time), cx, 170, slot == 0 ? 1 : -1, Color.White, 2);
        d.TextCentered(def.Name, cx, 176, Color.White, 2);
        d.TextCentered(def.Title, cx, 193, new Color(170, 160, 200));
        if (_locked[slot]) d.TextCentered("ГОТОВ!", cx, 44, Color.Yellow);
    }
}
