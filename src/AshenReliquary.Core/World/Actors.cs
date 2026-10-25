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

    /// <summary>A rising corpse the hero is walking over to smash.</summary>
    internal MonsterActor? CorpseTarget { get; set; }

    /// <summary>A found trap the hero is walking over to disarm.</summary>
    internal Trap? TrapTarget { get; set; }
    internal ShrineDefinition? ShrineTarget { get; set; }
    internal Interactive? InteractTarget { get; set; }
    internal double DodgeRemaining { get; set; }
    internal (double X, double Y) DodgeDirection { get; set; }
    internal double DodgeReadyAt { get; set; }

    /// <summary>How fast the current dodge or Tumble goes, in tiles a second.</summary>
    internal double DodgeSpeed { get; set; }

    /// <summary>The Trickster's Tumble: the enemies it has passed and left off-guard (null for a plain dodge).</summary>
    internal HashSet<MonsterActor>? DodgeFeinted { get; set; }

    /// <summary>Seconds left in the shadows (the Rogue's Hide in Shadows); 0 when seen.</summary>
    public double HiddenRemaining { get; internal set; }

    public bool IsHidden => HiddenRemaining > 0;

    /// <summary>The monster the Ranger hunts (Hunt Prey): their Strikes against it deal precision dice. Null when none.</summary>
    public MonsterActor? Prey { get; internal set; }
    internal double InvulnerableUntil { get; set; }

    /// <summary>The Choir's song holds the hero until then: they walk to the singer and can do nothing else.</summary>
    public double EnthralledUntil { get; internal set; }

    /// <summary>The sister whose song holds the hero.</summary>
    internal MonsterActor? Enthraller { get; set; }

    /// <summary>Cold exposure, 0 to 100 (a cold zone's: chilled at half, freezing at full).</summary>
    public double Cold { get; internal set; }

    /// <summary>When the embers underfoot next burn (Act 4's lava fields).</summary>
    internal double BurnIn { get; set; }

    /// <summary>True during a Step dodge: monsters' blows pass through.</summary>
    public bool IsDodging => DodgeRemaining > 0;
    internal double FlourishReadyAt { get; set; }
    internal double StaggerRemaining { get; set; }
    internal double RespawnIn { get; set; }
    internal double RepathIn { get; set; }
    /// <summary>Potion healing still to arrive (the HUD shows it as a buff).</summary>
    public double HealRemaining { get; internal set; }
    internal double HealPerSecond { get; set; }

    /// <summary>A raised shield or the Shield cantrip, while it lasts; null otherwise.</summary>
    public HeroWard? Ward { get; internal set; }

    /// <summary>A multiclass dedication's small pool of another class's resource, or null.</summary>
    public ClassResource? Dedication { get; internal set; }

    /// <summary>Rounds left in the gun in hand (the Gunslinger's), and the seconds a reload still takes (0: not reloading).</summary>
    public int Loaded { get; internal set; }
    public double ReloadRemaining { get; internal set; }
    internal double ReloadTotal { get; set; }

    /// <summary>The gun <see cref="Loaded"/> counts for: a gun newly taken in hand comes loaded.</summary>
    internal Items.Item? LoadedGun { get; set; }

    /// <summary>Until when an antidote's bonus against poison lasts (game time).</summary>
    public double AntidoteUntil { get; internal set; }

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

    /// <summary>Weapon dice bought with the resource spent (Finishing Blow).</summary>
    public int ExtraDice { get; init; }

    /// <summary>Begun out of hiding: its blow finds the target off-guard.</summary>
    public bool FromHiding { get; init; }

    /// <summary>Rounds a gun fires in this action (0: the ability's own count).</summary>
    public int Shots { get; init; }
}

/// <summary>
/// A ward on the hero: a raised shield or the Shield cantrip. <see cref="AcBonus"/> is a circumstance bonus to AC until
/// <see cref="EndsAt"/>; Shield Block takes <see cref="Hardness"/> off a blow.
/// </summary>
public sealed class HeroWard(AbilityDefinition ability, int acBonus, int hardness, double endsAt)
{
    public AbilityDefinition Ability { get; } = ability;
    public int AcBonus { get; } = acBonus;
    public int Hardness { get; } = hardness;
    public double EndsAt { get; internal set; } = endsAt;
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

    /// <summary>How many more enemies this spell leaps to after it strikes (Electric Arc).</summary>
    public int ChainLeft { get; init; }

    /// <summary>An arrow from the hero's bow, or a gun's shot: on arrival it is a weapon Strike (<see cref="Shot"/> says how).</summary>
    public bool IsArrow => Shot is not null;

    public ArrowShot? Shot { get; init; }
}

/// <summary>
/// What an arrow carries to its target: the Strike's extra dice and cadence, whether it builds the resource (only a
/// volley's first does), whether it was loosed from hiding or is a feint, and how many more enemies it passes through.
/// </summary>
public sealed record ArrowShot(int ExtraDice, int CadenceAttacks, bool Builds, bool FromHiding, bool Feint, int PierceLeft)
{
    /// <summary>A gun's shot: it flies three times as fast as an arrow and is drawn as a tracer.</summary>
    public bool Bullet { get; init; }
}

/// <summary>
/// An ally fighting beside the hero (the Beastmaster's wolf; the Necromancer's minions to come): a creature with its
/// own health and bite, following the hero and falling on what they fight (<see cref="GameWorld"/>'s allies).
/// </summary>
public sealed class AllyActor : Actor
{
    public AllyActor(CreatureDefinition definition, Creature creature, int level, int growth)
    {
        Definition = definition;
        Creature = creature;
        Level = level;
        Growth = growth;
        // Its body, not its picture: the wolf's sheet is drawn larger (Scale) to match Flare's own creatures.
        Radius = 0.4;
    }

    public CreatureDefinition Definition { get; }
    public Creature Creature { get; internal set; }

    /// <summary>A Necromancer's skeleton: it doesn't come back once it falls, and it crumbles when the hero does.</summary>
    public bool IsMinion { get; init; }

    /// <summary>A pet the hero keeps beside any companion (the drakeling hatched at the Ember Gate): it comes back as one does.</summary>
    public bool IsPet { get; init; }

    /// <summary>When a summoned ally goes (game time; Call of the Pack's wolves), or never.</summary>
    public double ExpiresAt { get; init; } = double.PositiveInfinity;

    /// <summary>Summoned for a while, beside the hero's held skeletons rather than among them.</summary>
    public bool IsTimed => !double.IsPositiveInfinity(ExpiresAt);

    /// <summary>When it was raised (game time): it climbs out of the ground.</summary>
    public double RaisedAt { get; init; } = double.NegativeInfinity;

    /// <summary>The hero level and feat growth its numbers were built for (rebuilt when either changes).</summary>
    public int Level { get; internal set; }
    public int Growth { get; internal set; }

    public override string Name => Creature.Name;
    public override bool IsDead => Creature.IsDead;

    /// <summary>Dominated until then: it fights the hero (the Catacombs' whisperers).</summary>
    public double TurnedUntil { get; internal set; }

    public bool IsTurned(double now) => !IsDead && now < TurnedUntil;

    /// <summary>Seconds until a fallen ally comes back to the hero.</summary>
    public double ReturnIn { get; internal set; }

    public double HitFlinch { get; internal set; }

    /// <summary>The monster it is fighting, or null while it follows the hero.</summary>
    public MonsterActor? Target { get; internal set; }

    /// <summary>Sent by Sic 'Em: its next bite is this ability's (extra dice, a condition), and it runs there faster.</summary>
    internal AbilityDefinition? Command { get; set; }

    internal AttackCadence Cadence { get; } = new();
    internal double AttackElapsed { get; set; } = -1;
    internal bool AttackLanded { get; set; }
    internal double AttackCooldown { get; set; }
    internal double RepathIn { get; set; }
    internal double RegenCarry { get; set; }
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

    public string Label => Drop.Item is { Count: > 1 } stack ? $"{stack.Name} ×{stack.Count}"
        : Drop.Item?.Name ?? (Drop.Draught ? "Healing draught" : $"{Drop.Gold} gold");
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

    /// <summary>Monster affixes (Act 1: <see cref="MonsterAffix.Vengeful"/>).</summary>
    public IReadOnlyList<MonsterAffix> Affixes { get; internal set; } = [];

    /// <summary>Vengeance stacks, 0 to <see cref="GameWorld.VengeanceMax"/>: each ally of its pack that dies adds one.</summary>
    public int Vengeance { get; internal set; }

    /// <summary>Damage a Shielded ally's ward will still soak, until <see cref="WardEndsAt"/>.</summary>
    public int Ward { get; internal set; }
    public double WardEndsAt { get; internal set; }

    /// <summary>When a Shielded monster next wards its pack.</summary>
    internal double WardPackIn { get; set; }

    /// <summary>Seconds spent circling for a flank before it gives up and strikes from where it is.</summary>
    internal double FlankTime { get; set; }

    /// <summary>Seconds until this corpse rises again (0 when it won't): a Barrow Fields reviver, see <see cref="GameWorld.MaxRises"/>.</summary>
    public double RisesIn { get; internal set; }

    /// <summary>How many times it has risen; a risen monster gives no XP or loot again.</summary>
    public int Rises { get; internal set; }

    /// <summary>An unseen monster is visible until then (it struck, was spotted or was caught in a blast).</summary>
    public double RevealedUntil { get; internal set; }

    internal double SpotIn { get; set; }

    /// <summary>An illusion the hero has had their Will save against.</summary>
    internal bool Doubted { get; set; }

    internal double DominateIn { get; set; } = 4;

    /// <summary>A Mirrored monster has split into its images (it does so once).</summary>
    internal bool HasSplit { get; set; }

    /// <summary>One of a Mirrored monster's images: weaker, see-through, gone when the original falls.</summary>
    public bool IsMirrorImage { get; internal set; }

    /// <summary>A sister of the Choir: her health is the boss's, and a blow at her lands on it.</summary>
    public MonsterActor? SharesWith { get; internal set; }

    /// <summary>One of the Choir's reflections: it looks like the true sister, and shatters at a touch.</summary>
    public bool IsReflection { get; internal set; }

    /// <summary>The true sister among her reflections, seen for what she is until then (a Perception check).</summary>
    public double TrueVoiceUntil { get; internal set; }

    internal double ShuffleIn { get; set; }
    internal double SongIn { get; set; }

    /// <summary>A boss phase's late arrival (Brother Caddoc) has come.</summary>
    internal bool Joined { get; set; }

    /// <summary>Act 4's pack leader that drives this one (null without one); a leader leads itself.</summary>
    public MonsterActor? Leader { get; internal set; }

    /// <summary>It leads its pack: it drives the others while it stands, and cries them on.</summary>
    public bool IsLeader { get; internal set; }

    /// <summary>When its leader's war cry quickens it until.</summary>
    public double CriedUntil { get; internal set; }

    internal double CryIn { get; set; }

    /// <summary>A Herald's Echo (the Reliquary): weaker, without its last phase, its death nothing of the Herald's own.</summary>
    public bool IsEcho { get; internal set; }

    /// <summary>An Echo whose sisters stay away (the Choir half laid to rest).</summary>
    public bool EchoAlone { get; internal set; }

    /// <summary>The last phase this Echo may enter, when it is held to fewer than its Herald's all but last.</summary>
    internal int? EchoLastPhase { get; set; }

    /// <summary>A boss's allies of the hero have been called for this fight.</summary>
    internal bool AlliesCalled { get; set; }

    /// <summary>A figure of ash waiting to be called into the fight (a Cinder Throne Echo): untouchable, and it does nothing.</summary>
    public bool Dormant { get; internal set; }

    /// <summary>It began dormant, and goes back to waiting when the fight starts again.</summary>
    internal bool WaitsToBeCalled { get; set; }

    internal double CallEchoIn { get; set; }

    /// <summary>A flying boss in the air (Vorthrax): out of a blade's reach until it swoops.</summary>
    public bool Aloft { get; internal set; }

    /// <summary>The cone of fire it has marked, or null.</summary>
    public BossBreath? Breath { get; internal set; }

    internal double BreathIn { get; set; }
    internal double SwoopIn { get; set; }
    internal double LandedUntil { get; set; }
    internal double CrumbleIn { get; set; }

    /// <summary>When its present phase began (the Burning God's Taint rises faster as it goes on), its next ash nova, and
    /// how many strikes it has made (a phase's <see cref="BossPhase.StrikeTypes"/> take turns).</summary>
    internal double PhaseStartedAt { get; set; }
    internal double NovaIn { get; set; }
    internal int StrikesMade { get; set; }

    /// <summary>The Heart's Pull: when it next pulls, and until when it staggers from a pull lost.</summary>
    internal double PullIn { get; set; }
    public double StaggeredUntil { get; internal set; }

    /// <summary>A corpse that will stand again unless it is destroyed.</summary>
    public bool IsRising => IsDead && RisesIn > 0;

    /// <summary>How long a kiting monster has been falling back (it stands its ground after <see cref="GameWorld.KiteSeconds"/>, until rested).</summary>
    internal double KiteTime { get; set; }

    /// <summary>Who it is fighting: the hero, or an ally that is nearer (chosen again every second).</summary>
    internal Actor? Victim { get; set; }
    internal double VictimIn { get; set; }

    /// <summary>A boss's threat: the damage each of its foes (the hero, an ally) has dealt it lately, fading.</summary>
    internal Dictionary<Actor, double> Threat { get; } = [];

    public bool Has(MonsterAffix affix) => Affixes.Contains(affix);

    /// <summary>Where it was placed: a boss's arena centre, where it fuses in its last phase.</summary>
    public double HomeX { get; internal set; }
    public double HomeY { get; internal set; }

    /// <summary>A boss's phase (an index into its phases), and its clocks.</summary>
    public int BossPhase { get; internal set; }
    public bool Fused { get; internal set; }
    internal double PoolIn { get; set; }
    internal double EnrageIn { get; set; }
    internal double DelugeIn { get; set; }
    internal double FloodRadius { get; set; }

    internal double ChargeIn { get; set; }

    /// <summary>A boss's charge under way (its lane marked, then the rush), or null.</summary>
    public BossCharge? Charge { get; internal set; }

    /// <summary>What it looks like now: its boss phase's own gear (Hadric with his armour cast off), or its own.</summary>
    public IReadOnlyDictionary<string, string>? Look =>
        Definition.Boss is { } b && b.Phases[BossPhase].Look is { } phaseLook ? phaseLook : Definition.Look;

    /// <summary>Seconds of the last phase left before the boss's deluge (0 once it has begun; null outside it).</summary>
    public double? EnrageLeft => Fused && Definition.Boss is { } b && b.Phases[BossPhase].EnrageSeconds > 0 ? Math.Max(0, EnrageIn) : null;
}

/// <summary>
/// Grim Dawn-style "hero" monster affixes (spec, section 12).
/// <list type="bullet">
/// <item><b>Vengeful</b>: each ally of its pack that falls nearby gives it a stack of Vengeance, up to three: +1 to
/// hit, +10% damage and +5% speed each.</item>
/// <item><b>Hasted</b>: a quarter more attacks a second and 15% more speed.</item>
/// <item><b>Shielded</b>: on waking and every 10 s it wards itself and its pack within 6 tiles: each soaks
/// 4 + 2 × level damage for 8 s.</item>
/// <item><b>Tainted</b>: it dies in a pool of Taint that burns for 8 s (a basic Fortitude save each second) and fills the
/// Taint meter.</item>
/// </list>
/// </summary>
public enum MonsterAffix
{
    Vengeful,
    Hasted,
    Shielded,
    Tainted,
    Mirrored,
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
    /// <summary>A near miss's half damage.</summary>
    Graze,
    /// <summary>Damage to one of the hero's allies.</summary>
    AllyHurt,
}

/// <summary>A floating combat number or word, in map space, rising and fading over <see cref="Lifetime"/>.</summary>
public sealed record FloatingText(double X, double Y, string Text, FloatingTextKind Kind, double BornAt)
{
    public const double Lifetime = 1.2;
}
