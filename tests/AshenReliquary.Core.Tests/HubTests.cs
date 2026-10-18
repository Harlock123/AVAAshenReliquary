using AshenReliquary.Core.Content;
using AshenReliquary.Core.Items;
using AshenReliquary.Core.Saving;
using AshenReliquary.Core.World;

namespace AshenReliquary.Core.Tests;

public class HubTests
{
    private static ContentDatabase Content => ContentDatabase.Embedded;

    private static GameWorld Fort(int seed = 3) =>
        new(Content.Zone("fort_gallowmere"), Content.Creatures, TestCharacters.Fighter(), seed, Content.AbilityBar("fighter"),
            Content.Loot, Content.Zones, Content.Dialogues);

    private static void Run(GameWorld world, double seconds)
    {
        for (var t = 0.0; t < seconds; t += 1 / 30.0)
            world.Update(1 / 30.0);
    }

    [Fact]
    public void The_fort_holds_its_people_and_everyone_can_be_walked_to()
    {
        var zone = Content.Zone("fort_gallowmere");
        Assert.Equal(["anselm", "brask", "coll", "ysolde"], (zone.Npcs ?? []).Select(n => n.Id).Order());
        Assert.Equal(["garrow", "wenna"], (zone.Vendors ?? []).Select(v => v.Id).Order());
        Assert.Contains(zone.Interactables ?? [], i => i.Action == "stash");
        Assert.Empty(Content.Zone("gallows_road").Vendors ?? []);  // Wenna moved inside

        var map = new TileMap(zone);
        var from = (zone.PlayerStart[0], zone.PlayerStart[1]);
        var places = (zone.Npcs ?? []).Select(n => (n.Name, n.At))
            .Concat((zone.Vendors ?? []).Select(v => (v.Name, v.At)))
            .Concat((zone.Interactables ?? []).Select(i => (i.Name, i.At)))
            .Concat(zone.Shrines.Select(s => (s.Name, s.At)));
        foreach (var (name, at) in places)
        {
            var near = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }.Select(d => (at[0] + d.Item1, at[1] + d.Item2)).Where(c => !map.IsBlocked(c.Item1, c.Item2));
            Assert.True(near.Any(c => Pathfinder.Find(map, from, c, maxNodes: 5000) is not null), $"{name} can't be reached");
        }
    }

    [Fact]
    public void The_smith_sells_only_arms_and_armour()
    {
        var world = Fort();
        var garrow = world.Vendors.Single(v => v.Definition.Id == "garrow");
        Assert.NotEmpty(garrow.Stock);
        Assert.All(garrow.Stock, i => Assert.True(i.Definition.IsWeapon || i.Definition.Category == ItemCategory.Armor, i.Definition.Id));
    }

    [Fact]
    public void The_stash_takes_and_gives_back_and_keeps_its_contents_through_a_save()
    {
        var world = Fort();
        var gear = world.Hero.Equipment;
        gear.TryAdd(new Item(Content.Item("dagger"), Rarity.Uncommon, 1));
        var bagIndex = gear.Bag.ToList().FindIndex(i => i is not null);
        Assert.False(world.Store(bagIndex));                       // not while the stash is shut

        world.Interact(world.Interactives.Single(i => i.Id == "stash"));
        Run(world, 6);
        Assert.True(world.StashOpen);
        Assert.Null(world.Dialogue);
        Assert.True(world.Store(bagIndex));
        Assert.Null(gear.Bag[bagIndex]);
        Assert.Equal("dagger", world.Stash.Slots[0]!.Definition.Id);

        var loaded = SaveGame.FromJson(SaveGame.Capture(world, "s", 3, null).ToJson()).Restore(Content);
        Assert.Equal("dagger", loaded.Stash.Slots[0]!.Definition.Id);

        Assert.True(world.Retrieve(0));
        Assert.Null(world.Stash.Slots[0]);
        Assert.Contains(gear.Bag, i => i?.Definition.Id == "dagger");

        world.MoveTo(15.5, 5.5);
        Run(world, 6);
        Assert.False(world.StashOpen);                             // walking away closes it
    }

    [Fact]
    public void Coll_only_answers_about_the_message_once_it_has_been_found()
    {
        var world = Fort();
        var coll = world.Interactives.Single(i => i.Id == "coll");
        world.Interact(coll);
        Run(world, 8);
        Assert.DoesNotContain(world.Dialogue!.Choices, c => c.Text.Contains("notice board"));
        world.EndDialogue();

        world.SetFlag("act1.notice", "seen");
        world.Interact(coll);
        Assert.Contains(world.Dialogue!.Choices, c => c.Text.Contains("message on the board"));
    }
}
