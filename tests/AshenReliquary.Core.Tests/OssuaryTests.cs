using AshenReliquary.Core.Content;
using AshenReliquary.Core.Creatures;
using AshenReliquary.Core.Items;
using AshenReliquary.Core.World;

namespace AshenReliquary.Core.Tests;

/// <summary>The Prior's Ossuary: an optional dungeon under the Drowned Chapel, its named elite and his confession.</summary>
public class OssuaryTests
{
    private static ContentDatabase Content => ContentDatabase.Embedded;

    private static void Run(GameWorld world, double seconds)
    {
        for (var t = 0.0; t < seconds; t += 1 / 30.0)
            world.Update(1 / 30.0);
    }

    private static GameWorld Ossuary(int seed = 3) =>
        new(Content.Zone("prior_ossuary"), Content.Creatures, TestCharacters.Fighter(), seed, Content.AbilityBar("fighter"),
            Content.Loot, Content.Zones, Content.Dialogues, Content.Abilities);

    [Fact]
    public void The_ossuary_loads_and_every_pack_chest_shrine_exit_and_the_lectern_can_be_walked_to()
    {
        var zone = Content.Zone("prior_ossuary");
        var map = new TileMap(zone);
        Assert.True(zone.IsDark);
        Assert.Equal(4, zone.Level);
        Assert.All(zone.Spawns.SelectMany(s => s.Pack), m => Assert.True(Content.Creatures.ContainsKey(m.Creature), m.Creature));
        Assert.Equal(2, zone.Shrines.Count);
        Assert.Equal(["hasted", "shielded", "tainted"], zone.Spawns.SelectMany(s => s.Pack).SelectMany(m => m.Affixes ?? []).Distinct().Order());

        var from = (zone.PlayerStart[0], zone.PlayerStart[1]);
        (int, int) Near(int[] at) => map.IsBlocked(at[0], at[1])
            ? new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }.Select(d => (at[0] + d.Item1, at[1] + d.Item2)).First(c => !map.IsBlocked(c.Item1, c.Item2))
            : (at[0], at[1]);
        var places = zone.Spawns.Select(s => ("pack", s.At))
            .Concat(zone.Caches!.Select(c => (c.Id, c.At)))
            .Concat(zone.Shrines.Select(s => (s.Id, s.At)))
            .Concat(zone.Exits.Select(e => (e.Id, e.At)))
            .Concat(zone.Interactables!.Select(i => (i.Id, i.At)));
        foreach (var (name, at) in places)
            Assert.True(Pathfinder.Find(map, from, Near(at), maxNodes: 100000) is not null, $"{name} at {at[0]},{at[1]} can't be reached");
    }

    [Fact]
    public void The_east_crypts_stair_leads_down_to_the_ossuary_and_back()
    {
        var chapel = Content.Zone("drowned_chapel");
        var world = new GameWorld(chapel, Content.Creatures, TestCharacters.Fighter(), 3, Content.AbilityBar("fighter"), Content.Loot, Content.Zones);
        var stair = chapel.Exits.Single(e => e.Id == "ossuary");
        (world.Hero.X, world.Hero.Y) = (stair.Arrive![0] + 0.5, stair.Arrive[1] + 0.5);
        world.MoveTo(stair.At[0] + 0.5, stair.At[1] + 0.5);
        Run(world, 6);
        Assert.Equal("prior_ossuary", world.Zone.Id);
        var back = world.Zone.Exits.Single(e => e.Id == "chapel");
        world.MoveTo(back.At[0] + 0.5, back.At[1] + 0.5);
        Run(world, 6);
        Assert.Equal("drowned_chapel", world.Zone.Id);
    }

    [Fact]
    public void Prior_aldous_is_a_named_shielded_elite_who_always_drops_a_rare_and_sets_his_flag()
    {
        var world = Ossuary();
        var prior = world.Monsters.Single(m => m.Definition.Id == "drowned_prior");
        Assert.Equal("Prior Aldous Venn", prior.Name);
        Assert.Equal(CreatureRank.Elite, prior.Creature.Stats.Rank);
        Assert.True(prior.Has(MonsterAffix.Shielded));
        foreach (var guard in world.Monsters.Where(m => m.PackId == prior.PackId).ToList())
            world.Slay(guard);
        Assert.Contains(world.GroundItems, g => g.Drop.Item?.Rarity == Rarity.Rare);
        Assert.Equal("slain", world.Flag("act1.prior"));
    }

    [Fact]
    public void The_priors_confession_writes_the_journal_once()
    {
        var world = Ossuary();
        var lectern = world.Interactives.Single(i => i.Id == "prior_confession");
        (world.Hero.X, world.Hero.Y) = (lectern.X + 1, lectern.Y);
        world.Interact(lectern);
        Run(world, 1);
        Assert.NotNull(world.Dialogue);
        world.Choose(0);
        Assert.Contains(world.Journal, j => j.Id == "act1_prior_confession");
        world.EndDialogue();
        world.Interact(lectern);
        Run(world, 1);
        Assert.Contains("Sallow", world.Dialogue!.Text);
    }
}
