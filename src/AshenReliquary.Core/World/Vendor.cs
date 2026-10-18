using AshenReliquary.Core.Items;

namespace AshenReliquary.Core.World;

/// <summary>
/// A trader placed in a zone. <see cref="Look"/> is the paper doll (Flare avatar layer per slot: head, chest,
/// hands, legs, feet, main); <see cref="Facing"/> is the sprite direction they stand in. <see cref="Sells"/> narrows
/// the stock to "weapons", "armour" and/or "jewellery" (a smith); null sells anything.
/// </summary>
public sealed record VendorDefinition(string Id, string Name, string Title, int[] At, IReadOnlyDictionary<string, string> Look,
    int Facing = 6, int StockSize = 12, string Greeting = "Coin for kit, kit for coin.", IReadOnlyList<string>? Sells = null)
{
    /// <summary>Whether this trader stocks items like <paramref name="item"/>.</summary>
    public bool Stocks(Items.ItemDefinition item) => Sells is null || Sells.Any(kind => kind switch
    {
        "weapons" => item.IsWeapon,
        "armour" => item.Category == Items.ItemCategory.Armor,
        "jewellery" => item.IsJewelry,
        _ => false,
    });
}

/// <summary>A trader in play: what is on the table, and what you sold this visit (buy it back at the price you got).</summary>
public sealed class Vendor
{
    public const int BuybackSize = 10;
    public const double TalkRadius = 1.8;

    private readonly List<Item> _stock = [];
    private readonly List<Item> _buyback = [];

    internal Vendor(VendorDefinition definition)
    {
        Definition = definition;
        X = definition.At[0] + 0.5;
        Y = definition.At[1] + 0.5;
    }

    public VendorDefinition Definition { get; }
    public double X { get; }
    public double Y { get; }
    public IReadOnlyList<Item> Stock => _stock;
    public IReadOnlyList<Item> Buyback => _buyback;

    /// <summary>The hero level the stock was last rolled for; a new level brings new stock.</summary>
    public int StockedForLevel { get; internal set; }

    internal void SetStock(IEnumerable<Item> items)
    {
        _stock.Clear();
        _stock.AddRange(items);
    }

    internal void Take(Item item) => _stock.Remove(item);

    internal void Sold(Item item)
    {
        _buyback.Insert(0, item);
        if (_buyback.Count > BuybackSize)
            _buyback.RemoveAt(_buyback.Count - 1);
    }

    internal void Unsold(Item item) => _buyback.Remove(item);

    internal void ClearBuyback() => _buyback.Clear();

    public double DistanceTo(double x, double y) => Math.Sqrt((X - x) * (X - x) + (Y - y) * (Y - y));
}
