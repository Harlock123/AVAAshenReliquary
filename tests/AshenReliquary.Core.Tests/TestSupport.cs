using AshenReliquary.Core.Characters;
using AshenReliquary.Core.Content;
using AshenReliquary.Core.Rules;

namespace AshenReliquary.Core.Tests;

/// <summary>Returns the scripted rolls in order and fails loudly if a roll is out of range or the script runs out.</summary>
internal sealed class ScriptedRandom(params int[] rolls) : IRandomSource
{
    private readonly Queue<int> _rolls = new(rolls);

    public int Remaining => _rolls.Count;

    public int Roll(int minInclusive, int maxInclusive)
    {
        if (_rolls.Count == 0)
            throw new InvalidOperationException($"No scripted roll left for {minInclusive}..{maxInclusive}.");
        var value = _rolls.Dequeue();
        if (value < minInclusive || value > maxInclusive)
            throw new InvalidOperationException($"Scripted roll {value} is outside {minInclusive}..{maxInclusive}.");
        return value;
    }
}

internal static class TestCharacters
{
    public static ContentDatabase Content => ContentDatabase.Embedded;

    /// <summary>
    /// Human Deserter Fighter: STR +4, DEX +2, CON +2, INT +0, WIS +1, CHA +0.
    /// </summary>
    public static CharacterChoices FighterChoices => new(
        "Ashbound", "human", "deserter", "fighter", AttributeKind.Strength,
        AncestryFreeBoosts: [AttributeKind.Strength, AttributeKind.Constitution],
        BackgroundBoost: AttributeKind.Strength,
        BackgroundFreeBoost: AttributeKind.Dexterity,
        FreeBoosts: [AttributeKind.Strength, AttributeKind.Dexterity, AttributeKind.Constitution, AttributeKind.Wisdom],
        AncestryFeat: "cooperative_nature"); // touches only Diplomacy, so the tests' numbers stay plain

    public static Character Fighter() => CharacterBuilder.Create(Content, FighterChoices);

    /// <summary>Elf Hedge Scholar Wizard: STR +0, DEX +2, CON +1, INT +4, WIS +1, CHA +1.</summary>
    public static Character Wizard() => CharacterBuilder.Create(Content, new CharacterChoices(
        "Ashbound", "elf", "hedge-scholar", "wizard", AttributeKind.Intelligence,
        AncestryFreeBoosts: [AttributeKind.Wisdom],
        BackgroundBoost: AttributeKind.Intelligence,
        BackgroundFreeBoost: AttributeKind.Constitution,
        FreeBoosts: [AttributeKind.Intelligence, AttributeKind.Dexterity, AttributeKind.Constitution, AttributeKind.Charisma],
        AncestryFeat: "elven_lore"));

    /// <summary>Human Street Urchin Rogue: STR +1, DEX +4, CON +3, INT +0, WIS +1, CHA +0.</summary>
    public static Character Rogue(string? subclass = null)
    {
        var hero = CharacterBuilder.Create(Content, new CharacterChoices(
            "Ashbound", "human", "street-urchin", "rogue", AttributeKind.Dexterity,
            AncestryFreeBoosts: [AttributeKind.Dexterity, AttributeKind.Constitution],
            BackgroundBoost: AttributeKind.Dexterity,
            BackgroundFreeBoost: AttributeKind.Constitution,
            FreeBoosts: [AttributeKind.Dexterity, AttributeKind.Constitution, AttributeKind.Wisdom, AttributeKind.Strength],
            AncestryFeat: "cooperative_nature"));
        if (subclass is not null)
            hero.ChooseSubclass(subclass);
        return hero;
    }

    /// <summary>Human Hunter Ranger: STR +1, DEX +4, CON +3, INT +0, WIS +1, CHA +0.</summary>
    public static Character Ranger(string? subclass = null)
    {
        var hero = CharacterBuilder.Create(Content, new CharacterChoices(
            "Ashbound", "human", "hunter", "ranger", AttributeKind.Dexterity,
            AncestryFreeBoosts: [AttributeKind.Dexterity, AttributeKind.Constitution],
            BackgroundBoost: AttributeKind.Dexterity,
            BackgroundFreeBoost: AttributeKind.Constitution,
            FreeBoosts: [AttributeKind.Dexterity, AttributeKind.Constitution, AttributeKind.Wisdom, AttributeKind.Strength],
            AncestryFeat: "cooperative_nature"));
        if (subclass is not null)
            hero.ChooseSubclass(subclass);
        return hero;
    }

    /// <summary>Human Lantern Acolyte Champion: STR +4, DEX +1, CON +2, INT +0, WIS +1, CHA +1.</summary>
    public static Character Champion(string? subclass = null)
    {
        var hero = CharacterBuilder.Create(Content, new CharacterChoices(
            "Ashbound", "human", "lantern-acolyte", "champion", AttributeKind.Strength,
            AncestryFreeBoosts: [AttributeKind.Strength, AttributeKind.Constitution],
            BackgroundBoost: AttributeKind.Charisma,
            BackgroundFreeBoost: AttributeKind.Strength,
            FreeBoosts: [AttributeKind.Strength, AttributeKind.Constitution, AttributeKind.Dexterity, AttributeKind.Wisdom],
            AncestryFeat: "cooperative_nature"));
        if (subclass is not null)
            hero.ChooseSubclass(subclass);
        return hero;
    }

    /// <summary>Human Gravedigger Necromancer: STR +0, DEX +1, CON +2, INT +4, WIS +2, CHA +0.</summary>
    public static Character Necromancer(string? subclass = null)
    {
        var hero = CharacterBuilder.Create(Content, new CharacterChoices(
            "Ashbound", "human", "gravedigger", "necromancer", AttributeKind.Intelligence,
            AncestryFreeBoosts: [AttributeKind.Intelligence, AttributeKind.Constitution],
            BackgroundBoost: AttributeKind.Wisdom,
            BackgroundFreeBoost: AttributeKind.Intelligence,
            FreeBoosts: [AttributeKind.Intelligence, AttributeKind.Dexterity, AttributeKind.Constitution, AttributeKind.Wisdom],
            AncestryFeat: "cooperative_nature"));
        if (subclass is not null)
            hero.ChooseSubclass(subclass);
        return hero;
    }

    /// <summary>
    /// The hero for a report's kit: "fighter", "wizard", "rogue", "ranger", "champion", "necromancer", or a class and a subclass ("fighter vanguard"), which
    /// is then taken as a new character would at creation.
    /// </summary>
    public static Character ForKit(string kit)
    {
        var words = kit.Split(' ');
        var hero = words[0] switch { "wizard" => Wizard(), "rogue" => Rogue(), "ranger" => Ranger(), "champion" => Champion(), "necromancer" => Necromancer(), _ => Fighter() };
        if (words.Length > 1 && hero.SubclassOptions.Any(s => s.Id == words[1]))
            hero.ChooseSubclass(words[1]);
        return hero;
    }

    public static Character AtLevel(Character character, int level)
    {
        while (character.Level < level)
        {
            var next = character.Level + 1;
            character.LevelUp(AttributeSet.BoostLevels.Contains(next)
                ? [AttributeKind.Strength, AttributeKind.Dexterity, AttributeKind.Constitution, AttributeKind.Wisdom]
                : null);
        }
        return character;
    }
}
