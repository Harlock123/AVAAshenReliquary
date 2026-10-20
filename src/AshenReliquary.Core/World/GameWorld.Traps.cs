using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Items;
using AshenReliquary.Core.Rules;

namespace AshenReliquary.Core.World;

/// <summary>
/// Traps (M4 step 3): found with Perception, disarmed with Thievery, sprung by a careless step. A trap's DC is its own
/// or the zone level's (Pathfinder's level-based DC, <see cref="Crafting.Dc"/>); its damage is (1 + level / 2)d6 against
/// a basic Reflex save. States live in world flags ("trap.&lt;zone&gt;.&lt;id&gt;"), so they last across zones and saves.
/// The pathfinder steps round spotted traps.
/// </summary>
public sealed partial class GameWorld
{
    /// <summary>How near the hero must come to get a Perception check to spot a trap.</summary>
    public const double TrapNoticeTiles = 3.5;

    /// <summary>How close the hero must be to disarm one.</summary>
    public const double DisarmReach = 1.6;

    private readonly List<Trap> _zoneTraps = [];

    public IReadOnlyList<Trap> ZoneTraps => _zoneTraps;

    /// <summary>A trap's DC: its own, or the zone level's.</summary>
    public int TrapDc(Trap trap) => trap.Definition.Dc ?? Crafting.Dc(Zone.Level);

    private string TrapFlag(Trap trap) => $"trap.{Zone.Id}.{trap.Definition.Id}";

    private void PopulateTraps(ZoneDefinition zone)
    {
        _zoneTraps.Clear();
        foreach (var definition in zone.Traps ?? [])
        {
            var trap = new Trap(definition);
            trap.State = _flags.GetValueOrDefault(TrapFlag(trap)) switch
            {
                "found" => TrapState.Found,
                "disarmed" => TrapState.Disarmed,
                "sprung" => TrapState.Sprung,
                _ => TrapState.Hidden,
            };
            trap.Checked = trap.State != TrapState.Hidden;
            _zoneTraps.Add(trap);
            if (trap.State == TrapState.Found)
                Map.SetAvoid(definition.At[0], definition.At[1], true);
        }
    }

    private void SetTrapState(Trap trap, TrapState state)
    {
        trap.State = state;
        SetFlag(TrapFlag(trap), state.ToString().ToLowerInvariant());
        Map.SetAvoid(trap.Definition.At[0], trap.Definition.At[1], state == TrapState.Found);
    }

    /// <summary>Each frame: spotting checks as the hero comes near, and springing under the hero's feet.</summary>
    private void UpdateZoneTraps()
    {
        if (_zoneTraps.Count == 0 || Hero.IsDead)
            return;
        var c = Hero.Character;
        foreach (var trap in _zoneTraps)
        {
            if (trap.State == TrapState.Hidden && !trap.Checked && trap.DistanceTo(Hero.X, Hero.Y) <= TrapNoticeTiles)
            {
                trap.Checked = true;
                if (Check.Roll(Random, c.Perception().Total, TrapDc(trap)).Degree >= DegreeOfSuccess.Success)
                {
                    SetTrapState(trap, TrapState.Found);
                    Float(Hero, "trap!", FloatingTextKind.Info, 0.8);
                    Say($"You spot a {trap.Definition.Name.ToLowerInvariant()} in the floor.");
                }
            }
            // A Step or Tumble skips over it; walking onto its cell springs it.
            if (trap.Armed && Time >= Hero.InvulnerableUntil && (int)Math.Floor(Hero.X) == trap.Definition.At[0]
                && (int)Math.Floor(Hero.Y) == trap.Definition.At[1])
                Spring(trap);
        }
    }

    /// <summary>The trap goes off under the hero: a basic Reflex save against its DC.</summary>
    private void Spring(Trap trap)
    {
        SetTrapState(trap, TrapState.Sprung);
        trap.SprungAt = Time;
        var c = Hero.Character;
        var dc = TrapDc(trap);
        var degree = Check.Roll(Random, c.Save(StatisticKind.Reflex).Total, dc).Degree;
        var dice = new Dice(1 + Zone.Level / 2, 6);
        var dealt = DamageCalculator.BasicSave(dice.Roll(Random), degree, trap.Definition.DamageType, c.Defenses);
        AddImpact(trap.X, trap.Y, trap.Definition.DamageType);
        Sound(SoundCue.Bomb, trap.X, trap.Y, trap.Definition.DamageType.ToString().ToLowerInvariant());
        Say($"A {trap.Definition.Name.ToLowerInvariant()} goes off under you!");
        if (dealt <= 0)
        {
            Float(Hero, "evaded", FloatingTextKind.Miss, 0.5);
            return;
        }
        if (!HurtHero(dealt))
            return;
        if (trap.Definition.Kind == "poison" && degree <= DegreeOfSuccess.Failure)
            Poison(new Dice(1 + Zone.Level / 4, 6));
    }

    /// <summary>Walk to a spotted trap (a click on it) and try to disarm it.</summary>
    public void Disarm(Trap trap)
    {
        if (Hero.IsDead || trap.State != TrapState.Found || !_zoneTraps.Contains(trap))
            return;
        Hero.Order = null;
        Hero.PickupTarget = null;
        Hero.TalkTarget = null;
        Hero.CacheTarget = null;
        Hero.CorpseTarget = null;
        if (trap.DistanceTo(Hero.X, Hero.Y) <= DisarmReach)
        {
            TryDisarm(trap);
            return;
        }
        Hero.TrapTarget = trap;
        var (fx, fy) = InFrontOf(trap.X, trap.Y);
        Hero.Goal = (fx, fy);
        PlanPath(Hero, fx, fy);
    }

    /// <summary>
    /// A Thievery check (trained only): a success disarms it for XP (double on a critical success), a failure fumbles
    /// (try again), a critical failure springs it.
    /// </summary>
    private void TryDisarm(Trap trap)
    {
        Hero.TrapTarget = null;
        Hero.Goal = null;
        Hero.Path.Clear();
        Hero.Face(trap.X, trap.Y);
        var c = Hero.Character;
        if (c.SkillRank("Thievery") == ProficiencyRank.Untrained)
        {
            Say($"Disarming the {trap.Definition.Name.ToLowerInvariant()} needs training in Thievery. Step round it.");
            return;
        }
        Hero.Play("cast", 1.5, restart: true);
        trap.Attempts++;
        var degree = Check.Roll(Random, c.Skill("Thievery").Total, TrapDc(trap)).Degree;
        switch (degree)
        {
            case DegreeOfSuccess.CriticalSuccess or DegreeOfSuccess.Success:
                SetTrapState(trap, TrapState.Disarmed);
                Float(Hero, "disarmed", FloatingTextKind.Info, 0.6);
                Say($"You disarm the {trap.Definition.Name.ToLowerInvariant()}.");
                GainXp(DisarmXp * (degree == DegreeOfSuccess.CriticalSuccess ? 2 : 1), Hero);
                break;
            case DegreeOfSuccess.Failure:
                Float(Hero, "fumbled", FloatingTextKind.Miss, 0.6);
                Say("You can't find the trick of it. Try again, or step round it.");
                break;
            default:
                Spring(trap);
                break;
        }
    }

    /// <summary>XP for a disarmed trap: a small hazard's worth.</summary>
    private int DisarmXp => 10 * Math.Max(1, Zone.Level);

    private void UpdateTrapTarget()
    {
        if (Hero.TrapTarget is not { } trap)
            return;
        if (trap.State != TrapState.Found)
        {
            Hero.TrapTarget = null;
            return;
        }
        if (trap.DistanceTo(Hero.X, Hero.Y) <= DisarmReach)
            TryDisarm(trap);
    }
}
