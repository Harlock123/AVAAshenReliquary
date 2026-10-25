using System.ComponentModel;
using AshenReliquary.App.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace AshenReliquary.App.Views;

/// <summary>
/// The epilogue: each slide's words over a scene of its place (a <see cref="Rendering.WorldView"/> in scene mode, its
/// world built from the story's flags and kept turning so its fires flicker), then the credits rolling up. Space, Enter
/// or the right arrow go on; Escape skips to the credits, and from the credits to the title.
/// </summary>
public partial class EpilogueView : UserControl
{
    /// <summary>How fast the credits roll, in pixels a second.</summary>
    public const double CreditsSpeed = 38;

    private EpilogueViewModel? _vm;

    public EpilogueView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Bind(DataContext as EpilogueViewModel);
        KeyDown += (_, e) =>
        {
            if (_vm is null)
                return;
            if (e.Key is Key.Space or Key.Enter or Key.Right)
                _vm.NextCommand.Execute(null);
            else if (e.Key == Key.Escape)
                _vm.SkipCommand.Execute(null);
            else
                return;
            e.Handled = true;
        };
    }

    private void Bind(EpilogueViewModel? vm)
    {
        if (_vm is not null)
            _vm.PropertyChanged -= Changed;
        _vm = vm;
        CreditsScroll.Offset = new Vector(0, 0);
        if (vm is null)
        {
            Scene.Session = null;
            return;
        }
        vm.PropertyChanged += Changed;
        ShowScene();
    }

    private void Changed(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EpilogueViewModel.Current))
            ShowScene();
        else if (e.PropertyName == nameof(EpilogueViewModel.RollingCredits) && _vm?.RollingCredits == true)
        {
            Scene.Session = null;
            CreditsScroll.Offset = new Vector(0, 0);
        }
    }

    /// <summary>The scene behind the slide on show: its place, as the story left it.</summary>
    private void ShowScene()
    {
        if (_vm?.Current is not { } slide)
            return;
        lock (Scene.Gate)
            Scene.Session = GameSession.Scene(slide.Zone, _vm.Flags, slide.Near);
    }

    /// <summary>The window's tick, while the epilogue shows: the scene's world turns over, and the credits roll.</summary>
    public void Advance(TimeSpan elapsed)
    {
        var seconds = Math.Min(0.1, elapsed.TotalSeconds);
        if (Scene.Session is { } session)
        {
            lock (Scene.Gate)
                session.World.Update(seconds);
            Scene.InvalidateVisual();
        }
        if (_vm?.RollingCredits == true)
            CreditsScroll.Offset = new Vector(0, CreditsScroll.Offset.Y + CreditsSpeed * seconds);
    }
}
