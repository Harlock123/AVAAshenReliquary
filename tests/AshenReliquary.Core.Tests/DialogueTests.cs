using AshenReliquary.Core.Characters;
using AshenReliquary.Core.Content;
using AshenReliquary.Core.Rules;
using AshenReliquary.Core.Saving;
using AshenReliquary.Core.World;

namespace AshenReliquary.Core.Tests;

public class DialogueTests
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

    private static Interactive Named(GameWorld world, string id) => world.Interactives.Single(i => i.Id == id);

    [Fact]
    public void Talking_walks_over_and_opens_the_conversation()
    {
        var world = Fort();
        var brask = Named(world, "brask");
        world.Interact(brask);
        Run(world, 4);
        var view = Assert.IsType<DialogueView>(world.Dialogue);
        Assert.Equal("Sergeant Brask", view.Speaker);
        Assert.Equal("flare/male05", view.Portrait);
        Assert.Equal(3, view.Choices.Count);
        Assert.Equal("Deception", view.Choices[1].CheckLabel);
        Assert.Equal(15, view.Choices[1].Dc);
        Assert.Equal(Check.Chance(world.CheckModifier("Deception"), 15), view.Choices[1].Chance!.Value, 6);
    }

    [Fact]
    public void An_answer_sets_a_flag_writes_the_journal_and_a_second_visit_opens_differently()
    {
        var world = Fort();
        var brask = Named(world, "brask");
        world.Interact(brask);
        Run(world, 4);
        world.Choose(0);                                   // the truth
        Assert.Equal("truth", world.Flag("act1.brask"));
        var entry = Assert.Single(world.Journal);
        Assert.Equal(("act1_fort", "main"), (entry.Id, entry.Kind));
        Assert.Equal("Leave", Assert.Single(world.Dialogue!.Choices).Text);
        world.Choose(0);
        Assert.Null(world.Dialogue);

        world.Interact(brask);
        Assert.StartsWith("\"Still breathing", world.Dialogue!.Text);
        world.Choose(0);                                   // what news?
        Assert.Contains("fens", world.Dialogue!.Text);
    }

    [Fact]
    public void A_check_rolls_and_goes_to_success_or_failure()
    {
        var outcomes = new HashSet<string>();
        for (var seed = 1; seed <= 60 && outcomes.Count < 2; seed++)
        {
            var world = Fort(seed);
            world.Interact(Named(world, "brask"));
            Run(world, 4);
            world.Choose(1);                               // the lie: a Deception check
            var flag = world.Flag("act1.brask")!;
            outcomes.Add(flag);
            Assert.Contains(flag, new[] { "lied", "caught" });
            Assert.StartsWith("Deception ", world.Dialogue!.LastRoll);
            Assert.Contains("against DC 15", world.Dialogue.LastRoll);
        }
        Assert.Equal(2, outcomes.Count);
    }

    [Fact]
    public void A_failed_look_cant_be_retried_but_a_found_message_stays_found()
    {
        string? seen = null, missed = null;
        for (var seed = 1; seed <= 60 && (seen is null || missed is null); seed++)
        {
            var world = Fort(seed);
            world.Interact(Named(world, "notice_board"));
            Run(world, 6);
            Assert.Equal(2, world.Dialogue!.Choices.Count);
            world.Choose(0);
            world.EndDialogue();
            world.Interact(Named(world, "notice_board"));
            if (world.Flag("act1.notice") == "seen")
            {
                seen = world.Dialogue!.Text;
                Assert.Contains(world.Journal, j => j.Id == "act1_ferry_hint");
            }
            else
            {
                missed = string.Join("|", world.Dialogue!.Choices.Select(c => c.Text));
            }
        }
        Assert.Contains("FERRY", seen);
        Assert.Equal("Leave it.", missed);                 // only "Leave it." is left
    }

    [Fact]
    public void Walking_away_ends_the_conversation()
    {
        var world = Fort();
        world.Interact(Named(world, "brask"));
        Run(world, 4);
        Assert.NotNull(world.Dialogue);
        world.MoveTo(10.5, 8.5);
        Run(world, 5);
        Assert.Null(world.Dialogue);
    }

    [Fact]
    public void Flags_and_the_journal_survive_a_save()
    {
        var world = Fort();
        world.Interact(Named(world, "brask"));
        Run(world, 4);
        world.Choose(0);
        var loaded = SaveGame.FromJson(SaveGame.Capture(world, "s", 3, null).ToJson()).Restore(Content);
        Assert.Equal("truth", loaded.Flag("act1.brask"));
        Assert.Equal("act1_fort", Assert.Single(loaded.Journal).Id);
    }

    [Fact]
    public void A_trained_skill_adds_proficiency_and_heavy_armour_hampers_dexterity_skills()
    {
        var hero = TestCharacters.Fighter();                // a Deserter: trained in Athletics and Warfare Lore
        var str = hero.Attributes[AttributeKind.Strength];
        Assert.Equal(str + 2 + hero.Level + 2, hero.Skill("Athletics").Total); // + Hardy Recovery, the Deserter's skill feat
        Assert.Equal(hero.Attributes[AttributeKind.Intelligence] + 2 + hero.Level, hero.Skill("Warfare Lore").Total);
        Assert.Equal(hero.Attributes[AttributeKind.Charisma] + 2, hero.Skill("Diplomacy").Total); // untrained, + Cooperative Nature
        Assert.Throws<RulesException>(() => hero.Skill("Juggling"));
    }

    [Fact]
    public void Every_dialogue_hangs_together()
    {
        var checks = new HashSet<string>(Skills.Attribute.Keys) { "Perception", "Fortitude", "Reflex", "Will" };
        foreach (var d in Content.Dialogues.Values)
        {
            Assert.True(d.Nodes.ContainsKey(d.Start), $"{d.Id}: start {d.Start}");
            foreach (var o in d.Openings ?? [])
                Assert.True(d.Nodes.ContainsKey(o.Node), $"{d.Id}: opening {o.Node}");
            foreach (var (id, node) in d.Nodes)
            foreach (var c in node.Choices ?? [])
            {
                if (c.To is { } to)
                    Assert.True(d.Nodes.ContainsKey(to), $"{d.Id}/{id}: to {to}");
                if (c.Check is { } check)
                {
                    Assert.True(d.Nodes.ContainsKey(check.Success) && d.Nodes.ContainsKey(check.Failure), $"{d.Id}/{id}: check outcomes");
                    Assert.True(checks.Contains(check.Skill) || Skills.IsLore(check.Skill), $"{d.Id}/{id}: unknown check {check.Skill}");
                }
            }
        }
        foreach (var zone in Content.Zones.Values)
        {
            foreach (var who in (zone.Npcs ?? []).Select(n => n.Dialogue).Concat((zone.Interactables ?? []).Select(i => i.Dialogue).OfType<string>()))
                Assert.True(Content.Dialogues.ContainsKey(who), $"{zone.Id}: no dialogue {who}");
        }
    }
}
