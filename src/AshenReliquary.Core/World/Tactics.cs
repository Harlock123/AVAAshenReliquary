using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Creatures;
using AshenReliquary.Core.Items;

namespace AshenReliquary.Core.World;

/// <summary>
/// How the computer plays a fight: the balance bots and the "--battle" demo. It plays plainly, never kiting, so its
/// results are a worst case. It finds abilities by id wherever they sit on the bar (the player can move them). The
/// fighter raises a shield as enemies close, charges from afar, cleaves a crowd, spends a full head of Fury on Finishing
/// Blow and otherwise Power Attacks; the wizard keeps a Shield up when something is close and casts Force Bolt whenever
/// it can; the rogue hides before an unaware pack and opens from the shadows, rolls away and hides when badly hurt, tumbles in from afar, opens a wound or sets a
/// snare, spends a full head of Momentum on Eviscerate and otherwise Sneak Strikes. All drink an antidote when poisoned
/// (<see cref="Upkeep"/>, between fights too) and throw a bomb at a champion. Subclass abilities go off against a crowd:
/// a Vanguard's Earthshaker, a Bulwark's Challenge, a Pyromancer's Burning Hands, a Stormcaller's Thunderclap. The ranger
/// hunts its target, springs away from anything in melee (the only bot that keeps its distance), sends its wolf, and
/// spends Focus on Piercing Shot into a crowd, Aimed Shot or Twin Shot. The champion keeps Shield of Faith up in a fight,
/// lays on hands when hurt, raises its subclass's aura with enemies close (the Oathbreaker's too when its Taint runs
/// high, to spend it before the whispers), and Smites with what Devotion is left over.
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
        bool Ready(string id, out string slot)
        {
            slot = world.SlotOf(id)!;
            return slot is not null && world.Bar.TryGetValue(slot, out var a) && world.CanAfford(a) && world.CooldownRemaining(a) <= 0;
        }
        int Within(double tiles) => world.Monsters.Count(m => !m.IsDead && m.DistanceTo(hero) <= tiles + m.Radius);
        var distance = target.DistanceTo(hero);
        var close = world.Monsters.Count(m => !m.IsDead && m.DistanceTo(hero) < 1.8);

        if (hero.Character.Persistent.DiceFor(DamageType.Poison) is not null && Belt(world, "antidote") is not null)
        {
            Upkeep(world);
            return;
        }
        if (target.Creature.Stats.Rank >= CreatureRank.Champion && distance < 6 && !hero.IsSwinging && !hero.IsHidden
            && Belt(world, "alchemists_fire", "frost_vial", "bottled_lightning", "acid_flask") is { } bomb)
        {
            world.UseBelt(bomb, target);
            return;
        }

        string slot;
        if (classId == "fighter")
        {
            if (hero.Ward is null && distance < 3 && Ready("raise_shield", out slot) && hero.Equipment.In(EquipSlot.OffHand) is { Definition.IsShield: true })
                world.UseSlot(slot, null);
            if (Ready("challenge", out slot) && Within(3) >= 2)
                world.UseSlot(slot, null);
            if (Ready("earthshaker", out slot) && world.WieldsTwoHanded && Within(1.8) >= 2 && !hero.IsSwinging)
                world.UseSlot(slot, null);
            else if (Ready("sudden_charge", out slot) && distance > 2.5)
                world.UseSlot(slot, target);
            else if (Ready("finishing_blow", out slot) && resource.Current >= 75 && distance < 1.6)
                world.UseSlot(slot, target);
            else if (Ready("cleave", out slot) && close >= 2 && distance < 1.6)
                world.UseSlot(slot, target);
            else if (Ready("power_attack", out slot) && distance < 1.6)
                world.UseSlot(slot, target);
            else if (hero.CurrentTarget != target)
                world.Attack(target);
        }
        else if (classId == "rogue")
        {
            // Badly hurt and not at a boss: roll away from the fight and slip into the shadows to shake it off.
            var hurt = hero.Character.HitPoints < hero.Character.MaxHitPoints * 0.3 && target.Definition.Boss is null;
            if (hurt && distance < 2.5 && Ready("tumble", out slot) && distance > 0.01)
            {
                world.UseSlot(slot, null, (hero.X + (hero.X - target.X) / distance * 4, hero.Y + (hero.Y - target.Y) / distance * 4));
                return;
            }
            if (hurt && !hero.IsHidden && distance > GameWorld.HideCloseTiles && Ready("hide", out slot))
            {
                world.UseSlot(slot, null);
                return;
            }
            // An unaware target: into the shadows first, to open with a blow it never sees coming.
            if (!hero.IsHidden && target.State == MonsterState.Idle && distance > 2.5 && Ready("hide", out slot))
                world.UseSlot(slot, null);
            if (Ready("snare", out slot) && Within(2.5) >= 1 && world.Traps.Count == 0 && !hero.IsHidden)
                world.UseSlot(slot, null);
            if (!hero.IsHidden && distance > 3 && distance < 6 && Ready("tumble", out slot))
                world.UseSlot(slot, target, (target.X, target.Y));
            else if (Ready("eviscerate", out slot) && resource.Current >= 70 && distance < 1.6)
                world.UseSlot(slot, target);
            else if (Ready("whirling_blades", out slot) && close >= 3 && distance < 1.6)
                world.UseSlot(slot, target);
            else if (Ready("gutting_strike", out slot) && distance < 1.6 && target.Creature.Persistent.DiceFor(DamageType.Bleed) is null
                     && target.Creature.Stats.Rank >= CreatureRank.Normal)
                world.UseSlot(slot, target);
            // Momentum is saved toward Eviscerate once the rogue has it.
            else if (Ready("twin_feint", out slot) && distance < 1.6 && (resource.Current < 60 || world.SlotOf("eviscerate") is null))
                world.UseSlot(slot, target);
            else if (Ready("sneak_strike", out slot) && distance < 1.6 && (resource.Current < 60 || world.SlotOf("eviscerate") is null)
                     && !world.IsOffGuardToHero(target))
                world.UseSlot(slot, target);
            else if (hero.CurrentTarget != target)
                world.Attack(target);
        }
        else if (classId == "champion")
        {
            bool Up(string id) => world.Auras.Any(a => a.Ability.Id == id && a.EndsAt - world.Time > 1);
            var c = hero.Character;
            if (c.HitPoints < c.MaxHitPoints * 0.55 && Ready("lay_on_hands", out slot))
                world.UseSlot(slot, null);
            else if (!Up("shield_of_faith") && distance < 4 && Ready("shield_of_faith", out slot))
                world.UseSlot(slot, null);
            else if (!Up("lanterns_radiance") && Within(2.4) >= 1 && Ready("lanterns_radiance", out slot))
                world.UseSlot(slot, null);
            else if ((!Up("aura_of_dread") && Within(2.8) >= 1 || c.Taint.Value >= 80) && Ready("aura_of_dread", out slot))
                world.UseSlot(slot, null);
            // Devotion is kept for Lay on Hands and the auras; what is over goes on Smite.
            else if (Ready("smite", out slot) && distance < 1.6 && resource.Current >= 50)
                world.UseSlot(slot, target);
            else if (hero.CurrentTarget != target)
                world.Attack(target);
        }
        else if (classId == "ranger")
        {
            // Keep the bow's distance: spring away from anything in melee, toward open ground behind.
            var pressing = world.Monsters.Where(m => !m.IsDead && m.DistanceTo(hero) < 1.8).MinBy(m => m.DistanceTo(hero));
            if (pressing is not null && world.Shoots && Ready("disengage", out slot))
            {
                var d = Math.Max(0.01, pressing.DistanceTo(hero));
                world.UseSlot(slot, null, (hero.X + (hero.X - pressing.X) / d * 4, hero.Y + (hero.Y - pressing.Y) / d * 4));
                return;
            }
            if (hero.Prey != target && Ready("hunt_prey", out slot) && target.Creature.Stats.Rank >= CreatureRank.Normal)
                world.UseSlot(slot, target);
            if (Ready("sic", out slot) && world.Companion is { IsDead: false } && distance < 9)
                world.UseSlot(slot, target);
            if (Ready("piercing_shot", out slot) && world.Monsters.Count(m => !m.IsDead && m.DistanceTo(target) < 3) >= 2)
                world.UseSlot(slot, target);
            else if (Ready("aimed_shot", out slot) && resource.Current >= 30)
                world.UseSlot(slot, target);
            else if (Ready("twin_shot", out slot))
                world.UseSlot(slot, target);
            else if (hero.CurrentTarget != target)
                world.Attack(target);
        }
        else
        {
            if (hero.Ward is null && Ready("shield", out slot) && world.Monsters.Any(m => !m.IsDead && m.DistanceTo(hero) < 2.5))
                world.UseSlot(slot, null);
            if (Ready("thunderclap", out slot) && Within(2.3) >= 2 && !hero.IsSwinging)
                world.UseSlot(slot, null);
            else if (Ready("burning_hands", out slot) && distance < 2.6
                     && world.Monsters.Count(m => !m.IsDead && m.DistanceTo(target) < 1.8) >= 2)
                world.UseSlot(slot, target);
            else if (Ready("force_bolt", out slot))
                world.UseSlot(slot, target);
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
