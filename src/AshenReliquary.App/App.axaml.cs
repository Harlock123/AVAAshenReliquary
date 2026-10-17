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
            // The title screen (or character creation, with no saves) comes first; "--fighter" or "--wizard" skips both.
            var args = desktop.Args ?? [];
            var window = new MainWindow();
            // Sound: OpenAL with a mixing period big enough for a virtual machine's sound card; "--mute" stays silent.
            var engine = args.Contains("--mute")
                ? new Audio.NullAudioEngine()
                : Audio.OpenAlAudioEngine.CreateOrSilent(Audio.AudioBuffer.PeriodFrames(Audio.AudioBuffer.Automatic, Audio.VirtualMachine.Detect()));
            window.Audio = new Services.AudioDirector(engine);
            if (args.Contains("--wizard") || args.Contains("--fighter"))
                window.Session = GameSession.NewGame(args.Contains("--wizard") ? "wizard" : "fighter", seed: Environment.TickCount);
            else
                window.ShowStart();
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
