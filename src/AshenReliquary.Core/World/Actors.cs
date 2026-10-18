using AshenReliquary.Core.Abilities;
using AshenReliquary.Core.Characters;
using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Creatures;
using AshenReliquary.Core.Items;

namespace AshenReliquary.Core.World;

/// <summary>
/// Something with a position on the map. Positions are in tiles; <see cref="Direction"/> uses the sprite
/// convention 0 to 7 = W, NW, N, NE, E, SE, S, SW as seen on screen.
/// </summary>
public abstract class Actor
{
    private static int _nextId;

    public int Id { get; } = Interlocked.Increment(ref _nextId);
    public double X { get; internal set; }
    public double Y { get; internal set; }
    public int Direction { get; internal set; } = 6;
    public string Animation { get; private set; } = "stance";
    public double AnimationTime { get; private set; }

    /// <summary>Animation playback speed: a swing is stretched to fit the attack interval.</summary>
    public double AnimationRate { get; private set; } = 1.0;

    public double Radius { get; init; } = 0.35;
    public abstract string Name { get; }
    public abstract bool IsDead { get; }

    internal List<(double X, double Y)> Path { get; } = [];

    public double DistanceTo(double x, double y) => Math.Sqrt((X - x) * (X - x) + (Y - y) * (Y - y));
    public double DistanceTo(Actor other) => DistanceTo(other.X, other.Y);

    internal void Play(string animation, double rate = 1.0, bool restart = false)
    {
        if (Animation == animation && !restart)
        {
            AnimationRate = rate;
            return;
        }
        Animation = animation;
        AnimationTime = 0;
        AnimationRate = rate;
    }

    internal void Advance(double dt) => AnimationTime += dt * AnimationRate;

    internal void Face(double x, double y)
    {
        if (Math.Abs(x - X) + Math.Abs(y - Y) > 1e-6)
            Direction = DirectionOf(x - X, y - Y);
    }

    /// <summary>The sprite direction for a movement of (dx, dy) in map space, through the isometric projection.</summary>
    public static int DirectionOf(double dx, double dy)
    {
        double sx = dx - dy, sy = (dx + dy) / 2;
        var angle = Math.Atan2(sy, sx) * 180 / Math.PI;
        if (angle < 0)
            angle += 360;
        var d = (int)Math.Round((angle - 180) / 45.0);
        return ((d % 8) + 8) % 8;
    }
}

public sealed class HeroActor : Actor
{
    public HeroActor(Character character)
    {
        Character = character;
        Resource = new ClassResource(character);
        Equipment = new Equipment(character);
    }

    public Equipment Equipment { get; }

    public Character Character { get; }
    public ClassResource Resource { get; }
    public override string Name => Character.Name;
    public override bool IsDead => Character.IsDead;

    internal AttackCadence Cadence { get; } = new();
    internal HeroOrder? Order { get; set; }
    internal HeroAction? Action { get; set; }
    internal (double X, double Y)? Goal { get; set; }
    internal Dictionary<string, double> ReadyAt { get; } = [];
    internal GroundItem? PickupTarget { get; set; }
    internal Vendor? TalkTarget { get; set; }
    internal Cache? CacheTarget { get; set; }
    internal double FlourishReadyAt { get; set; }
    internal double StaggerRemaining { get; set; }
    internal double RespawnIn { get; set; }
    internal double RepathIn { get; set; }
    /// <summary>Potion healing still to arrive (the HUD shows it as a buff).</summary>
    public double HealRemaining { get; internal set; }
    internal double HealPerSecond { get; set; }

    /// <summary>True while winding up, swinging, casting or dashing.</summary>
    public bool IsSwinging => Action is not null;

    /// <summary>The ability being performed right now, if any.</summary>
    public AbilityDefinition? CurrentAbility => Action?.Ability;

    public MonsterActor? CurrentTarget => Action?.Target ?? Order?.Target;
}

/// <summary>What the player asked for: use this ability on that monster (repeating ones keep going until it dies).</summary>
internal sealed record HeroOrder(AbilityDefinition Ability, MonsterActor Target);

/// <summary>An ability being carried out: an optional dash, then the wind-up, with the effect landing part-way.</summary>
internal sealed class HeroAction(AbilityDefinition ability, MonsterActor target, double duration)
{
    public AbilityDefinition Ability { get; } = ability;
    public MonsterActor Target { get; } = target;
    public double Duration { get; } = duration;
    public double Elapsed { get; set; }
    public bool Landed { get; set; }
    public bool Dashing { get; set; }
    public double DashTime { get; set; }
}

/// <summary>A spell in flight, homing on its target. <see cref="LaunchAt"/> staggers a volley of missiles.</summary>
public sealed class Projectile(AbilityDefinition ability, MonsterActor target, int rank, double x, double y, double now, int index)
{
    public AbilityDefinition Ability { get; } = ability;
    public MonsterActor Target { get; } = target;
    public int Rank { get; } = rank;
    public int Index { get; } = index;
    public double X { get; internal set; } = x;
    public double Y { get; internal set; } = y;
    public double LaunchAt { get; } = now + index * 0.12;
}

/// <summary>Loot lying on the ground: an item, a pile of gold or a healing draught.</summary>
public sealed class GroundItem(double x, double y, LootDrop drop, double droppedAt)
{
    private static int _nextId;

    public int Id { get; } = Interlocked.Increment(ref _nextId);
    public double X { get; } = x;
    public double Y { get; } = y;
    public LootDrop Drop { get; } = drop;
    public double DroppedAt { get; } = droppedAt;

    public string Label => Drop.Item?.Name ?? (Drop.Draught ? "Healing draught" : $"{Drop.Gold} gold");
    public Rarity Rarity => Drop.Item?.Rarity ?? Rarity.Common;
}

/// <summary>A short burst where a spell or charge lands, for the renderer.</summary>
public sealed record Impact(double X, double Y, DamageType Type, double At)
{
    public const double Lifetime = 0.35;
}

public enum MonsterState
{
    Idle,
    Chase,
    Attack,
    Dead,
}

public sealed class MonsterActor : Actor
{
    public MonsterActor(CreatureDefinition definition, Creature creature, int packId)
    {
        Definition = definition;
        Creature = creature;
        PackId = packId;
    }

    public CreatureDefinition Definition { get; }
    public Creature Creature { get; }
    public int PackId { get; }

    /// <summary>Which placed monster this is, in zone order (-1 for a summoned minion). Saves list the dead ones.</summary>
    public int SpawnIndex { get; internal set; } = -1;
    public override string Name => Creature.Name;
    public override bool IsDead => Creature.IsDead;
    public MonsterState State { get; internal set; }

    /// <summary>Seconds since death, for the corpse fade.</summary>
    public double DeadFor { get; internal set; }

    internal AttackCadence Cadence { get; } = new();
    internal double AttackElapsed { get; set; } = -1;
    internal bool AttackLanded { get; set; }
    internal double AttackCooldown { get; set; }
    internal double RepathIn { get; set; }
    internal double SummonIn { get; set; }
    internal int Summoned { get; set; }
    internal MonsterActor? SummonedBy { get; set; }
    /// <summary>Seconds left of the red hit flash.</summary>
    public double HitFlinch { get; internal set; }
}

public enum FloatingTextKind
{
    DamageDealt,
    DamageTaken,
    Critical,
    Miss,
    Heal,
    Info,
    Xp,
}

/// <summary>A floating combat number or word, in map space, rising and fading over <see cref="Lifetime"/>.</summary>
public sealed record FloatingText(double X, double Y, string Text, FloatingTextKind Kind, double BornAt)
{
    public const double Lifetime = 1.2;
}
