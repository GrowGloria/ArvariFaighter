using System.Reflection;
using DndFighter.Data;

namespace DndFighter.Combat.Effects;

/// <summary>Помечает класс эффекта; строка — значение поля "type" в JSON приёма.</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class EffectAttribute : Attribute
{
    public string Type { get; }
    public EffectAttribute(string type) => Type = type;
}

public interface IMoveEffect
{
    /// <summary>Можно ли вообще начать приём (например, лимит снарядов на экране).</summary>
    bool CanStart(Fighter self, EffectDef def, FightWorld world);
    /// <summary>Срабатывает на кадре def.Frame приёма.</summary>
    void Execute(Fighter self, EffectDef def, FightWorld world);
}

/// <summary>
/// Базовый класс эффекта с типизированными параметрами. Пример нового эффекта:
/// <code>
/// [Effect("slow")]
/// public sealed class SlowEffect : MoveEffect&lt;SlowEffect.Params&gt;
/// {
///     public sealed class Params { public int Frames { get; set; } = 60; }
///     protected override void Execute(Fighter self, Params p, FightWorld world) { ... }
/// }
/// </code>
/// После этого в JSON можно писать { "type": "slow", "frame": 5, "frames": 90 }.
/// </summary>
public abstract class MoveEffect<TParams> : IMoveEffect where TParams : new()
{
    public bool CanStart(Fighter self, EffectDef def, FightWorld world) => CanStart(self, def.GetParams<TParams>(), world);
    public void Execute(Fighter self, EffectDef def, FightWorld world) => Execute(self, def.GetParams<TParams>(), world);

    protected virtual bool CanStart(Fighter self, TParams p, FightWorld world) => true;
    protected abstract void Execute(Fighter self, TParams p, FightWorld world);
}

/// <summary>Находит все классы с [Effect] в сборке — регистрировать вручную ничего не нужно.</summary>
public static class EffectRegistry
{
    private static readonly Dictionary<string, IMoveEffect> Effects = Discover();

    public static bool Has(string type) => Effects.ContainsKey(type);

    public static IMoveEffect Get(string type) =>
        Effects.TryGetValue(type, out var e) ? e : throw new KeyNotFoundException($"неизвестный эффект '{type}'");

    public static IEnumerable<string> Names => Effects.Keys;

    private static Dictionary<string, IMoveEffect> Discover()
    {
        var result = new Dictionary<string, IMoveEffect>(StringComparer.OrdinalIgnoreCase);
        foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
        {
            var attr = type.GetCustomAttribute<EffectAttribute>();
            if (attr != null && typeof(IMoveEffect).IsAssignableFrom(type))
                result[attr.Type] = (IMoveEffect)Activator.CreateInstance(type)!;
        }
        return result;
    }
}
