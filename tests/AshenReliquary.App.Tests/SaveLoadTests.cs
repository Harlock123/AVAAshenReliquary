using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using AshenReliquary.App.Services;
using AshenReliquary.App.Views;

namespace AshenReliquary.App.Tests;

public class SaveLoadTests
{
    private static string? Shot(string name) =>
        Environment.GetEnvironmentVariable("ASHEN_SHOTS") is { Length: > 0 } dir ? Path.Combine(dir, name + ".png") : null;

    /// <summary>A fresh, empty save folder for one test.</summary>
    private static void FreshSaves() =>
        SaveStore.FolderOverride = Path.Combine(Path.GetTempPath(), "ashen-tests", Guid.NewGuid().ToString("N"));

    private static void Pump()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static MainWindow Open()
    {
        var window = new MainWindow { Width = 1288, Height = 770 };
        window.ShowStart();
        window.Show();
        Pump();
        return window;
    }

    private static void Play(MainWindow window, double seconds)
    {
        for (var t = 0.0; t < seconds; t += 1 / 30.0)
            window.Tick(TimeSpan.FromSeconds(1 / 30.0));
        Pump();
    }

    [AvaloniaFact]
    public void A_saved_copy_is_a_numbered_save_of_its_own_and_the_game_keeps_saving_to_the_original()
    {
        FreshSaves();
        var window = new MainWindow { Width = 1288, Height = 770, Session = GameSession.NewGame("fighter", 5) };
        window.Show();
        Pump();
        var session = window.Session!;
        var world = session.World;
        var name = world.Hero.Character.Name;
        window.SaveNow(announce: false);
        world.Hero.Equipment.AddGold(50);

        // F6: the first copy is the character's second save, with 50 gold, beside the original.
        window.KeyPress(Key.F6, RawInputModifiers.None, PhysicalKey.F6, null);
        Pump();
        var saves = SaveStore.List();
        Assert.Equal(2, saves.Count);
        var copy = saves.Single(s => s.CopyOf is not null);
        var original = saves.Single(s => s.CopyOf is null);
        Assert.Equal(session.SaveId, copy.CopyOf);
        Assert.Equal(2, copy.CopyNumber);
        Assert.Equal($"{session.SaveId}-2", copy.SaveId);
        Assert.Equal(50, copy.Gold);
        Assert.Equal(0, original.Gold);

        // Playing on saves over the original, never the copy; the next copy is number 3.
        world.Hero.Equipment.AddGold(25);
        window.SaveNow(announce: false);
        Assert.Equal(75, SaveStore.List().Single(s => s.SaveId == session.SaveId).Gold);
        Assert.Equal(50, SaveStore.List().Single(s => s.CopyNumber == 2).Gold);
        Assert.Equal($"{name}-3", window.SaveCopy());

        // On the title screen the copies are named by number; a loaded copy saves over itself.
        var title = new ViewModels.TitleViewModel();
        Assert.Equal(new[] { name, $"{name}-2", $"{name}-3" }, title.Saves.Select(e => e.Name).Order());
        var resumed = GameSession.FromSave(copy);
        Assert.Equal(copy.SaveId, resumed.SaveId);
        resumed.World.Hero.Equipment.AddGold(5);
        resumed.Save(announce: false);
        var after = SaveStore.List().Single(s => s.SaveId == copy.SaveId);
        Assert.Equal(55, after.Gold);
        Assert.Equal(2, after.CopyNumber);

        // A copy of the copy takes the character's next number.
        Assert.Equal($"{name}-4", resumed.SaveCopy());
        var fourth = SaveStore.List().Single(s => s.CopyNumber == 4);
        Assert.Equal(session.SaveId, fourth.CopyOf);
        Assert.Equal($"{session.SaveId}-4", fourth.SaveId);
    }

    [AvaloniaFact]
    public void With_no_saves_the_game_opens_at_character_creation()
    {
        FreshSaves();
        var window = Open();
        Assert.True(window.CreationView.IsVisible);
        Assert.False(window.TitleScreen.IsVisible);
    }

    [AvaloniaFact]
    public void A_new_character_is_saved_at_once_and_appears_on_the_title_screen()
    {
        FreshSaves();
        var window = Open();
        var creation = (ViewModels.CharacterCreationViewModel)window.CreationView.DataContext!;
        creation.Choose("orc", "deserter", "fighter");
        creation.Name = "Grashna";
        creation.BeginCommand.Execute(null);
        Pump();
        Assert.Single(SaveStore.List());

        var reopened = Open();
        Assert.True(reopened.TitleScreen.IsVisible);
        var title = (ViewModels.TitleViewModel)reopened.TitleScreen.DataContext!;
        Assert.Equal("Grashna", title.Selected!.Name);
        Assert.Contains("Level 1 Orc Fighter", title.Selected.Detail);
    }

    [AvaloniaFact]
    public void Continue_restores_the_game_where_it_was_saved()
    {
        FreshSaves();
        var window = new MainWindow { Width = 1288, Height = 770, Session = GameSession.NewGame("fighter", 5) };
        window.Show();
        var world = window.Session!.World;
        world.MoveTo(world.Hero.X - 3, world.Hero.Y - 3);
        Play(window, 2);
        world.Hero.Equipment.AddGold(77);
        window.KeyPress(Key.F5, RawInputModifiers.None, PhysicalKey.F5, null);
        Pump();
        Assert.Equal("Game saved.", world.LastMessage);
        var (x, y) = (world.Hero.X, world.Hero.Y);

        var title = new MainWindow { Width = 1288, Height = 770 };
        title.ShowStart();
        title.Show();
        Pump();
        var vm = (ViewModels.TitleViewModel)title.TitleScreen.DataContext!;
        title.TitleScreen.FindControl<Avalonia.Controls.Button>("ContinueButton")!.Command!.Execute(null);
        Pump();
        Assert.True(title.View.IsVisible);
        var restored = title.Session!.World;
        Assert.Equal(77, restored.Hero.Equipment.Gold);
        Assert.Equal(x, restored.Hero.X, 6);
        Assert.Equal(y, restored.Hero.Y, 6);
        Assert.StartsWith("Welcome back", restored.LastMessage);
        Assert.NotNull(vm);
    }

    [AvaloniaFact]
    public void Escape_pauses_and_the_menu_can_save_and_return_to_the_title()
    {
        FreshSaves();
        var window = new MainWindow { Width = 1288, Height = 770, Session = GameSession.NewGame("wizard", 6) };
        window.Show();
        Pump();
        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Pump();
        Assert.True(window.MenuOpen);
        Assert.True(window.Paused);
        var time = window.Session!.World.Time;
        Play(window, 1);
        Assert.Equal(time, window.Session!.World.Time);
        window.Capture(Shot("escape-menu"));

        window.FindControl<Avalonia.Controls.Button>("TitleButton")!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Pump();
        Assert.True(window.TitleScreen.IsVisible);
        Assert.Single(SaveStore.List());
        window.Capture(Shot("title-screen"));
    }

    [AvaloniaFact]
    public void Save_and_quit_saves_but_just_quit_leaves_the_last_save_alone()
    {
        FreshSaves();
        var saved = new MainWindow { Width = 1288, Height = 770, Session = GameSession.NewGame("fighter", 8) };
        saved.Show();
        Pump();
        saved.FindControl<Avalonia.Controls.Button>("QuitGameButton")!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Pump();
        Assert.Single(SaveStore.List());

        FreshSaves();
        var session = GameSession.NewGame("fighter", 9);
        session.Save(announce: false);
        var before = File.ReadAllText(SaveStore.PathFor(session.SaveId));
        var window = new MainWindow { Width = 1288, Height = 770, Session = session };
        window.Show();
        Pump();
        Play(window, 1);
        session.World.Hero.Equipment.AddGold(77);
        window.FindControl<Avalonia.Controls.Button>("JustQuitButton")!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Pump();
        Assert.Equal(before, File.ReadAllText(SaveStore.PathFor(session.SaveId)));
    }

    [AvaloniaFact]
    public void Deleting_a_character_needs_confirmation()
    {
        FreshSaves();
        GameSession.NewGame("fighter", 1).Save(announce: false);
        var window = Open();
        var vm = (ViewModels.TitleViewModel)window.TitleScreen.DataContext!;
        vm.DeleteCommand.Execute(null);
        Assert.True(vm.ConfirmingDelete);
        Assert.Single(SaveStore.List());
        vm.CancelDeleteCommand.Execute(null);
        vm.DeleteCommand.Execute(null);
        vm.DeleteCommand.Execute(null);
        Assert.Empty(SaveStore.List());
        Assert.False(vm.HasSaves);
    }

    [AvaloniaFact]
    public void A_damaged_save_file_is_skipped_not_deleted()
    {
        FreshSaves();
        Directory.CreateDirectory(SaveStore.Folder);
        var junk = Path.Combine(SaveStore.Folder, "broken.json");
        File.WriteAllText(junk, "{ not json");
        GameSession.NewGame("fighter", 1).Save(announce: false);
        Assert.Single(SaveStore.List());
        Assert.True(File.Exists(junk));
    }
}

internal static class WindowCaptureExtensions
{
    public static void Capture(this MainWindow window, string? path)
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        var frame = window.CaptureRenderedFrame();
        if (path is not null && frame is not null)
            frame.Save(path, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }
}
