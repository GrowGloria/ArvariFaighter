using DndFighter.Data;
using Microsoft.Xna.Framework;

namespace DndFighter.Combat;

/// <summary>Параметры снаряда — задаются прямо в эффекте "projectile" приёма.</summary>
public sealed class ProjectileParams
{
    /// <summary>Идентификатор для лимита «не больше N на экране».</summary>
    public string Id { get; set; } = "projectile";
    public string Animation { get; set; } = "projectile";
    public float OffsetX { get; set; } = 24;
    public float OffsetY { get; set; } = -32;
    public float SpeedX { get; set; } = 3;
    public float SpeedY { get; set; }
    public float Gravity { get; set; }
    public BoxDef Box { get; set; } = new() { X = -6, Y = -6, W = 12, H = 12 };
    public HitDef Hit { get; set; } = new();
    /// <summary>Сколько раз снаряд может попасть (и сколько «прочности» при столкновении снарядов).</summary>
    public int Hits { get; set; } = 1;
    /// <summary>Пауза (в тиках) между попаданиями многоударного снаряда.</summary>
    public int HitInterval { get; set; } = 8;
    public int Lifetime { get; set; } = 240;
    /// <summary>Максимум снарядов с этим Id от одного бойца. 0 — без лимита.</summary>
    public int Limit { get; set; }
}

public sealed class Projectile
{
    public Fighter Owner { get; }
    public ProjectileParams Params { get; }
    public Vector2 Pos;
    public Vector2 Vel;
    public int Facing;
    public int Age;
    public int HitsLeft;
    public int Hitstop;
    public int Cooldown;
    public bool Dead;

    public Projectile(Fighter owner, ProjectileParams p)
    {
        Owner = owner;
        Params = p;
        Facing = owner.Facing;
        Pos = owner.Pos + new Vector2(p.OffsetX * Facing, p.OffsetY);
        Vel = new Vector2(p.SpeedX * Facing, p.SpeedY);
        HitsLeft = p.Hits;
    }

    public RectangleF Box => new(
        Pos.X + (Facing > 0 ? Params.Box.X : -Params.Box.X - Params.Box.W),
        Pos.Y + Params.Box.Y, Params.Box.W, Params.Box.H);

    public void Tick(StageDef stage)
    {
        if (Hitstop > 0) { Hitstop--; return; }
        if (Cooldown > 0) Cooldown--;
        Age++;
        Vel.Y += Params.Gravity;
        Pos += Vel;
        if (Age > Params.Lifetime || Pos.X < -40 || Pos.X > stage.Width + 40 || Pos.Y > 0) Dead = true;
    }
}
