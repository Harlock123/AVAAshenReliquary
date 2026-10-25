using AshenReliquary.Core.Content;
using AshenReliquary.Core.World;

namespace AshenReliquary.Core.Tests;

/// <summary>
/// M5 step 4: the choice at the Throne (Destroy, Bind, Claim, and with Malachar spared the Lantern-Bird), Bind's
/// requirement (the Chronicle and three side stories), and the epilogue's slides for the endings and the story's flags.
/// </summary>
public class EndingsTests
{
    private static ContentDatabase Content => ContentDatabase.Embedded;

    private static GameWorld AtTheThrone(params (string Flag, string Value)[] flags)
    {
        var world = new GameWorld(Content.Zone("cinder_throne"), Content.Creatures, TestCharacters.Fighter(), 3, Content.AbilityBar("fighter"),
            Content.Loot, Content.Zones, Content.Dialogues, Content.Abilities);
        world.RestoreStory(flags.Select(f => new KeyValuePair<string, string>(f.Flag, f.Value)), []);
        world.TrainTo(20);
        foreach (var m in world.Monsters.ToList())
            world.Slay(m);
        if (world.Flag("act5.malachar") is null)
            world.SetFlag("act5.malachar", "slain");
        var throne = world.Interactives.Single(i => i.Id == "throne");
        (world.Hero.X, world.Hero.Y) = (throne.X, throne.Y + 1);
        world.Interact(throne);
        Assert.NotNull(world.Dialogue);
        return world;
    }

    private static List<string> Choices(GameWorld world) => world.Dialogue!.Choices.Select(c => c.Text).ToList();

    private static void Pick(GameWorld world, string starts) =>
        world.Choose(Choices(world).FindIndex(c => c.StartsWith(starts, StringComparison.Ordinal)));

    [Fact]
    public void With_malachar_fallen_the_throne_offers_destroy_claim_and_a_closed_bind()
    {
        var world = AtTheThrone();
        var choices = Choices(world);
        Assert.Contains(choices, c => c.StartsWith("Break the Ember Heart"));
        Assert.Contains(choices, c => c.StartsWith("Sit on the Throne"));
        Assert.Contains(choices, c => c == "Take the god into yourself...");
        Assert.DoesNotContain(choices, c => c.Contains("as Ashur once did"));
        Assert.DoesNotContain(choices, c => c.StartsWith("Let Malachar"));
        Pick(world, "Take the god into yourself...");
        Assert.Contains("Chronicle", world.Dialogue!.Text);
        Assert.Null(world.PendingEnding);
    }

    [Fact]
    public void Bind_opens_with_the_chronicle_and_three_side_stories_seen_through()
    {
        Assert.Equal(12, GameWorld.SideStories.Count);
        var two = AtTheThrone(("act3.library", "complete"), ("act1.scarecrow", "freed"), ("act4.egg", "taken"));
        Assert.Equal(2, two.SideStoriesSettled);                                            // an egg carried off isn't settled
        Assert.False(two.BindOpen);
        Assert.DoesNotContain(Choices(two), c => c.Contains("as Ashur once did"));

        var three = AtTheThrone(("act3.library", "complete"), ("act1.scarecrow", "freed"), ("act4.egg", "hatched"));
        Assert.True(three.BindOpen);
        Assert.Contains(Choices(three), c => c.Contains("as Ashur once did"));

        var noChronicle = AtTheThrone(("act1.scarecrow", "freed"), ("act4.egg", "hatched"), ("act2.confessor", "repaired"), ("act4.beacon", "lit"));
        Assert.False(noChronicle.BindOpen);
    }

    [Fact]
    public void Choosing_an_ending_keeps_it_and_hands_it_to_the_window_once()
    {
        var world = AtTheThrone();
        Pick(world, "Break the Ember Heart");
        Assert.Contains("It would end you too", world.Dialogue!.Text);
        Pick(world, "Not yet.");
        Assert.Null(world.PendingEnding);
        Pick(world, "Break the Ember Heart");
        Pick(world, "Do it.");
        Assert.Null(world.Dialogue);
        Assert.Equal("destroy", world.Ending);
        Assert.Equal("destroy", world.TakePendingEnding());
        Assert.Null(world.TakePendingEnding());

        // Chosen again from the same save, it can end otherwise.
        var throne = world.Interactives.Single(i => i.Id == "throne");
        world.Interact(throne);
        Pick(world, "Sit on the Throne");
        Pick(world, "Do it.");
        Assert.Equal("claim", world.Ending);
    }

    [Fact]
    public void Malachar_spared_he_waits_by_the_throne_and_can_carry_the_heart_away()
    {
        var world = AtTheThrone(("act5.malachar", "spared"));
        Assert.Contains("Wren's wooden bird", world.Dialogue!.Text);
        Pick(world, "Let Malachar take the Heart");
        Pick(world, "Do it.");
        Assert.Equal("mercy", world.Ending);
    }

    [Fact]
    public void The_slides_tell_the_ending_then_each_place_the_story_reached()
    {
        var epilogue = ContentDatabase.Epilogue;
        Assert.Equal(["bind", "claim", "destroy", "mercy"], epilogue.Endings.Keys.Order());
        var flags = new Dictionary<string, string>
        {
            ["act5.ending"] = "destroy", ["act5.malachar"] = "slain", ["act1.sallow_relic"] = "cured", ["act2.refugees"] = "saved",
            ["act4.egg"] = "hatched", ["act4.beacon"] = "dark", ["act1.scarecrow"] = "taken",
        };
        var slides = epilogue.SlidesFor("destroy", flags);
        Assert.True(slides[0].IsEnding);
        Assert.Equal("Ashes to Ashes", slides[0].Title);
        Assert.Equal(["destroy", "throne", "well", "refugees", "scarecrow", "order", "drakeling"], slides.Select(s => s.Id));
        Assert.Contains("Ysolde's cure", slides.Single(s => s.Id == "well").Text);
        Assert.Contains("never lit", slides.Single(s => s.Id == "order").Text);

        // The New Prophet: the province burns, and only what was written for that is told.
        var claimed = epilogue.SlidesFor("claim", new Dictionary<string, string>(flags) { ["act5.ending"] = "claim" });
        Assert.Equal(["claim", "throne", "vardmoor"], claimed.Select(s => s.Id));

        // An untold story has no slide; mercy tells Malachar's own end in the ending itself.
        var mercy = epilogue.SlidesFor("mercy", new Dictionary<string, string> { ["act5.ending"] = "mercy", ["act5.malachar"] = "spared" });
        Assert.Equal(["mercy"], mercy.Select(s => s.Id));
    }

    [Fact]
    public void Every_scene_is_set_somewhere_real_and_staged_empty()
    {
        var epilogue = ContentDatabase.Epilogue;
        var scenes = epilogue.Endings.Values.Select(e => (e.Zone, e.Near)).Concat(epilogue.Slides.Select(s => (s.Zone, s.Near))).Distinct();
        foreach (var (zoneId, near) in scenes)
        {
            var zone = Content.Zone(zoneId);
            var ids = (zone.Npcs ?? []).Select(n => n.Id).Concat((zone.Interactables ?? []).Select(i => i.Id)).Concat((zone.Vendors ?? []).Select(v => v.Id));
            Assert.Contains(near, ids);
            var world = new GameWorld(zone, Content.Creatures, TestCharacters.Fighter(), 3, Content.AbilityBar("fighter"),
                Content.Loot, Content.Zones, Content.Dialogues, Content.Abilities);
            world.StageScene(new Dictionary<string, string> { ["act5.malachar"] = "spared", ["act3.penitent"] = "forgiven" }, near);
            Assert.Empty(world.Monsters);
            Assert.True(world.Hero.X > 0 && world.Hero.X < zone.Rows[0].Length && world.Hero.Y > 0 && world.Hero.Y < zone.Rows.Count,
                $"{zoneId}/{near}: the camera is off the map");
            world.Update(0.5);
        }
    }
}
