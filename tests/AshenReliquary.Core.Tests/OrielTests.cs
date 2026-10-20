using AshenReliquary.Core.Content;
using AshenReliquary.Core.World;

namespace AshenReliquary.Core.Tests;

/// <summary>M4 step 5: Saint Oriel's Rest, Act 3's hub, on the snow-and-ruins tileset.</summary>
public class OrielTests
{
    private static ContentDatabase Content => ContentDatabase.Embedded;

    private static void Run(GameWorld world, double seconds)
    {
        for (var t = 0.0; t < seconds; t += 1 / 30.0)
            world.Update(1 / 30.0);
    }

    private static GameWorld Rest() => new(Content.Zone("saint_oriels_rest"), Content.Creatures, TestCharacters.Fighter(), 3,
        Content.AbilityBar("fighter"), Content.Loot, Content.Zones, Content.Dialogues, Content.Abilities);

    [Fact]
    public void The_rest_is_act_threes_snowy_hub_and_everything_in_it_can_be_walked_to()
    {
        var zone = Content.Zone("saint_oriels_rest");
        Assert.Equal(3, zone.Act);
        Assert.True(zone.Town);
        Assert.True(zone.Outdoors && zone.Snowy);
        var map = new TileMap(zone);
        var from = (zone.PlayerStart[0], zone.PlayerStart[1]);
        (int, int) Near(int[] at) => map.IsBlocked(at[0], at[1])
            ? new[] { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, 1) }.Select(d => (at[0] + d.Item1, at[1] + d.Item2)).First(c => !map.IsBlocked(c.Item1, c.Item2))
            : (at[0], at[1]);
        var places = zone.Shrines.Select(s => (s.Id, s.At))
            .Concat(zone.Exits.Select(e => (e.Id, e.At)))
            .Concat((zone.Npcs ?? []).Select(n => (n.Id, n.At)))
            .Concat((zone.Vendors ?? []).Select(v => (v.Id, v.At)))
            .Concat((zone.Interactables ?? []).Select(i => (i.Id, i.At)));
        foreach (var (name, at) in places)
            Assert.True(Pathfinder.Find(map, from, Near(at), maxNodes: 100000) is not null, $"{name} can't be reached");
        Assert.Equal(["halvard", "mora"], (zone.Vendors ?? []).Select(v => v.Id).Order());
        Assert.Contains(zone.Vendors!, v => v.Runework);
    }

    [Fact]
    public void The_abbess_tells_of_the_choir_and_caddoc_promises_a_confession()
    {
        var world = Rest();
        void Use(string id)
        {
            var who = world.Interactives.Single(i => i.Id == id);
            (world.Hero.X, world.Hero.Y) = (who.X, who.Y + 1.2);
            world.Interact(who);
            Run(world, 1);
        }
        Use("ilse");
        Assert.Equal("met", world.Flags["act3.ilse"]);
        Assert.Contains(world.Journal, e => e.Id == "act3_rest" && e.Kind == "main");
        world.EndDialogue();
        Use("caddoc");
        Assert.Contains(world.Journal, e => e.Id == "act3_penitent");
    }

    [Fact]
    public void The_north_door_opens_on_the_hollow_woods()
    {
        var world = Rest();
        var door = world.Zone.Exits.Single(e => e.Id == "north_door");
        (world.Hero.X, world.Hero.Y) = (door.At[0] + 0.5, door.At[1] + 4.5);
        world.MoveTo(door.At[0] + 0.5, door.At[1] + 0.5);
        Run(world, 3);
        Assert.Equal("hollow_woods", world.Zone.Id);
    }
}
