using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AshenReliquary.App.Views;

namespace AshenReliquary.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // The title screen (or character creation, with no saves) comes first; "--fighter", "--wizard", "--rogue",
            // "--ranger", "--champion" or "--necromancer" skips both (or a subclass: "--vanguard", "--bulwark",
            // "--pyromancer", "--stormcaller", "--cutthroat", "--trickster", "--marksman", "--beastmaster",
            // "--lanternbearer", "--oathbreaker", "--paladin", "--bonelord", "--plaguebringer", "--gunslinger", "--pistolero",
            // "--sniper"), and "--battle [zone]" (the Fens by default) starts a fight that plays itself.
            var args = desktop.Args ?? [];
            var window = new MainWindow();
            // Sound: OpenAL with a mixing period big enough for a virtual machine's sound card; "--mute" stays silent.
            var engine = args.Contains("--mute")
                ? new Audio.NullAudioEngine()
                : Audio.OpenAlAudioEngine.CreateOrSilent(Audio.AudioBuffer.PeriodFrames(Audio.AudioBuffer.Automatic, Audio.VirtualMachine.Detect()));
            window.Audio = new Services.AudioDirector(engine);
            var battle = Array.IndexOf(args, "--battle");
            if (battle >= 0)
            {
                // A demo fight: nothing it does is saved over the player's games.
                Services.SaveStore.FolderOverride = Path.Combine(Path.GetTempPath(), "ashen-reliquary-battle");
                var (classId, subclass) = GameSession.QuickStart(args);
                var zone = battle + 1 < args.Length && !args[battle + 1].StartsWith("--") ? args[battle + 1] : "rotwater_fens";
                window.Session = GameSession.NewGame(classId, seed: Environment.TickCount, zoneId: zone, subclass: subclass);
                window.Session.World.TrainTo(window.Session.World.Zone.Level);
                window.Demo = new Services.BattleDemo(window.Session.World, classId);
            }
            else if (new[] { "--wizard", "--fighter", "--rogue", "--ranger", "--vanguard", "--bulwark", "--pyromancer", "--stormcaller",
                             "--cutthroat", "--trickster", "--marksman", "--beastmaster", "--champion", "--lanternbearer", "--oathbreaker", "--paladin",
                             "--necromancer", "--bonelord", "--plaguebringer", "--gunslinger", "--pistolero", "--sniper" }
                         .Any(args.Contains))
            {
                var (classId, subclass) = GameSession.QuickStart(args);
                window.Session = GameSession.NewGame(classId, seed: Environment.TickCount, subclass: subclass);
            }
            else
                window.ShowStart();
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
