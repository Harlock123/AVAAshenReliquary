using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Creatures;
using AshenReliquary.Core.Items;

namespace AshenReliquary.Core.World;

/// <summary>
/// How the computer plays a fight: the balance bots and the "--battle" demo. It plays plainly, never kiting, so its
/// results are a worst case. The fighter raises a shield as enemies close, charges from afar, cleaves a crowd, spends a
/// full head of Fury on Finishing Blow and otherwise Power Attacks; the wizard keeps a Shield up when something is
/// close and casts Force Bolt whenever it can. Both drink an antidote when poisoned (<see cref="Upkeep"/>, between
/// fights too) and throw a bomb at a champion.
/// </summary>
public static class Tactics
{
    /// <summary>What to see to between fights as well as in them: poison gets an antidote.</summary>
    public static void Upkeep(GameWorld world)
    {
        if (world.Hero.Character.Persistent.DiceFor(DamageType.Poison) is not null && Belt(world, "antidote") is { } antidote)
            world.UseBelt(antidote, null);
    }

    public static void Act(GameWorld world, string classId, MonsterActor target)
    {
        var hero = world.Hero;
        var resource = hero.Resource;
        bool Ready(string slot) => world.Bar.TryGetValue(slot, out var a) && resource.CanSpend(a.Cost) && world.CooldownRemaining(a) <= 0;
        var distance = target.DistanceTo(hero);

        if (hero.Character.Persistent.DiceFor(DamageType.Poison) is not null && Belt(world, "antidote") is not null)
        {
            Upkeep(world);
            return;
        }
        if (target.Creature.Stats.Rank >= CreatureRank.Champion && distance < 6 && !hero.IsSwinging
            && Belt(world, "alchemists_fire", "frost_vial", "bottled_lightning", "acid_flask") is { } bomb)
        {
            world.UseBelt(bomb, target);
            return;
        }

        if (classId == "fighter")
        {
            var close = world.Monsters.Count(m => !m.IsDead && m.DistanceTo(hero) < 1.8);
            if (hero.Ward is null && distance < 3 && Ready("3") && hero.Equipment.In(EquipSlot.OffHand) is { Definition.IsShield: true })
                world.UseSlot("3", null);
            if (Ready("1") && distance > 2.5)
                world.UseSlot("1", target);
            else if (Ready("4") && resource.Current >= 75 && distance < 1.6)
                world.UseSlot("4", target);
            else if (Ready("2") && close >= 2 && distance < 1.6)
                world.UseSlot("2", target);
            else if (Ready("rmb") && distance < 1.6)
                world.UseSlot("rmb", target);
            else if (hero.CurrentTarget != target)
                world.Attack(target);
        }
        else
        {
            if (hero.Ward is null && Ready("2") && world.Monsters.Any(m => !m.IsDead && m.DistanceTo(hero) < 2.5))
                world.UseSlot("2", null);
            if (Ready("rmb"))
                world.UseSlot("rmb", target);
            else if (hero.CurrentTarget != target)
                world.Attack(target);
        }
    }

    /// <summary>The belt slot holding the first of these items that is there, or null.</summary>
    private static int? Belt(GameWorld world, params string[] ids)
    {
        foreach (var id in ids)
        {
            for (var i = 0; i < GameWorld.BeltSize; i++)
            {
                if (world.Belt[i]?.Id == id && world.BeltCount(i) > 0)
                    return i;
            }
        }
        return null;
    }
}
