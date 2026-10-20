using AshenReliquary.Core.Abilities;
using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Creatures;
using AshenReliquary.Core.Rules;

namespace AshenReliquary.Core.World;

/// <summary>
/// The Necromancer's rules (spec, section 7): Grave Charges from the dead and the skeletons it raises on the ally
/// framework.
/// <list type="bullet">
/// <item>Every monster that dies within <see cref="GraveReach"/> tiles of a Necromancer leaves a Grave Charge (more with
/// the Plaguebringer's feature), up to <see cref="MaxGraveCharges"/>; a fall loses them. Abilities spend them as well
/// as Mana, and Raise Bones takes one in place of its Mana when there is one.</item>
/// <item>Raise Bones lifts a skeleton out of the ground beside the hero (<see cref="MinionStats"/>), up to
/// <see cref="MinionCap"/>; at the cap the oldest crumbles to make room. Skeletons fight as the wolf does, but one that
/// falls is gone, and all of them crumble when the hero falls.</item>
/// </list>
/// </summary>
public sealed partial class GameWorld
{
    public const double GraveReach = 8.0;
    public const int BaseGraveCharges = 5;
    public const int BaseMinions = 2;

    /// <summary>The Grave Charges the Necromancer holds.</summary>
    public int GraveCharges { get; private set; }

    /// <summary>The most Grave Charges the hero can hold (none for a class without them).</summary>
    public int MaxGraveCharges => GathersGraveCharges ? BaseGraveCharges + Hero.Character.GraveChargeBonus : 0;

    /// <summary>
    /// How many skeletons the hero can hold at once: 2, a third at level 5, and feats' more; one for a hero dedicated to
    /// the Necromancer.
    /// </summary>
    public int MinionCap => !Hero.Character.Class.GraveCharges ? 1 : BaseMinions + (Hero.Character.Level >= 5 ? 1 : 0) + Hero.Character.MinionBonus;

    /// <summary>
    /// A skeleton's creature at a hero level: AC 13 + level, attack 5 + level, (6 + 5 × level) HP, its dice + half the
    /// level; a step of growth adds half its HP again, 1 to AC and attack, and a die. Frailer than the wolf: they come
    /// in numbers.
    /// </summary>
    public static Creature MinionStats(CreatureDefinition definition, int level, int growth)
    {
        var dice = Dice.Parse(definition.Damage ?? "1d6");
        var stats = new CreatureStats(level, CreatureRank.Minion, level,
            ArmorClass: 13 + level + growth,
            AttackBonus: 5 + level + growth,
            SaveDc: 14 + level,
            HitPoints: (int)Math.Round((6 + 5 * level) * (1 + 0.5 * growth) * definition.HitPointsScale),
            DamageMultiplier: 1.0);
        return new Creature(definition.Name, stats, dice with { Count = dice.Count + growth, Bonus = dice.Bonus + level / 2 },
            definition.DamageType, definition.BuildDefenses(), definition.AttacksPerSecond);
    }

    /// <summary>A monster fell: a Necromancer near enough gathers its Grave Charges.</summary>
    private void GatherGraveCharge(MonsterActor fallen)
    {
        if (MaxGraveCharges <= 0 || Hero.IsDead || Hero.DistanceTo(fallen) > GraveReach || GraveCharges >= MaxGraveCharges)
            return;
        var gained = Math.Min(MaxGraveCharges - GraveCharges, 1 + Hero.Character.ChargesPerCorpseBonus);
        GraveCharges += gained;
        Float(fallen, gained == 1 ? "+1 grave charge" : $"+{gained} grave charges", FloatingTextKind.Info, 0.9);
    }

    /// <summary>
    /// Raise Bones (and the summons like it): a skeleton climbs out of the ground beside the hero; at the cap the oldest
    /// crumbles first. A summon that lasts only a while (<see cref="AbilityDefinition.BuffSeconds"/>: Call of the Pack)
    /// raises its <see cref="AbilityDefinition.Count"/> beside the held ones, outside the cap.
    /// </summary>
    private void RaiseMinion(AbilityDefinition ability)
    {
        if (ability.Creature is not { } id || !_creatures.TryGetValue(id, out var definition))
            return;
        if (!BeginSelf(ability))
            return;
        var timed = ability.BuffSeconds > 0;
        var c = Hero.Character;
        for (var i = 0; i < Math.Max(1, ability.Count); i++)
        {
            var standing = _allies.Where(a => a.IsMinion && !a.IsDead && !a.IsTimed).ToList();
            if (!timed && standing.Count >= MinionCap)
                Crumble(standing[0]);
            var minion = new AllyActor(definition, MinionStats(definition, c.Level, c.MinionGrowth), c.Level, c.MinionGrowth)
            {
                IsMinion = true,
                RaisedAt = Time,
                ExpiresAt = timed ? Time + ability.BuffSeconds : double.PositiveInfinity,
            };
            var (x, y) = OpenNear(Hero.X + i * 0.6, Hero.Y - i * 0.6);
            (minion.X, minion.Y) = (x, y);
            minion.Face(Hero.X, Hero.Y);
            _allies.Add(minion);
            AddImpact(x, y, timed ? DamageType.Force : DamageType.Void);
        }
        var (sx, sy) = (Hero.X, Hero.Y);
        if (!Hero.IsSwinging)
            Hero.Play("cast", 1.5, restart: true);
        Sound(timed ? SoundCue.MonsterAggro : SoundCue.GraveRise, sx, sy, timed ? definition.Voice : "grave");
    }

    /// <summary>A skeleton falls apart (made room for, or its master fell).</summary>
    private void Crumble(AllyActor minion)
    {
        if (minion.IsDead)
            return;
        minion.Creature.TakeDamage(minion.Creature.HitPoints);
        minion.ReturnIn = MinionCrumbleSeconds;
        minion.Target = null;
        minion.Command = null;
        minion.Play("die", restart: true);
        foreach (var m in _monsters.Where(o => o.Victim == minion))
            m.Victim = null;
    }

    /// <summary>A fall: the skeletons crumble and the Grave Charges are lost.</summary>
    private void ClearNecromancerState()
    {
        foreach (var minion in _allies.Where(a => a.IsMinion).ToList())
            Crumble(minion);
        GraveCharges = 0;
    }

    /// <summary>A save's Grave Charges.</summary>
    internal void RestoreGraveCharges(int charges) => GraveCharges = Math.Clamp(charges, 0, MaxGraveCharges);

    /// <summary>Grave Charges an ability needs that the hero doesn't have, or 0.</summary>
    private bool HasCharges(AbilityDefinition ability) => GraveCharges >= ability.Charges;

    private void SpendCharges(AbilityDefinition ability) => GraveCharges -= Math.Min(GraveCharges, ability.Charges);
}
