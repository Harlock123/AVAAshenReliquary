using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using AshenReliquary.App.Views;
using AshenReliquary.Core.Rules;

namespace AshenReliquary.App.Tests;

public class CreationTests
{
    private static string? Shot(string name) =>
        Environment.GetEnvironmentVariable("ASHEN_SHOTS") is { Length: > 0 } dir ? Path.Combine(dir, name + ".png") : null;

    private static (MainWindow Window, ViewModels.CharacterCreationViewModel Vm, TestKitLite Kit) Open(int width = 1312, int height = 824)
    {
        var window = new MainWindow { Width = width, Height = height };
        var vm = window.ShowCharacterCreation();
        window.Show();
        var kit = new TestKitLite(window);
        kit.Pump();
        return (window, vm, kit);
    }

    [AvaloniaFact]
    public void Creation_screen_opens_with_a_legal_recommended_build()
    {
        var (window, vm, kit) = Open();
        Assert.True(window.CreationView.IsVisible);
        Assert.True(vm.CanBegin, vm.Problem);
        Assert.Equal("+4", vm.Rows.Single(r => r.Kind == AttributeKind.Strength).Value);
        kit.Capture(Shot("character-creation"));
    }

    [AvaloniaFact]
    public void Trained_skills_and_the_ancestry_feat_are_picked_at_creation()
    {
        var (window, vm, kit) = Open();
        var athletics = vm.SkillOptions.Single(o => o.Name == "Athletics");
        Assert.True(athletics.IsSelected && !athletics.IsEnabled);          // the Deserter fighter's, fixed
        var picked = vm.SkillOptions.Where(o => o.IsSelected && o.IsEnabled).ToList();
        Assert.Equal(3, picked.Count);                                     // the fighter's 3 + INT +0
        Assert.True(vm.CanBegin, vm.Problem);

        picked[0].Toggle.Execute(null);                                    // one short now
        Assert.False(vm.CanBegin);
        Assert.Contains("1 more trained skill", vm.Problem);
        var society = vm.SkillOptions.Single(o => o.Name == "Society");
        Assert.False(society.IsSelected);
        society.Toggle.Execute(null);
        vm.AncestryFeat = vm.AncestryFeats.Single(f => f.Id == "road_hardened");
        kit.Pump();
        Assert.True(vm.CanBegin, vm.Problem);
        kit.Capture(Shot("character-creation-skills"));

        vm.BeginCommand.Execute(null);
        kit.Pump();
        var hero = window.Session!.World.Hero.Character;
        Assert.Equal(AshenReliquary.Core.Rules.ProficiencyRank.Trained, hero.SkillRank("Society"));
        Assert.Contains(hero.Feats, f => f.Id == "road_hardened");
    }

    [AvaloniaFact]
    public void The_subclass_is_picked_at_creation_and_a_vanguard_starts_with_a_greatsword()
    {
        var (window, vm, kit) = Open();
        Assert.Equal(["vanguard", "bulwark"], vm.Subclasses.Select(s => s.Id));
        Assert.Equal("vanguard", vm.Subclass?.Id);
        Assert.Contains(vm.Abilities, a => a.StartsWith("Vanguard  Earthshaker"));
        Assert.Contains(vm.Abilities, a => a.Contains("Greatsword"));
        vm.Choose("human", "hedge-scholar", "wizard", "stormcaller");
        kit.Pump();
        Assert.Equal(["pyromancer", "stormcaller"], vm.Subclasses.Select(s => s.Id));
        Assert.Contains(vm.Abilities, a => a.StartsWith("Stormcaller  Thunderclap"));
        vm.Choose("human", "deserter", "fighter", "vanguard");
        kit.Pump();
        kit.Capture(Shot("character-creation-subclass"));
        vm.BeginCommand.Execute(null);
        kit.Pump();
        var world = window.Session!.World;
        Assert.Equal("vanguard", world.Hero.Character.Subclass?.Id);
        Assert.Equal("greatsword", world.Hero.Equipment.In(AshenReliquary.Core.Items.EquipSlot.MainHand)?.Definition.Id);
        Assert.Null(world.Hero.Equipment.In(AshenReliquary.Core.Items.EquipSlot.OffHand));
        Assert.Contains(world.Bar.Values, a => a.Id == "earthshaker");
    }

    [AvaloniaFact]
    public void A_hero_from_before_subclasses_chooses_one_on_the_level_up_screen()
    {
        var save = AshenReliquary.Core.Saving.SaveGame.Capture(GameSession.NewGame("fighter", 3).World, "old", 3, null);
        save = save with { Choices = save.Choices with { Subclass = null } };
        var window = new MainWindow { Session = GameSession.FromSave(AshenReliquary.Core.Saving.SaveGame.FromJson(save.ToJson())), Width = 1288, Height = 770 };
        window.View.FixedTime = 0;
        window.Show();
        var kit = new TestKitLite(window);
        kit.Pump();
        var world = window.Session!.World;
        Assert.True(world.Hero.Character.NeedsSubclass);
        Assert.DoesNotContain(world.Bar.Values, a => a.Id == "challenge");
        window.View.OpenLevelUp();
        Assert.True(window.View.LevelUpOpen);
        window.Tick(TimeSpan.FromSeconds(0.05));
        kit.Capture(null);
        var panel = window.View.LevelUp;
        Assert.True(panel.ShowsSubclass);
        var (rect, _) = panel.Options.Single(o => o.Title == "Bulwark");
        Click(window, kit, new Point(rect.MidX, rect.MidY));
        kit.Capture(Shot("subclass-page"));
        Click(window, kit, new Point(panel.ConfirmButton.MidX, panel.ConfirmButton.MidY));
        Assert.Equal("bulwark", world.Hero.Character.Subclass?.Id);
        Assert.Contains(world.Bar.Values, a => a.Id == "challenge");
        window.Tick(TimeSpan.FromSeconds(0.05));
        kit.Capture(null);
        Assert.False(window.View.LevelUpOpen); // nothing else waiting: the screen closes
    }

    private static void Click(MainWindow window, TestKitLite kit, Point at)
    {
        window.MouseDown(at, Avalonia.Input.MouseButton.Left);
        window.MouseUp(at, Avalonia.Input.MouseButton.Left);
        kit.Pump();
    }

    [AvaloniaFact]
    public void A_new_level_offers_the_level_up_screen()
    {
        var kit = TestKit.Open();
        var world = kit.World;
        TestHooks.GainXp(world, 1000);
        kit.Play(0.2);
        kit.Window.View.LevelUpOpen = true;
        kit.Play(0.2);
        kit.Capture(Shot("level-up"));
        Assert.Equal([2], world.Hero.Character.PendingLevels);
        Assert.NotEmpty(world.Hero.Character.FeatOptions(AshenReliquary.Core.Characters.FeatType.Class, 2));
        // Level 2 has no skill increase: the screen says when the next one comes.
        Assert.Equal("No skill increase at level 2; the next comes at level 3.", Rendering.LevelUpPanel.SkillNote(2));
    }

    [AvaloniaFact]
    public void In_a_narrow_window_the_level_up_screen_fits_and_says_what_is_left_to_choose()
    {
        var kit = TestKit.Open();
        kit.Window.MinWidth = 0;
        kit.Window.Width = 731;
        kit.Window.Height = 946;
        var world = kit.World;
        TestHooks.GainXp(world, 4000);
        foreach (var (level, choice) in new (int, AshenReliquary.Core.Characters.LevelChoice)[]
        {
            (2, new(ClassFeat: "lunge", SkillFeat: "titan_wrestler")),
            (3, new(GeneralFeat: "toughness", SkillIncrease: "Athletics")),
            (4, new(ClassFeat: "swipe", SkillFeat: "battle_medicine")),
        })
            world.ChooseLevel(level, choice);
        kit.Window.View.LevelUpOpen = true;
        kit.Play(0.1);
        kit.Capture();
        var panel = kit.Window.View.LevelUp;
        var character = world.Hero.Character;
        Assert.InRange(panel.Bounds.Left, 0, 20);
        Assert.InRange(panel.Bounds.Right, 711, 731);
        Assert.Equal(["ancestry feat", "a skill increase", "4 attribute boosts"], panel.Missing(character, 5));

        void Pick(string title)
        {
            var (rect, _) = panel.Options.First(o => o.Title == title);
            Assert.True(rect.Left >= 0 && rect.Right <= 731, $"{title} is drawn off the window: {rect}");
            kit.Click(new Point(rect.MidX, rect.MidY));
            kit.Capture();
        }
        Pick(panel.Options.First().Title);   // the ancestry feat column, leftmost
        Pick("Medicine");
        Pick("Strength");
        Pick("Dexterity");
        Assert.Equal(["2 more attribute boosts"], panel.Missing(character, 5));
        kit.Capture(Shot("level-up-narrow"));
        Pick("Constitution");
        Pick("Wisdom");
        Assert.Empty(panel.Missing(character, 5));
        Assert.True(panel.ConfirmButton.Right <= 731);
        kit.Click(new Point(panel.ConfirmButton.MidX, panel.ConfirmButton.MidY));
        Assert.Empty(character.PendingLevels);
    }

    [AvaloniaFact]
    public void Hovering_a_skill_increase_shows_its_checks_before_and_after_and_what_it_opens()
    {
        var kit = TestKit.Open();
        var world = kit.World;
        TestHooks.GainXp(world, 2000);                                    // to level 3, which brings a skill increase
        world.ChooseLevel(2, new AshenReliquary.Core.Characters.LevelChoice(ClassFeat: "lunge", SkillFeat: "titan_wrestler"));
        kit.Window.View.LevelUpOpen = true;
        kit.Play(0.1);
        var panel = kit.Window.View.LevelUp;
        var character = world.Hero.Character;
        Assert.Equal(AshenReliquary.Core.Rules.ProficiencyRank.Untrained, character.SkillRank("Thievery"));
        var (rect, _) = panel.Options.First(o => o.Title == "Thievery");
        kit.Window.MouseMove(new Point(rect.MidX, rect.MidY));
        kit.Play(0.05);
        kit.Capture(Shot("level-up-skill-tip"));
        var tip = panel.Tip!.Select(l => l.Text + (l.Right is { } r ? " " + r : "")).ToList();
        Assert.Equal("Thievery: Untrained → Trained", tip[0]);
        Assert.Contains($"Thievery checks {character.Skill("Thievery").Total:+0;-0;+0} → {character.SkillAt("Thievery", AshenReliquary.Core.Rules.ProficiencyRank.Trained).Total:+0;-0;+0}", tip);
        Assert.Contains(tip, l => l.Contains("disarm traps"));
        Assert.Contains(tip, l => l.Contains("Quick Lockpick"));
        kit.Window.MouseMove(new Point(4, 4));
        kit.Play(0.05);
        Assert.Null(panel.Tip);
    }

    [AvaloniaFact]
    public void Respec_at_the_lantern_altar_closes_the_conversation_and_opens_the_level_up_screen()
    {
        var kit = TestKit.Open(zoneId: "fort_gallowmere");
        var world = kit.World;
        TestHooks.GainXp(world, 1000);
        world.ChooseLevel(2, new AshenReliquary.Core.Characters.LevelChoice(ClassFeat: "lunge", SkillFeat: "titan_wrestler"));
        world.Interact(world.Interactives.Single(i => i.Id == "altar"));
        kit.PlayUntil(() => world.Dialogue is not null, 8);
        kit.Window.View.Answer(0);
        kit.Play(0.2);
        kit.Capture(Shot("altar-respec"));
        Assert.Null(world.Dialogue);
        Assert.True(kit.Window.View.LevelUpOpen);
        Assert.Equal([2], world.Hero.Character.PendingLevels);
    }

    [AvaloniaFact]
    public void Opening_the_level_up_screen_closes_a_conversation()
    {
        var kit = TestKit.Open(zoneId: "fort_gallowmere");
        var world = kit.World;
        TestHooks.GainXp(world, 1000);
        world.Interact(world.Interactives.Single(i => i.Id == "altar"));
        kit.PlayUntil(() => world.Dialogue is not null, 8);
        kit.Window.View.OpenLevelUp();
        Assert.Null(world.Dialogue);
        Assert.True(kit.Window.View.LevelUpOpen);
    }

    [Fact]
    public void The_fighter_starts_in_chain_mail_without_slowing_down()
    {
        var session = GameSession.NewGame("fighter", seed: 1);
        var hero = session.World.Hero.Character;
        Assert.Equal("Chain mail", hero.Armor.Definition.Name);
        Assert.Equal(18, hero.ArmorClass().Total);
        Assert.Equal(0, hero.Armor.SpeedPenaltyFeet(hero.Attributes[AttributeKind.Strength])); // STR meets its requirement
    }

    [AvaloniaFact]
    public void Picking_a_dwarf_wizard_and_beginning_starts_the_game()
    {
        var (window, vm, kit) = Open(1288, 770);
        vm.Choose("dwarf", "hedge-scholar", "wizard");
        vm.Heritage = vm.Heritages.First(h => h.Id == "forge-blooded");
        vm.Name = "Brunna";
        kit.Pump();
        Assert.True(vm.CanBegin, vm.Problem);
        Assert.Equal("−1", vm.Rows.Single(r => r.Kind == AttributeKind.Charisma).Ancestry.Text);
        kit.Capture(Shot("character-creation-dwarf-wizard"));

        vm.BeginCommand.Execute(null);
        kit.Pump();
        Assert.False(window.CreationView.IsVisible);
        var hero = window.Session!.World.Hero.Character;
        Assert.Equal(("Brunna", "dwarf", "wizard"), (hero.Name, hero.Ancestry.Id, hero.Class.Id));
        Assert.Equal(5, hero.Defenses.Resistances[Core.Combat.DamageType.Fire]);
        Assert.Equal("staff", hero.Weapon.Definition.Id);
    }

    /// <summary>
    /// At 1280x700 every section still fits its column without overlapping the one above (each column stacks
    /// its parts in order and scrolls if it must).
    /// </summary>
    [AvaloniaFact]
    public void Columns_never_overlap_on_a_short_window()
    {
        var (window, vm, kit) = Open(1280, 700);
        vm.Choose("orc", "noble-exile", "wizard");
        kit.Capture(Shot("character-creation-short"));
        foreach (var name in new[] { "AncestryList", "HeritageList", "BackgroundList", "ClassList" })
            Assert.NotNull(window.CreationView.FindControl<ListBox>(name));
        var lists = new[] { ("AncestryList", "HeritageList"), ("BackgroundList", "ClassList") };
        foreach (var (upper, lower) in lists)
        {
            var a = window.CreationView.FindControl<ListBox>(upper)!;
            var b = window.CreationView.FindControl<ListBox>(lower)!;
            var aBottom = a.TranslatePoint(new Avalonia.Point(0, a.Bounds.Height), window.CreationView)!.Value.Y;
            var bTop = b.TranslatePoint(new Avalonia.Point(0, 0), window.CreationView)!.Value.Y;
            Assert.True(bTop >= aBottom, $"{lower} starts at {bTop:0} but {upper} ends at {aBottom:0}");
        }
    }

    [AvaloniaFact]
    public void Every_ancestry_has_portraits()
    {
        var (_, vm, _) = Open();
        int Count(string ancestry)
        {
            vm.Choose(ancestry, "deserter", "fighter");
            return vm.AncestryPortraits.Count;
        }
        Assert.True(Count("human") >= 40);
        Assert.Equal(8, Count("elf"));
        Assert.Equal(9, Count("dwarf"));
        Assert.Equal(3, Count("orc"));
        Assert.Equal(4, Count("ashborn"));
        Assert.Equal(4, Count("halfling"));
        Assert.True(vm.HasPortrait);
        Assert.True(vm.CanBegin);
    }

    [AvaloniaFact]
    public void Portrait_arrows_wrap_and_change_resets_with_ancestry()
    {
        var (_, vm, _) = Open();
        vm.Choose("dwarf", "deserter", "fighter");
        var first = vm.SelectedPortrait;
        vm.PreviousPortraitCommand.Execute(null);
        Assert.Equal(vm.AncestryPortraits.Count - 1, vm.PortraitIndex);
        vm.NextPortraitCommand.Execute(null);
        Assert.Equal(first, vm.SelectedPortrait);
        vm.NextPortraitCommand.Execute(null);
        Assert.Contains($"2 / {vm.AncestryPortraits.Count}", vm.PortraitCaption);
        vm.Choose("orc", "deserter", "fighter");
        Assert.Equal(0, vm.PortraitIndex);
        Assert.Equal("orc", vm.SelectedPortrait!.Ancestry);
    }

    [AvaloniaFact]
    public void The_chosen_portrait_goes_into_the_game()
    {
        var (window, vm, kit) = Open(1288, 770);
        vm.Choose("orc", "deserter", "fighter");
        vm.NextPortraitCommand.Execute(null);
        var chosen = vm.SelectedPortrait!.Id;
        kit.Capture(Shot("character-creation-orc"));
        vm.BeginCommand.Execute(null);
        kit.Pump();
        Assert.Equal(chosen, window.Session!.PortraitId);
        window.View.InventoryOpen = true;
        window.Tick(TimeSpan.FromSeconds(1 / 30.0));
        kit.Capture(Shot("inventory-portrait"));
    }

    [AvaloniaFact]
    public void Every_class_is_playable_and_missing_boosts_block_begin()
    {
        var (_, vm, kit) = Open();
        Assert.All(vm.Classes, c => Assert.True(GameSession.IsPlayable(c.Definition.Id), c.Definition.Id));
        vm.SelectedClass = vm.Classes.First(c => c.Definition.Id == "necromancer");
        kit.Pump();
        vm.RecommendCommand.Execute(null);
        Assert.True(vm.CanBegin, vm.Problem);

        vm.SelectedClass = vm.Classes.First(c => c.Definition.Id == "fighter");
        vm.ToggleFree(vm.Rows.First(r => r.Free.IsSelected).Kind);
        Assert.False(vm.CanBegin);
        Assert.Contains("1 more free boost", vm.Problem);
        vm.RecommendCommand.Execute(null);
        Assert.True(vm.CanBegin);
    }

    [AvaloniaFact]
    public void Boost_columns_only_allow_legal_picks()
    {
        var (_, vm, _) = Open(); // Human Deserter Fighter
        var str = vm.Rows.Single(r => r.Kind == AttributeKind.Strength);
        var intel = vm.Rows.Single(r => r.Kind == AttributeKind.Intelligence);
        Assert.False(intel.Background.IsEnabled);   // Deserter boosts STR or CON
        Assert.False(intel.Key.IsEnabled);          // the Fighter's key is STR or DEX
        Assert.False(str.BackgroundFree.IsEnabled); // already the background's boost
        Assert.True(str.Key.IsSelected);
    }
}

/// <summary>Pumps and captures a window that has no game session yet.</summary>
internal sealed class TestKitLite(MainWindow window)
{
    public void Pump()
    {
        Dispatcher.UIThread.RunJobs();
        Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    public void Capture(string? path)
    {
        Pump();
        var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No frame was rendered.");
        if (path is not null)
            frame.Save(path, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }
}
