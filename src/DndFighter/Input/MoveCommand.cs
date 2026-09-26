namespace DndFighter.Input;

/// <summary>
/// Разобранная команда приёма в нумпад-нотации.
///   "5L"   — стоя + L          "2M"  — присед + M        "6H" — вперёд + H
///   "j.H"  — в прыжке + H      "236L" — четверть круга вперёд + L
///   "623H" — «драгон-панч» + H "LM"  — L и M вместе (бросок)
/// </summary>
public sealed class MoveCommand
{
    public bool Air { get; private init; }
    /// <summary>Движение стиком (2+ направления), например "236". Пусто для обычных ударов.</summary>
    public string Motion { get; private init; } = "";
    /// <summary>Направление, которое надо удерживать в момент нажатия ('5', '2', '6'…), или null — любое.</summary>
    public char? Held { get; private init; }
    public Buttons[] Buttons { get; private init; } = Array.Empty<Buttons>();

    public bool IsThrow => Motion.Length == 0 && Held == null && Buttons.Length >= 2;
    public bool IsSpecificDirection => Held is char h && h != '5' && h != '2';
    public bool IsCrouching => !Air && Held is '1' or '2' or '3';

    public static MoveCommand Parse(string input)
    {
        var s = input.Trim();
        bool air = false;
        if (s.StartsWith("j.", StringComparison.OrdinalIgnoreCase)) { air = true; s = s[2..]; }

        int i = 0;
        while (i < s.Length && char.IsDigit(s[i])) i++;
        var digits = s[..i];
        var buttons = s[i..].Select(InputFrame.ParseButton).ToArray();
        if (buttons.Length == 0) throw new FormatException($"в команде '{input}' нет кнопки");
        if (digits.Contains('0')) throw new FormatException($"в команде '{input}' недопустимое направление 0");

        return new MoveCommand
        {
            Air = air,
            Motion = digits.Length > 1 ? digits : "",
            Held = digits.Length == 1 ? digits[0] : null,
            Buttons = buttons,
        };
    }

    /// <summary>Подходит ли текущее направление под требуемое удержание.</summary>
    public bool HeldMatches(int numpad)
    {
        if (Held is not char h) return true;
        return h switch
        {
            '5' => numpad is 4 or 5 or 6,
            '2' => numpad is 1 or 2 or 3,
            _ => numpad == h - '0',
        };
    }
}
