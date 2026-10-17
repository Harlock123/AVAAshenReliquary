using AshenReliquary.Core.Rules;

namespace AshenReliquary.Core.Tests;

public class ModifierStackTests
{
    [Fact]
    public void Only_the_highest_bonus_of_each_type_applies()
    {
        var total = ModifierStack.Combine([
            new Modifier("Potency", 2, BonusType.Item),
            new Modifier("Ring", 1, BonusType.Item),
            new Modifier("Heroism", 1, BonusType.Status),
            new Modifier("Bless", 1, BonusType.Status),
            new Modifier("Cover", 2, BonusType.Circumstance),
        ]);
        Assert.Equal(5, total.Total);
        Assert.Equal(2, total.Suppressed.Count);
    }

    [Fact]
    public void Only_the_worst_penalty_of_each_type_applies_alongside_the_best_bonus()
    {
        var total = ModifierStack.Combine([
            new Modifier("Heroism", 2, BonusType.Status),
            new Modifier("Frightened 2", -2, BonusType.Status),
            new Modifier("Sickened 1", -1, BonusType.Status),
        ]);
        Assert.Equal(0, total.Total);
        Assert.Contains(total.Suppressed, m => m.Source == "Sickened 1");
    }

    [Fact]
    public void Untyped_bonuses_and_penalties_all_add_up()
    {
        var total = ModifierStack.Combine([
            new Modifier("STR", 4),
            new Modifier("Expert", 5),
            new Modifier("Cadence", -2),
            new Modifier("Affix", 1),
        ]);
        Assert.Equal(8, total.Total);
        Assert.Empty(total.Suppressed);
    }

    [Fact]
    public void Zero_value_modifiers_are_left_out()
    {
        var total = ModifierStack.Combine([new Modifier("+0 potency", 0, BonusType.Item), new Modifier("DEX", 3)]);
        Assert.Single(total.Applied);
    }
}
