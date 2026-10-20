using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Rules;

namespace AshenReliquary.Core.World;

/// <summary>
/// Act 3's dangers (M4 step 6):
/// <list type="bullet">
/// <item>Crowd-control casters (the AI's third behaviour): a creature that <see cref="CreatureDefinition.Inflicts"/> a
/// condition lays it on the hero with each hit, against a save.</item>
/// <item>Cold (the Frostbitten Pass, a <see cref="ZoneDefinition.Cold"/> zone): the hero's exposure rises away from
/// fires, three times as fast in a blizzard; at half it makes them clumsy, full it freezes them.</item>
/// <item>The unseen (the Hollow Woods): invisible until a Perception check finds them, they strike, or an area attack
/// catches them; blows at one unseen must pass a DC 11 flat check.</item>
/// <item>Illusions: a Will save on first sight sees through one; a hit dissolves it; it is worth nothing.</item>
/// <item>Domination (the Catacombs of Oriel): a dominating caster turns the hero's ally against them for a while, or
/// with none to turn, frightens the hero.</item>
/// <item>The Library of Ash: seven burned pages across the three zones; all read, the chronicle is whole.</item>
/// </list>
/// </summary>
public sealed partial class GameWorld
{
    /// <summary>Cold exposure gained a second out in the cold (full in about 80 s).</summary>
    public const double ColdPerSecond = 1.25;

    /// <summary>How much faster it rises in a blizzard.</summary>
    public const double BlizzardColdScale = 3.0;

    /// <summary>How fast a fire (a legend light in firelight) warms the hero within its reach, a second.</summary>
    public const double FireWarmthPerSecond = 10.0;

    /// <summary>Blizzards: one every so often, lasting a while.</summary>
    public const double BlizzardEverySeconds = 75, BlizzardSeconds = 20;

    /// <summary>How long a monster that strikes stays seen; how near the hero must be to try to spot one.</summary>
    public const double RevealSeconds = 4, SpotTiles = 5;

    /// <summary>The flat check a blow at an unseen monster must pass (Pathfinder's hidden).</summary>
    public const int UnseenFlatCheckDc = 11;

    /// <summary>How long a dominated ally stays turned; how often a dominator tries.</summary>
    public const double TurnedSeconds = 6, DominateEverySeconds = 10;

    private double _blizzardAt = BlizzardEverySeconds, _freezeIn;

    /// <summary>A blizzard blowing (a cold zone's): exposure triples and the renderer whitens the air.</summary>
    public bool Blizzard => Zone.Cold && Time >= _blizzardAt && Time < _blizzardAt + BlizzardSeconds;

    // ----- Crowd control -------------------------------------------------------------

    /// <summary>A crowd-control caster's hit: the hero makes the save or takes its condition.</summary>
    private void Inflict(MonsterActor m)
    {
        if (m.Definition.Inflicts is not { } inflict || Hero.IsDead)
            return;
        var degree = Check.Roll(Random, Hero.Character.Save(inflict.Save).Total, m.Creature.Stats.SaveDc).Degree;
        if (degree >= DegreeOfSuccess.Success)
            return;
        var seconds = inflict.Seconds * (degree == DegreeOfSuccess.CriticalFailure ? 2 : 1);
        Hero.Character.Conditions.Apply(inflict.Condition, inflict.Value, seconds);
        Float(Hero, inflict.Condition.ToString().ToLowerInvariant(), FloatingTextKind.Critical, 0.7);
    }

    // ----- Cold ----------------------------------------------------------------------

    /// <summary>The hero's exposure: up out in the cold, down by a fire, and its effects.</summary>
    private void UpdateCold(double dt)
    {
        var hero = Hero;
        if (!Zone.Cold || hero.IsDead)
        {
            hero.Cold = Math.Max(0, hero.Cold - FireWarmthPerSecond * dt);
            return;
        }
        if (Time >= _blizzardAt + BlizzardSeconds)
            _blizzardAt += BlizzardEverySeconds + BlizzardSeconds;
        if (Time >= _blizzardAt && Time - dt < _blizzardAt)
            Say("A blizzard comes down the pass. Find a fire.");
        var warm = Map.Lights.Any(l => l.Color is null && hero.DistanceTo(l.X, l.Y) <= l.Radius * 0.8);
        if (warm)
            hero.Cold = Math.Max(0, hero.Cold - FireWarmthPerSecond * dt);
        else
        {
            var resist = Math.Clamp(hero.Character.Defenses.Resistances.GetValueOrDefault(DamageType.Cold), 0, 10);
            hero.Cold = Math.Min(100, hero.Cold + ColdPerSecond * (Blizzard ? BlizzardColdScale : 1) * (1 - resist / 12.0) * dt);
        }
        // Chilled: stiff hands at half; freezing at full, a basic Fortitude save every 3 s.
        if (hero.Cold >= 50)
            hero.Character.Conditions.Apply(ConditionKind.Clumsy, 1, 1.0);
        if (hero.Cold < 100)
        {
            _freezeIn = 0;
            return;
        }
        _freezeIn -= dt;
        if (_freezeIn > 0)
            return;
        _freezeIn = 3;
        var degree = Check.Roll(Random, hero.Character.Save(StatisticKind.Fortitude).Total, 14 + Zone.Level).Degree;
        var dealt = DamageCalculator.BasicSave(new Dice(1 + Zone.Level / 3, 6).Roll(Random), degree, DamageType.Cold, hero.Character.Defenses);
        if (dealt > 0)
        {
            Float(hero, "freezing", FloatingTextKind.Critical, 0.5);
            HurtHero(dealt);
        }
    }

    // ----- The unseen and illusions --------------------------------------------------

    /// <summary>An invisible monster not revealed just now.</summary>
    public bool IsUnseen(MonsterActor m) => m.Definition.Unseen && !m.IsDead && Time >= m.RevealedUntil;

    /// <summary>A monster the hero (and the bots) can pick out to attack.</summary>
    public bool CanTarget(MonsterActor m) => !m.IsDead && !IsUnseen(m);

    private void RevealMonster(MonsterActor m, double seconds = RevealSeconds)
    {
        if (!m.Definition.Unseen)
            return;
        if (Time >= m.RevealedUntil)
            Float(m, "revealed", FloatingTextKind.Info, 0.5);
        m.RevealedUntil = Math.Max(m.RevealedUntil, Time + seconds);
    }

    /// <summary>
    /// Each frame for each monster: the hero's Perception against an unseen one near (every 2 s), a Will save against an
    /// illusion on first sight, a dominator's attempts.
    /// </summary>
    private void UpdateVeil(MonsterActor m, double dt)
    {
        var def = m.Definition;
        if (Hero.IsDead || m.IsDead)
            return;
        var near = m.DistanceTo(Hero) <= SpotTiles;
        if (def.Unseen && IsUnseen(m) && near)
        {
            m.SpotIn -= dt;
            if (m.SpotIn <= 0)
            {
                m.SpotIn = 2;
                if (Check.Roll(Random, Hero.Character.Perception().Total, m.Creature.PerceptionDc).Degree >= DegreeOfSuccess.Success)
                {
                    RevealMonster(m, 6);
                    Say($"You make out a {def.Name.ToLowerInvariant()} in the gloom.");
                }
            }
        }
        if (def.Illusion && !m.Doubted && m.DistanceTo(Hero) <= 6 && Map.HasLineOfSight(m.X, m.Y, Hero.X, Hero.Y))
        {
            m.Doubted = true;
            if (Check.Roll(Random, Hero.Character.Save(StatisticKind.Will).Total, m.Creature.Stats.SaveDc).Degree >= DegreeOfSuccess.Success)
            {
                Say($"You see through the {def.Name.ToLowerInvariant()}: there is nothing there.");
                Dissolve(m);
                return;
            }
        }
        if (def.Dominates && m.State != MonsterState.Idle)
            UpdateDomination(m, dt);
    }

    /// <summary>An illusion seen through or struck: it is gone, and worth nothing.</summary>
    private void Dissolve(MonsterActor m)
    {
        m.Creature.TakeDamage(m.Creature.HitPoints);
        m.State = MonsterState.Dead;
        m.DeadFor = CorpseSeconds - 1.0;                          // it fades at once
        m.Path.Clear();
        Float(m, "an illusion", FloatingTextKind.Info, 0.4);
        Sound(SoundCue.Ward, m, "monster");
    }

    // ----- Domination ----------------------------------------------------------------

    private void UpdateDomination(MonsterActor m, double dt)
    {
        m.DominateIn -= dt;
        if (m.DominateIn > 0)
            return;
        m.DominateIn = DominateEverySeconds;
        var dc = m.Creature.Stats.SaveDc;
        var ally = _allies.Where(a => !a.IsDead && !a.IsTurned(Time) && a.DistanceTo(m) <= 8 && Map.HasLineOfSight(m.X, m.Y, a.X, a.Y))
            .MinBy(a => a.DistanceTo(m));
        Sound(SoundCue.WitchCast, m);
        if (ally is not null)
        {
            if (Check.Roll(Random, ally.Creature.Save(StatisticKind.Will).Total, dc).Degree >= DegreeOfSuccess.Success)
            {
                Float(ally, "resists", FloatingTextKind.Miss, 0.5);
                return;
            }
            ally.TurnedUntil = Time + TurnedSeconds;
            ally.Target = null;
            ally.Command = null;
            Float(ally, "TURNED", FloatingTextKind.Critical, 0.8);
            Say($"The {m.Name.ToLowerInvariant()} whispers, and your {ally.Name.ToLowerInvariant()} turns on you.");
            return;
        }
        if (m.DistanceTo(Hero) > 8 || !Map.HasLineOfSight(m.X, m.Y, Hero.X, Hero.Y))
            return;
        if (Check.Roll(Random, Hero.Character.Save(StatisticKind.Will).Total, dc).Degree >= DegreeOfSuccess.Success)
            return;
        Hero.Character.Conditions.Apply(ConditionKind.Frightened, 2);
        Float(Hero, "frightened", FloatingTextKind.Critical, 0.7);
    }

    /// <summary>A turned ally goes for the hero, biting as a monster would, until the whispers let it go.</summary>
    private void TurnedAllyActs(AllyActor ally, double dt)
    {
        if (ally.AttackCooldown > 0)
            ally.AttackCooldown -= dt;
        if (ally.DistanceTo(Hero) <= ally.Definition.AttackRange + Hero.Radius)
        {
            ally.Path.Clear();
            ally.Face(Hero.X, Hero.Y);
            if (ally.AttackCooldown > 0)
                return;
            ally.AttackCooldown = 1.0 / ally.Creature.AttacksPerSecond;
            ally.Play("swing", restart: true);
            if (Time < Hero.InvulnerableUntil)
                return;
            var result = Strike.Resolve(ally.Creature, Hero.Character, ally.Cadence, Time, Random);
            if (result.Damage > 0)
                HurtHero(result.Damage);
            else
                Float(Hero, "miss", FloatingTextKind.Miss);
            return;
        }
        Chase(ally, Hero.X, Hero.Y, 1, dt);
    }

    // ----- The Library of Ash ----------------------------------------------------------

    /// <summary>A page's flag is this plus its number (1 to <see cref="LibraryPages"/>), "read" once read.</summary>
    public const string LibraryPageFlag = "act3.page.";

    public const int LibraryPages = 7;

    /// <summary>The XP for the whole chronicle (a side story's: an encounter two levels up, at Act 3's levels).</summary>
    public const int LibraryXp = 900;

    /// <summary>A page read: the journal counts them; the seventh makes the chronicle whole, with its boon.</summary>
    private void ReadLibraryPage()
    {
        var read = Enumerable.Range(1, LibraryPages).Count(i => Flag(LibraryPageFlag + i) == "read");
        if (read < LibraryPages)
        {
            WriteJournal(new JournalEntryDefinition("act3_library", "The Library of Ash",
                $"When the Choir came, the monastery's library burned and the wind carried the pages over the mountain. I have read {read} of the seven " +
                "pages of the Chronicle of Saint Oriel. It tells of Ashur, before he was a god."));
            Say($"A page of the Chronicle of Saint Oriel ({read} of {LibraryPages}).");
            return;
        }
        if (Flag("act3.library") == "complete")
            return;
        SetFlag("act3.library", "complete");
        WriteJournal(new JournalEntryDefinition("act3_library", "The Library of Ash",
            "I have read all seven pages of the Chronicle of Saint Oriel. Ashur was never a god: he was a shepherd who took the mountain's fire into his chest " +
            "and carried it for nine hundred years, dying and rising, until he could carry it no longer and the Order killed him for it. Saint Oriel believed " +
            "another could take the fire up as he did. The shard in my chest is a piece of it."));
        GainXp(LibraryXp, Hero);
        Apply(new DialogueEffect(Boon: "ashurs_chronicle"));
    }
}
