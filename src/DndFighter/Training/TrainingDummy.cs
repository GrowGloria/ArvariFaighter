using DndFighter.Combat;
using DndFighter.Data;
using DndFighter.Input;

namespace DndFighter.Training;

public enum DummyMode { Stand, Crouch, Jump, BlockAll, BlockRandom }

/// <summary>Статический «ИИ» манекена в тренировке: стоит, приседает, прыгает или блокирует.</summary>
public sealed class TrainingDummy : IInputSource
{
    private readonly Random _rng = new();
    private MoveDef? _lastSeenMove;
    private int _lastSeenFrame;
    private bool _blockThisAttack = true;

    public DummyMode Mode { get; set; } = DummyMode.Stand;
    public Fighter Self { get; set; } = null!;

    public static string ModeName(DummyMode m) => m switch
    {
        DummyMode.Stand => "СТОИТ",
        DummyMode.Crouch => "СИДИТ",
        DummyMode.Jump => "ПРЫГАЕТ",
        DummyMode.BlockAll => "БЛОК ВСЕГО",
        _ => "БЛОК СЛУЧАЙНО",
    };

    public InputFrame Poll()
    {
        switch (Mode)
        {
            case DummyMode.Crouch: return new InputFrame(false, false, false, true, Buttons.None);
            case DummyMode.Jump: return new InputFrame(false, false, true, false, Buttons.None);
            case DummyMode.BlockAll: return Block();
            case DummyMode.BlockRandom:
                var opp = Self.Opponent;
                // Новая атака соперника — заново решаем, блокировать ли её.
                if (opp.Move != null && (opp.Move != _lastSeenMove || opp.MoveFrame < _lastSeenFrame))
                    _blockThisAttack = _rng.Next(2) == 0;
                _lastSeenMove = opp.Move;
                _lastSeenFrame = opp.MoveFrame;
                return _blockThisAttack ? Block() : InputFrame.Empty;
            default: return InputFrame.Empty;
        }
    }

    private InputFrame Block()
    {
        var opp = Self.Opponent;
        bool awayIsLeft = opp.Pos.X > Self.Pos.X;
        // Честно «подглядываем» высоту атаки: низ — сидя, оверхед и прыжок — стоя, остальное сидя.
        var height = opp.Move?.Hit.Height ?? HitHeight.Mid;
        bool crouch = height != HitHeight.Overhead && opp.Grounded;
        return new InputFrame(awayIsLeft, !awayIsLeft, false, crouch, Buttons.None);
    }
}
