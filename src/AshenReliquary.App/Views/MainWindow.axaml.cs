using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using AshenReliquary.App.Audio;
using AshenReliquary.App.Rendering;
using AshenReliquary.App.Services;
using AshenReliquary.App.ViewModels;
using AshenReliquary.Core.Saving;

namespace AshenReliquary.App.Views;

/// <summary>
/// The one window: the title screen, character creation, or the game (with its Escape menu over it).
/// The game autosaves at shrines (GameSession), when you leave through the menu, and when the window closes.
/// </summary>
public partial class MainWindow : Window
{
    private GameLoop? _loop;
    private GameSession? _session;

    /// <summary>False in UI tests: they call <see cref="Tick"/> themselves so every frame is deterministic.</summary>
    public static bool RunGameLoop { get; set; } = true;

    public MainWindow()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        ResumeButton.Click += (_, _) => CloseMenu();
        SaveButton.Click += (_, _) => { SaveNow(); MenuNote.Text = "Saved."; };
        TitleButton.Click += (_, _) => { SaveNow(); ShowTitle(); };
        QuitGameButton.Click += (_, _) => Close();
        JustQuitButton.Click += (_, _) => { _quitWithoutSaving = true; Close(); };
        Closing += (_, _) =>
        {
            if (!_quitWithoutSaving)
                SaveNow(announce: false);
        };
        // Every button in the game clicks.
        AddHandler(Button.ClickEvent, (_, _) => Audio.Play("click", 0.6f), RoutingStrategies.Bubble, handledEventsToo: true);
        var settings = Services.Settings.Load();
        MasterSlider.Value = settings.Master;
        EffectsSlider.Value = settings.Effects;
        MusicSlider.Value = settings.Music;
        AmbienceSlider.Value = settings.Ambience;
        foreach (var slider in new[] { MasterSlider, EffectsSlider, MusicSlider, AmbienceSlider })
            slider.PropertyChanged += (_, e) => { if (e.Property == Slider.ValueProperty) VolumesChanged(); };
    }

    private Services.AudioDirector _audio = new(new NullAudioEngine());

    /// <summary>What plays: the window drives it each frame. Silent (NullAudioEngine) until the app gives it OpenAL.</summary>
    public Services.AudioDirector Audio
    {
        get => _audio;
        set
        {
            _audio = value;
            SoundNote.Text = value.Engine is NullAudioEngine { Reason: { } reason } ? reason : "";
        }
    }

    private void VolumesChanged()
    {
        var settings = new Services.Settings
        {
            Master = (float)MasterSlider.Value, Effects = (float)EffectsSlider.Value,
            Music = (float)MusicSlider.Value, Ambience = (float)AmbienceSlider.Value,
        };
        Audio.Apply(settings);
        settings.Save();
    }

    public WorldView View => World;
    public CharacterCreationView CreationView => Creation;
    public TitleView TitleScreen => TitlePage;
    public bool MenuOpen => GameMenu.IsVisible;
    public bool Paused { get; private set; }

    public GameSession? Session
    {
        get => _session;
        set
        {
            _session = value;
            World.Session = value;
            World.InvalidateVisual();
            if (value is not null)
                ShowGame();
        }
    }

    /// <summary>The first screen: the title if any character is saved, otherwise straight into character creation.</summary>
    public void ShowStart()
    {
        if (SaveStore.List().Count > 0)
            ShowTitle();
        else
            ShowCharacterCreation();
    }

    public TitleViewModel ShowTitle()
    {
        _session = null;
        World.Session = null;
        var vm = new TitleViewModel();
        vm.LoadRequested += save => Session = GameSession.FromSave(save);
        vm.NewRequested += () => ShowCharacterCreation();
        vm.QuitRequested += Close;
        TitlePage.DataContext = vm;
        ShowOnly(TitlePage);
        return vm;
    }

    /// <summary>Shows the character creation screen; Begin starts a new game on Gallows Road.</summary>
    public CharacterCreationViewModel ShowCharacterCreation()
    {
        var vm = new CharacterCreationViewModel();
        vm.Started += (choices, portrait) =>
        {
            var session = GameSession.NewGame(choices, Environment.TickCount);
            session.PortraitId = portrait?.Id;
            Session = session;
            SaveNow(announce: false);
        };
        Creation.DataContext = vm;
        ShowOnly(Creation);
        return vm;
    }

    private void ShowGame()
    {
        ShowOnly(World);
        Paused = false;
        World.Focus();
    }

    private void ShowOnly(Control screen)
    {
        TitlePage.IsVisible = screen == TitlePage;
        Creation.IsVisible = screen == Creation;
        World.IsVisible = screen == World;
        GameMenu.IsVisible = false;
    }

    /// <summary>Saves the game in play, if there is one; a failed save is reported, never thrown at the player.</summary>
    public void SaveNow(bool announce = true)
    {
        if (_session is null || !World.IsVisible)
            return;
        try
        {
            _session.Save(announce);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _session.World.Announce($"Could not save: {e.Message}");
        }
    }

    private void OpenMenu()
    {
        GameMenu.IsVisible = true;
        MenuNote.Text = "";
        Paused = true;
        ResumeButton.Focus();
    }

    private void CloseMenu()
    {
        GameMenu.IsVisible = false;
        Paused = false;
        World.Focus();
    }

    /// <summary>How long a critical hit freezes the world (hit-stop).</summary>
    public const double HitStopSeconds = 0.07;

    private double _hitStop;
    private int _crits, _critsTaken;

    /// <summary>"--battle": the hero fights the zone alone.</summary>
    public Services.BattleDemo? Demo { get; set; }

    /// <summary>Set by "Just quit": the window closes without writing the save.</summary>
    private bool _quitWithoutSaving;

    public void Tick(TimeSpan elapsed)
    {
        if (!Paused && _session is not null && World.IsVisible)
        {
            // A critical hit stops the world for a few frames and shakes the view: the blow lands with weight.
            var world = _session.World;
            if (world.HeroCrits != _crits || world.HeroCritsTaken != _critsTaken)
            {
                World.Shake(world.HeroCrits != _crits ? 0.22 : 0.3, world.HeroCrits != _crits ? 5 : 7);
                _hitStop = HitStopSeconds;
                (_crits, _critsTaken) = (world.HeroCrits, world.HeroCritsTaken);
            }
            if (_hitStop > 0)
            {
                _hitStop -= elapsed.TotalSeconds;
                elapsed = TimeSpan.Zero;
            }
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            _session.World.Update(elapsed.TotalSeconds);
            World.Stats.Add("update", System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            Demo?.Update(elapsed.TotalSeconds);
            _session.AddPlayTime(elapsed.TotalSeconds);
        }
        if (_session is not null && World.IsVisible)
            Audio.Update(_session.World, elapsed.TotalSeconds, Paused);
        else
            Audio.Title(elapsed.TotalSeconds);
        World.InvalidateVisual();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (RunGameLoop)
        {
            _loop = new GameLoop(Tick);
            _loop.Start();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _loop?.Dispose();
        Audio.Engine.Dispose();
        base.OnClosed(e);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (!World.IsVisible || _session?.World is not { } world)
            return;
        if (e.Key == Key.Escape)
        {
            if (GameMenu.IsVisible) CloseMenu();
            else if (World.LevelUpOpen) World.LevelUpOpen = false;
            else if (world.Dialogue is not null) world.EndDialogue();
            else if (World.JournalOpen) World.JournalOpen = false;
            else if (world.OpenVendor is not null) world.CloseVendor();
            else if (world.OpenShrine is not null) world.CloseRift();
            else if (world.StashOpen) world.CloseStash();
            else OpenMenu();
            e.Handled = true;
            return;
        }
        if (GameMenu.IsVisible)
            return;
        switch (e.Key)
        {
            case Key.Q or Key.H:
                world.DrinkPotion();
                break;
            case Key.Space or Key.P:
                Paused = !Paused;
                break;
            case Key.S:
                world.Stop();
                break;
            case Key.F5:
                SaveNow();
                break;
            case >= Key.D1 and <= Key.D9 when world.Dialogue is not null:
                World.Answer((int)(e.Key - Key.D1));
                break;
            case >= Key.D1 and <= Key.D6:
                World.UseSlot(((int)(e.Key - Key.D1) + 1).ToString());
                break;
            case >= Key.D7 and <= Key.D9:
                World.UseBelt((int)(e.Key - Key.D7));
                break;
            case Key.D0:
                World.UseBelt(3);
                break;
            case Key.J:
                World.JournalOpen = !World.JournalOpen;
                break;
            case Key.L:
                if (World.LevelUpOpen)
                    World.LevelUpOpen = false;
                else
                    World.OpenLevelUp();
                break;
            case Key.LeftShift or Key.RightShift:
                World.DodgeTowardPointer();
                break;
            case Key.I or Key.Tab:
                World.InventoryOpen = !World.InventoryOpen;
                Audio.Play("inventory", 0.7f);
                break;
            case Key.C:
                World.CharacterSheetOpen = !World.CharacterSheetOpen;
                break;
            case Key.F3:
                World.ShowStats = !World.ShowStats;
                break;
            default:
                return;
        }
        e.Handled = true;
    }
}
