using AshenReliquary.Core.Abilities;
using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Items;
using AshenReliquary.Core.Rules;

namespace AshenReliquary.Core.World;

/// <summary>
/// Guns (the Gunslinger's, and anyone's who takes one up): Ironhold's firearms hold a few rounds, fire one a Strike,
/// and must be loaded again once they are empty.
/// <list type="bullet">
/// <item>The load is the gun's <see cref="ItemDefinition.Capacity"/>, one more for each refit at the forge, the
/// off-hand pistol's beside it, and feats' rounds. A gun newly taken in hand comes loaded.</item>
/// <item>An empty gun reloads by itself (<see cref="ItemDefinition.ReloadSeconds"/>, half as long again with a second
/// pistol to load, less with feats); R reloads early. The hero can walk while reloading but not fire. Risky Reload and
/// Hit the Dirt! load it in an instant.</item>
/// <item>A natural 1 is a misfire (Pathfinder's): the rest of the load is fouled, and the gun must be loaded again.</item>
/// <item>Two pistols fire a quarter faster than one. The Sniper's Strikes at a target <see cref="DistantTiles"/> off or
/// more deal its distant damage; a silvered round burns the undead.</item>
/// </list>
/// </summary>
public sealed partial class GameWorld
{
    /// <summary>How far a target must be for the Sniper's distant damage.</summary>
    public const double DistantTiles = 6.0;

    /// <summary>How much faster two pistols fire than one.</summary>
    public const double TwinPistolSpeed = 1.25;

    /// <summary>A bullet's speed, as many times an arrow's.</summary>
    public const double BulletSpeedScale = 3.0;

    /// <summary>Whether the hero holds a gun.</summary>
    public bool WieldsFirearm => Hero.Equipment.In(EquipSlot.MainHand) is { Definition.IsFirearm: true };

    /// <summary>A second pistol in the off hand, beside one in the main hand.</summary>
    public bool TwinPistols => WieldsFirearm && Hero.Equipment.In(EquipSlot.OffHand) is { Definition.IsPistol: true };

    /// <summary>How many rounds the gun in hand holds (0 without one).</summary>
    public int Capacity
    {
        get
        {
            if (Hero.Equipment.In(EquipSlot.MainHand) is not { Definition.IsFirearm: true } gun)
                return 0;
            var rounds = gun.Definition.Capacity + gun.Refits + Hero.Character.CapacityBonus;
            if (Hero.Equipment.In(EquipSlot.OffHand) is { Definition.IsPistol: true } second)
                rounds += second.Definition.Capacity + second.Refits;
            return Math.Max(1, rounds);
        }
    }

    /// <summary>How long a reload takes now.</summary>
    public double ReloadSeconds
    {
        get
        {
            if (Hero.Equipment.In(EquipSlot.MainHand) is not { Definition.IsFirearm: true } gun)
                return 0;
            var seconds = gun.Definition.ReloadSeconds * (TwinPistols ? 1.5 : 1.0);
            return seconds * (1 - Math.Clamp(Hero.Character.ReloadSpeedBonus, 0, 0.6)) * Hero.Character.Conditions.ActionTimeMultiplier;
        }
    }

    /// <summary>A reload's progress, 0 to 1, or null when the gun isn't being loaded.</summary>
    public double? ReloadProgress => Hero.ReloadRemaining > 0 && Hero.ReloadTotal > 0 ? 1 - Hero.ReloadRemaining / Hero.ReloadTotal : null;

    /// <summary>Loads the gun early (R). False when there is no gun, it is full, or it is loading already.</summary>
    public bool Reload()
    {
        if (!WieldsFirearm || Hero.IsDead || Hero.ReloadRemaining > 0 || Hero.Loaded >= Capacity)
            return false;
        BeginReload();
        return true;
    }

    private void BeginReload()
    {
        Hero.ReloadTotal = Hero.ReloadRemaining = Math.Max(0.1, ReloadSeconds);
        Float(Hero, "reloading", FloatingTextKind.Info, 0.3);
        Sound(SoundCue.Reload, Hero);
    }

    /// <summary>The gun loads; an empty one starts to by itself; a gun newly in hand comes loaded.</summary>
    private void UpdateReload(double dt)
    {
        var gun = Hero.Equipment.In(EquipSlot.MainHand) is { Definition.IsFirearm: true } held ? held : null;
        if (gun != Hero.LoadedGun)
        {
            Hero.LoadedGun = gun;
            Hero.Loaded = gun is null ? 0 : Capacity;
            Hero.ReloadRemaining = 0;
        }
        if (gun is null)
            return;
        Hero.Loaded = Math.Min(Hero.Loaded, Capacity);
        if (Hero.ReloadRemaining > 0)
        {
            Hero.ReloadRemaining -= dt;
            if (Hero.ReloadRemaining <= 0)
            {
                Hero.ReloadRemaining = 0;
                Hero.Loaded = Capacity;
            }
            return;
        }
        if (Hero.Loaded <= 0 && Hero.Action is null)
            BeginReload();
    }

    /// <summary>Whether a weapon ability must wait on the gun: empty, or being loaded (one that reloads never waits).</summary>
    private bool HoldsFire(AbilityDefinition ability) =>
        ability.UsesWeapon && WieldsFirearm && !ability.Reloads && (Hero.ReloadRemaining > 0 || Hero.Loaded <= 0);

    /// <summary>
    /// A gun ability starting: loaded first if it reloads, then the rounds it fires taken from the load (every round
    /// for Fan the Hammer). Returns how many it fires.
    /// </summary>
    private int FireRounds(AbilityDefinition ability)
    {
        if (ability.Reloads)
        {
            Hero.Loaded = Capacity;
            Hero.ReloadRemaining = 0;
        }
        var shots = ability.SpendLoad ? Math.Max(1, Hero.Loaded) : ability.StrikeCount;
        shots = Math.Max(1, Math.Min(shots, Math.Max(1, Hero.Loaded)));
        Hero.Loaded = Math.Max(0, Hero.Loaded - shots);
        return shots;
    }

    /// <summary>
    /// A gun goes off: its shots fly (a spread finds more enemies within reach and its arc), with a flash at the
    /// muzzle and the report.
    /// </summary>
    private void FireGun(AbilityDefinition ability, MonsterActor target, int dice, int shots, bool fromHiding)
    {
        var rank = AbilityDefinition.SpellRank(Hero.Character.Level);
        var targets = new List<MonsterActor> { target };
        if (ability.Targets > 1)
            targets.AddRange(_monsters.Where(m => !m.IsDead && m != target && !m.Definition.IsSpawner && IsLit(m.X, m.Y)
                    && Hero.DistanceTo(m) <= ReachFor(ability, m) + 0.3 && WithinArc(ability.Arc, target, m)
                    && Map.HasLineOfSight(Hero.X, Hero.Y, m.X, m.Y))
                .OrderBy(m => Hero.DistanceTo(m)).Take(ability.Targets - 1));
        var first = true;
        foreach (var at in targets)
        {
            for (var i = 0; i < (at == target ? shots : 1); i++)
            {
                _projectiles.Add(new Projectile(ability, at, rank, Hero.X, Hero.Y, Time, at == target ? i : 0)
                {
                    Shot = new ArrowShot(dice, ability.CadenceAttacks, first, fromHiding && first, ability.Feint, ability.Pierce + Hero.Character.ArrowPierce)
                    {
                        Bullet = true,
                    },
                });
                first = false;
            }
        }
        var d = Math.Max(1e-6, Hero.DistanceTo(target));
        AddImpact(Hero.X + (target.X - Hero.X) / d * 0.6, Hero.Y + (target.Y - Hero.Y) / d * 0.6, DamageType.Fire);
        Sound(SoundCue.Gunshot, Hero, Hero.Equipment.In(EquipSlot.MainHand) is { Definition.IsTwoHanded: true } ? "heavy" : null);
    }

    /// <summary>
    /// What a gun's hit adds: the Sniper's distant damage, a silvered round's vitality against the undead (doubled on a
    /// critical hit), and a round put back on a critical hit with Crit Reload.
    /// </summary>
    private int GunStrikeExtra(AbilityDefinition? ability, MonsterActor target, bool critical)
    {
        if (!WieldsFirearm || !Shoots)
            return 0;
        var c = Hero.Character;
        var extra = 0;
        if (c.DistantDamage > 0 && Hero.DistanceTo(target) >= DistantTiles)
            extra += c.DistantDamage;
        if (ability is { Round: true, Holy: true, Dice: { } dice } && target.Definition.Undead)
            extra += DamageCalculator.ApplyDefenses(Dice.Parse(dice).Roll(Random) * (critical ? 2 : 1), DamageType.Vitality, target.Creature.Defenses);
        if (critical && c.CritReload && Hero.Loaded < Capacity && Hero.ReloadRemaining <= 0)
        {
            Hero.Loaded++;
            Float(Hero, "+1 round", FloatingTextKind.Info, 0.5);
        }
        return extra;
    }

    /// <summary>A gun's natural 1: a misfire fouls the rest of the load.</summary>
    private void Misfire()
    {
        if (!WieldsFirearm || !Shoots)
            return;
        Hero.Loaded = 0;
        Float(Hero, "misfire", FloatingTextKind.Miss, 0.4);
        Sound(SoundCue.Misfire, Hero);
    }
}
