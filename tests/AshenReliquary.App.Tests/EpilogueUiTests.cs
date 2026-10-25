using Avalonia.Headless.XUnit;
using AshenReliquary.App.Services;
using AshenReliquary.App.ViewModels;

namespace AshenReliquary.App.Tests;

/// <summary>
/// M5 step 4 on screen: the choice at the Throne, then the epilogue (the ending's slide and each place's, every one over a
/// scene drawn from the game's own zones), the credits rolling, and the title showing the ending reached.
/// </summary>
public class EpilogueUiTests
{
    private static string? Shot(string name) =>
        Environment.GetEnvironmentVariable("ASHEN_SHOTS") is { Length: > 0 } dir ? Path.Combine(dir, name + ".png") : null;

    [AvaloniaFact]
    public void Breaking_the_heart_ends_the_game_with_its_slides_its_credits_and_the_title_remembering()
    {
        SaveStore.FolderOverride = Path.Combine(Path.GetTempPath(), "ashen-tests", Guid.NewGuid().ToString("N"));
        var kit = TestKit.Open("fighter", zoneId: "cinder_throne");
        var world = kit.World;
        world.TrainTo(20);
        foreach (var (flag, value) in new[] { ("act1.sallow_relic", "cured"), ("act2.refugees", "saved"), ("act4.beacon", "lit"),
                     ("act4.egg", "hatched"), ("act3.penitent", "forgiven"), ("act2.confessor", "repaired") })
            world.SetFlag(flag, value);
        foreach (var m in world.Monsters.ToList())
            TestHooks.Slay(world, m);
        world.SetFlag("act5.malachar", "slain");
        var throne = world.Interactives.Single(i => i.Id == "throne");
        TestHooks.Place(world, throne.X, throne.Y + 1.2);
        world.Interact(throne);
        kit.Play(0.3);
        kit.Capture(Shot("throne-the-choice"));
        world.Choose(world.Dialogue!.Choices.ToList().FindIndex(c => c.Text.StartsWith("Break the Ember Heart")));
        world.Choose(0);
        kit.Play(0.2);

        Assert.True(kit.Window.EpilogueView.IsVisible);
        var vm = Assert.IsType<EpilogueViewModel>(kit.Window.EpilogueView.DataContext);
        Assert.Equal("Ashes to Ashes", vm.Current!.Title);
        Assert.Equal(["destroy", "throne", "well", "refugees", "confessor", "penitent", "order", "drakeling"], vm.Slides.Select(s => s.Id));
        kit.Play(0.5);
        kit.Capture(Shot("epilogue-ending"));
        vm.NextCommand.Execute(null);
        vm.NextCommand.Execute(null);
        kit.Play(0.5);
        Assert.Equal("well", vm.Current!.Id);
        kit.Capture(Shot("epilogue-well"));
        while (!vm.RollingCredits)
            vm.NextCommand.Execute(null);
        kit.Play(4);
        Assert.Contains(vm.Credits, c => c.IsHeading && c.Text.StartsWith("Flare"));
        kit.Capture(Shot("epilogue-credits"));
        vm.NextCommand.Execute(null);
        kit.Pump();

        // The title: the character finished, with the ending it reached; loading it puts the hero back at the Throne.
        Assert.True(kit.Window.TitleScreen.IsVisible);
        var title = Assert.IsType<TitleViewModel>(kit.Window.TitleScreen.DataContext);
        var saved = Assert.Single(title.Saves);
        Assert.Equal("Finished: Ashes to Ashes", saved.Finished);
        kit.Capture(Shot("title-finished"));
        Assert.Equal("cinder_throne", saved.Save.ZoneId);
    }
}
