using Microsoft.Xna.Framework;

namespace DndFighter.Combat.Effects;

// Библиотека эффектов, из которых собираются приёмы в JSON.
// Добавить свой — создать класс с атрибутом [Effect("имя")] (см. EffectRegistry.cs).

/// <summary>{ "type": "projectile", "frame": 12, "speedX": 3.5, "hit": {...}, ... } — см. ProjectileParams.</summary>
[Effect("projectile")]
public sealed class ProjectileEffect : MoveEffect<ProjectileParams>
{
    protected override bool CanStart(Fighter self, ProjectileParams p, FightWorld world) =>
        p.Limit <= 0 || world.CountProjectiles(self, p.Id) < p.Limit;

    protected override void Execute(Fighter self, ProjectileParams p, FightWorld world) =>
        world.Projectiles.Add(new Projectile(self, p));
}

/// <summary>Толчок бойца: { "type": "impulse", "frame": 3, "x": 5, "y": 0 }. X — вперёд по взгляду.</summary>
[Effect("impulse")]
public sealed class ImpulseEffect : MoveEffect<ImpulseEffect.Params>
{
    public sealed class Params
    {
        public float X { get; set; }
        public float? Y { get; set; }
        /// <summary>true — заменить скорость, false — прибавить.</summary>
        public bool Set { get; set; } = true;
    }

    protected override void Execute(Fighter self, Params p, FightWorld world)
    {
        float vx = p.X * self.Facing;
        self.Vel.X = p.Set ? vx : self.Vel.X + vx;
        if (p.Y is float y)
        {
            self.Vel.Y = p.Set ? y : self.Vel.Y + y;
            if (y < 0 && self.Pos.Y >= 0) self.Pos.Y = -0.01f;
        }
    }
}

/// <summary>Мгновенное перемещение: { "type": "teleport", "x": 90 } или { "behindOpponent": true }.</summary>
[Effect("teleport")]
public sealed class TeleportEffect : MoveEffect<TeleportEffect.Params>
{
    public sealed class Params
    {
        public float X { get; set; }
        public bool BehindOpponent { get; set; }
        public float Distance { get; set; } = 30;
    }

    protected override void Execute(Fighter self, Params p, FightWorld world)
    {
        world.AddSpark(self.Pos + new Vector2(0, -30), SparkKind.Magic);
        if (p.BehindOpponent)
        {
            var opp = self.Opponent;
            int side = Math.Sign(opp.Pos.X - self.Pos.X);
            if (side == 0) side = self.Facing;
            self.Pos.X = opp.Pos.X + side * p.Distance;
        }
        else self.Pos.X += p.X * self.Facing;
        self.Pos.X = Math.Clamp(self.Pos.X, FightWorld.EdgeMargin, world.Stage.Width - FightWorld.EdgeMargin);
        world.AddSpark(self.Pos + new Vector2(0, -30), SparkKind.Magic);
    }
}

/// <summary>Лечение: { "type": "heal", "frame": 20, "amount": 120 }.</summary>
[Effect("heal")]
public sealed class HealEffect : MoveEffect<HealEffect.Params>
{
    public sealed class Params { public float Amount { get; set; } = 100; }

    protected override void Execute(Fighter self, Params p, FightWorld world)
    {
        self.Health = Math.Min(self.Def.Stats.Health, self.Health + p.Amount);
        world.AddPopup($"+{p.Amount:0}", self.Pos + new Vector2(0, -64), Color.LightGreen);
        world.AddSpark(self.Pos + new Vector2(0, -30), SparkKind.Magic);
    }
}

/// <summary>Изменить ресурс: { "type": "resource", "id": "rage", "amount": 10 }.</summary>
[Effect("resource")]
public sealed class ResourceEffect : MoveEffect<ResourceEffect.Params>
{
    public sealed class Params
    {
        public string Id { get; set; } = "";
        public float Amount { get; set; }
    }

    protected override void Execute(Fighter self, Params p, FightWorld world) => self.AddResource(p.Id, p.Amount);
}

/// <summary>Неуязвимость: { "type": "invulnerable", "frame": 1, "frames": 8 }.</summary>
[Effect("invulnerable")]
public sealed class InvulnerableEffect : MoveEffect<InvulnerableEffect.Params>
{
    public sealed class Params { public int Frames { get; set; } = 6; }

    protected override void Execute(Fighter self, Params p, FightWorld world) =>
        self.Invulnerable = Math.Max(self.Invulnerable, p.Frames);
}

/// <summary>Всплывающая надпись: { "type": "popup", "frame": 5, "text": "КАРА!", "color": "#ffd24a" }.</summary>
[Effect("popup")]
public sealed class PopupEffect : MoveEffect<PopupEffect.Params>
{
    public sealed class Params
    {
        public string Text { get; set; } = "";
        public string Color { get; set; } = "#ffffff";
    }

    protected override void Execute(Fighter self, Params p, FightWorld world) =>
        world.AddPopup(p.Text, self.Pos + new Vector2(0, -70), Engine.ColorUtil.Parse(p.Color));
}
