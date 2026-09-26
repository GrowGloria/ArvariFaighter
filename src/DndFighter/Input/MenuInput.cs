using Microsoft.Xna.Framework.Input;

namespace DndFighter.Input;

public enum MenuAction { Up, Down, Left, Right, Confirm, Back }

/// <summary>Навигация по меню для обоих игроков (по их раскладкам) + Enter/Escape.</summary>
public sealed class MenuInput
{
    private readonly ControlsConfig _controls;
    private KeyboardState _prev, _cur;

    public MenuInput(ControlsConfig controls) => _controls = controls;

    public void Update()
    {
        _prev = _cur;
        _cur = Keyboard.GetState();
    }

    public bool KeyPressed(Keys k) => _cur.IsKeyDown(k) && !_prev.IsKeyDown(k);

    /// <summary>player: 0 или 1. Enter/Escape считаются за первого игрока.</summary>
    public bool Pressed(int player, MenuAction action)
    {
        var b = _controls.For(player);
        var key = action switch
        {
            MenuAction.Up => b.Up,
            MenuAction.Down => b.Down,
            MenuAction.Left => b.Left,
            MenuAction.Right => b.Right,
            MenuAction.Confirm => b.L,
            _ => b.M,
        };
        if (KeyPressed(key)) return true;
        if (player != 0) return false;
        return action switch
        {
            MenuAction.Confirm => KeyPressed(Keys.Enter),
            MenuAction.Back => KeyPressed(Keys.Escape),
            _ => false,
        };
    }

    public bool AnyPressed(MenuAction action) => Pressed(0, action) || Pressed(1, action);
}
