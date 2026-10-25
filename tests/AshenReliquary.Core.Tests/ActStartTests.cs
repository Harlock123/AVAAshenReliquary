using AshenReliquary.Core.Content;
using AshenReliquary.Core.Items;
using AshenReliquary.Core.World;

namespace AshenReliquary.Core.Tests;

/// <summary>
/// M4 step 10: a hero can begin at Acts 2, 3 or 4 (for the exit test's play of Acts 2-4): in the act's first zone, at the
/// level the campaign reaches it, with the Heralds before it dead, their hubs' shrines woken, and the kit etched to suit.
/// </summary>
public class ActStartTests
{
    private static ContentDatabase Content => ContentDatabase.Embedded;

    private static GameWorld Begin(int act, string kit = "fighter vanguard")
    {
        var start = GameWorld.ActStarts.Single(a => a.Act == act);
        var hero = TestCharacters.ForKit(kit);
        var world = new GameWorld(Content.Zone(start.Zone), Content.Creatures, hero, 3, Content.AbilityBar(hero.Class.Id), Content.Loot,
            Content.Zones, Content.Dialogues, Content.Abilities);
        BalanceReport.Dress(hero, world.Hero.Equipment, kit);
        world.BeginAct(start);
        return world;
    }

    [Fact]
    public void The_acts_begin_where_their_roads_start_each_a_level_on_from_the_last_herald()
    {
        Assert.Equal([1, 2, 3, 4], GameWorld.ActStarts.Select(a => a.Act));
        Assert.All(GameWorld.ActStarts, a => Assert.True(Content.Zones.ContainsKey(a.Zone), a.Zone));
        Assert.All(GameWorld.ActStarts, a => Assert.All(a.Shrines,
            s => Assert.Contains(Content.Zones.Values.SelectMany(z => z.Shrines), z => z.Id == s)));
        Assert.Equal(["gallows_road", "iron_march", "frostbitten_pass", "ember_gate"], GameWorld.ActStarts.Select(a => a.Zone));
        Assert.True(GameWorld.ActStarts.Zip(GameWorld.ActStarts.Skip(1)).All(p => p.First.Level < p.Second.Level));
    }

    [Theory]
    [InlineData(2, "act1.sallow", 6)]
    [InlineData(3, "act2.hadric", 11)]
    [InlineData(4, "act3.choir", 15)]
    public void A_hero_begun_at_an_act_has_the_acts_before_done_and_its_level_and_kit(int act, string herald, int level)
    {
        var world = Begin(act);
        var c = world.Hero.Character;
        Assert.Equal(level, c.Level);
        Assert.Equal("slain", world.Flag(herald));
        Assert.Equal(c.MaxHitPoints, c.HitPoints);
        Assert.True(c.PendingLevels.Count > 0);                                   // the player makes the level-up picks
        var weapon = world.Hero.Equipment.In(EquipSlot.MainHand)!;
        Assert.Equal(level, weapon.ItemLevel);
        Assert.Equal(Runework.Ceiling(weapon, RuneKind.Potency), weapon.Potency);
        Assert.True(weapon.Potency >= 1);
        Assert.True(world.Hero.Equipment.Gold >= GameWorld.ActStarts.Single(a => a.Act == act).Gold);
        Assert.All(GameWorld.ActStarts.Single(a => a.Act == act).Shrines, s => Assert.Contains(s, world.ActivatedShrines));
    }

    [Fact]
    public void Act_four_begins_at_the_ember_gate_with_the_road_behind_open()
    {
        var world = Begin(4, "gunslinger pistolero");
        Assert.Equal("ember_gate", world.Zone.Id);
        Assert.Contains(world.Interactives, i => i.Id == "corwen");
        var cliff = Content.Zone("bell_tower").Exits.Single(e => e.Id == "cliff_road");
        Assert.All(cliff.Requires!, r => Assert.Equal(r.Value, world.Flag(r.Key)));
        Assert.All(world.Hero.Equipment.Worn.Values, i => Assert.Equal(15, i.ItemLevel));
    }
}
