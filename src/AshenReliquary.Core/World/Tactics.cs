using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Creatures;
using AshenReliquary.Core.Items;

namespace AshenReliquary.Core.World;

/// <summary>
/// How the computer plays a fight: the balance bots and the "--battle" demo. It plays plainly, never kiting, so its
/// results are a worst case. It finds abilities by id wherever they sit on the bar (the player can move them). The
/// fighter raises a shield as enemies close, charges from afar, cleaves a crowd, spends a full head of Fury on Finishing
/// Blow and otherwise Power Attacks; the wizard keeps a Shield up when something is close and casts Force Bolt whenever
/// it can; the rogue hides before an unaware pack and opens from the shadows, rolls away and hides when badly hurt, tumbles in from afar (a Trickster
/// through a champion or boss, to leave it off-guard), opens a wound or sets a snare, spends a full head of Momentum on Eviscerate and otherwise Sneak Strikes. All drink an antidote when poisoned
/// (<see cref="Upkeep"/>, between fights too) and throw a bomb at a champion. Subclass abilities go off against a crowd:
/// a Vanguard's Earthshaker, a Bulwark's Challenge, a Pyromancer's Burning Hands, a Stormcaller's Thunderclap. The ranger
/// hunts its target, springs away from anything in melee (the only bot that keeps its distance), sends its wolf, and
/// spends Focus on Piercing Shot into a crowd, Aimed Shot or Twin Shot. The casters (wizard and necromancer) Step clear of
/// anything in melee when the Step is ready. The champion keeps Shield of Faith up in a fight,
/// lays on hands when hurt, raises its subclass's aura with enemies close (the Oathbreaker's too when its Taint runs
/// high, to spend it before the whispers), brings the Paladin's Blade of Justice down on anything tough, and Smites with
/// what Devotion is left over. The necromancer keeps its
/// skeletons raised, puts on Bone Armor with something close, blasts a crowd with Corpse Explosion, rushes its bones at
/// a champion, siphons life when hurt and otherwise casts Grave Bolt. The gunslinger keeps its distance as the ranger
/// does (Hit the Dirt! out of melee, loading the gun as it lands), fires Risky Reload when its gun is empty, Headshots
/// anything tough, fans the hammer with two rounds or more loaded, scatters shot into a crowd and Crack Shots with Grit
/// to spare. Every class goes for the Choir's true voice once it is heard among her reflections.
/// </summary>
public static class Tactics
{
    /// <summary>What to see to between fights as well as in them: poison gets an antidote.</summary>
    public static void Upkeep(GameWorld world)
    {
        if (world.Hero.Character.Persistent.DiceFor(DamageType.Poison) is not null && Belt(world, "antidote") is { } antidote)
            world.UseBelt(antidote, null);
    }

    /// <summary>How many tries a bot gives a trap before it walks round it.</summary>
    public const int DisarmTries = 3;

    /// <summary>
    /// Between fights: smash a rising corpse near at hand before it stands, and, trained in Thievery, disarm a spotted trap
    /// (three tries). Never with a foe within 3 tiles. True when it gave an order (the caller then lets it play out).
    /// </summary>
    public static bool Tidy(GameWorld world)
    {
        var hero = world.Hero;
        if (hero.IsSwinging || hero.CorpseTarget is not null || hero.TrapTarget is not null)
            return hero.CorpseTarget is not null || hero.TrapTarget is not null;
        if (world.Monsters.Any(m => !m.IsDead && m.DistanceTo(hero) < 3))
            return false;
        if (world.RisingCorpses.Where(c => c.DistanceTo(hero) < 8).MinBy(c => c.DistanceTo(hero)) is { } corpse)
        {
            world.SmashCorpse(corpse);
            return true;
        }
        if (hero.Character.SkillRank("Thievery") > Rules.ProficiencyRank.Untrained
            && world.ZoneTraps.Where(t => t.State == TrapState.Found && t.Attempts < DisarmTries && t.DistanceTo(hero.X, hero.Y) < 6)
                .MinBy(t => t.DistanceTo(hero.X, hero.Y)) is { } trap)
        {
            world.Disarm(trap);
            return true;
        }
        return false;
    }

    /// <summary>
    /// In a cold zone, between fights: past half exposure (a third in a blizzard) walk to the nearest campfire, and wait
    /// by it until warm and the blizzard has blown over, as the hermit of the pass advises. True while it is doing so.
    /// </summary>
    public static bool WarmUp(GameWorld world)
    {
        var hero = world.Hero;
        if (!world.Zone.Cold || hero.IsDead)
            return false;
        var fire = world.Map.Lights.Where(l => l.Color is null).MinBy(l => hero.DistanceTo(l.X, l.Y));
        if (fire is null)
            return false;
        var by = hero.DistanceTo(fire.X, fire.Y) <= fire.Radius * 0.8 - 0.3;
        if (by)
            return hero.Cold > 2 || world.Blizzard;
        if (hero.Cold < (world.Blizzard ? 33 : 50) || world.Monsters.Any(m => !m.IsDead && m.State != MonsterState.Idle && m.DistanceTo(hero) < 6))
            return false;
        // The open spot round the fire nearest the hero: the one on its own side may be a rock or an ice spire, and a walk
        // to it went nowhere (a frozen gunslinger once waited there for an hour).
        var spots = Enumerable.Range(0, 16).Select(i => i * Math.PI / 8)
            .SelectMany(a => new[] { 1.6, 1.1, 2.0 }.Select(r => (X: fire.X + Math.Cos(a) * r, Y: fire.Y + Math.Sin(a) * r)))
            .Where(p => world.Map.IsWalkable(p.X, p.Y))
            .ToList();
        if (spots.Count == 0)
            return false;
        var (x, y) = spots.MinBy(p => hero.DistanceTo(p.X, p.Y));
        if (hero.Path is not [.., var end] || Math.Abs(end.X - x) + Math.Abs(end.Y - y) > 2)
            world.MoveTo(x, y);
        return true;
    }

    public static void Act(GameWorld world, string classId, MonsterActor target)
    {
        var hero = world.Hero;
        // Among the Choir's reflections, the true voice once heard (anything else may be a reflection).
        if (world.Monsters.FirstOrDefault(m => !m.IsDead && world.Time < m.TrueVoiceUntil) is { } voice)
            target = voice;
        var resource = hero.Resource;
        // Abilities by id, on the bar or waiting in the list (a player would put them on the bar).
        bool Ready(string id, out string slot)
        {
            slot = id;
            return world.KnownAbilities.FirstOrDefault(k => k.Id == id) is { } a && world.CanAfford(a) && world.CooldownRemaining(a) <= 0;
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
        // What a multiclass dedication grants, used by any class: Lay on Hands when hurt, Hunt Prey on what it fights,
        // Power Attack in reach. (The champion's own branch uses Lay on Hands as its class does.)
        if (classId != "champion" && hero.Character.HitPoints < hero.Character.MaxHitPoints * 0.5 && Ready("lay_on_hands", out slot))
        {
            world.UseAbility(slot, null);
            return;
        }
        if (classId != "ranger" && hero.Prey != target && target.Creature.Stats.Rank >= CreatureRank.Normal && Ready("hunt_prey", out slot))
            world.UseAbility(slot, target);
        if (classId != "fighter" && distance < 1.6 && !hero.IsSwinging && Ready("power_attack", out slot))
        {
            world.UseAbility(slot, target);
            return;
        }
        // A caster steps clear of anything in melee (the Step is free, on its cooldown) and goes on casting from there.
        if (classId is "wizard" or "necromancer" && world.DodgeCooldownRemaining <= 0 && !hero.IsSwinging
            && world.Monsters.Where(m => !m.IsDead && !m.Definition.Ranged && m.DistanceTo(hero) < 1.6).MinBy(m => m.DistanceTo(hero)) is { } melee
            && melee.DistanceTo(hero) > 0.01)
        {
            var away = melee.DistanceTo(hero);
            world.Dodge(hero.X + (hero.X - melee.X) / away * 4, hero.Y + (hero.Y - melee.Y) / away * 4);
            return;
        }
        // Past level 5 (M4): each class's level-6 and level-11 abilities and its subclass's level-8 and level-13
        // signatures, by id, the bigger first.
        int Near(MonsterActor at, double tiles) => world.Monsters.Count(m => !m.IsDead && m.DistanceTo(at) <= tiles + m.Radius);
        var tough = target.Creature.Stats.Rank >= CreatureRank.Champion;
        var health = hero.Character.HitPoints / (double)hero.Character.MaxHitPoints;
        bool AuraUp(string id) => world.Auras.Any(a => a.Ability.Id == id && a.EndsAt - world.Time > 1);
        if (!hero.IsSwinging)
        {
            if (classId is "wizard" && world.DodgeCooldownRemaining > 0 && Ready("blink", out slot)
                && world.Monsters.Where(m => !m.IsDead && !m.Definition.Ranged && m.DistanceTo(hero) < 1.6).MinBy(m => m.DistanceTo(hero)) is { } pressing
                && pressing.DistanceTo(hero) > 0.01)
            {
                var away = pressing.DistanceTo(hero);
                world.UseAbility(slot, null, (hero.X + (hero.X - pressing.X) / away * 6, hero.Y + (hero.Y - pressing.Y) / away * 6));
                return;
            }
            (string Id, bool When, MonsterActor? At, (double X, double Y)? Toward)[] signatures =
            [
                ("earthsplitter", Within(3.5) >= 2 && world.WieldsTwoHanded, target, null),
                ("shield_rush", distance > 2.5 && distance < 7 && hero.Equipment.In(EquipSlot.OffHand) is { Definition.IsShield: true }, target, null),
                ("dawnbreak", Within(4) >= 2 || (health < 0.5 && Within(4) >= 1), null, null),
                ("ruinous_blow", distance < 1.8 && (tough || hero.Character.Taint.Value > 60), target, null),
                ("final_judgment", distance < 1.8 && (Within(2.2) >= 2 || tough), target, null),
                ("assassinate", distance < 1.8 && tough, target, null),
                ("caltrops", Within(2.5) >= 2, null, null),
                ("killshot", tough && distance < 10, target, null),
                ("hunt_as_one", tough && world.Allies.Any(a => !a.IsDead), target, null),
                ("meteor", distance < 10 && Near(target, 3.5) >= 2, target, null),
                ("ball_lightning", distance < 9 && Near(target, 2.5) >= 2, target, null),
                ("one_shot_one_kill", tough && distance < 13, target, null),
                ("twin_thunder", tough, target, null),
                ("bone_colossus", (tough || Within(8) >= 3) && world.Allies.All(a => a.IsDead || a.Definition.Id != "bone_colossus"), null, null),
                ("blight", distance < 9 && Near(target, 3) >= 2, target, null),
                ("sundering_blow", distance < 1.8 && tough, target, null),
                ("divine_wrath", Within(3) >= 2, null, null),
                ("hail_of_lead", distance < 10 && Near(target, 2.5) >= 2, target, null),
                ("crippling_strike", distance < 1.8 && target.Creature.Stats.Rank >= CreatureRank.Normal, target, null),
                ("pinning_shot", distance < 10 && distance > 2 && !target.Definition.Ranged, target, null),
                ("cone_of_cold", Within(5) >= 2, target, null),
                ("grasp_of_the_grave", distance < 8 && Near(target, 2.5) >= 2, target, null),
                ("intimidating_roar", Within(3) >= 2, null, null),
                ("whirlwind_strike", close >= 2 && distance < 1.6 && world.WieldsTwoHanded, target, null),
                ("shield_slam", distance < 1.6 && target.Creature.Stats.Rank >= CreatureRank.Normal, target, null),
                ("retributive_strike", distance < 2 && !AuraUp("retributive_strike"), null, null),
                ("beacon_of_hope", health < 0.5 || hero.Character.Taint.Value > 60, null, null),
                ("dread_harvest", distance < 7 && Near(target, 3) >= 2, target, null),
                ("aura_of_justice", distance < 2.5 && !AuraUp("aura_of_justice"), null, null),
                ("dead_mans_hand", Within(6) >= 3, target, null),
                ("vital_shot", target.Creature.Stats.Rank >= CreatureRank.Normal && distance < 12, target, null),
                ("penetrating_shot", distance < 10 && Near(target, 4) >= 2, target, null),
                ("shadowstep", !hero.IsHidden && distance > 3 && distance < 7, target, (target.X, target.Y)),
                ("fan_of_knives", Within(2.8) >= 2, null, null),
                ("smoke_bomb", health < 0.4 && Within(3) >= 1 && !hero.IsHidden, null, null),
                ("rain_of_arrows", distance < 10 && Near(target, 2.5) >= 2, target, null),
                ("deadeye", tough && distance < 10, target, null),
                ("call_of_the_pack", tough || Within(8) >= 3, null, null),
                ("fireball", distance < 10 && Near(target, 2.5) >= 2, target, null),
                ("chain_lightning", distance < 9 && Near(target, 4) >= 2, target, null),
                ("bone_spear", distance < 9 && world.GraveCharges >= 2 && target.Creature.Stats.Rank >= CreatureRank.Normal, target, null),
                ("raise_bone_knight", world.Minions.All(m => m.IsDead || m.Definition.Id != "raised_knight"), null, null),
                ("plague_cloud", distance < 9 && Near(target, 3) >= 2, target, null),
            ];
            foreach (var (id, when, at, toward) in signatures)
            {
                if (when && Ready(id, out slot))
                {
                    world.UseAbility(slot, at, toward);
                    return;
                }
            }
        }
        if (classId == "fighter")
        {
            // The shield goes up as a fight starts and stays up (it costs nothing): a ranged boss never comes close.
            if (hero.Ward is null && distance < 8 && Ready("raise_shield", out slot) && hero.Equipment.In(EquipSlot.OffHand) is { Definition.IsShield: true })
                world.UseAbility(slot, null);
            if (Ready("challenge", out slot) && Within(3) >= 2)
                world.UseAbility(slot, null);
            if (Ready("earthshaker", out slot) && world.WieldsTwoHanded && Within(1.8) >= 2 && !hero.IsSwinging)
                world.UseAbility(slot, null);
            else if (Ready("sudden_charge", out slot) && distance > 2.5)
                world.UseAbility(slot, target);
            else if (Ready("finishing_blow", out slot) && resource.Current >= 75 && distance < 1.6)
                world.UseAbility(slot, target);
            else if (Ready("cleave", out slot) && close >= 2 && distance < 1.6)
                world.UseAbility(slot, target);
            else if (Ready("power_attack", out slot) && distance < 1.6)
                world.UseAbility(slot, target);
            else if (hero.CurrentTarget != target)
                world.Attack(target);
        }
        else if (classId == "rogue")
        {
            // Badly hurt and not at a boss: roll away from the fight and slip into the shadows to shake it off.
            var hurt = hero.Character.HitPoints < hero.Character.MaxHitPoints * 0.3 && target.Definition.Boss is null;
            if (hurt && distance < 2.5 && Ready("tumble", out slot) && distance > 0.01)
            {
                world.UseAbility(slot, null, (hero.X + (hero.X - target.X) / distance * 4, hero.Y + (hero.Y - target.Y) / distance * 4));
                return;
            }
            if (hurt && !hero.IsHidden && distance > GameWorld.HideCloseTiles && Ready("hide", out slot))
            {
                world.UseAbility(slot, null);
                return;
            }
            // An unaware target: into the shadows first, to open with a blow it never sees coming.
            if (!hero.IsHidden && target.State == MonsterState.Idle && distance > 2.5 && Ready("hide", out slot))
                world.UseAbility(slot, null);
            if (Ready("snare", out slot) && Within(2.5) >= 1 && world.Traps.Count == 0 && !hero.IsHidden)
                world.UseAbility(slot, null);
            if (!hero.IsHidden && distance > 3 && distance < 6 && Ready("tumble", out slot))
                world.UseAbility(slot, target, (target.X, target.Y));
            // A Trickster rolls clean through what it fights to leave it off-guard, then strikes its back.
            else if (hero.Character.TumbleFeint && distance < 1.8 && distance > 0.01 && !world.IsOffGuardToHero(target) && Ready("tumble", out slot)
                     && target.Creature.Stats.Rank >= CreatureRank.Champion)
                world.UseAbility(slot, target, (target.X + (target.X - hero.X) / distance * 1.5, target.Y + (target.Y - hero.Y) / distance * 1.5));
            else if (Ready("eviscerate", out slot) && resource.Current >= 70 && distance < 1.6)
                world.UseAbility(slot, target);
            else if (Ready("whirling_blades", out slot) && close >= 3 && distance < 1.6)
                world.UseAbility(slot, target);
            else if (Ready("gutting_strike", out slot) && distance < 1.6 && target.Creature.Persistent.DiceFor(DamageType.Bleed) is null
                     && target.Creature.Stats.Rank >= CreatureRank.Normal)
                world.UseAbility(slot, target);
            // Momentum is saved toward Eviscerate once the rogue has it.
            else if (Ready("twin_feint", out slot) && distance < 1.6 && (resource.Current < 60 || world.SlotOf("eviscerate") is null))
                world.UseAbility(slot, target);
            else if (Ready("sneak_strike", out slot) && distance < 1.6 && (resource.Current < 60 || world.SlotOf("eviscerate") is null)
                     && !world.IsOffGuardToHero(target))
                world.UseAbility(slot, target);
            else if (hero.CurrentTarget != target)
                world.Attack(target);
        }
        else if (classId == "champion")
        {
            bool Up(string id) => world.Auras.Any(a => a.Ability.Id == id && a.EndsAt - world.Time > 1);
            var c = hero.Character;
            if (c.HitPoints < c.MaxHitPoints * 0.55 && Ready("lay_on_hands", out slot))
                world.UseAbility(slot, null);
            else if (!Up("shield_of_faith") && distance < 4 && Ready("shield_of_faith", out slot))
                world.UseAbility(slot, null);
            else if (!Up("lanterns_radiance") && Within(2.4) >= 1 && Ready("lanterns_radiance", out slot))
                world.UseAbility(slot, null);
            else if ((!Up("aura_of_dread") && Within(2.8) >= 1 || c.Taint.Value >= 80) && Ready("aura_of_dread", out slot))
                world.UseAbility(slot, null);
            // The Paladin's Blade of Justice on anything tough, or with Devotion to spare.
            else if (Ready("blade_of_justice", out slot) && distance < 1.6 && (tough || resource.Current >= 45))
                world.UseAbility(slot, target);
            // Devotion is kept for Lay on Hands and the auras; what is over goes on Smite.
            else if (Ready("smite", out slot) && distance < 1.6 && resource.Current >= 50)
                world.UseAbility(slot, target);
            else if (hero.CurrentTarget != target)
                world.Attack(target);
        }
        else if (classId == "necromancer")
        {
            var standing = world.Minions.Count(m => !m.IsDead);
            var c = hero.Character;
            if (standing < world.MinionCap && Ready("raise_bones", out slot))
                world.UseAbility(slot, null);
            else if (hero.Ward is null && Ready("bone_armor", out slot) && world.Monsters.Any(m => !m.IsDead && m.DistanceTo(hero) < 3))
                world.UseAbility(slot, null);
            else if (Ready("corpse_explosion", out slot) && world.Monsters.Count(m => !m.IsDead && m.DistanceTo(target) < 2.2) >= 2)
                world.UseAbility(slot, target);
            else if (Ready("bone_rush", out slot) && standing >= 2 && target.Creature.Stats.Rank >= CreatureRank.Champion)
                world.UseAbility(slot, target);
            else if (Ready("siphon_life", out slot) && c.HitPoints < c.MaxHitPoints * 0.7)
                world.UseAbility(slot, target);
            else if (hero.CurrentTarget != target)
                world.Attack(target);
        }
        else if (classId == "gunslinger")
        {
            // Keep the gun's distance as the ranger does, and load it on the way out of trouble.
            var pressing = world.Monsters.Where(m => !m.IsDead && m.DistanceTo(hero) < 1.8).MinBy(m => m.DistanceTo(hero));
            if (pressing is not null && world.Shoots && Ready("hit_the_dirt", out slot))
            {
                var d = Math.Max(0.01, pressing.DistanceTo(hero));
                world.UseAbility(slot, null, (hero.X + (hero.X - pressing.X) / d * 4, hero.Y + (hero.Y - pressing.Y) / d * 4));
                return;
            }
            if (hero.Loaded <= 0 && Ready("risky_reload", out slot))
                world.UseAbility(slot, target);
            else if (Ready("headshot", out slot) && (tough || resource.Current >= 40))
                world.UseAbility(slot, target);
            else if (Ready("fan_the_hammer", out slot) && hero.Loaded >= 2)
                world.UseAbility(slot, target);
            else if (Ready("scattershot", out slot) && Near(target, 2.5) >= 2)
                world.UseAbility(slot, target);
            else if (Ready("crack_shot", out slot) && resource.Current >= 30)
                world.UseAbility(slot, target);
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
                world.UseAbility(slot, null, (hero.X + (hero.X - pressing.X) / d * 4, hero.Y + (hero.Y - pressing.Y) / d * 4));
                return;
            }
            if (hero.Prey != target && Ready("hunt_prey", out slot) && target.Creature.Stats.Rank >= CreatureRank.Normal)
                world.UseAbility(slot, target);
            if (Ready("sic", out slot) && world.Companion is { IsDead: false } && distance < 9)
                world.UseAbility(slot, target);
            if (Ready("piercing_shot", out slot) && world.Monsters.Count(m => !m.IsDead && m.DistanceTo(target) < 3) >= 2)
                world.UseAbility(slot, target);
            else if (Ready("aimed_shot", out slot) && resource.Current >= 30)
                world.UseAbility(slot, target);
            else if (Ready("twin_shot", out slot))
                world.UseAbility(slot, target);
            else if (hero.CurrentTarget != target)
                world.Attack(target);
        }
        else
        {
            if (hero.Ward is null && Ready("shield", out slot) && distance < 8)
                world.UseAbility(slot, null);
            if (Ready("thunderclap", out slot) && Within(2.3) >= 2 && !hero.IsSwinging)
                world.UseAbility(slot, null);
            else if (Ready("burning_hands", out slot) && distance < 2.6
                     && world.Monsters.Count(m => !m.IsDead && m.DistanceTo(target) < 1.8) >= 2)
                world.UseAbility(slot, target);
            else if (Ready("force_bolt", out slot))
                world.UseAbility(slot, target);
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
