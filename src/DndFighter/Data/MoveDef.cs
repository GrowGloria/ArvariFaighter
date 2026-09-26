using System.Text.Json;
using System.Text.Json.Serialization;
using DndFighter.Input;

namespace DndFighter.Data;

public enum MoveType { Normal, Special, Super, Throw }

public enum HitHeight { Mid, Low, Overhead }

/// <summary>
/// Приём персонажа: фреймдата, хитбоксы, свойства попадания, отмены, цена и эффекты.
/// </summary>
public sealed class MoveDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";

    /// <summary>Команда в нумпад-нотации: "5L", "2M", "j.H", "236L", "623H", "LM".</summary>
    public string Input { get; set; } = "";
    public MoveType Type { get; set; } = MoveType.Normal;

    /// <summary>Имя анимации из character.json. По умолчанию совпадает с Id.</summary>
    public string? Animation { get; set; }

    public int Startup { get; set; } = 5;
    public int Active { get; set; } = 3;
    public int Recovery { get; set; } = 10;

    public List<BoxDef> Hitboxes { get; set; } = new();
    public HitDef Hit { get; set; } = new();

    /// <summary>Для бросков: дальность захвата от центра бойца.</summary>
    public float ThrowRange { get; set; } = 40;

    /// <summary>В какие приёмы можно отменить этот после контакта:
    /// id приёмов или категории "normal", "special", "super".</summary>
    public List<string> Cancels { get; set; } = new();

    /// <summary>Стоимость в классовых ресурсах: { "slots": 1 }.</summary>
    public Dictionary<string, float> Cost { get; set; } = new();

    /// <summary>Эффекты из библиотеки: projectile, impulse, teleport, heal, resource, invulnerable…</summary>
    public List<EffectDef> Effects { get; set; } = new();

    [JsonIgnore] public int TotalFrames => Startup + Active + Recovery;
    [JsonIgnore] public MoveCommand Command { get; set; } = MoveCommand.Parse("5L");
    [JsonIgnore] public string AnimationName => Animation ?? Id;

    public bool IsActiveFrame(int frame) => frame > Startup && frame <= Startup + Active;
}

public sealed class HitDef
{
    public int Damage { get; set; } = 50;
    /// <summary>Урон по блоку.</summary>
    public int Chip { get; set; }
    public int Hitstun { get; set; } = 15;
    public int Blockstun { get; set; } = 10;
    /// <summary>Заморозка обоих бойцов при попадании (ощущение «удара»).</summary>
    public int Hitstop { get; set; } = 8;
    public float Pushback { get; set; } = 3f;
    public HitHeight Height { get; set; } = HitHeight.Mid;
    public bool Knockdown { get; set; }
    /// <summary>Подброс при попадании: {"x": 2, "y": -6} (x — от атакующего).</summary>
    public LaunchDef? Launch { get; set; }
}

public sealed class LaunchDef
{
    public float X { get; set; }
    public float Y { get; set; }
}

/// <summary>
/// Вызов эффекта из библиотеки. Все поля кроме type/frame — параметры конкретного эффекта.
/// </summary>
public sealed class EffectDef
{
    public string Type { get; set; } = "";
    /// <summary>Кадр приёма (1-based), на котором срабатывает эффект.</summary>
    public int Frame { get; set; } = 1;

    [JsonExtensionData] public Dictionary<string, JsonElement> Params { get; set; } = new();

    private object? _parsed;

    /// <summary>Разбирает параметры в типизированный класс (один раз, с кэшем).</summary>
    public T GetParams<T>() where T : new()
    {
        if (_parsed is T cached) return cached;
        var json = JsonSerializer.Serialize(Params, ContentLoader.JsonOptions);
        var result = JsonSerializer.Deserialize<T>(json, ContentLoader.JsonOptions) ?? new T();
        _parsed = result;
        return result;
    }
}
