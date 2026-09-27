using DndFighter.Combat;
using DndFighter.Data;
using DndFighter.Engine;
using DndFighter.Input;
using DndFighter.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace DndFighter.Scenes;

/// <summary>
/// Страница «Список приёмов»: все приёмы персонажа с командами стрелками и клавишами игрока.
/// Строится автоматически из moves.json — новые персонажи и приёмы появляются здесь сами.
/// </summary>
public sealed class MoveListScene : Scene
{
    private const int ListTop = 60, RowH = 10, VisibleRows = 15;

    private readonly Scene _returnTo;
    private int _charIndex;
    private int _cursor;   // индекс выбранного приёма в списке строк
    private int _scroll;   // первая видимая строка
    private List<Row> _rows = new();

    /// <summary>Строка списка: либо заголовок раздела, либо приём.</summary>
    private sealed record Row(string? Header, MoveDef? Move);

    /// <param name="returnTo">Экран, на который вернуться без перезапуска (пауза боя).</param>
    public MoveListScene(FighterGame game, int charIndex, Scene returnTo) : base(game)
    {
        _charIndex = charIndex;
        _returnTo = returnTo;
    }

    private List<CharacterDef> Roster => Game.Loader.Characters;
    private CharacterDef? Current => Roster.Count == 0 ? null : Roster[Math.Clamp(_charIndex, 0, Roster.Count - 1)];

    public override void Enter() => Rebuild();

    private void Rebuild()
    {
        _rows = BuildRows(Current);
        _cursor = _rows.FindIndex(r => r.Move != null);
        _scroll = 0;
    }

    /// <summary>Приёмы по разделам: сначала спецприёмы (самое важное), потом броски, обычные, в прыжке.</summary>
    private static List<Row> BuildRows(CharacterDef? def)
    {
        var rows = new List<Row>();
        if (def == null) return rows;
        var moves = def.Moves.Where(m => !string.IsNullOrWhiteSpace(m.Input)).ToList();

        void Section(string title, IEnumerable<MoveDef> items)
        {
            var list = items.ToList();
            if (list.Count == 0) return;
            rows.Add(new Row(title, null));
            rows.AddRange(list.Select(m => new Row(null, m)));
        }

        Section("СПЕЦПРИЁМЫ", moves.Where(m => m.Type is MoveType.Special or MoveType.Super));
        Section("БРОСОК", moves.Where(m => m.Type == MoveType.Throw));
        Section("ОБЫЧНЫЕ УДАРЫ", moves.Where(m => m.Type == MoveType.Normal && !m.Command.Air));
        Section("В ПРЫЖКЕ", moves.Where(m => m.Type == MoveType.Normal && m.Command.Air));
        return rows;
    }

    // ───────────────────────────── ввод ─────────────────────────────

    public override void Update()
    {
        var m = Game.Menu;
        if (m.AnyPressed(MenuAction.Back))
        {
            Game.ResumeScene(_returnTo);
            return;
        }
        if (m.KeyPressed(Keys.F5)) { Game.Loader.LoadAll(); Rebuild(); }

        int n = Roster.Count;
        if (n > 0 && m.AnyPressed(MenuAction.Left)) { _charIndex = (_charIndex + n - 1) % n; Rebuild(); }
        if (n > 0 && m.AnyPressed(MenuAction.Right)) { _charIndex = (_charIndex + 1) % n; Rebuild(); }
        if (m.AnyPressed(MenuAction.Up)) MoveCursor(-1);
        if (m.AnyPressed(MenuAction.Down)) MoveCursor(+1);
    }

    /// <summary>Переход к соседнему приёму, пропуская заголовки разделов; список прокручивается за курсором.</summary>
    private void MoveCursor(int dir)
    {
        int i = _cursor;
        do i += dir; while (i >= 0 && i < _rows.Count && _rows[i].Move == null);
        if (i < 0 || i >= _rows.Count) return;
        _cursor = i;

        int top = _cursor;
        if (top > 0 && _rows[top - 1].Header != null) top--; // показать и заголовок раздела
        if (top < _scroll) _scroll = top;
        if (_cursor >= _scroll + VisibleRows) _scroll = _cursor - VisibleRows + 1;
    }

    // ───────────────────────────── отрисовка ─────────────────────────────

    public override void Draw(Draw d)
    {
        d.Rect(0, 0, 480, 270, new Color(20, 17, 32));
        var def = Current;
        if (def == null)
        {
            d.TextCentered("НЕТ ПЕРСОНАЖЕЙ", 240, 130, Color.White);
            return;
        }

        // Шапка: портрет, имя, ресурсы.
        var idle = def.GetAnimation("idle");
        if (idle != null) WorldRenderer.DrawFrame(d, def, idle, 0, 30, 52, 1, Color.White);
        d.TextCentered("СПИСОК ПРИЁМОВ", 240, 4, new Color(150, 140, 190));
        d.TextCentered(def.Name, 240, 14, new Color(255, 210, 90), 2);
        d.TextCentered(def.Title, 240, 31, new Color(170, 160, 200));
        if (Roster.Count > 1)
        {
            d.Text("←", 150, 18, Color.White);
            d.Text("→", 325, 18, Color.White);
        }

        // Заголовки колонок.
        var dim = new Color(120, 115, 150);
        d.Text("ПРИЁМ", 64, 48, dim);
        d.Text("КОМАНДА", 216, 48, dim);
        d.Text("ЦЕНА", 346, 48, dim);
        d.Text("УРОН", 388, 48, dim);
        d.Text("СТАРТ", 430, 48, dim);
        d.Rect(8, 57, 464, 1, dim * 0.5f);

        var keys = Game.Controls.P1;
        for (int i = _scroll; i < Math.Min(_rows.Count, _scroll + VisibleRows); i++)
        {
            var row = _rows[i];
            int y = ListTop + (i - _scroll) * RowH;
            if (row.Header != null)
            {
                d.Text(row.Header, 64, y, new Color(140, 200, 255));
                continue;
            }
            var move = row.Move!;
            bool sel = i == _cursor;
            if (sel) d.Rect(60, y - 2, 414, RowH, new Color(70, 60, 110));
            var nameColor = move.Type is MoveType.Special or MoveType.Super ? new Color(255, 225, 140) : Color.White;
            d.Text(Fit(move.Name, 23), 70, y, sel ? Color.White : nameColor);
            d.Text(FormatCommand(move.Command, keys), 216, y, sel ? Color.White : new Color(200, 230, 255));
            DrawCost(d, def, move, 346, y);
            d.Text(DamageText(move), 388, y, new Color(255, 170, 150));
            d.Text(move.Startup.ToString(), 436, y, new Color(190, 190, 210));
        }

        // Полоса прокрутки, если строки не помещаются.
        if (_rows.Count > VisibleRows)
        {
            int track = VisibleRows * RowH;
            int thumb = Math.Max(8, track * VisibleRows / _rows.Count);
            int pos = (track - thumb) * _scroll / Math.Max(1, _rows.Count - VisibleRows);
            d.Rect(474, ListTop - 2, 2, track, dim * 0.4f);
            d.Rect(474, ListTop - 2 + pos, 2, thumb, dim);
        }

        DrawDetails(d, def);

        d.Rect(0, 249, 480, 21, Color.Black * 0.4f);
        string back = $"{SelectScene.KeyName(keys.M)}/ESC";
        d.TextCentered($"↑ ↓ ВЫБОР   ← → ПЕРСОНАЖ   {back} НАЗАД", 240, 251, dim);
        d.TextCentered($"КЛАВИШИ ИГРОКА 1 ({SelectScene.KeyName(keys.L)} {SelectScene.KeyName(keys.M)} {SelectScene.KeyName(keys.H)}). СТОИШЬ СПРАВА - СТРЕЛКИ НАОБОРОТ",
            240, 261, dim);
    }

    /// <summary>Нижняя панель: описание выбранного приёма и его свойства.</summary>
    private void DrawDetails(Draw d, CharacterDef def)
    {
        if (_cursor < 0 || _cursor >= _rows.Count || _rows[_cursor].Move is not { } m) return;
        d.Rect(8, 212, 464, 1, new Color(120, 115, 150) * 0.5f);
        int y = 216;
        foreach (var line in Wrap(m.Description, 76).Take(2))
        {
            d.Text(line, 12, y, new Color(225, 220, 240));
            y += 10;
        }
        var props = new List<string> { $"СТАРТ {m.Startup}", $"АКТИВ {m.Active}", $"ВОССТ {m.Recovery}" };
        if (m.Hitboxes.Count > 0)
        {
            props.Add(m.Hit.Height switch
            {
                HitHeight.Low => "НИЗ: БЛОК СИДЯ",
                HitHeight.Overhead => "ВЕРХ: БЛОК СТОЯ",
                _ => "СРЕДНИЙ",
            });
            if (m.Hit.Knockdown || m.Hit.Launch != null) props.Add("СБИВАЕТ С НОГ");
        }
        if (m.Type == MoveType.Throw) props.Add("НЕ БЛОКИРУЕТСЯ");
        d.Text(string.Join("  ", props), 12, 238, new Color(150, 145, 180));
    }

    private static void DrawCost(Draw d, CharacterDef def, MoveDef m, int x, int y)
    {
        if (m.Cost.Count == 0) { d.Text("-", x + 6, y, new Color(110, 105, 140)); return; }
        foreach (var (id, amount) in m.Cost)
        {
            var res = def.Resources.FirstOrDefault(r => r.Id == id);
            var color = res != null ? ColorUtil.Parse(res.Color) : Color.White;
            d.Rect(x, y + 1, 5, 5, color);   // значок ресурса его цветом (как на HUD)
            d.Text($"{amount:0.#}", x + 8, y, color);
            x += 20;
        }
    }

    // ───────────────────────────── форматирование ─────────────────────────────

    /// <summary>
    /// Команда стрелками и клавишами игрока: "236H" → "↓ ↘ → + L", "2M" → "↓ + K", "j.L" → "ПРЫЖОК: J".
    /// Направления даны для персонажа, стоящего слева (смотрит вправо).
    /// </summary>
    public static string FormatCommand(MoveCommand cmd, KeyBindings keys)
    {
        string Arrow(char c) => c switch
        {
            '1' => "↙", '2' => "↓", '3' => "↘", '4' => "←",
            '6' => "→", '7' => "↖", '8' => "↑", '9' => "↗", _ => "",
        };
        string dirs = cmd.Motion.Length > 0 ? string.Join(" ", cmd.Motion.Select(Arrow))
            : cmd.Held is char h && h != '5' ? Arrow(h) : "";
        string buttons = string.Join("+", cmd.Buttons.Select(b => SelectScene.KeyName(b switch
        {
            Input.Buttons.L => keys.L,
            Input.Buttons.M => keys.M,
            _ => keys.H,
        })));
        string text = dirs.Length > 0 ? $"{dirs} + {buttons}" : buttons;
        return cmd.Air ? $"ПРЫЖОК: {text}" : text;
    }

    /// <summary>Урон приёма: удар, снаряды ("3X35"), лечение ("+150") или "-".</summary>
    public static string DamageText(MoveDef m)
    {
        if (m.Hitboxes.Count > 0 || m.Type == MoveType.Throw) return m.Hit.Damage.ToString();
        var shots = m.Effects.Where(e => e.Type == "projectile")
            .Select(e => e.GetParams<ProjectileParams>().Hit.Damage).ToList();
        if (shots.Count == 1) return shots[0].ToString();
        if (shots.Count > 1) return shots.Distinct().Count() == 1 ? $"{shots.Count}X{shots[0]}" : shots.Sum().ToString();
        var heal = m.Effects.FirstOrDefault(e => e.Type == "heal");
        if (heal != null && heal.Params.TryGetValue("amount", out var amount)) return $"+{amount}";
        return "-";
    }

    private static string Fit(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + ".";

    private static IEnumerable<string> Wrap(string text, int width)
    {
        var line = "";
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length > 0 && line.Length + 1 + word.Length > width)
            {
                yield return line;
                line = "";
            }
            line = line.Length == 0 ? word : line + " " + word;
        }
        if (line.Length > 0) yield return line;
    }
}
