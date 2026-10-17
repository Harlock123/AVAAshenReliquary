using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using AshenReliquary.App.Rendering;
using AshenReliquary.App.Services;
using AshenReliquary.App.ViewModels;

namespace AshenReliquary.App.Views;

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
    }

    public WorldView View => World;

    public CharacterCreationView CreationView => Creation;

    /// <summary>Shows the character creation screen; Begin starts a new game on Gallows Road.</summary>
    public CharacterCreationViewModel ShowCharacterCreation()
    {
        var vm = new CharacterCreationViewModel();
        vm.Started += (choices, portrait) =>
        {
            Session = GameSession.NewGame(choices, Environment.TickCount) is var session && portrait is not null
                ? session.WithPortrait(portrait.Id)
                : session;
            Creation.IsVisible = false;
            World.IsVisible = true;
            World.Focus();
        };
        Creation.DataContext = vm;
        Creation.IsVisible = true;
        World.IsVisible = false;
        return vm;
    }

    public bool Paused { get; private set; }

    public GameSession? Session
    {
        get => _session;
        set
        {
            _session = value;
            World.Session = value;
            World.InvalidateVisual();
        }
    }

    public void Tick(TimeSpan elapsed)
    {
        if (!Paused)
            _session?.World.Update(elapsed.TotalSeconds);
        World.InvalidateVisual();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        World.Focus();
        if (RunGameLoop)
        {
            _loop = new GameLoop(Tick);
            _loop.Start();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _loop?.Dispose();
        base.OnClosed(e);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (Creation.IsVisible || _session?.World is not { } world)
            return;
        switch (e.Key)
        {
            case Key.Q or Key.H:
                world.DrinkPotion();
                break;
            case Key.Space or Key.P or Key.Escape:
                Paused = !Paused;
                break;
            case Key.S:
                world.Stop();
                break;
            case >= Key.D1 and <= Key.D6:
                World.UseSlot(((int)(e.Key - Key.D1) + 1).ToString());
                break;
            case Key.I or Key.Tab or Key.C:
                World.InventoryOpen = !World.InventoryOpen;
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
