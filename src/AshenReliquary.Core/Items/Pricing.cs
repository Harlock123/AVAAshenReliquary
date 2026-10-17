using AshenReliquary.Core.Characters;
using AshenReliquary.Core.Rules;

namespace AshenReliquary.Core.Items;

/// <summary>
/// What things cost. An item's value grows with its item level, rarity, runes and affixes; traders sell at twice
/// the value and buy at a quarter of it. Charisma haggles (2% per point of the modifier, either way, at most 10%)
/// and a Street Urchin background takes another 5% off what you pay.
/// </summary>
public static class Pricing
{
    public const double SellMarkup = 2.0;
    public const double BuyRate = 0.25;

    /// <summary>The item's value in gold before any trader's cut.</summary>
    public static int Value(Item item)
    {
        var d = item.Definition;
        double basePrice = d.IsJewelry ? 20 : d.IsWeapon ? 12 : d.IsBodyArmor ? 10 + 4 * d.AcBonus : 7;
        var rarity = item.Rarity switch
        {
            Rarity.Common => 1.0,
            Rarity.Uncommon => 2.5,
            Rarity.Rare => 6.0,
            _ => 12.0,
        };
        var value = basePrice * rarity * (1 + 0.35 * (Math.Max(1, item.ItemLevel) - 1))
            + 25 * item.Potency + 40 * item.Striking + 30 * item.Resilient + 4 * item.Affixes.Count;
        return Math.Max(1, (int)Math.Round(value));
    }

    /// <summary>The share of the price a character pays: 1.0 is list price.</summary>
    public static double Haggle(Character c)
    {
        var factor = 1 - Math.Clamp(c.Attributes[AttributeKind.Charisma] * 0.02, -0.10, 0.10);
        if (c.Background.Id == "street-urchin")
            factor -= 0.05;
        return factor;
    }

    /// <summary>What a trader asks for the item.</summary>
    public static int BuyPrice(Item item, Character c) => Math.Max(1, (int)Math.Ceiling(Value(item) * SellMarkup * Haggle(c)));

    /// <summary>What a trader pays for the item; selling something back costs the same within a visit (buyback).</summary>
    public static int SellPrice(Item item) => Math.Max(1, (int)Math.Floor(Value(item) * BuyRate));

    /// <summary>One healing potion charge from a trader.</summary>
    public static int PotionPrice(Character c) => Math.Max(1, (int)Math.Ceiling((10 + 5 * c.Level) * Haggle(c)));
}
