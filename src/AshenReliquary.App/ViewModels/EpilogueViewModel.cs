using AshenReliquary.Core.Content;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AshenReliquary.App.ViewModels;

/// <summary>A line of the credits roll: a heading (gold, larger) or a line under it (a blank one between sources).</summary>
public sealed record CreditLine(string Text, bool IsHeading);

/// <summary>
/// The epilogue (M5 step 4), shown when an ending is chosen at the Throne: the ending's slide, then a slide for each place
/// the hero's choices reached (<see cref="EpilogueDefinition.SlidesFor"/>), each over a scene of that place drawn from the
/// game's own zones, then the credits rolled from the asset manifest; then back to the title.
/// </summary>
public sealed partial class EpilogueViewModel : ObservableObject
{
    public EpilogueViewModel(string ending, IReadOnlyDictionary<string, string> flags, string heroName)
    {
        Slides = ContentDatabase.Epilogue.SlidesFor(ending, flags);
        Flags = flags;
        HeroName = heroName;
        Ending = ContentDatabase.Epilogue.Endings.GetValueOrDefault(ending)?.Title ?? ending;
        _current = Slides.FirstOrDefault();
        Credits = CreditsFor(Ending, heroName);
    }

    /// <summary>The slides in order, the ending's first.</summary>
    public IReadOnlyList<EpilogueSlide> Slides { get; }

    /// <summary>The story's flags, for the scenes (the people in each place are the ones the story left there).</summary>
    public IReadOnlyDictionary<string, string> Flags { get; }

    public string HeroName { get; }

    /// <summary>The ending's name.</summary>
    public string Ending { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Counter), nameof(ShowingSlides), nameof(NextLabel))]
    private EpilogueSlide? _current;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowingSlides))]
    private bool _rollingCredits;

    /// <summary>Raised when the credits are over (or skipped): the window goes back to the title.</summary>
    public event Action? Finished;

    public bool ShowingSlides => !RollingCredits && Current is not null;

    public string Counter => Current is { } slide ? $"{Slides.ToList().IndexOf(slide) + 1} / {Slides.Count}" : "";

    public string NextLabel => Current is { } slide && Slides.ToList().IndexOf(slide) == Slides.Count - 1 ? "The credits  ›" : "Continue  ›";

    /// <summary>The credits roll's lines.</summary>
    public IReadOnlyList<CreditLine> Credits { get; }

    /// <summary>The next slide; after the last, the credits.</summary>
    [RelayCommand]
    private void Next()
    {
        if (RollingCredits)
        {
            Finished?.Invoke();
            return;
        }
        var index = Current is { } slide ? Slides.ToList().IndexOf(slide) : -1;
        if (index + 1 < Slides.Count)
            Current = Slides[index + 1];
        else
            RollingCredits = true;
    }

    /// <summary>Straight to the end of it (Escape): the credits if the slides are showing, else the title.</summary>
    [RelayCommand]
    private void Skip()
    {
        if (RollingCredits)
            Finished?.Invoke();
        else
            RollingCredits = true;
    }

    private static IReadOnlyList<CreditLine> CreditsFor(string ending, string heroName)
    {
        var lines = new List<CreditLine>
        {
            new("ASHEN RELIQUARY", true),
            new($"{heroName}  ·  {ending}", false),
            new(" ", false),
            new("Rules", true),
            new("After Pathfinder 2e Remaster, published under the ORC licence. The Pathfinder name, setting and trademarks are not used.", false),
            new(" ", false),
        };
        foreach (var source in ContentDatabase.Credits.Sources)
        {
            lines.Add(new CreditLine(source.Title, true));
            lines.Add(new CreditLine(source.Authors, false));
            lines.Add(new CreditLine(source.Licence, false));
            if (source.Credit is { } credit)
                lines.Add(new CreditLine(credit, false));
            lines.Add(new CreditLine(" ", false));
        }
        lines.Add(new CreditLine("Thank you for playing.", true));
        return lines;
    }
}
