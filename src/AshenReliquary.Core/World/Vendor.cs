using AshenReliquary.Core.Items;

namespace AshenReliquary.Core.World;

/// <summary>
/// A trader placed in a zone. <see cref="Look"/> is the paper doll (Flare avatar layer per slot: head, chest,
/// hands, legs, feet, main); <see cref="Facing"/> is the sprite direction they stand in. <see cref="Sells"/> narrows
/// the stock to "weapons" (no guns), "firearms", "armour" and/or "jewellery" (a smith); null sells anything. <see cref="Runework"/>: the
/// trader also etches and moves runes (<see cref="Items.Runework"/>). <see cref="Supplies"/>: belt items (bombs,
/// elixirs) always for sale, as many as the hero can pay for. <see cref="Terms"/>: how the trader deals with the hero
/// as world flags stand (the first whose flags hold applies): a price, rares on the table, a greeting.
/// </summary>
public sealed record VendorDefinition(string Id, string Name, string Title, int[] At, IReadOnlyDictionary<string, string> Look,
    int Facing = 6, int StockSize = 12, string Greeting = "Coin for kit, kit for coin.", IReadOnlyList<string>? Sells = null,
    bool Runework = false, IReadOnlyList<string>? Supplies = null, IReadOnlyList<VendorTerms>? Terms = null)
{
    /// <summary>Whether this trader stocks items like <paramref name="item"/>.</summary>
    public bool Stocks(Items.ItemDefinition item) => Sells is null || Sells.Any(kind => kind switch
    {
        "weapons" => item.IsWeapon && !item.IsFirearm,
        "firearms" => item.IsFirearm,
        "armour" => item.Category == Items.ItemCategory.Armor,
        "jewellery" => item.IsJewelry,
        "shields" => item.IsShield,
        _ => false,
    });
}

/// <summary>
/// A trader's dealings with the hero while <see cref="Requires"/> holds (world flags, "" for unset): their prices
/// times <see cref="PriceScale"/>, <see cref="Rares"/> of the stock rolled rare, and their own <see cref="Greeting"/>.
/// Act 1's ferry choice sets the Brothers' fence's terms in Ironhold.
/// </summary>
public sealed record VendorTerms(IReadOnlyDictionary<string, string> Requires, double PriceScale = 1.0, int Rares = 0, string? Greeting = null);

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
