namespace AshenReliquary.Core.Content;

/// <summary>
/// An ending (the choice at the Throne): its name, its slide's words, and where its scene is set (a zone, and the
/// person or thing there to frame: an NPC, an interactable or a vendor's id). An ending told <see cref="Alone"/> (the New
/// Prophet: the province burns) is followed only by the slides written for it.
/// </summary>
public sealed record EndingDefinition(string Title, string Text, string Zone, string Near, bool Alone = false);

/// <summary>One telling of a slide, shown while its flags hold (flag name to value; "" for "not set").</summary>
public sealed record EpilogueVariant(IReadOnlyDictionary<string, string> Requires, string Text);

/// <summary>A place the hero's choices reached: told by the first of its variants whose flags hold, or not at all.</summary>
public sealed record EpilogueSlideDefinition(string Id, string Title, string Zone, string Near, IReadOnlyList<EpilogueVariant> Variants);

/// <summary>
/// The epilogue's words (epilogue.json): the four endings, and the slides after them, each for a place the hero's
/// choices reached (Gallowmere's refugees, the Ferry, Ironhold's faction, the Confessor, the Penitent, the Order, the
/// drakeling, the scarecrow, Malachar ...).
/// </summary>
public sealed record EpilogueDefinition(IReadOnlyDictionary<string, EndingDefinition> Endings, IReadOnlyList<EpilogueSlideDefinition> Slides)
{
    /// <summary>The flag the ending reached is kept in (destroy, bind, claim or mercy).</summary>
    public const string EndingFlag = "act5.ending";

    /// <summary>
    /// The slides for an ending and the flags the hero's story left: the ending's first, then each place in order whose
    /// telling holds.
    /// </summary>
    public IReadOnlyList<EpilogueSlide> SlidesFor(string ending, IReadOnlyDictionary<string, string> flags)
    {
        bool Holds(IReadOnlyDictionary<string, string> requires) =>
            requires.All(r => r.Value.Length == 0 ? !flags.ContainsKey(r.Key) : flags.GetValueOrDefault(r.Key) == r.Value);
        var slides = new List<EpilogueSlide>();
        var end = Endings.GetValueOrDefault(ending);
        if (end is not null)
            slides.Add(new EpilogueSlide(ending, end.Title, end.Text, end.Zone, end.Near, true));
        foreach (var slide in Slides)
        {
            if (slide.Variants.FirstOrDefault(v => Holds(v.Requires)) is { } told && (end?.Alone != true || told.Requires.ContainsKey(EndingFlag)))
                slides.Add(new EpilogueSlide(slide.Id, slide.Title, told.Text, slide.Zone, slide.Near, false));
        }
        return slides;
    }
}

/// <summary>A slide as shown: its words, and the scene behind them.</summary>
public sealed record EpilogueSlide(string Id, string Title, string Text, string Zone, string Near, bool IsEnding);

/// <summary>One third-party source in the credits roll (from the asset manifest, by tools/build_credits.py).</summary>
public sealed record CreditSource(string Title, string Authors, string Licence, string? Credit);

/// <summary>The credits roll's sources (credits.json).</summary>
public sealed record CreditsDefinition(IReadOnlyList<CreditSource> Sources);
