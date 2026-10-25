using System.Collections.ObjectModel;
using AshenReliquary.App.Services;
using AshenReliquary.Core.Abilities;
using AshenReliquary.Core.Characters;
using AshenReliquary.Core.Content;
using AshenReliquary.Core.Rules;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AshenReliquary.App.ViewModels;

/// <summary>A piece of a guide page; the view has a template for each kind.</summary>
public abstract record GuideBlock;

/// <summary>A heading within a page.</summary>
public sealed record GuideHeading(string Text) : GuideBlock;

/// <summary>A paragraph of prose.</summary>
public sealed record GuideParagraph(string Text) : GuideBlock;

/// <summary>A page's opening line, set larger and in the gold of the headings.</summary>
public sealed record GuideTagline(string Text) : GuideBlock;

/// <summary>A boxed aside: tips, strengths, a recommended start.</summary>
public sealed record GuideNote(string Title, IReadOnlyList<string> Lines) : GuideBlock;

/// <summary>One label and value among a page's facts (hit points, speed, boosts).</summary>
public sealed record GuideFact(string Label, string Value);

/// <summary>A grid of facts, beside an optional picture (a class in its kit).</summary>
public sealed record GuideFacts(IReadOnlyList<GuideFact> Facts, Bitmap? Picture = null) : GuideBlock
{
    public bool HasPicture => Picture is not null;
}

/// <summary>A row of portraits (an ancestry's faces).</summary>
public sealed record GuidePortraits(IReadOnlyList<Bitmap> Images) : GuideBlock;

/// <summary>An entry with an icon: an ability (its level and where it comes from), a condition, a feat.</summary>
public sealed record GuideEntry(Bitmap? Icon, string Name, string Meta, string Text) : GuideBlock
{
    public bool HasIcon => Icon is not null;
    public bool HasMeta => Meta.Length > 0;
}

/// <summary>One page of the guide, in the list on the left: a chapter, or an ancestry's or class's page under one.</summary>
public sealed class GuidePage(string id, string title, bool isSub, IReadOnlyList<GuideBlock> blocks)
{
    public string Id { get; } = id;
    public string Title { get; } = title;
    public bool IsSub { get; } = isSub;
    public IReadOnlyList<GuideBlock> Blocks { get; } = blocks;
}

/// <summary>
/// The in-game guide (opened from the title screen, the creation screen and the Escape menu): the chapters of
/// guide.json in order, with a page for each ancestry under "Ancestries" and for each class under "Classes". Every
/// number on those pages is read from the game's content: an ancestry's hit points, speed, boosts, heritages and feats; a
/// class's hit points, key attribute, resource, armour, skills, and every ability it and its subclasses gain, by level.
/// </summary>
public sealed partial class GuideViewModel : ObservableObject
{
    private static readonly ContentDatabase Content = ContentDatabase.Embedded;

    public GuideViewModel()
    {
        foreach (var page in BuildPages())
            Pages.Add(page);
        Selected = Pages[0];
    }

    public ObservableCollection<GuidePage> Pages { get; } = [];

    [ObservableProperty] private GuidePage _selected;

    /// <summary>Raised by the Back button.</summary>
    public event Action? Closed;

    [RelayCommand]
    private void Back() => Closed?.Invoke();

    [RelayCommand]
    private void Next()
    {
        var i = Pages.IndexOf(Selected);
        if (i < Pages.Count - 1)
            Selected = Pages[i + 1];
    }

    [RelayCommand]
    private void Previous()
    {
        var i = Pages.IndexOf(Selected);
        if (i > 0)
            Selected = Pages[i - 1];
    }

    /// <summary>Opens the page with this id (a chapter, "class:fighter", "ancestry:elf").</summary>
    public void Open(string id)
    {
        if (Pages.FirstOrDefault(p => p.Id == id) is { } page)
            Selected = page;
    }

    private static IEnumerable<GuidePage> BuildPages()
    {
        var guide = ContentDatabase.Guide;
        foreach (var chapter in guide.Chapters)
        {
            var blocks = Prose(chapter).ToList();
            switch (chapter.Id)
            {
                case "attributes":
                    blocks.Insert(1, new GuideHeading("The six attributes"));
                    blocks.InsertRange(2, Enum.GetValues<AttributeKind>().Select(a =>
                        new GuideEntry(null, $"{a}  ·  {a.Abbreviation()}", "", guide.Attributes[a.ToString()])));
                    break;
                case "ancestries":
                    blocks.Add(new GuideHeading("At a glance"));
                    blocks.AddRange(Content.Ancestries.Values.Select(a => new GuideEntry(null, a.Name, Glance(a), guide.Ancestries[a.Id].Tagline)));
                    break;
                case "classes":
                    blocks.Add(new GuideHeading("At a glance"));
                    blocks.AddRange(Content.Classes.Values.Select(c => new GuideEntry(null, c.Name, Glance(c), guide.Classes[c.Id].Tagline)));
                    break;
                case "conditions":
                    blocks = Conditions(chapter).ToList();
                    break;
            }
            yield return new GuidePage(chapter.Id, chapter.Title, false, blocks);
            if (chapter.Id == "ancestries")
                foreach (var ancestry in Content.Ancestries.Values)
                    yield return AncestryPage(ancestry, guide.Ancestries[ancestry.Id]);
            if (chapter.Id == "classes")
                foreach (var cls in Content.Classes.Values)
                    yield return ClassPage(cls, guide.Classes[cls.Id], guide.Subclasses);
        }
    }

    private static IEnumerable<GuideBlock> Prose(GuideChapter chapter)
    {
        foreach (var section in chapter.Sections)
        {
            if (section.Heading is { } heading)
                yield return new GuideHeading(heading);
            yield return new GuideParagraph(section.Text);
        }
    }

    /// <summary>The conditions chapter: its opening, then each condition with its icon.</summary>
    private static IEnumerable<GuideBlock> Conditions(GuideChapter chapter)
    {
        foreach (var section in chapter.Sections)
        {
            if (section.Heading is not { } heading)
                yield return new GuideParagraph(section.Text);
            else
                yield return new GuideEntry(GuideArt.Icon(heading), heading == "OffGuard" ? "Off-guard" : heading, "", section.Text);
        }
    }

    private static string Signed(int value) => value >= 0 ? $"+{value}" : value.ToString();

    private static string Glance(AncestryDefinition a) =>
        $"{a.HitPoints} HP · {a.SpeedFeet} ft · {Boosts(a)}";

    private static string Boosts(AncestryDefinition a)
    {
        var parts = a.Boosts.Select(b => $"+{b.Abbreviation()}").ToList();
        if (a.FreeBoosts > 0)
            parts.Add(a.FreeBoosts == 1 ? "+1 free" : $"+{a.FreeBoosts} free");
        parts.AddRange(a.Flaws.Select(f => $"−{f.Abbreviation()}"));
        return string.Join(", ", parts);
    }

    private static string Glance(ClassDefinition c) =>
        $"{c.HitPointsPerLevel} HP a level · {string.Join(" or ", c.KeyAttributes.Select(k => k.Abbreviation()))} · {c.Resource}";

    private static GuidePage AncestryPage(AncestryDefinition a, GuideAncestry words)
    {
        var blocks = new List<GuideBlock> { new GuideTagline(words.Tagline) };
        var portraits = Portraits.For(a.Id).Take(5).Select(Portraits.Bitmap).ToList();
        if (portraits.Count > 0)
            blocks.Add(new GuidePortraits(portraits));
        blocks.Add(new GuideFacts(
        [
            new("Hit points", $"{a.HitPoints} at level 1, before your class's"),
            new("Speed", $"{a.SpeedFeet} feet"),
            new("Senses", a.Senses),
            new("Boosts", a.Boosts.Count == 0 ? $"{a.FreeBoosts} free" : string.Join(", ", a.Boosts.Select(b => b.ToString()))
                + (a.Boosts.Count > 0 && a.FreeBoosts > 0 ? $", and {a.FreeBoosts} free" : "")),
            new("Flaw", a.Flaws.Count == 0 ? "None" : string.Join(", ", a.Flaws.Select(f => f.ToString()))),
            new("Trait", a.Trait),
        ]));
        blocks.Add(new GuideParagraph(words.Text));
        blocks.Add(new GuideNote("Suits", [words.Suits]));
        blocks.Add(new GuideHeading("Heritages"));
        blocks.AddRange((a.Heritages ?? []).Select(h => new GuideEntry(null, h.Name, "", h.Description)));
        blocks.Add(new GuideHeading("Ancestry feats"));
        blocks.AddRange(Content.Feats.Values.Where(f => f.Type == FeatType.Ancestry && f.Ancestry == a.Id).OrderBy(f => f.Level)
            .Select(f => new GuideEntry(null, f.Name, $"Level {f.Level}", f.Description)));
        return new GuidePage($"ancestry:{a.Id}", a.Name, true, blocks);
    }

    private static GuidePage ClassPage(ClassDefinition c, GuideClass words, IReadOnlyDictionary<string, string> subclassWords)
    {
        var subclasses = Content.Feats.Values.Where(f => f.Type == FeatType.Subclass && f.Class == c.Id).ToList();
        var blocks = new List<GuideBlock>
        {
            new GuideTagline(words.Tagline),
            new GuideFacts(
            [
                new("Hit points", $"{c.HitPointsPerLevel} a level, plus Constitution"),
                new("Key attribute", string.Join(" or ", c.KeyAttributes.Select(k => k.ToString()))),
                new("Resource", c.Resource),
                new("Armour", c.ArmorCategories is not { Count: > 0 } armour ? "None" : string.Join(", ", armour)),
                new("Trained skills", string.Join(", ", c.TrainedSkills ?? []) + $", and {c.SkillPicks} of your choice (more with Intelligence)"),
                new("Subclasses", string.Join(", ", subclasses.Select(s => s.Name))),
            ], GuideArt.Doll(c.Id, subclasses.FirstOrDefault()?.Id)),
            new GuideParagraph(words.Text),
            new GuideNote("Strengths", [words.Strengths]),
            new GuideNote("Weaknesses", [words.Weaknesses]),
            new GuideNote("Tips", words.Tips),
            new GuideNote("A good start", [words.Start]),
            new GuideHeading($"{c.Name} abilities"),
        };
        blocks.AddRange(ClassAbilities(c.Id).Select(Entry));
        foreach (var sub in subclasses)
        {
            blocks.Add(new GuideHeading($"Subclass: {sub.Name}"));
            blocks.Add(new GuideParagraph(subclassWords[sub.Id]));
            blocks.Add(new GuideParagraph(sub.Description));
            blocks.AddRange(SubclassAbilities(sub).Select(Entry));
        }
        return new GuidePage($"class:{c.Id}", c.Name, true, blocks);
    }

    /// <summary>A class's own abilities, by the level they join the bar.</summary>
    private static IEnumerable<AbilityDefinition> ClassAbilities(string classId) =>
        Content.Abilities.Values.Where(a => a.Class == classId && !a.Feat).OrderBy(a => a.Level).ThenBy(a => SlotOrder(a.Slot));

    /// <summary>The abilities a subclass grants (not those it only sharpens), by level.</summary>
    private static IEnumerable<AbilityDefinition> SubclassAbilities(FeatDefinition subclass) =>
        subclass.Effects.Where(e => e.Ability is not null && !e.ModifiesAbility)
            .Select(e => Content.Abilities.GetValueOrDefault(e.Ability!))
            .OfType<AbilityDefinition>()
            .Where(a => a.Feat || a.Class == subclass.Class)
            .OrderBy(a => a.Level);

    private static int SlotOrder(string slot) => slot switch { "lmb" => 0, "rmb" => 1, _ => int.TryParse(slot, out var n) ? 1 + n : 99 };

    private static GuideEntry Entry(AbilityDefinition a)
    {
        var key = a.Slot switch { "lmb" => "left mouse", "rmb" => "right mouse", "feat" => null, var s when int.TryParse(s, out var n) && n <= 6 => $"key {s}", _ => null };
        var meta = $"Level {a.Level}" + (key is null ? "" : $" · {key}");
        return new GuideEntry(GuideArt.Icon(a.Id), a.Name, meta, a.Description);
    }
}
