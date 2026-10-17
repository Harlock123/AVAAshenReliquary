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
        FreeBoosts: [AttributeKind.Strength, AttributeKind.Dexterity, AttributeKind.Constitution, AttributeKind.Wisdom]);

    public static Character Fighter() => CharacterBuilder.Create(Content, FighterChoices);

    /// <summary>Elf Hedge Scholar Wizard: STR +0, DEX +2, CON +1, INT +4, WIS +1, CHA +1.</summary>
    public static Character Wizard() => CharacterBuilder.Create(Content, new CharacterChoices(
        "Ashbound", "elf", "hedge-scholar", "wizard", AttributeKind.Intelligence,
        AncestryFreeBoosts: [AttributeKind.Wisdom],
        BackgroundBoost: AttributeKind.Intelligence,
        BackgroundFreeBoost: AttributeKind.Constitution,
        FreeBoosts: [AttributeKind.Intelligence, AttributeKind.Dexterity, AttributeKind.Constitution, AttributeKind.Charisma]));

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
