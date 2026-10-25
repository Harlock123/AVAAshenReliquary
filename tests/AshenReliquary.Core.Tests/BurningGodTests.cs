using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Content;
using AshenReliquary.Core.World;

namespace AshenReliquary.Core.Tests;

/// <summary>
/// M5 step 2: the Burning God, Malachar's second phase: the Taint rising in the hero faster and faster, the lantern lights
/// that cleanse it, the ash novas to Step through, and Malachar channelling the god (harder to hurt, fire and void).
/// </summary>
public class BurningGodTests
{
    private static ContentDatabase Content => ContentDatabase.Embedded;

    private static BossPhase Phase => Content.Creatures["malachar"].Boss!.Phases[1];

    private static void Run(GameWorld world, double seconds)
    {
        for (var t = 0.0; t < seconds; t += 1 / 30.0)
            world.Update(1 / 30.0);
    }

    /// <summary>Malachar's court with every relic dealt with (no Echoes), the fight begun and him brought to the phase.</summary>
    private static (GameWorld World, MonsterActor Malachar) Burning()
    {
        var world = new GameWorld(Content.Zone("cinder_throne"), Content.Creatures, TestCharacters.Fighter(), 3, Content.AbilityBar("fighter"),
            Content.Loot, Content.Zones, Content.Dialogues, Content.Abilities);
        world.RestoreStory(new Dictionary<string, string>
        {
            ["act1.sallow_relic"] = "destroyed", ["act2.hadric_relic"] = "returned", ["act3.candles"] = "rekindled",
            ["act4.egg"] = "smashed",
        }, []);
        world.TrainTo(20);
        world.Hero.Character.CannotFall = true;
        var malachar = world.Monsters.Single(m => m.Definition.Id == "malachar");
        foreach (var m in world.Monsters.Where(m => m != malachar).ToList())
            world.Slay(m);
        (world.Hero.X, world.Hero.Y) = (malachar.X, malachar.Y + 6);
        Run(world, 1);
        malachar.Creature.SetHitPoints((int)(malachar.Creature.Stats.HitPoints * 0.55));
        Run(world, 0.1);
        return (world, malachar);
    }

    /// <summary>The hero kept out of every light and every nova's way: far from the arena's heart, the lights put out.</summary>
    private static void OutOfTheLight(GameWorld world)
    {
        foreach (var l in world.LanternLights)
            (l.X, l.Y, l.ToX, l.ToY) = (world.Hero.X + 20, world.Hero.Y, world.Hero.X + 20, world.Hero.Y);
    }

    [Fact]
    public void Below_three_fifths_malachar_becomes_the_burning_god_and_the_lanterns_kindle()
    {
        var (world, malachar) = Burning();
        Assert.Equal(1, malachar.BossPhase);
        Assert.Equal("The Burning God", Phase.Name);
        Assert.Equal(Phase.Lights, world.LanternLights.Count);
        Assert.All(world.LanternLights, l => Assert.True(world.Map.IsWalkable(l.X, l.Y)));
        var before = world.LanternLights.Select(l => (l.X, l.Y)).ToList();
        Run(world, 3);
        Assert.Contains(world.LanternLights.Select(l => (l.X, l.Y)), p => !before.Contains(p));       // they drift
    }

    [Fact]
    public void The_taint_rises_faster_and_faster_and_a_light_cleanses_it()
    {
        var (world, _) = Burning();
        var taint = world.Hero.Character.Taint;
        taint.Cleanse();
        // Steady the lights far off, and measure the rise early and late in the phase.
        void Rise(double seconds) { for (var t = 0.0; t < seconds; t += 1 / 30.0) { OutOfTheLight(world); world.Update(1 / 30.0); } }
        Rise(2);
        var early = taint.Value;
        taint.Cleanse();
        Rise(20);
        taint.Cleanse();
        Rise(2);
        var late = taint.Value;
        Assert.True(early > 0);
        Assert.True(late > early * 1.5, $"early {early:F1}, late {late:F1}");

        // In a light: no rise, and the Taint washes out.
        taint.Add(80);
        var light = world.LanternLights[0];
        (light.X, light.Y, light.ToX, light.ToY) = (world.Hero.X, world.Hero.Y, world.Hero.X, world.Hero.Y);
        var full = taint.Value;
        Run(world, 1);
        Assert.True(world.InLanternLight);
        Assert.True(taint.Value < full - Phase.LightCleanse * 0.8 * taint.RateMultiplier || taint.Value < full - 20);
    }

    [Fact]
    public void When_the_taint_boils_over_the_god_burns_through_the_hero()
    {
        var (world, _) = Burning();
        OutOfTheLight(world);
        world.Hero.Character.Taint.Cleanse();
        world.Hero.Character.Taint.Add(99.5 / world.Hero.Character.Taint.RateMultiplier);
        var hp = world.Hero.Character.HitPoints;
        Run(world, 0.5);
        Assert.Contains(world.Texts, t => t.Text == "WHISPERS");
        Assert.True(world.Hero.Character.HitPoints < hp || world.Hero.Character.Defenses.Immunities.Contains(DamageType.Fire));
    }

    [Fact]
    public void An_ash_nova_burns_whoever_it_crosses_once_and_a_step_passes_through_it()
    {
        var (world, malachar) = Burning();
        Run2(world, () => world.Novas.Count > 0, Phase.NovaEverySeconds);
        var nova = Assert.Single(world.Novas);
        Assert.Equal((malachar.HomeX, malachar.HomeY), (nova.X, nova.Y));
        int Burns() => world.Texts.Count(t => t.Text.StartsWith("ash "));
        Run2(world, () => Burns() > 0, world.Hero.DistanceTo(nova.X, nova.Y) / nova.Speed + 0.5);
        Assert.Equal(1, Burns());
        Run(world, 0.8);
        Assert.Equal(1, Burns());                                                           // once

        // The next: Stepped through, toward the Throne, as it arrives.
        Run2(world, () => world.Novas.Count > 0 && world.Novas[^1] != nova, Phase.NovaEverySeconds + 1);
        var ring = world.Novas[^1];
        Run2(world, () => world.Hero.DistanceTo(ring.X, ring.Y) - ring.RadiusAt(world.Time) < 0.45, 10);
        Run2(world, () => world.DodgeCooldownRemaining <= 0, 5);
        Assert.True(world.Dodge(ring.X, ring.Y));
        Run(world, 0.3);
        Assert.Contains(world.Texts, f => f.Text == "stepped through");
        Assert.Equal(0, Burns());
    }

    private static bool Run2(GameWorld world, Func<bool> until, double limit)
    {
        for (var t = 0.0; t < limit; t += 1 / 30.0)
        {
            if (until())
                return true;
            world.Update(1 / 30.0);
        }
        return until();
    }

    [Fact]
    public void Channelling_the_god_he_takes_less_from_every_blow_and_strikes_with_fire_and_void()
    {
        var (world, malachar) = Burning();
        var hp = malachar.Creature.HitPoints;
        typeof(GameWorld).GetMethod("DamageMonster", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(world, [malachar, 100]);
        Assert.Equal((int)Math.Round(100 * Phase.DamageTakenScale), hp - malachar.Creature.HitPoints);
        var seen = new HashSet<DamageType>();
        Run2(world, () =>
        {
            seen.Add(malachar.Creature.StrikeDamageType);
            return seen.Count >= 2;
        }, 15);
        Assert.Equal([DamageType.Fire, DamageType.Void], seen.Order());
    }

    [Fact]
    public void The_lights_and_novas_go_out_when_the_hero_falls_or_malachar_does()
    {
        var (world, malachar) = Burning();
        Run(world, Phase.NovaEverySeconds);
        Assert.NotEmpty(world.Novas);
        typeof(GameWorld).GetMethod("ResetBosses", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(world, []);
        Assert.Empty(world.LanternLights);
        Assert.Empty(world.Novas);
        Assert.Equal(0, malachar.BossPhase);
        Assert.Equal(DamageType.Void, malachar.Creature.StrikeDamageType);

        (world, malachar) = Burning();
        Run(world, Phase.NovaEverySeconds);
        world.Slay(malachar);
        Assert.Empty(world.LanternLights);
        Assert.Empty(world.Novas);
    }
}
