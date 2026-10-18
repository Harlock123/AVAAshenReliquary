using AshenReliquary.Core.Content;
using AshenReliquary.Core.Items;
using AshenReliquary.Core.Saving;
using AshenReliquary.Core.World;

namespace AshenReliquary.Core.Tests;

/// <summary>Act 1's three side stories (M2 step 9), played through the dialogue system as a player would.</summary>
public class SideStoryTests
{
    private static ContentDatabase Content => ContentDatabase.Embedded;

    private static GameWorld In(string zoneId, int seed = 3) =>
        new(Content.Zone(zoneId), Content.Creatures, TestCharacters.Fighter(), seed, Content.AbilityBar("fighter"),
            Content.Loot, Content.Zones, Content.Dialogues, Content.Abilities);

    private static void Run(GameWorld world, double seconds)
    {
        for (var t = 0.0; t < seconds; t += 1 / 30.0)
            world.Update(1 / 30.0);
    }

    /// <summary>Walks up to someone or something and opens its conversation.</summary>
    private static void Talk(GameWorld world, string id)
    {
        world.EndDialogue();
        var who = world.Interactives.Single(i => i.Id == id);
        (world.Hero.X, world.Hero.Y) = FreeBeside(world, who.X, who.Y);
        world.Interact(who);
        Run(world, 3);
        Assert.NotNull(world.Dialogue);
    }

    private static (double, double) FreeBeside(GameWorld world, double x, double y)
    {
        foreach (var (dx, dy) in new[] { (0, 1.2), (1.2, 0), (0, -1.2), (-1.2, 0), (1.2, 1.2) })
            if (world.Map.IsWalkable(x + dx, y + dy))
                return (x + dx, y + dy);
        throw new InvalidOperationException("nowhere to stand");
    }

    /// <summary>Picks the answer whose text contains <paramref name="words"/>.</summary>
    private static void Say(GameWorld world, string words)
    {
        var choices = world.Dialogue!.Choices;
        var index = choices.ToList().FindIndex(c => c.Text.Contains(words));
        Assert.True(index >= 0, $"no answer with \"{words}\" among: {string.Join(" | ", choices.Select(c => c.Text))}");
        world.Choose(index);
    }

    /// <summary>Kills whatever stands near someone, as a player clears a yard before talking.</summary>
    private static void ClearAround(GameWorld world, string id)
    {
        var who = world.Interactives.Single(i => i.Id == id);
        foreach (var m in world.Monsters.Where(m => !m.IsDead && m.DistanceTo(who.X, who.Y) < 14).ToList())
            world.Slay(m);
    }

    private static bool Has(GameWorld world, string itemId) => world.Hero.Equipment.Bag.Any(i => i?.Definition.Id == itemId);

    // ----- The Well-Keeper's Daughter -------------------------------------------------

    [Fact]
    public void Marens_letter_comes_out_of_the_well_with_an_athletics_check()
    {
        var world = In("fort_gallowmere");
        for (var tries = 0; tries < 40 && !world.Flags.ContainsKey("act1.letter"); tries++)
        {
            Talk(world, "well");
            Say(world, "Climb down");
        }
        Assert.Equal("found", world.Flags["act1.letter"]);
        Assert.Contains(world.Journal, e => e.Id == "act1_well");
        Talk(world, "well");
        Assert.Contains("letter is in your pack", world.Dialogue!.Text);
    }

    [Fact]
    public void Sallows_hearth_holds_her_cinder_relic_once_she_is_dead_and_smashing_it_is_a_boon()
    {
        var world = In("sallows_mire");
        world.SetFlag("act1.letter", "found");
        Talk(world, "sallows_hearth");
        Assert.Contains("not while she lives", world.Dialogue!.Text);

        world.Slay(world.Monsters.Single(m => m.Definition.Id == "mother_sallow"));
        var will = world.Hero.Character.Save(Rules.StatisticKind.Will).Total;
        var xp = world.Experience.Xp;
        Talk(world, "sallows_hearth");
        Say(world, "Lift the hearthstone");
        Assert.Contains("Cinder Relic", world.Dialogue!.Text);
        Say(world, "Smash it");
        Assert.Equal("destroyed", world.Flags["act1.sallow_relic"]);
        Assert.Contains(world.Hero.Character.Feats, f => f.Id == "ember_breaker");
        Assert.Equal(will + 1, world.Hero.Character.Save(Rules.StatisticKind.Will).Total);
        Assert.True(world.Experience.Xp >= xp + 160 || world.Hero.Character.Level > 1);
        Talk(world, "sallows_hearth");
        Assert.Contains("Nothing beats", world.Dialogue!.Text);
    }

    [Fact]
    public void Carried_to_Ysolde_the_relic_becomes_a_cure_and_her_phial()
    {
        var world = In("sallows_mire");
        world.Slay(world.Monsters.Single(m => m.Definition.Id == "mother_sallow"));
        world.SetFlag("act1.letter", "found");
        Talk(world, "sallows_hearth");
        Say(world, "Lift the hearthstone");
        Say(world, "take it to Ysolde");
        Assert.True(Has(world, "cinder_relic"));
        Assert.False(world.DropFromBag(world.Hero.Equipment.Bag.ToList().FindIndex(i => i?.Definition.Id == "cinder_relic")));

        // Off to the fort, the relic and the flags travelling with the hero.
        var save = SaveGame.Capture(world, "s", 3, null);
        var fort = In("fort_gallowmere");
        fort.RestoreStory(save.Flags, []);
        fort.Hero.Equipment.TryAdd(new Item(Content.Item("cinder_relic"), Rarity.Common, 1));
        Talk(fort, "ysolde");
        Say(fort, "under Mother Sallow's hearth");
        Say(fort, "Make your cure");
        Assert.Equal("cured", fort.Flags["act1.sallow_relic"]);
        Assert.False(Has(fort, "cinder_relic"));
        var phial = fort.Hero.Equipment.Bag.OfType<Item>().Single(i => i.Definition.Id == "ysoldes_phial");
        Assert.Equal(Rarity.Unique, phial.Rarity);
        Assert.Equal("Ysolde's Phial", phial.Name);
        Assert.Contains(fort.Journal, e => e.Id == "act1_well" && e.Text.Contains("cure"));
    }

    // ----- Brothers of the Ferry --------------------------------------------------------

    [Fact]
    public void Coll_sends_the_hero_to_clear_the_cult_camp_and_the_brothers_reward_it()
    {
        var fort = In("fort_gallowmere");
        fort.SetFlag("act1.notice", "seen");
        fort.SetFlag("act1.coll", "met");
        fort.SetFlag("act1.coll_message", "asked");
        Talk(fort, "coll");
        Say(fort, "reason to trust you");
        Assert.Equal("job", fort.Flags["act1.ferry"]);
        Assert.Contains(fort.Journal, e => e.Id == "act1_ferry");

        // The camp: the drowned village's champion pack in the Fens; its fall sets the flag.
        var fens = In("rotwater_fens");
        var camp = Content.Zone("rotwater_fens").Spawns.Single(s => s.Flag == "act1.village");
        var packId = Content.Zone("rotwater_fens").Spawns.ToList().IndexOf(camp) + 1;
        var members = fens.Monsters.Where(m => m.PackId == packId).ToList();
        Assert.Equal(3, members.Count);
        foreach (var m in members.Take(2))
            fens.Slay(m);
        Assert.False(fens.Flags.ContainsKey("act1.village"));
        fens.Slay(members[2]);
        Assert.Equal("cleared", fens.Flags["act1.village"]);

        fort.SetFlag("act1.village", "cleared");
        var gold = fort.Hero.Equipment.Gold;
        Talk(fort, "coll");
        Say(fort, "village is clear");
        Say(fort, "with the Brothers");
        Assert.Equal("brothers", fort.Flags["act1.ferry"]);
        Assert.Equal(gold + 120, fort.Hero.Equipment.Gold);
        Assert.Equal(Rarity.Unique, fort.Hero.Equipment.Bag.OfType<Item>().Single(i => i.Definition.Id == "ferrymans_knot").Rarity);

        Talk(fort, "anselm");
        Assert.Contains("smugglers", fort.Dialogue!.Text);                      // the Lantern's way is closed now
    }

    [Fact]
    public void Brother_Anselm_takes_the_lantern_side_instead_and_coll_spurns_the_hero()
    {
        var fort = In("fort_gallowmere");
        fort.SetFlag("act1.ferry", "job");
        fort.SetFlag("act1.village", "cleared");
        Talk(fort, "anselm");
        Say(fort, "Tell me about the Brothers");
        Say(fort, "Tell him");
        Assert.Equal("lantern", fort.Flags["act1.ferry"]);
        Assert.Contains(fort.Hero.Character.Feats, f => f.Id == "lantern_ward");
        Assert.Equal(2, fort.Hero.Character.Defenses.Resistances[Combat.DamageType.Void]);
        Talk(fort, "coll");
        Assert.Contains("Lantern's dog", fort.Dialogue!.Text);
    }

    // ----- The Scarecrow Who Wept --------------------------------------------------------

    [Fact]
    public void The_scarecrow_weeps_on_the_third_visit_once_the_chapel_is_cleansed()
    {
        var world = In("gallows_road");
        ClearAround(world, "scarecrow");
        Talk(world, "scarecrow");
        Assert.Contains("button eyes are wet", world.Dialogue!.Text);
        Talk(world, "scarecrow");
        Assert.Contains("held breath", world.Dialogue!.Text);
        Talk(world, "scarecrow");
        Assert.Contains("waiting", world.Dialogue!.Text);                       // the chapel isn't cleansed yet
        Assert.DoesNotContain(world.Journal, e => e.Id == "act1_scarecrow");    // hidden until it weeps

        world.SetFlag("act1.chapel", "cleared");
        Talk(world, "scarecrow");
        Assert.Contains("weeps", world.Dialogue!.Text);
        Say(world, "Take the soul-gem");
        Assert.Equal("taken", world.Flags["act1.scarecrow"]);
        Assert.Equal("The Weeping Soul-gem", world.Hero.Equipment.Bag.OfType<Item>().Single(i => i.Definition.Unique).Name);
        Assert.Contains(world.Journal, e => e.Id == "act1_scarecrow");
    }

    [Fact]
    public void Freeing_the_scarecrow_is_a_boon_that_outlives_a_save_and_a_respec()
    {
        var world = In("gallows_road");
        ClearAround(world, "scarecrow");
        world.SetFlag("act1.scarecrow", "2");
        world.SetFlag("act1.chapel", "cleared");
        var speed = world.Hero.Character.SpeedFeet;
        Talk(world, "scarecrow");
        Say(world, "Let it go");
        Assert.Equal(speed + 5, world.Hero.Character.SpeedFeet);

        var loaded = SaveGame.FromJson(SaveGame.Capture(world, "s", 3, null).ToJson()).Restore(Content);
        Assert.Contains(loaded.Hero.Character.Feats, f => f.Id == "scarecrows_peace");
        Assert.Equal(speed + 5, loaded.Hero.Character.SpeedFeet);

        loaded.Hero.Character.Respec();
        loaded.ApplyBoons();
        Assert.Contains(loaded.Hero.Character.Feats, f => f.Id == "scarecrows_peace");
    }

    [Fact]
    public void The_chapels_sanctum_cleansed_sets_the_flag_the_scarecrow_waits_for()
    {
        var chapel = In("drowned_chapel");
        var zone = Content.Zone("drowned_chapel");
        var sanctum = zone.Spawns.Single(s => s.Flag == "act1.chapel");
        var packId = zone.Spawns.ToList().IndexOf(sanctum) + 1;
        foreach (var m in chapel.Monsters.Where(m => m.PackId == packId).ToList())
            chapel.Slay(m);
        Assert.Equal("cleared", chapel.Flags["act1.chapel"]);
    }

    [Fact]
    public void Uniques_and_key_items_never_roll_as_loot_and_every_story_item_exists()
    {
        foreach (var id in new[] { "cinder_relic", "ysoldes_phial", "ferrymans_knot", "weeping_soulgem", "pilgrims_lantern" })
            Assert.True(Content.Items.ContainsKey(id), id);
        var random = new Rules.SeededRandom(9);
        for (var i = 0; i < 2000; i++)
        {
            var item = Content.Loot.Generate(5, Rarity.Rare, random);
            Assert.False(item.Definition.Unique || item.Definition.Category == ItemCategory.Key, item.Definition.Id);
        }
        Assert.All(new[] { "ember_breaker", "lantern_ward", "scarecrows_peace" },
            id => Assert.Equal(Characters.FeatType.Boon, Content.Feats[id].Type));
    }
}
