using DndFighter.Data;
using Microsoft.Xna.Framework;

namespace DndFighter.Combat;

public enum SparkKind { Hit, Block, Heavy, Throw, Magic }

public sealed class Spark
{
    public Vector2 Pos;
    public SparkKind Kind;
    public int Age;
    public int Life = 14;
    public int Seed;
}

public sealed class Popup
{
    public string Text = "";
    public Vector2 Pos;
    public Color Color = Color.White;
    public int Age;
}

/// <summary>Итог попадания — для HUD и тренировочного режима (фреймдата).</summary>
public sealed record HitReport(Fighter Attacker, Fighter Defender, string MoveName, bool Blocked, int Damage, int? Advantage);

/// <summary>
/// Симуляция боя: два бойца, снаряды, столкновения. Не знает про раунды, HUD и меню —
/// этим занимается FightScene. Один вызов Update = один тик (1/60 секунды).
/// </summary>
public sealed class FightWorld
{
    public const int ScreenWidth = 480;
    public const float EdgeMargin = 14;

    public Fighter[] Fighters { get; }
    public StageDef Stage { get; }
    public List<Projectile> Projectiles { get; } = new();
    public List<Spark> Sparks { get; } = new();
    public List<Popup> Popups { get; } = new();
    public float CameraX { get; private set; }
    public long Tick { get; private set; }
    private readonly Random _rng = new();

    public event Action<Fighter, MoveDef>? MoveStarted;
    public event Action<HitReport>? HitResolved;

    public FightWorld(StageDef stage, Fighter p1, Fighter p2)
    {
        Stage = stage;
        Fighters = new[] { p1, p2 };
        p1.Opponent = p2;
        p2.Opponent = p1;
    }

    public void ResetRound()
    {
        float mid = Stage.Width / 2f;
        Fighters[0].ResetForRound(mid - 70, 1);
        Fighters[1].ResetForRound(mid + 70, -1);
        Projectiles.Clear();
        Sparks.Clear();
        Popups.Clear();
        CameraX = mid - ScreenWidth / 2f;
        foreach (var f in Fighters) f.Behavior?.OnRoundStart(this);
    }

    public void Update(bool inputEnabled)
    {
        Tick++;
        foreach (var f in Fighters)
            f.Input.Push(inputEnabled ? f.Source.Poll() : Input.InputFrame.Empty);

        foreach (var f in Fighters) f.Tick(this);
        foreach (var p in Projectiles) p.Tick(Stage);

        SeparateFighters();
        ClampToStage();
        UpdateFacing();
        ResolveHits();
        Projectiles.RemoveAll(p => p.Dead);

        foreach (var f in Fighters)
        {
            foreach (var r in f.Def.Resources)
                if (r.RegenPerSecond != 0 && f.State != FighterState.KO)
                    f.AddResource(r.Id, r.RegenPerSecond / 60f);
            f.DisplayedHealth = f.DisplayedHealth > f.Health && f.Hitstop == 0 && f.ComboHits == 0
                ? Math.Max(f.Health, f.DisplayedHealth - f.Def.Stats.Health / 120f)
                : Math.Max(f.DisplayedHealth, f.Health);
            f.Behavior?.OnTick(this);
        }

        foreach (var s in Sparks) s.Age++;
        Sparks.RemoveAll(s => s.Age > s.Life);
        foreach (var p in Popups) p.Age++;
        Popups.RemoveAll(p => p.Age > 50);

        UpdateCamera();
    }

    internal void RaiseMoveStarted(Fighter f, MoveDef m) => MoveStarted?.Invoke(f, m);

    // ───────────────────────────── движение ─────────────────────────────

    private void SeparateFighters()
    {
        var (a, b) = (Fighters[0], Fighters[1]);
        if (a.State == FighterState.KO || b.State == FighterState.KO) return;
        var ra = a.Pushbox;
        var rb = b.Pushbox;
        if (!ra.Intersects(rb)) return;

        float overlap = Math.Min(ra.Right, rb.Right) - Math.Max(ra.X, rb.X);
        int dir = Math.Sign(b.Pos.X - a.Pos.X);
        if (dir == 0) dir = a.Facing;
        a.Pos.X -= dir * overlap / 2;
        b.Pos.X += dir * overlap / 2;

        // У стены весь сдвиг достаётся тому, кто не упирается.
        float min = Math.Max(EdgeMargin, CameraX + EdgeMargin);
        float max = Math.Min(Stage.Width - EdgeMargin, CameraX + ScreenWidth - EdgeMargin);
        foreach (var (f, other) in new[] { (a, b), (b, a) })
        {
            if (f.Pos.X < min) { other.Pos.X += min - f.Pos.X; f.Pos.X = min; }
            if (f.Pos.X > max) { other.Pos.X -= f.Pos.X - max; f.Pos.X = max; }
        }
    }

    private void ClampToStage()
    {
        float min = Math.Max(EdgeMargin, CameraX + EdgeMargin);
        float max = Math.Min(Stage.Width - EdgeMargin, CameraX + ScreenWidth - EdgeMargin);
        foreach (var f in Fighters) f.Pos.X = Math.Clamp(f.Pos.X, min, max);
    }

    public bool AtWall(Fighter f)
    {
        float min = Math.Max(EdgeMargin, CameraX + EdgeMargin);
        float max = Math.Min(Stage.Width - EdgeMargin, CameraX + ScreenWidth - EdgeMargin);
        return f.Pos.X <= min + 2 || f.Pos.X >= max - 2;
    }

    private void UpdateFacing()
    {
        foreach (var f in Fighters)
        {
            if (!f.Grounded) continue;
            if (f.State is not (FighterState.Idle or FighterState.Walk or FighterState.Crouch
                or FighterState.Landing or FighterState.PreJump or FighterState.GetUp)) continue;
            float dx = f.Opponent.Pos.X - f.Pos.X;
            if (Math.Abs(dx) > 1) f.Facing = Math.Sign(dx);
        }
    }

    private void UpdateCamera()
    {
        float mid = (Fighters[0].Pos.X + Fighters[1].Pos.X) / 2;
        float target = Math.Clamp(mid - ScreenWidth / 2f, 0, Math.Max(0, Stage.Width - ScreenWidth));
        CameraX += (target - CameraX) * 0.25f;
    }

    // ───────────────────────────── попадания ─────────────────────────────

    private void ResolveHits()
    {
        var pending = new List<Action>();

        foreach (var att in Fighters)
        {
            var def = att.Opponent;
            if (!def.IsHittable) continue;
            foreach (var (box, _) in att.ActiveHitboxes())
            {
                var hit = att.ToWorld(box);
                var hurt = def.Hurtboxes().FirstOrDefault(h => h.Intersects(hit));
                if (hurt.W <= 0) continue;
                var group = box.Group;
                var move = att.Move!;
                var contact = hit.Intersection(hurt).Center;
                pending.Add(() =>
                {
                    att.HitGroups.Add(group);
                    ApplyHit(att, def, move.Hit, move.Name, att.Pos.X, contact, att);
                });
                break;
            }
        }

        // Столкновение снарядов разных бойцов.
        for (int i = 0; i < Projectiles.Count; i++)
            for (int j = i + 1; j < Projectiles.Count; j++)
            {
                var (p, q) = (Projectiles[i], Projectiles[j]);
                if (p.Dead || q.Dead || p.Owner == q.Owner || !p.Box.Intersects(q.Box)) continue;
                p.HitsLeft--; q.HitsLeft--;
                if (p.HitsLeft <= 0) p.Dead = true;
                if (q.HitsLeft <= 0) q.Dead = true;
                AddSpark(p.Box.Intersection(q.Box).Center, SparkKind.Magic);
            }

        foreach (var p in Projectiles)
        {
            if (p.Dead || p.Hitstop > 0 || p.Cooldown > 0) continue;
            var def = p.Owner.Opponent;
            if (!def.IsHittable) continue;
            var box = p.Box;
            var hurt = def.Hurtboxes().FirstOrDefault(h => h.Intersects(box));
            if (hurt.W <= 0) continue;
            pending.Add(() =>
            {
                ApplyHit(p.Owner, def, p.Params.Hit, p.Params.Id, p.Pos.X, box.Intersection(hurt).Center, null);
                p.HitsLeft--;
                p.Hitstop = p.Params.Hit.Hitstop;
                p.Cooldown = p.Params.HitInterval;
                if (p.HitsLeft <= 0) p.Dead = true;
            });
        }

        // Все попадания применяются одновременно — взаимные удары (trade) возможны.
        foreach (var a in pending) a();
    }

    /// <param name="melee">Атакующий, если удар ближний (для хитстопа и отталкивания у стены).</param>
    public void ApplyHit(Fighter att, Fighter def, HitDef hit, string moveName, float sourceX, Vector2 contact, Fighter? melee)
    {
        if (!def.IsHittable) return;
        int away = Math.Sign(def.Pos.X - sourceX);
        if (away == 0) away = att.Facing;

        bool blocked = def.TryBlock(hit.Height, sourceX);
        int damage;
        int? advantage = null;
        int remaining = melee?.Move is { } mv ? mv.TotalFrames - melee.MoveFrame : 0;

        if (blocked)
        {
            damage = hit.Chip;
            def.Health = Math.Max(1, def.Health - damage); // блок не убивает
            def.EnterBlockstun(hit.Blockstun);
            def.Knock = away * hit.Pushback;
            advantage = hit.Blockstun - remaining;
            foreach (var r in att.Def.Resources) att.AddResource(r.Id, r.GainOnBlocked);
            AddSpark(contact, SparkKind.Block);
        }
        else
        {
            bool inCombo = def.State is FighterState.Hitstun or FighterState.AirHit;
            def.ComboHits = inCombo ? def.ComboHits + 1 : 1;
            if (!inCombo) def.ComboDamage = 0;
            float scale = def.ComboHits <= 2 ? 1f : Math.Max(0.3f, 1f - 0.1f * (def.ComboHits - 2));
            damage = Math.Max(1, (int)MathF.Round(hit.Damage * scale));
            def.Health -= damage;
            def.ComboDamage += damage;

            if (def.Health <= 0)
            {
                def.Health = 0;
                def.IsKo = true;
                def.Launch(new Vector2(away * 2.5f, -5f));
            }
            else if (hit.Launch != null)
                def.Launch(new Vector2(away * hit.Launch.X, hit.Launch.Y));
            else if (!def.Grounded)
                def.Launch(new Vector2(away * 1.5f, -3.5f));
            else if (hit.Knockdown)
                def.Launch(new Vector2(away * 1.5f, -2.5f));
            else
            {
                def.EnterHitstun(hit.Hitstun);
                def.Knock = away * hit.Pushback;
                advantage = hit.Hitstun - remaining;
            }

            foreach (var r in att.Def.Resources) att.AddResource(r.Id, r.GainOnHit);
            foreach (var r in def.Def.Resources) def.AddResource(r.Id, r.GainOnDamaged);
            AddSpark(contact, hit.Damage >= 80 ? SparkKind.Heavy : SparkKind.Hit);
        }

        def.Hitstop = hit.Hitstop;
        if (melee != null)
        {
            melee.Hitstop = hit.Hitstop;
            melee.MoveContact = true;
            if (AtWall(def) && def.Grounded) melee.Knock = -away * hit.Pushback;
        }

        att.Behavior?.OnHitLanded(def, hit, blocked, this);
        def.Behavior?.OnDamaged(att, hit, blocked, this);
        HitResolved?.Invoke(new HitReport(att, def, moveName, blocked, damage, advantage));
    }

    /// <summary>Попытка броска на первом активном кадре приёма-броска.</summary>
    public void TryThrow(Fighter att, MoveDef move)
    {
        var def = att.Opponent;
        if (!def.Grounded || !def.IsHittable) return;
        if (def.State is FighterState.Hitstun or FighterState.Blockstun or FighterState.AirHit) return;
        if (Math.Abs(def.Pos.X - att.Pos.X) > move.ThrowRange) return;
        int away = Math.Sign(def.Pos.X - att.Pos.X);
        if (away == 0) away = att.Facing;

        // Тех: защищающийся тоже нажал бросок почти одновременно.
        bool defThrowing = def.IsAttacking && def.Move!.Type == MoveType.Throw && def.MoveFrame <= def.Move.Startup;
        bool defPressed = def.Input.WasPressed(Input.Buttons.L, 8) && def.Input.WasPressed(Input.Buttons.M, 8);
        if (defThrowing || defPressed)
        {
            att.EnterBlockstun(14);
            def.EnterBlockstun(14);
            att.Knock = -away * 4;
            def.Knock = away * 4;
            AddPopup("ТЕХ!", (att.Pos + def.Pos) / 2 + new Vector2(0, -60), Color.LightSkyBlue);
            AddSpark((att.Pos + def.Pos) / 2 + new Vector2(0, -30), SparkKind.Block);
            return;
        }

        att.MoveContact = true;
        var hit = move.Hit;
        def.ComboHits = 1;
        def.ComboDamage = hit.Damage;
        def.Health -= hit.Damage;
        if (def.Health <= 0) { def.Health = 0; def.IsKo = true; }
        var launch = hit.Launch ?? new LaunchDef { X = 3, Y = -4 };
        def.Launch(new Vector2(away * launch.X, launch.Y));
        def.Hitstop = att.Hitstop = hit.Hitstop;
        AddSpark(def.Pos + new Vector2(0, -30), SparkKind.Throw);
        att.Behavior?.OnHitLanded(def, hit, false, this);
        def.Behavior?.OnDamaged(att, hit, false, this);
        HitResolved?.Invoke(new HitReport(att, def, move.Name, false, hit.Damage, null));
    }

    // ───────────────────────────── визуальные эффекты ─────────────────────────────

    public void AddSpark(Vector2 pos, SparkKind kind) =>
        Sparks.Add(new Spark { Pos = pos, Kind = kind, Seed = _rng.Next(), Life = kind == SparkKind.Heavy ? 18 : 14 });

    public void AddPopup(string text, Vector2 pos, Color color) =>
        Popups.Add(new Popup { Text = text, Pos = pos, Color = color });

    public int CountProjectiles(Fighter owner, string id) =>
        Projectiles.Count(p => p.Owner == owner && p.Params.Id == id && !p.Dead);
}
