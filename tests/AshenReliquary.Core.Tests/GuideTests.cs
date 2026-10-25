using AshenReliquary.Core.Content;
using AshenReliquary.Core.Rules;

namespace AshenReliquary.Core.Tests;

/// <summary>The in-game guide's words cover everything the game has: every attribute, ancestry, class and subclass.</summary>
public class GuideTests
{
    private static ContentDatabase Content => ContentDatabase.Embedded;
    private static GuideDefinition Guide => ContentDatabase.Guide;

    [Fact]
    public void Every_attribute_ancestry_class_and_subclass_has_its_page()
    {
        Assert.All(Enum.GetValues<AttributeKind>(), a => Assert.False(string.IsNullOrWhiteSpace(Guide.Attributes.GetValueOrDefault(a.ToString())), a.ToString()));
        Assert.All(Content.Ancestries.Keys, id => Assert.True(Guide.Ancestries.ContainsKey(id), id));
        Assert.All(Content.Classes.Keys, id => Assert.True(Guide.Classes.ContainsKey(id), id));
        var subclasses = Content.Feats.Values.Where(f => f.Type == Characters.FeatType.Subclass).Select(f => f.Id);
        Assert.All(subclasses, id => Assert.True(Guide.Subclasses.ContainsKey(id), id));
        Assert.All(Guide.Classes.Values, c => Assert.NotEmpty(c.Tips));
    }

    [Fact]
    public void Its_chapters_are_in_order_and_the_conditions_chapter_names_every_condition()
    {
        Assert.Equal(["welcome", "controls", "attributes", "ancestries", "classes", "combat", "conditions", "gear", "levelling"],
            Guide.Chapters.Select(c => c.Id));
        var conditions = Guide.Chapters.Single(c => c.Id == "conditions").Sections.Select(s => s.Heading).ToHashSet();
        Assert.All(Enum.GetValues<Combat.ConditionKind>(), k => Assert.Contains(k.ToString(), conditions));
        Assert.All(Guide.Chapters.SelectMany(c => c.Sections), s => Assert.False(string.IsNullOrWhiteSpace(s.Text)));
    }
}
