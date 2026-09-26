using System.Reflection;
using DndFighter.Data;

namespace DndFighter.Combat.Behaviors;

/// <summary>Помечает класс поведения; строка — значение поля "behavior" в character.json.</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class BehaviorAttribute : Attribute
{
    public string Name { get; }
    public BehaviorAttribute(string name) => Name = name;
}

/// <summary>
/// Уникальная механика персонажа, которую нельзя собрать из эффектов в JSON.
/// Переопредели только нужные хуки. Экземпляр создаётся на каждого бойца в матче.
/// </summary>
public abstract class CharacterBehavior
{
    public Fighter Fighter { get; internal set; } = null!;

    public virtual void OnRoundStart(FightWorld world) { }
    public virtual void OnTick(FightWorld world) { }
    public virtual bool CanUseMove(MoveDef move) => true;
    public virtual void OnMoveStart(MoveDef move, FightWorld world) { }
    public virtual void OnHitLanded(Fighter target, HitDef hit, bool blocked, FightWorld world) { }
    public virtual void OnDamaged(Fighter attacker, HitDef hit, bool blocked, FightWorld world) { }
    public virtual void OnKnockdown(FightWorld world) { }
}

public static class BehaviorRegistry
{
    private static readonly Dictionary<string, Type> Types = Discover();

    public static bool Has(string name) => Types.ContainsKey(name);

    public static CharacterBehavior? Create(string? name, Fighter fighter)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (!Types.TryGetValue(name, out var type))
            throw new KeyNotFoundException($"неизвестное поведение '{name}'");
        var b = (CharacterBehavior)Activator.CreateInstance(type)!;
        b.Fighter = fighter;
        return b;
    }

    private static Dictionary<string, Type> Discover()
    {
        var result = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
        {
            var attr = type.GetCustomAttribute<BehaviorAttribute>();
            if (attr != null && typeof(CharacterBehavior).IsAssignableFrom(type) && !type.IsAbstract)
                result[attr.Name] = type;
        }
        return result;
    }
}
