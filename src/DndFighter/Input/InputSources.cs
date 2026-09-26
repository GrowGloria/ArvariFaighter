using DndFighter.Data;
using Microsoft.Xna.Framework.Input;

namespace DndFighter.Input;

/// <summary>Откуда боец получает ввод: клавиатура, геймпад, ИИ, повтор записи…</summary>
public interface IInputSource
{
    InputFrame Poll();
}

/// <summary>Раскладка клавиш одного игрока. Настраивается в Content/controls.json.</summary>
public sealed class KeyBindings
{
    public Keys Up { get; set; } = Keys.W;
    public Keys Down { get; set; } = Keys.S;
    public Keys Left { get; set; } = Keys.A;
    public Keys Right { get; set; } = Keys.D;
    public Keys L { get; set; } = Keys.J;
    public Keys M { get; set; } = Keys.K;
    public Keys H { get; set; } = Keys.L;
}

public sealed class ControlsConfig
{
    public KeyBindings P1 { get; set; } = new();
    public KeyBindings P2 { get; set; } = new()
    {
        Up = Keys.Up, Down = Keys.Down, Left = Keys.Left, Right = Keys.Right,
        L = Keys.NumPad1, M = Keys.NumPad2, H = Keys.NumPad3,
    };

    public static ControlsConfig Load(string path)
    {
        try
        {
            if (File.Exists(path)) return ContentLoader.ReadJson<ControlsConfig>(path);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"controls.json: {e.Message} — используются клавиши по умолчанию");
        }
        return new ControlsConfig();
    }

    public KeyBindings For(int player) => player == 0 ? P1 : P2;
}

public sealed class KeyboardSource : IInputSource
{
    private readonly KeyBindings _keys;

    public KeyboardSource(KeyBindings keys) => _keys = keys;

    public InputFrame Poll()
    {
        var k = Keyboard.GetState();
        var b = Buttons.None;
        if (k.IsKeyDown(_keys.L)) b |= Buttons.L;
        if (k.IsKeyDown(_keys.M)) b |= Buttons.M;
        if (k.IsKeyDown(_keys.H)) b |= Buttons.H;
        return new InputFrame(k.IsKeyDown(_keys.Left), k.IsKeyDown(_keys.Right),
            k.IsKeyDown(_keys.Up), k.IsKeyDown(_keys.Down), b);
    }
}

/// <summary>Ничего не нажимает. Используется, пока ввод заблокирован (заставка раунда).</summary>
public sealed class NullSource : IInputSource
{
    public static readonly NullSource Instance = new();
    public InputFrame Poll() => InputFrame.Empty;
}
