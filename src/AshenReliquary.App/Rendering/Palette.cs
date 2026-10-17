using AshenReliquary.Core.Items;
using SkiaSharp;

namespace AshenReliquary.App.Rendering;

internal static class Palette
{
    /// <summary>The spec's rarity colours: grey-white, yellow, blue, purple, green.</summary>
    public static SKColor Rarity(Rarity rarity) => rarity switch
    {
        Core.Items.Rarity.Uncommon => new SKColor(240, 205, 80),
        Core.Items.Rarity.Rare => new SKColor(100, 155, 255),
        Core.Items.Rarity.Unique => new SKColor(190, 110, 240),
        Core.Items.Rarity.Set => new SKColor(110, 220, 110),
        _ => new SKColor(215, 212, 205),
    };

    public static readonly SKColor Ink = new(232, 220, 196);
    public static readonly SKColor Dim = new(165, 155, 135);
    public static readonly SKColor Frame = new(90, 76, 58);
    public static readonly SKColor Panel = new(14, 11, 10, 238);
    public static readonly SKColor Gold = new(240, 200, 90);
}
