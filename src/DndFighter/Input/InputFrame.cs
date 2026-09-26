namespace DndFighter.Input;

[Flags]
public enum Buttons : byte
{
    None = 0,
    L = 1,   // лёгкая атака
    M = 2,   // средняя атака
    H = 4,   // тяжёлая атака
}

/// <summary>Состояние контроллера за один тик. Направления «сырые» (лево/право), без учёта стороны.</summary>
public readonly record struct InputFrame(bool Left, bool Right, bool Up, bool Down, Buttons Buttons)
{
    public static readonly InputFrame Empty = new(false, false, false, false, Buttons.None);

    /// <summary>Направление в нумпад-нотации относительно того, куда смотрит боец (facing: +1 вправо, -1 влево).
    /// 6 — вперёд, 4 — назад, 2 — вниз, 8 — вверх, 5 — нейтраль.</summary>
    public int Numpad(int facing)
    {
        int x = (Right ? 1 : 0) - (Left ? 1 : 0);   // SOCD: лево+право = нейтраль
        int y = Up ? 1 : Down ? -1 : 0;             // вверх приоритетнее вниз
        return 5 + x * facing + 3 * y;
    }

    public static Buttons ParseButton(char c) => char.ToUpperInvariant(c) switch
    {
        'L' => Buttons.L,
        'M' => Buttons.M,
        'H' => Buttons.H,
        _ => throw new FormatException($"неизвестная кнопка '{c}' (допустимы L, M, H)"),
    };
}
