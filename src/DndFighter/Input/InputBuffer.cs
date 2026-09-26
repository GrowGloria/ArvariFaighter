namespace DndFighter.Input;

/// <summary>
/// История ввода бойца за последние тики. Распознаёт нажатия с буфером и движения стиком (236, 623…).
/// </summary>
public sealed class InputBuffer
{
    public const int Size = 64;
    /// <summary>Сколько тиков нажатие кнопки «живёт» в буфере.</summary>
    public const int ButtonWindow = 5;
    /// <summary>За сколько тиков до нажатия должно уложиться движение.</summary>
    public const int MotionWindow = 20;
    /// <summary>Допуск на неодновременное нажатие нескольких кнопок.</summary>
    public const int ComboButtonWindow = 3;

    private readonly InputFrame[] _frames = new InputFrame[Size];
    private readonly Buttons[] _pressed = new Buttons[Size];
    private long _tick = -1;
    private long _consumedTick = -1;

    public InputFrame Current => _tick < 0 ? InputFrame.Empty : _frames[_tick % Size];

    public void Push(InputFrame frame)
    {
        var prev = Current;
        _tick++;
        _frames[_tick % Size] = frame;
        _pressed[_tick % Size] = frame.Buttons & ~prev.Buttons;
    }

    public void Clear()
    {
        Array.Clear(_frames);
        Array.Clear(_pressed);
        _tick = -1;
        _consumedTick = -1;
    }

    /// <summary>Нажатия до текущего тика больше не используются (после запуска приёма).</summary>
    public void Consume() => _consumedTick = _tick;

    private InputFrame FrameAt(int ago) => ago > _tick ? InputFrame.Empty : _frames[(_tick - ago) % Size];

    private bool PressedAt(int ago, Buttons b) =>
        ago <= _tick && _tick - ago > _consumedTick && (_pressed[(_tick - ago) % Size] & b) != 0;

    /// <summary>Сколько тиков назад была нажата кнопка (в пределах окна), иначе -1.</summary>
    public int PressedAgo(Buttons b, int window = ButtonWindow)
    {
        for (int ago = 0; ago < window; ago++)
            if (PressedAt(ago, b)) return ago;
        return -1;
    }

    public bool WasPressed(Buttons b, int window = ButtonWindow) => PressedAgo(b, window) >= 0;

    /// <summary>
    /// Проверяет команду: кнопки нажаты недавно, и перед нажатием было нужное движение стиком.
    /// </summary>
    public bool Matches(MoveCommand cmd, int facing)
    {
        int window = cmd.Buttons.Length > 1 ? ComboButtonWindow : ButtonWindow;
        int latestPress = int.MaxValue;
        foreach (var b in cmd.Buttons)
        {
            int ago = PressedAgo(b, window);
            if (ago < 0) return false;
            latestPress = Math.Min(latestPress, ago);
        }

        if (cmd.Motion.Length == 0)
            return cmd.HeldMatches(FrameAt(latestPress).Numpad(facing));

        return MatchMotion(cmd.Motion, facing, latestPress);
    }

    private bool MatchMotion(string motion, int facing, int pressAgo)
    {
        // Идём назад по времени от момента нажатия и ищем направления в обратном порядке.
        int ago = pressAgo;
        int limit = pressAgo + MotionWindow;
        for (int i = motion.Length - 1; i >= 0; i--)
        {
            int want = motion[i] - '0';
            bool found = false;
            for (; ago <= limit; ago++)
            {
                if (FrameAt(ago).Numpad(facing) == want)
                {
                    found = true;
                    break;
                }
            }
            if (!found) return false;
        }
        return true;
    }
}
