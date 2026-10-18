using AshenReliquary.Core.Characters;
using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Content;
using AshenReliquary.Core.Rules;

namespace AshenReliquary.Core.Tests;

public class CreationTests
{
    private static ContentDatabase Content => ContentDatabase.Embedded;

    [Fact]
    public void Every_ancestry_background_and_class_has_a_legal_recommended_build()
    {
        foreach (var a in Content.Ancestries.Keys)
        foreach (var b in Content.Backgrounds.Keys)
        foreach (var c in Content.Classes.Keys)
        {
            var character = CharacterBuilder.Create(Content, CharacterBuilder.Recommend(Content, "Test", a, b, c));
            var key = character.KeyAttribute;
            Assert.True(character.Attributes[key] >= 3, $"{a}/{b}/{c}: key {key} only {character.Attributes[key]}");
        }
    }

    [Fact]
    public void Every_ancestry_has_two_heritages()
    {
        Assert.All(Content.Ancestries.Values, a => Assert.Equal(2, a.Heritages!.Count));
    }

    private static Character Make(string ancestry, string heritage, string classId = "fighter") =>
        CharacterBuilder.Create(Content, CharacterBuilder.Recommend(Content, "Test", ancestry, "deserter", classId, heritageId: heritage));

    [Fact]
    public void Heritages_with_numbers_apply_them()
    {
        Assert.Equal(5, Make("dwarf", "forge-blooded").Defenses.Resistances[DamageType.Fire]);
        Assert.Equal(30, Make("halfling", "nimble-foot").SpeedFeet);
        var scarred = Make("orc", "battle-scarred");
        var plain = Make("orc", "grave-marked");
        Assert.Equal(plain.MaxHitPoints + 2, scarred.MaxHitPoints);
        Assert.Equal(Make("ashborn", "cinder-veined").Save(StatisticKind.Will).Total + 1, Make("ashborn", "pale-lit").Save(StatisticKind.Will).Total);
    }

    [Fact]
    public void Ashborn_fire_resistance_grows_with_level()
    {
        var ashborn = Make("ashborn", "cinder-veined");
        // Half the level (at least 1), plus 2 from the recommended ancestry feat, Cinder Skin.
        Assert.Equal(1 + 2, ashborn.Defenses.Resistances[DamageType.Fire]);
        TestCharacters.AtLevel(ashborn, 6);
        Assert.Equal(3 + 2, ashborn.Defenses.Resistances[DamageType.Fire]);
    }

    [Fact]
    public void A_heritage_from_another_ancestry_is_refused() =>
        Assert.Throws<RulesException>(() => Make("elf", "forge-blooded"));

    [Fact]
    public void A_name_is_required() =>
        Assert.Throws<RulesException>(() => CharacterBuilder.Create(Content, CharacterBuilder.Recommend(Content, "  ", "human", "deserter", "fighter")));

    [Fact]
    public void Recommended_builds_put_the_key_attribute_first()
    {
        var wizard = CharacterBuilder.Create(Content, CharacterBuilder.Recommend(Content, "W", "elf", "hedge-scholar", "wizard"));
        Assert.Equal(4, wizard.Attributes[AttributeKind.Intelligence]);
        var rogue = CharacterBuilder.Create(Content, CharacterBuilder.Recommend(Content, "R", "halfling", "smuggler", "rogue"));
        Assert.Equal(4, rogue.Attributes[AttributeKind.Dexterity]);
    }
}
