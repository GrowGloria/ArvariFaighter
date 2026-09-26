using System.Text.Json.Serialization;
using DndFighter.Combat;
using Microsoft.Xna.Framework.Graphics;

namespace DndFighter.Data;

/// <summary>
/// Описание персонажа. Загружается из Content/characters/&lt;id&gt;/character.json.
/// Движок ничего не знает о конкретных персонажах — всё поведение берётся отсюда.
/// </summary>
public sealed class CharacterDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Title { get; set; } = "";

    public StatsDef Stats { get; set; } = new();

    /// <summary>Спрайтшиты персонажа по имени. "body" — основной.</summary>
    public Dictionary<string, SheetDef> Sheets { get; set; } = new();

    /// <summary>Анимации по имени. Стандартные: idle, walk, crouch, jump, hit, block,
    /// crouch_block, air_hit, knockdown, getup, victory. Остальные — для приёмов и снарядов.</summary>
    public Dictionary<string, AnimationDef> Animations { get; set; } = new();

    /// <summary>Хёртбоксы (уязвимые зоны) по стойке: stand, crouch, air.</summary>
    public Dictionary<string, List<BoxDef>> Hurtboxes { get; set; } = new();

    /// <summary>Пушбокс — «тело», сквозь которое нельзя пройти.</summary>
    public BoxDef Pushbox { get; set; } = new() { X = -10, Y = -48, W = 20, H = 48 };

    /// <summary>Классовые ресурсы: ячейки заклинаний, ярость, ки и т.п.</summary>
    public List<ResourceDef> Resources { get; set; } = new();

    /// <summary>Необязательный C#-скрипт с уникальной механикой (см. Combat/Behaviors).</summary>
    public string? Behavior { get; set; }

    /// <summary>Заполняется загрузчиком из moves.json и папки moves/.</summary>
    [JsonIgnore] public List<MoveDef> Moves { get; } = new();

    /// <summary>Приёмы в порядке проверки ввода (сложные команды раньше простых).</summary>
    [JsonIgnore] public List<MoveDef> MovesByPriority { get; set; } = new();

    [JsonIgnore] public string Directory { get; set; } = "";
    [JsonIgnore] public Dictionary<string, Texture2D> Textures { get; } = new();

    public AnimationDef? GetAnimation(string? name) =>
        name != null && Animations.TryGetValue(name, out var a) ? a : null;

    public List<BoxDef> GetHurtboxes(Stance stance)
    {
        var key = stance switch { Stance.Crouch => "crouch", Stance.Air => "air", _ => "stand" };
        if (Hurtboxes.TryGetValue(key, out var list)) return list;
        return Hurtboxes.TryGetValue("stand", out var stand) ? stand : new List<BoxDef>();
    }
}

public sealed class StatsDef
{
    public int Health { get; set; } = 1000;
    public float WalkSpeed { get; set; } = 1.6f;
    public float BackWalkSpeed { get; set; } = 1.2f;
    public float JumpVelocity { get; set; } = 6.5f;
    public float JumpSpeedX { get; set; } = 2.0f;
    public float Gravity { get; set; } = 0.32f;
    public int PreJumpFrames { get; set; } = 3;
    public int LandingFrames { get; set; } = 3;
}

public sealed class SheetDef
{
    public string File { get; set; } = "";
    public int FrameWidth { get; set; } = 64;
    public int FrameHeight { get; set; } = 64;
    /// <summary>Точка кадра, совпадающая с позицией персонажа (обычно — между ступнями).</summary>
    public int OriginX { get; set; } = 32;
    public int OriginY { get; set; } = 60;
}

public sealed class AnimationDef
{
    public string Sheet { get; set; } = "body";
    public int Row { get; set; }
    public int Frames { get; set; } = 1;
    /// <summary>Длительность кадра в тиках. 0 — растянуть анимацию на всю длину приёма.</summary>
    public int FrameTime { get; set; } = 6;
    public bool Loop { get; set; } = true;
    /// <summary>Для анимаций приёмов: индекс «ударного» кадра. Кадры до него растягиваются на startup,
    /// он сам показывается в active, остальные — в recovery. Так спрайт всегда совпадает с фреймдатой.</summary>
    public int HitFrame { get; set; } = -1;

    /// <summary>Кадр анимации приёма на тике tick (0-based) с учётом фаз startup/active/recovery.</summary>
    public int FrameForMove(int tick, int startup, int active, int recovery)
    {
        if (HitFrame < 0 || HitFrame >= Frames) return FrameAt(tick, startup + active + recovery);
        if (tick < startup) return HitFrame == 0 ? 0 : Math.Min(HitFrame - 1, tick * HitFrame / Math.Max(1, startup));
        if (tick < startup + active) return HitFrame;
        int after = Frames - HitFrame - 1;
        if (after <= 0) return HitFrame;
        int t = tick - startup - active;
        return Math.Min(Frames - 1, HitFrame + 1 + t * after / Math.Max(1, recovery));
    }

    public int FrameAt(int tick, int totalTicks = 0)
    {
        if (Frames <= 1) return 0;
        if (FrameTime <= 0)
        {
            if (totalTicks <= 0) return 0;
            return Math.Clamp(tick * Frames / totalTicks, 0, Frames - 1);
        }
        int f = tick / FrameTime;
        return Loop ? f % Frames : Math.Min(f, Frames - 1);
    }
}

/// <summary>Прямоугольник относительно позиции бойца. X — вперёд по взгляду, Y — вниз (ноги на 0).</summary>
public sealed class BoxDef
{
    public float X { get; set; }
    public float Y { get; set; }
    public float W { get; set; }
    public float H { get; set; }
    /// <summary>Для хитбоксов приёма: кадры активности (1-based, включительно). 0 — по умолчанию активная фаза.</summary>
    public int Start { get; set; }
    public int End { get; set; }
    /// <summary>Приём попадает не больше одного раза на группу. Разные группы = многоударный приём.</summary>
    public int Group { get; set; }
}

public sealed class ResourceDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public float Max { get; set; } = 3;
    public float Start { get; set; }
    public float RegenPerSecond { get; set; }
    public float GainOnHit { get; set; }
    public float GainOnBlocked { get; set; }
    public float GainOnDamaged { get; set; }
    /// <summary>"pips" — отдельные кружки (ячейки), "bar" — полоса (ярость).</summary>
    public string Display { get; set; } = "pips";
    public string Color { get; set; } = "#6aa0ff";
}
