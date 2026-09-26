namespace DndFighter.Engine;

/// <summary>Экран игры: меню, выбор персонажа, бой. Update вызывается 60 раз в секунду.</summary>
public abstract class Scene
{
    protected FighterGame Game { get; }

    protected Scene(FighterGame game) => Game = game;

    public virtual void Enter() { }
    public abstract void Update();
    /// <summary>Рисование в виртуальный экран 480x270.</summary>
    public abstract void Draw(Draw d);
}
