using Microsoft.Xna.Framework;

namespace DndFighter.Combat.Behaviors;

/// <summary>
/// Пример уникальной механики. «Магическое восстановление» волшебника из D&amp;D:
/// раз за раунд, упав после сбивающего удара, персонаж восстанавливает одну ячейку заклинаний.
/// Подключается строкой "behavior": "arcane_recovery" в character.json.
/// </summary>
[Behavior("arcane_recovery")]
public sealed class ArcaneRecoveryBehavior : CharacterBehavior
{
    private const string Resource = "slots";
    private bool _used;

    public override void OnRoundStart(FightWorld world) => _used = false;

    public override void OnKnockdown(FightWorld world)
    {
        if (_used || Fighter.IsKo) return;
        _used = true;
        Fighter.AddResource(Resource, 1);
        world.AddPopup("ВОССТАНОВЛЕНИЕ", Fighter.Pos + new Vector2(0, -40), new Color(150, 170, 255));
    }
}
