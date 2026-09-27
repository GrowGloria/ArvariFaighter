using DndFighter.Combat.Behaviors;
using DndFighter.Combat.Effects;
using DndFighter.Data;
using DndFighter.Input;
using Microsoft.Xna.Framework;

namespace DndFighter.Combat;

public enum Stance { Stand, Crouch, Air }

public enum FighterState
{
    Idle, Walk, Crouch, PreJump, Air, Landing,
    Attack,
    Hitstun, Blockstun, AirHit, Knockdown, GetUp,
    KO, Victory,
}

/// <summary>
/// Боец на арене. Универсален: всё, что отличает персонажей, берётся из CharacterDef.
/// Координаты мира: X — вправо, Y — вниз, пол на Y = 0 (в воздухе Y &lt; 0).
/// </summary>
public sealed class Fighter
{
    public const int KnockdownFrames = 40;
    public const int GetUpFrames = 18;

    public CharacterDef Def { get; }
    public int Index { get; }
    public Fighter Opponent { get; set; } = null!;
    public IInputSource Source { get; set; }
    public InputBuffer Input { get; } = new();
    public CharacterBehavior? Behavior { get; }

    public Vector2 Pos;
    public Vector2 Vel;
    /// <summary>Отталкивание по земле после удара/блока, затухает.</summary>
    public float Knock;
    public int Facing = 1;

    public FighterState State { get; private set; }
    public int StateTime { get; private set; }
    public int StunLeft;
    private int _jumpDir;
    private bool _stunCrouching;

    public MoveDef? Move { get; private set; }
    public int MoveFrame { get; private set; }
    /// <summary>Приём попал или был заблокирован — открывает отмены.</summary>
    public bool MoveContact;
    public readonly HashSet<int> HitGroups = new();
    private MoveDef? _queuedCancel;
    private MoveDef? _unaffordable;
    private long _lastNoResourcePopup = -1000;

    public float Health;
    public float DisplayedHealth; // для «красного следа» урона на HUD
    public readonly Dictionary<string, float> Resources = new();

    public int Hitstop;
    public int Invulnerable;
    public int ComboHits;
    public int ComboDamage;
    public bool IsKo;
    /// <summary>Счётчик анимации, не идёт во время хитстопа.</summary>
    public int AnimClock;

    public Fighter(CharacterDef def, int index, IInputSource source)
    {
        Def = def;
        Index = index;
        Source = source;
        Behavior = BehaviorRegistry.Create(def.Behavior, this);
    }

    public bool Grounded => Pos.Y >= 0 && Vel.Y >= 0;
    public bool IsAttacking => State == FighterState.Attack && Move != null;

    public Stance Stance =>
        !Grounded || State is FighterState.Air or FighterState.AirHit ? Stance.Air
        : State is FighterState.Crouch or FighterState.Landing ? Stance.Crouch
        : State is FighterState.Blockstun or FighterState.Hitstun && _stunCrouching ? Stance.Crouch
        : State == FighterState.Attack && Move!.Command.IsCrouching ? Stance.Crouch
        : Stance.Stand;

    /// <summary>Может ли боец сейчас получать урон.</summary>
    public bool IsHittable => Invulnerable <= 0 && State is not (FighterState.Knockdown or FighterState.GetUp or FighterState.KO);

    // ───────────────────────────── раунд ─────────────────────────────

    public void ResetForRound(float x, int facing)
    {
        Pos = new Vector2(x, 0);
        Vel = Vector2.Zero;
        Knock = 0;
        Facing = facing;
        Health = DisplayedHealth = Def.Stats.Health;
        Resources.Clear();
        foreach (var r in Def.Resources) Resources[r.Id] = r.Start;
        Hitstop = Invulnerable = ComboHits = ComboDamage = StunLeft = 0;
        IsKo = false;
        Move = null;
        Input.Clear();
        SetState(FighterState.Idle);
    }

    public void SetState(FighterState s)
    {
        State = s;
        StateTime = 0;
        _queuedCancel = null;
        if (s != FighterState.Attack) Move = null;
    }

    // ───────────────────────────── ресурсы ─────────────────────────────

    public float GetResource(string id) => Resources.TryGetValue(id, out var v) ? v : 0;

    public void AddResource(string id, float amount)
    {
        var def = Def.Resources.FirstOrDefault(r => r.Id == id);
        if (def == null) return;
        Resources[id] = Math.Clamp(GetResource(id) + amount, 0, def.Max);
    }

    public bool CanAfford(MoveDef m) => m.Cost.All(c => GetResource(c.Key) >= c.Value - 0.0001f);

    // ───────────────────────────── тик ─────────────────────────────

    public void Tick(FightWorld world)
    {
        if (Hitstop > 0)
        {
            Hitstop--;
            // Отмену можно ввести во время заморозки — она сработает сразу после неё.
            if (IsAttacking && MoveContact && _queuedCancel == null)
            {
                _queuedCancel = FindMove(world, CanCancelInto);
                if (_queuedCancel != null) Input.Consume();
                else ReportUnaffordable(world);
            }
            return;
        }

        StateTime++;
        AnimClock++;
        if (Invulnerable > 0) Invulnerable--;

        var input = Input.Current;
        int dir = input.Numpad(Facing);

        switch (State)
        {
            case FighterState.Idle:
            case FighterState.Walk:
            case FighterState.Crouch:
                if (TryStartMove(world, null)) break;
                NeutralMovement(dir);
                break;

            case FighterState.PreJump:
                if (StateTime >= Def.Stats.PreJumpFrames)
                {
                    float vx = _jumpDir == 9 ? Def.Stats.JumpSpeedX : _jumpDir == 7 ? -Def.Stats.JumpSpeedX : 0;
                    Vel = new Vector2(vx * Facing, -Def.Stats.JumpVelocity);
                    Pos.Y = -0.01f;
                    SetState(FighterState.Air);
                }
                break;

            case FighterState.Air:
                TryStartMove(world, null);
                break;

            case FighterState.Landing:
                if (StateTime >= Def.Stats.LandingFrames) SetState(FighterState.Idle);
                break;

            case FighterState.Attack:
                if (_queuedCancel is { } queued)
                {
                    _queuedCancel = null;
                    if (CanAfford(queued)) { StartMove(queued, world); break; }
                }
                if (MoveContact && MoveFrame > Move!.Startup && TryStartMove(world, CanCancelInto)) break;
                AdvanceMove(world);
                break;

            case FighterState.Hitstun:
            case FighterState.Blockstun:
                if (--StunLeft <= 0)
                {
                    ComboHits = 0;
                    SetState(dir is 1 or 2 or 3 ? FighterState.Crouch : FighterState.Idle);
                }
                break;

            case FighterState.Knockdown:
                if (StateTime >= KnockdownFrames) SetState(FighterState.GetUp);
                break;

            case FighterState.GetUp:
                if (StateTime >= GetUpFrames) { SetState(FighterState.Idle); Invulnerable = 2; }
                break;
        }

        Physics(world);
    }

    private void NeutralMovement(int dir)
    {
        if (dir is 7 or 8 or 9)
        {
            _jumpDir = dir;
            Vel.X = 0;
            SetState(FighterState.PreJump);
        }
        else if (dir is 1 or 2 or 3)
        {
            Vel.X = 0;
            if (State != FighterState.Crouch) SetState(FighterState.Crouch);
        }
        else if (dir == 6)
        {
            Vel.X = Def.Stats.WalkSpeed * Facing;
            if (State != FighterState.Walk) SetState(FighterState.Walk);
        }
        else if (dir == 4)
        {
            Vel.X = -Def.Stats.BackWalkSpeed * Facing;
            if (State != FighterState.Walk) SetState(FighterState.Walk);
        }
        else
        {
            Vel.X = 0;
            if (State != FighterState.Idle) SetState(FighterState.Idle);
        }
    }

    private void Physics(FightWorld world)
    {
        bool airborne = Pos.Y < 0 || Vel.Y < 0;
        if (airborne)
        {
            Vel.Y += Def.Stats.Gravity;
            Pos += Vel;
            if (Pos.Y >= 0) Land(world);
        }
        else
        {
            if (State == FighterState.Attack) Vel.X *= 0.85f;
            if (Math.Abs(Vel.X) < 0.05f) Vel.X = 0;
            Pos.X += Vel.X + Knock;
        }

        Knock *= 0.82f;
        if (Math.Abs(Knock) < 0.1f) Knock = 0;
    }

    private void Land(FightWorld world)
    {
        Pos.Y = 0;
        Vel.Y = 0;
        switch (State)
        {
            case FighterState.AirHit:
                Vel.X = 0;
                if (IsKo) { SetState(FighterState.KO); break; }
                SetState(FighterState.Knockdown);
                ComboHits = 0;
                Behavior?.OnKnockdown(world);
                break;
            case FighterState.Attack when Move!.Command.Air:
            case FighterState.Air:
                Vel.X = 0;
                SetState(FighterState.Landing);
                break;
            // Наземный приём с подпрыгиванием (апперкот) просто продолжается.
        }
    }

    // ───────────────────────────── приёмы ─────────────────────────────

    private bool CanCancelInto(MoveDef next)
    {
        var cancels = Move!.Cancels;
        if (cancels.Count == 0 || next == Move) return false;
        return cancels.Contains(next.Id) || next.Type switch
        {
            MoveType.Special => cancels.Contains("special"),
            MoveType.Super => cancels.Contains("super") || cancels.Contains("special"),
            MoveType.Normal => cancels.Contains("normal"),
            _ => false,
        };
    }

    private bool TryStartMove(FightWorld world, Func<MoveDef, bool>? filter)
    {
        var m = FindMove(world, filter);
        ReportUnaffordable(world);
        if (m == null) return false;
        StartMove(m, world);
        return true;
    }

    /// <summary>Команду ввели, но не хватило ресурса — показываем подсказку (не чаще раза в полсекунды).</summary>
    private void ReportUnaffordable(FightWorld world)
    {
        if (_unaffordable is not { } m) return;
        _unaffordable = null;
        if (world.Tick - _lastNoResourcePopup < 30) return;
        _lastNoResourcePopup = world.Tick;
        var missing = m.Cost.FirstOrDefault(c => GetResource(c.Key) < c.Value - 0.0001f).Key;
        var name = Def.Resources.FirstOrDefault(r => r.Id == missing)?.Name ?? missing;
        world.AddPopup($"НЕ ХВАТАЕТ: {name}", Pos + new Vector2(0, -64), new Color(255, 120, 120));
    }

    /// <summary>Первый по приоритету приём, чья команда введена и который сейчас доступен.</summary>
    private MoveDef? FindMove(FightWorld world, Func<MoveDef, bool>? filter)
    {
        bool inAir = !Grounded;
        foreach (var m in Def.MovesByPriority)
        {
            if (m.Command.Air != inAir) continue;
            if (filter != null && !filter(m)) continue;
            if (!Input.Matches(m.Command, Facing)) continue;
            if (!CanAfford(m)) { _unaffordable ??= m; continue; }
            if (Behavior != null && !Behavior.CanUseMove(m)) continue;
            if (!m.Effects.All(e => EffectRegistry.Get(e.Type).CanStart(this, e, world))) continue;
            return m;
        }
        return null;
    }

    public void StartMove(MoveDef m, FightWorld world)
    {
        foreach (var (id, cost) in m.Cost) AddResource(id, -cost);
        Input.Consume();
        SetState(FighterState.Attack);
        Move = m;
        MoveFrame = 0;
        MoveContact = false;
        HitGroups.Clear();
        AnimClock = 0;
        if (Grounded) Vel.X = 0;
        Behavior?.OnMoveStart(m, world);
        world.RaiseMoveStarted(this, m);
        AdvanceMove(world);
    }

    private void AdvanceMove(FightWorld world)
    {
        var m = Move!;
        MoveFrame++;

        foreach (var e in m.Effects)
            if (e.Frame == MoveFrame)
                EffectRegistry.Get(e.Type).Execute(this, e, world);

        if (m.Type == MoveType.Throw && MoveFrame == m.Startup + 1)
            world.TryThrow(this, m);

        if (State == FighterState.Attack && MoveFrame >= m.TotalFrames)
        {
            if (!Grounded) SetState(FighterState.Air);
            else SetState(m.Command.IsCrouching ? FighterState.Crouch : FighterState.Idle);
        }
    }

    /// <summary>Хитбоксы приёма, активные в текущем кадре.</summary>
    public IEnumerable<(BoxDef box, int index)> ActiveHitboxes()
    {
        if (!IsAttacking || Move!.Type == MoveType.Throw) yield break;
        var m = Move;
        for (int i = 0; i < m.Hitboxes.Count; i++)
        {
            var b = m.Hitboxes[i];
            int start = b.Start > 0 ? b.Start : m.Startup + 1;
            int end = b.End > 0 ? b.End : m.Startup + m.Active;
            if (MoveFrame >= start && MoveFrame <= end && !HitGroups.Contains(b.Group))
                yield return (b, i);
        }
    }

    public IEnumerable<RectangleF> Hurtboxes()
    {
        if (State is FighterState.Knockdown or FighterState.GetUp or FighterState.KO) yield break;
        foreach (var b in Def.GetHurtboxes(Stance)) yield return ToWorld(b);
        if (!IsAttacking) yield break;
        foreach (var b in Move!.Hurtboxes)
        {
            int start = b.Start > 0 ? b.Start : Move.Startup + 1;
            int end = b.End > 0 ? b.End : Move.TotalFrames;
            if (MoveFrame >= start && MoveFrame <= end) yield return ToWorld(b);
        }
    }

    public RectangleF Pushbox => ToWorld(Def.Pushbox);

    public RectangleF ToWorld(BoxDef b) =>
        new(Pos.X + (Facing > 0 ? b.X : -b.X - b.W), Pos.Y + b.Y, b.W, b.H);

    // ───────────────────────────── получение ударов ─────────────────────────────

    /// <summary>Держит ли боец блок против удара, пришедшего с позиции sourceX.</summary>
    public bool TryBlock(HitHeight height, float sourceX)
    {
        if (!Grounded) return false;
        if (State is not (FighterState.Idle or FighterState.Walk or FighterState.Crouch or FighterState.Blockstun))
            return false;
        var input = Input.Current;
        int away = Math.Sign(Pos.X - sourceX);
        if (away == 0) away = -Facing;
        bool holdingAway = away > 0 ? input.Right && !input.Left : input.Left && !input.Right;
        if (!holdingAway) return false;
        bool crouching = input.Down;
        return height switch
        {
            HitHeight.Low => crouching,
            HitHeight.Overhead => !crouching,
            _ => true,
        };
    }

    public void EnterBlockstun(int frames)
    {
        _stunCrouching = Input.Current.Down;
        SetState(FighterState.Blockstun);
        StunLeft = frames;
        Vel.X = 0;
    }

    public void EnterHitstun(int frames)
    {
        _stunCrouching = Stance == Stance.Crouch;
        SetState(FighterState.Hitstun);
        StunLeft = frames;
        Vel.X = 0;
    }

    public void Launch(Vector2 velocity)
    {
        SetState(FighterState.AirHit);
        Vel = velocity;
        if (Pos.Y >= 0) Pos.Y = -0.01f;
    }

    public void Victory()
    {
        if (State is FighterState.KO) return;
        Move = null;
        Vel = Vector2.Zero;
        SetState(FighterState.Victory);
    }
}

/// <summary>Прямоугольник с float-координатами для хитбоксов.</summary>
public readonly record struct RectangleF(float X, float Y, float W, float H)
{
    public float Right => X + W;
    public float Bottom => Y + H;
    public Vector2 Center => new(X + W / 2, Y + H / 2);

    public bool Intersects(RectangleF o) => X < o.Right && o.X < Right && Y < o.Bottom && o.Y < Bottom;

    public RectangleF Intersection(RectangleF o)
    {
        float x = Math.Max(X, o.X), y = Math.Max(Y, o.Y);
        return new RectangleF(x, y, Math.Min(Right, o.Right) - x, Math.Min(Bottom, o.Bottom) - y);
    }
}
