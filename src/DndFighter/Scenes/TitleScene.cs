using DndFighter.Engine;
using DndFighter.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace DndFighter.Scenes;

public enum GameMode { Versus, Training }

public sealed class TitleScene : Scene
{
    private static readonly string[] Items = { "ДУЭЛЬ: 2 ИГРОКА", "ТРЕНИРОВКА", "ВЫХОД" };
    private int _cursor;
    private int _time;

    public TitleScene(FighterGame game) : base(game) { }

    public override void Update()
    {
        _time++;
        var m = Game.Menu;
        if (m.AnyPressed(MenuAction.Up)) _cursor = (_cursor + Items.Length - 1) % Items.Length;
        if (m.AnyPressed(MenuAction.Down)) _cursor = (_cursor + 1) % Items.Length;
        if (m.KeyPressed(Keys.F5)) Game.Loader.LoadAll();

        if (m.AnyPressed(MenuAction.Confirm))
        {
            if (_cursor == 2) { Game.Exit(); return; }
            if (Game.Loader.Characters.Count == 0) return;
            Game.ChangeScene(new SelectScene(Game, _cursor == 0 ? GameMode.Versus : GameMode.Training));
        }
    }

    public override void Draw(Draw d)
    {
        d.Rect(0, 0, 480, 270, new Color(18, 16, 30));
        for (int i = 0; i < 40; i++)
        {
            int x = (i * 97 + 13) % 480, y = (i * 53 + 7) % 150;
            float tw = (MathF.Sin(_time * 0.05f + i) + 1) / 2;
            d.Rect(x, y, 1, 1, Color.White * (0.3f + tw * 0.7f));
        }

        d.TextCentered(FighterGame.Title, 240, 50, new Color(255, 210, 90), 4);
        d.TextCentered("ФАЙТИНГ ПО МИРУ ДНД • ПРОТОТИП", 240, 90, new Color(170, 160, 200));

        for (int i = 0; i < Items.Length; i++)
        {
            bool sel = i == _cursor;
            var c = sel ? Color.White : new Color(130, 125, 160);
            d.TextCentered(sel ? $"> {Items[i]} <" : Items[i], 240, 130 + i * 18, c, 2);
        }

        var k1 = Game.Controls.P1;
        var k2 = Game.Controls.P2;
        d.TextCentered($"И1: {k1.Up}{k1.Left}{k1.Down}{k1.Right} + {k1.L} {k1.M} {k1.H}", 240, 212, new Color(120, 170, 255));
        d.TextCentered($"И2: СТРЕЛКИ + {Key(k2.L)} {Key(k2.M)} {Key(k2.H)}", 240, 222, new Color(255, 130, 130));
        d.TextCentered($"ПЕРСОНАЖЕЙ: {Game.Loader.Characters.Count}  •  F5 ПЕРЕЗАГРУЗИТЬ  •  F11 ПОЛНЫЙ ЭКРАН", 240, 236, new Color(110, 105, 140));

        int errY = 250;
        foreach (var err in Game.Loader.Errors.Take(2))
        {
            d.TextCentered("ОШИБКА " + Truncate(err, 70), 240, errY, new Color(255, 90, 90));
            errY += 9;
        }
    }

    private static string Key(Keys k) => k.ToString().Replace("NumPad", "NUM");
    private static string Truncate(string s, int n) => s.Length <= n ? s : s[..n] + "...";
}
