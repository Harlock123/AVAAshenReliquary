using AshenReliquary.Core.Abilities;
using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Content;
using AshenReliquary.Core.Creatures;
using AshenReliquary.Core.World;

namespace AshenReliquary.Core.Tests;

/// <summary>
/// M4 step 9: Vorthrax the Cinderwyrm in the Wyrm's Nest (his breath, his flight and firestorm, the platforms that sink
/// in his last phase), the Ember Gate he guards, and his Echo in the Reliquary.
/// </summary>
public class VorthraxTests
{
    private static ContentDatabase Content => ContentDatabase.Embedded;

    private static void Run(GameWorld world, double seconds)
    {
        for (var t = 0.0; t < seconds; t += 1 / 30.0)
            world.Update(1 / 30.0);
    }

    /// <summary>The Wyrm's Nest with only Vorthrax in it, a level-19 hero of the kit beside him, the fight begun.</summary>
    private static (GameWorld World, MonsterActor Wyrm) Arena(string kit = "fighter vanguard")
    {
        var nest = Content.Zone("wyrms_nest");
        var zone = nest with { Spawns = [nest.Spawns.Single(s => s.Pack.Any(m => m.Creature == "vorthrax"))] };
        var hero = TestCharacters.ForKit(kit);
        var world = new GameWorld(zone, Content.Creatures, hero, 7, Content.AbilityBar(hero.Class.Id), Content.Loot, Content.Zones,
            Content.Dialogues, Content.Abilities);
        BalanceReport.Dress(hero, world.Hero.Equipment, kit);
        BalanceReport.LevelTo(world, 19);
        world.Hero.Character.CannotFall = true;
        var wyrm = world.Monsters.Single(m => m.Definition.Id == "vorthrax");
        (world.Hero.X, world.Hero.Y) = (wyrm.X, wyrm.Y + 3);
        Run(world, 0.5);
        return (world, wyrm);
    }

    private static void Wound(MonsterActor m, double share) => m.Creature.SetHitPoints((int)(m.Creature.Stats.HitPoints * share));

    [Fact]
    public void Vorthrax_guards_the_ember_gate_on_six_platforms_round_a_firm_core()
    {
        var zone = Content.Zone("wyrms_nest");
        var spawn = zone.Spawns.Single(s => s.Pack.Any(m => m.Creature == "vorthrax"));
        Assert.Equal(CreatureRank.Herald, spawn.Pack.Single().Rank);
        Assert.Equal(18, spawn.Level);
        Assert.Equal("act4.vorthrax", Content.Creatures["vorthrax"].Boss!.DeathFlag);
        var map = new TileMap(zone);
        Assert.Equal(6, map.Platforms.Count);
        Assert.All(map.Platforms.Values, cells => Assert.All(cells, c => Assert.False(map.IsBlocked(c.X, c.Y))));
        // His home is firm ground, not a platform.
        Assert.DoesNotContain(map.Platforms.Values.SelectMany(c => c), c => c == (spawn.At[0], spawn.At[1]));
    }

    [Fact]
    public void His_breath_is_marked_first_then_burns_what_stands_in_the_cone()
    {
        var (world, wyrm) = Arena();
        var hp = world.Hero.Character.HitPoints;
        for (var t = 0.0; t < 12 && wyrm.Breath is null; t += 1 / 30.0)
            world.Update(1 / 30.0);
        Assert.NotNull(wyrm.Breath);
        var breath = wyrm.Breath!;
        Assert.True(breath.Covers(world.Hero.X, world.Hero.Y));
        Assert.True(world.Time < breath.BurnAt);
        var before = world.Hero.Character.HitPoints;
        Run(world, breath.BurnAt - world.Time + 0.2);
        Assert.True(breath.Burned);
        Assert.True(world.Hero.Character.HitPoints <= before);
        Assert.False(breath.Covers(wyrm.X + Math.Cos(breath.Direction + Math.PI) * 3, wyrm.Y + Math.Sin(breath.Direction + Math.PI) * 3));
        Assert.True(hp > 0);
    }

    [Fact]
    public void Aloft_he_is_out_of_a_blade_s_reach_rains_fire_and_swoops_down_to_be_struck()
    {
        var (world, wyrm) = Arena();
        Wound(wyrm, 0.55);
        Run(world, 0.5);
        Assert.Equal(1, wyrm.BossPhase);
        Assert.True(wyrm.Aloft);
        // A sword can't reach him up there.
        world.Attack(wyrm);
        var hp = wyrm.Creature.HitPoints;
        Run(world, 2);
        Assert.True(wyrm.Aloft || wyrm.Creature.HitPoints <= hp);
        Assert.Contains(world.Hazards, h => h.Type == DamageType.Fire);
        Assert.True(world.Hazards.Count(h => h.Type == DamageType.Fire) >= 3);
        // He comes down beside the hero.
        Run(world, Content.Creatures["vorthrax"].Boss!.Phases[1].SwoopEverySeconds);
        var landed = false;
        for (var t = 0.0; t < 6 && !landed; t += 1 / 30.0)
        {
            world.Update(1 / 30.0);
            landed = !wyrm.Aloft && wyrm.DistanceTo(world.Hero) < 3;
        }
        Assert.True(landed, "he never swooped");
    }

    [Fact]
    public void Arrows_and_spells_reach_him_aloft()
    {
        var (world, wyrm) = Arena("wizard pyromancer");
        Wound(wyrm, 0.55);
        Run(world, 0.5);
        Assert.True(wyrm.Aloft);
        var hp = wyrm.Creature.HitPoints;
        world.Hero.Resource.Gain(1000);
        for (var i = 0; i < 6 && wyrm.Creature.HitPoints == hp; i++)
        {
            world.UseAbility("disintegrate", wyrm);
            Run(world, 1.5);
            world.Hero.Resource.Gain(1000);
        }
        Assert.True(wyrm.Creature.HitPoints < hp, "no spell reached him aloft");
    }

    [Fact]
    public void On_the_burning_rock_the_platforms_crack_and_sink_and_come_back_when_the_hero_falls()
    {
        var (world, wyrm) = Arena();
        Wound(wyrm, 0.2);
        Run(world, 0.5);
        Assert.Equal(2, wyrm.BossPhase);
        Assert.False(wyrm.Aloft);
        // Stand on a platform: it is the one that cracks first.
        var platform = world.Map.Platforms.First();
        var (px, py) = platform.Value[platform.Value.Count / 2];
        (world.Hero.X, world.Hero.Y) = (px + 0.5, py + 0.5);
        world.Hero.Path.Clear();
        Run(world, 2.5);
        Assert.Single(world.Crumbling);
        Assert.Equal(platform.Key, world.Crumbling[0].Group);
        Assert.True(world.Map.IsCrumbling(px, py));
        Assert.True(world.Map.IsEmbers(px, py));
        var hp = world.Hero.Character.HitPoints;
        Run(world, 3.5);
        Assert.Empty(world.Crumbling);
        Assert.True(world.Map.IsLava(px, py) && world.Map.IsBlocked(px, py));
        Assert.True(world.Map.IsWalkable(world.Hero.X, world.Hero.Y), "the hero was left in the lava");
        Assert.True(world.Hero.Character.HitPoints < hp);
        // A fall brings the rock back whole (and the fight from the start): the bosses reset as the hero gets up again.
        typeof(GameWorld).GetMethod("ResetBosses", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(world, []);
        Assert.False(world.Map.IsLava(px, py));
        Assert.False(world.Map.IsBlocked(px, py));
        Assert.Equal(0, wyrm.BossPhase);
    }

    [Fact]
    public void His_death_opens_the_ember_gate_and_the_rock_is_whole_again()
    {
        var (world, wyrm) = Arena();
        Wound(wyrm, 0.2);
        Run(world, 0.5);
        Run(world, 2.5);
        Assert.NotEmpty(world.Crumbling);
        world.Slay(wyrm);
        Assert.Equal("slain", world.Flag("act4.vorthrax"));
        Assert.Contains(world.Journal, j => j.Id == "act4_vorthrax");
        Assert.Empty(world.Crumbling);
        Assert.All(world.Map.Platforms.Values.SelectMany(c => c), c => Assert.False(world.Map.IsBlocked(c.X, c.Y)));
        var gate = Content.Zone("wyrms_nest").Exits.Single(e => e.Id == "reliquary");
        Assert.All(gate.Requires!, r => Assert.Equal(r.Value, world.Flag(r.Key)));
    }

    [Fact]
    public void His_echo_waits_in_the_vault_of_flame_unless_his_egg_was_smashed_and_never_lands_on_the_burning_rock()
    {
        var reliquary = new GameWorld(Content.Zone("reliquary"), Content.Creatures, TestCharacters.Fighter(), 3, Content.AbilityBar("fighter"),
            Content.Loot, Content.Zones, Content.Dialogues, Content.Abilities);
        var echo = reliquary.Monsters.Single(m => m.IsEcho && m.Definition.Id == "vorthrax");
        Assert.Equal("Echo of Vorthrax the Cinderwyrm", echo.Name);
        Assert.Equal(1, GameWorld.LastPhase(echo));
        Assert.False(reliquary.GateOpen(Content.Zone("reliquary").Gates!.Single(g => g.Id == "vault_of_flame")));

        var smashed = new GameWorld(Content.Zone("reliquary"), Content.Creatures, TestCharacters.Fighter(), 3, Content.AbilityBar("fighter"),
            Content.Loot, Content.Zones, Content.Dialogues, Content.Abilities);
        smashed.RestoreStory([new("act4.egg", "smashed")], []);
        Assert.DoesNotContain(smashed.Monsters, m => m.Definition.Id == "vorthrax");
        Assert.Equal("laid", smashed.Flag("act4.echo.vorthrax"));
        Assert.True(smashed.GateOpen(Content.Zone("reliquary").Gates!.Single(g => g.Id == "vault_of_flame")));
    }

    [Fact]
    public void Corwen_has_words_once_he_is_dead()
    {
        var world = new GameWorld(Content.Zone("ember_gate"), Content.Creatures, TestCharacters.Fighter(), 3, Content.AbilityBar("fighter"),
            Content.Loot, Content.Zones, Content.Dialogues, Content.Abilities);
        world.SetFlag("act4.corwen", "met");
        world.SetFlag("act4.vorthrax", "slain");
        var corwen = world.Interactives.Single(i => i.Id == "corwen");
        (world.Hero.X, world.Hero.Y) = (corwen.X + 1, corwen.Y + 1);
        world.Interact(corwen);
        Assert.Contains("red light", world.Dialogue!.Text);
    }
}
