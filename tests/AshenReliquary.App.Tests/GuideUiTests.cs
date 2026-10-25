using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using AshenReliquary.App.ViewModels;
using AshenReliquary.App.Views;
using AshenReliquary.Core.Content;

namespace AshenReliquary.App.Tests;

/// <summary>The in-game guide: opened from the title, the creation screen and the Escape menu, a page for everything.</summary>
public class GuideUiTests
{
    private static string? Shot(string name) =>
        Environment.GetEnvironmentVariable("ASHEN_SHOTS") is { Length: > 0 } dir ? Path.Combine(dir, name + ".png") : null;

    private static (MainWindow Window, TestKitLite Kit) Window()
    {
        var window = new MainWindow { Width = 1440, Height = 900 };
        window.Show();
        return (window, new TestKitLite(window));
    }

    [AvaloniaFact]
    public void The_guide_has_a_page_for_every_chapter_ancestry_and_class()
    {
        var vm = new GuideViewModel();
        var content = ContentDatabase.Embedded;
        Assert.Equal(ContentDatabase.Guide.Chapters.Count + content.Ancestries.Count + content.Classes.Count, vm.Pages.Count);
        Assert.Equal("welcome", vm.Pages[0].Id);
        Assert.All(content.Ancestries.Keys, id => Assert.Contains(vm.Pages, p => p.Id == $"ancestry:{id}" && p.IsSub));
        Assert.All(content.Classes.Keys, id => Assert.Contains(vm.Pages, p => p.Id == $"class:{id}" && p.IsSub));
        // A class's page lists every ability it and its subclasses gain, each with its icon and level.
        var gunslinger = vm.Pages.Single(p => p.Id == "class:gunslinger");
        var entries = gunslinger.Blocks.OfType<GuideEntry>().ToList();
        Assert.Contains(entries, e => e.Name == "Smoke and Thunder" && e.Meta.StartsWith("Level 16") && e.HasIcon);
        Assert.Contains(entries, e => e.Name == "Perfect Shot" && e.Meta.StartsWith("Level 18"));
        Assert.Contains(entries, e => e.Name == "Fan the Hammer");
        Assert.Contains(gunslinger.Blocks.OfType<GuideFacts>(), f => f.HasPicture && f.Facts.Any(x => x.Label == "Resource" && x.Value == "Grit"));
        // An ancestry's page reads its numbers from the ancestry.
        var dwarf = vm.Pages.Single(p => p.Id == "ancestry:dwarf");
        Assert.Contains(dwarf.Blocks.OfType<GuideFacts>().Single().Facts, f => f.Label == "Speed" && f.Value == "20 feet");
        Assert.NotEmpty(dwarf.Blocks.OfType<GuidePortraits>().Single().Images);
        Assert.Equal(GuideConditions(), vm.Pages.Single(p => p.Id == "conditions").Blocks.OfType<GuideEntry>().Count(e => e.HasIcon));
        vm.NextCommand.Execute(null);
        Assert.Equal("controls", vm.Selected.Id);
        vm.PreviousCommand.Execute(null);
        Assert.Equal("welcome", vm.Selected.Id);
    }

    private static int GuideConditions() => Enum.GetValues<Core.Combat.ConditionKind>().Length;

    [AvaloniaFact]
    public void The_title_s_guide_button_opens_it_and_back_returns()
    {
        var (window, kit) = Window();
        var title = window.ShowTitle();
        kit.Pump();
        title.GuideCommand.Execute(null);
        kit.Pump();
        var guide = (GuideViewModel)window.FindControl<GuideView>("Guide")!.DataContext!;
        Assert.True(window.FindControl<GuideView>("Guide")!.IsVisible);
        kit.Capture(Shot("guide-welcome"));
        foreach (var page in new[] { "attributes", "ancestry:dwarf", "class:fighter", "class:gunslinger", "conditions" })
        {
            guide.Open(page);
            kit.Pump();
            kit.Capture(Shot($"guide-{page.Replace(':', '-')}"));
        }
        guide.BackCommand.Execute(null);
        kit.Pump();
        Assert.False(window.FindControl<GuideView>("Guide")!.IsVisible);
        Assert.True(window.FindControl<TitleView>("TitlePage")!.IsVisible);
    }

    [AvaloniaFact]
    public void The_creation_screen_opens_the_guide_at_the_class_being_made()
    {
        var (window, kit) = Window();
        var vm = window.ShowCharacterCreation();
        kit.Pump();
        vm.Choose("elf", "hedge-scholar", "wizard", "stormcaller");
        vm.GuideCommand.Execute(null);
        kit.Pump();
        var guide = (GuideViewModel)window.FindControl<GuideView>("Guide")!.DataContext!;
        Assert.Equal("class:wizard", guide.Selected.Id);
        guide.BackCommand.Execute(null);
        kit.Pump();
        Assert.True(window.FindControl<CharacterCreationView>("Creation")!.IsVisible);
        Assert.Equal("wizard", vm.SelectedClass.Definition.Id);                      // the hero being made is kept
    }

    [AvaloniaFact]
    public void From_a_game_the_guide_goes_back_to_the_paused_menu()
    {
        var kit = TestKit.Open();
        var window = kit.Window;
        window.FindControl<Avalonia.Controls.Button>("GuideButton")!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        kit.Pump();
        Assert.True(window.FindControl<GuideView>("Guide")!.IsVisible);
        Assert.True(window.Paused);
        ((GuideViewModel)window.FindControl<GuideView>("Guide")!.DataContext!).BackCommand.Execute(null);
        kit.Pump();
        Assert.True(window.MenuOpen);
        Assert.True(window.Paused);
    }
}
