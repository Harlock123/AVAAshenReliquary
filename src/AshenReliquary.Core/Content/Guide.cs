namespace AshenReliquary.Core.Content;

/// <summary>A titled stretch of a guide chapter's prose (a null heading continues the chapter's opening).</summary>
public sealed record GuideSection(string? Heading, string Text);

/// <summary>One hand-written chapter of the in-game guide.</summary>
public sealed record GuideChapter(string Id, string Title, IReadOnlyList<GuideSection> Sections);

/// <summary>An ancestry's page: who they are, and what they suit (its numbers come from the ancestry itself).</summary>
public sealed record GuideAncestry(string Tagline, string Text, string Suits);

/// <summary>A class's page: how it plays, its strengths and weaknesses, tips, and a recommended start.</summary>
public sealed record GuideClass(string Tagline, string Text, string Strengths, string Weaknesses, IReadOnlyList<string> Tips, string Start);

/// <summary>
/// The in-game guide's words (guide.json): the hand-written chapters, and a write-up for every attribute, ancestry,
/// class and subclass. Everything numeric (hit points, boosts, abilities and their levels) is read from the game's own
/// content when the guide is shown, so it never disagrees with the rules.
/// </summary>
public sealed record GuideDefinition(
    IReadOnlyList<GuideChapter> Chapters,
    IReadOnlyDictionary<string, string> Attributes,
    IReadOnlyDictionary<string, GuideAncestry> Ancestries,
    IReadOnlyDictionary<string, GuideClass> Classes,
    IReadOnlyDictionary<string, string> Subclasses);
