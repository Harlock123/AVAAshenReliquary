using AshenReliquary.Core.Abilities;
using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Creatures;
using AshenReliquary.Core.Items;
using AshenReliquary.Core.Rules;

namespace AshenReliquary.Core.World;

/// <summary>A trap the hero has set (the Trickster's Snare): it arms after a moment and springs on the first enemy close to it.</summary>
public sealed record SetTrap(AbilityDefinition Ability, double X, double Y, double SetAt)
{
    public bool IsArmed(double now) => now - SetAt >= GameWorld.TrapArmSeconds;
}

/// <summary>
/// The Rogue's rules (spec, section 7): sneak attack's precision dice against an off-guard target, hiding in the
/// shadows, the Tumble and the Trickster's traps.
/// <list type="bullet">
/// <item>A target is off-guard to the hero's Strike when a condition says so (prone, off-guard), when it is unaware of
/// the hero (a monster that hasn't noticed them, or lost them), when the blow comes out of hiding, or for a feint
/// (Sneak Strike, Twin Feint's second blow). Off-guard without a condition is −2 AC for that Strike.</item>
/// <item>Sneak attack: the class's d6s, on a Strike with an agile or finesse weapon (or a ranged one) against an
/// off-guard target, added to the weapon's dice so a critical hit doubles them.</item>
/// <item>Hiding: unaware monsters notice the hero only within <see cref="HiddenNoticeTiles"/>; those already hunting
/// them, further than <see cref="HideCloseTiles"/>, each lose them on a Stealth check against their Perception DC (a
/// boss never does). The hero moves at <see cref="HiddenSpeed"/>. Attacking or being hit ends it.</item>
/// </list>
/// </summary>
public sealed partial class GameWorld
{
    public const double HiddenNoticeTiles = 1.0;
    public const double HideCloseTiles = 2.0;
    public const double HiddenSpeed = 0.75;
    public const double TumbleSeconds = 0.35;
    public const double TumbleFeintTiles = 1.2;
    public const double TumbleFeintSeconds = 3.0;
    public const double TrapArmSeconds = 0.5;
    public const double TrapTriggerTiles = 0.8;
    public const int MaxTraps = 2;

    private readonly List<SetTrap> _traps = [];

    /// <summary>The traps lying set (the renderer draws them).</summary>
    public IReadOnlyList<SetTrap> Traps => _traps;

    /// <summary>
    /// How a Strike finds its target: the AC adjustment for being off-guard without a condition saying so (−2), and
    /// the sneak attack dice that apply (none unless the target is off-guard and the weapon suits).
    /// </summary>
    private (int TargetAc, int SneakDice) SneakAttack(MonsterActor target, bool offGuardToThis)
    {
        var conditioned = target.Creature.Conditions.IsOffGuard;
        var offGuard = conditioned || offGuardToThis || target.State == MonsterState.Idle;
        var w = Hero.Character.Weapon.Definition;
        var suits = w.IsRanged || w.Has(WeaponTrait.Agile) || w.Has(WeaponTrait.Finesse);
        return (offGuard && !conditioned ? -ConditionSet.OffGuardAcPenalty : 0, offGuard && suits ? Hero.Character.SneakAttackDice : 0);
    }

    /// <summary>Whether a monster is off-guard to the hero right now (the target frame shows it): a condition, or unaware of them.</summary>
    public bool IsOffGuardToHero(MonsterActor m) => m.Creature.Conditions.IsOffGuard || m.State == MonsterState.Idle || Hero.IsHidden;

    /// <summary>The checks, cost and cooldown every self ability shares; false (with a message) when it can't be used now.</summary>
    private bool BeginSelf(AbilityDefinition ability)
    {
        if (WhyNot(ability) is { } problem)
        {
            Say(problem);
            return false;
        }
        Pay(ability);
        if (ability.Cooldown > 0)
            Hero.ReadyAt[ability.Id] = Time + ability.Cooldown;
        if (ability.IsFlourish)
            Hero.FlourishReadyAt = Time + FlourishLockoutSeconds;
        return true;
    }

    // ----- Hiding ------------------------------------------------------------------------

    /// <summary>
    /// Into the shadows: for <see cref="AbilityDefinition.BuffSeconds"/> the hero is hidden; every monster hunting them
    /// from further than <see cref="HideCloseTiles"/> makes a Perception check against their Stealth and, failing, loses
    /// them (it stands where it is, unaware). A boss never loses them.
    /// </summary>
    private void Hide(AbilityDefinition ability)
    {
        if (!BeginSelf(ability))
            return;
        Hero.HiddenRemaining = ability.BuffSeconds;
        var stealth = Hero.Character.Skill("Stealth").Total;
        var lost = 0;
        foreach (var m in _monsters.Where(m => !m.IsDead && m.State != MonsterState.Idle && m.Definition.Boss is null
                     && m.DistanceTo(Hero) > HideCloseTiles))
        {
            if (Check.Roll(Random, stealth, m.Creature.PerceptionDc).Degree < DegreeOfSuccess.Success)
                continue;
            m.State = MonsterState.Idle;
            m.Path.Clear();
            m.AttackElapsed = -1;
            m.FlankTime = 0;
            Float(m, "lost you", FloatingTextKind.Info, 0.3);
            lost++;
        }
        Hero.Order = null;
        Hero.Play("stance");
        Sound(SoundCue.Swing, Hero);
        Say(lost > 0 ? $"You slip into the shadows; {lost} lose{(lost == 1 ? "s" : "")} you." : "You slip into the shadows.");
    }

    /// <summary>Out of the shadows, saying why (no message when the hero chose to strike).</summary>
    private void Unhide(string? why)
    {
        if (!Hero.IsHidden)
            return;
        Hero.HiddenRemaining = 0;
        if (why is not null)
            Say(why);
    }

    private void UpdateHidden(double dt)
    {
        if (!Hero.IsHidden)
            return;
        Hero.HiddenRemaining = Math.Max(0, Hero.HiddenRemaining - dt);
        if (Hero.HiddenRemaining <= 0)
            Say("You step out of the shadows.");
    }

    // ----- Tumble ------------------------------------------------------------------------

    /// <summary>
    /// Rolls up to the ability's range toward a point (or straight on without one), through enemies, untouchable on the
    /// way. With the Trickster's feature every enemy passed close by is left off-guard.
    /// </summary>
    private void Tumble(AbilityDefinition ability, (double X, double Y)? toward)
    {
        if (!Hero.Character.Conditions.CanMove)
        {
            Say("You can't move.");
            return;
        }
        var (tx, ty) = toward ?? (Hero.X + 1, Hero.Y + 1);
        var (dx, dy) = (tx - Hero.X, ty - Hero.Y);
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length < 1e-6 || Hero.DodgeRemaining > 0 || !BeginSelf(ability))
            return;
        var tiles = Math.Min(ability.Range, Math.Max(1.0, length));
        Hero.Order = null;
        Hero.Action = null;
        Hero.Goal = null;
        Hero.Path.Clear();
        Hero.DodgeDirection = (dx / length, dy / length);
        Hero.DodgeRemaining = TumbleSeconds;
        Hero.DodgeSpeed = tiles / TumbleSeconds;
        Hero.DodgeFeinted = Hero.Character.TumbleFeint ? [] : null;
        Hero.InvulnerableUntil = Time + TumbleSeconds;
        Hero.Face(tx, ty);
        Hero.Play("run", rate: 2.5, restart: true);
        Sound(SoundCue.Swing, Hero);
    }

    /// <summary>A Trickster's Tumble leaves each enemy it passes close by off-guard.</summary>
    private void FeintPassing()
    {
        if (Hero.DodgeFeinted is not { } passed)
            return;
        foreach (var m in _monsters.Where(m => !m.IsDead && !passed.Contains(m) && m.DistanceTo(Hero) <= TumbleFeintTiles + m.Radius))
        {
            passed.Add(m);
            m.Creature.Conditions.Apply(ConditionKind.OffGuard, 1, TumbleFeintSeconds);
            Float(m, "off-guard", FloatingTextKind.Info, 0.3);
        }
    }

    // ----- Traps -------------------------------------------------------------------------

    /// <summary>Sets a trap at the hero's feet; with <see cref="MaxTraps"/> already set, the oldest goes.</summary>
    private void SetTrap(AbilityDefinition ability)
    {
        if (!BeginSelf(ability))
            return;
        if (_traps.Count(t => t.Ability.Id == ability.Id) >= MaxTraps)
            _traps.Remove(_traps.First(t => t.Ability.Id == ability.Id));
        _traps.Add(new SetTrap(ability, Hero.X, Hero.Y, Time));
        Hero.Play("cast", 1.5, restart: true);
        Sound(SoundCue.Swing, Hero);
    }

    /// <summary>Armed traps spring on the first enemy within <see cref="TrapTriggerTiles"/>; spent and expired ones go.</summary>
    private void UpdateTraps()
    {
        foreach (var trap in _traps.ToList())
        {
            if (Time - trap.SetAt > trap.Ability.BuffSeconds)
            {
                _traps.Remove(trap);
                continue;
            }
            if (!trap.IsArmed(Time) || !_monsters.Any(m => !m.IsDead && !m.Definition.Stationary && m.DistanceTo(trap.X, trap.Y) <= TrapTriggerTiles + m.Radius))
                continue;
            _traps.Remove(trap);
            Burst(trap.Ability, null, (trap.X, trap.Y));
        }
    }

    /// <summary>A new zone or a fall: no traps carry over, nobody stays hidden, and the hunt is off.</summary>
    private void ClearRogueState()
    {
        _traps.Clear();
        Hero.Prey = null;
        Hero.HiddenRemaining = 0;
        Hero.DodgeFeinted = null;
    }
}
