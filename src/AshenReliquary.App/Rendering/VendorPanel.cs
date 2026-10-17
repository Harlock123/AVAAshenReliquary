using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Items;
using AshenReliquary.Core.World;
using SkiaSharp;

namespace AshenReliquary.App.Rendering;

/// <summary>
/// A trader's table, on the left while the inventory is open on the right. Wares with their prices, a potion
/// charge, and what you sold this visit (buy it back for what you were paid). Left-click buys; right-click a bag
/// item (in the inventory) sells it. Tooltips show the price and what wearing the item would change.
/// </summary>
internal sealed class VendorPanel
{
    public const float Width = 440;
    private const int Columns = 6;
    private const float Cell = 52, Step = 68, Pad = 20;

    private readonly Action<SKCanvas, int, SKRect> _drawIcon;

    public VendorPanel(Action<SKCanvas, int, SKRect> drawIcon) => _drawIcon = drawIcon;

    public SKRect Bounds { get; private set; }
    private float _waresTop, _potionTop, _buybackTop;
    private int _rows;

    private SKRect PotionButton => SKRect.Create(Bounds.Left + Pad, _potionTop, Width - 2 * Pad, 34);

    private SKRect WareRect(int index) => CellRect(_waresTop, index);
    private SKRect BuybackRect(int index) => CellRect(_buybackTop, index);

    private SKRect CellRect(float top, int index) =>
        SKRect.Create(Bounds.Left + Pad + index % Columns * Step, top + index / Columns * Step, Cell, Cell);

    public void Layout(SKSize size, Vendor vendor)
    {
        var height = Math.Max(500, size.Height - 32 - 150);
        Bounds = SKRect.Create(16, 16, Width, height);
        _rows = Math.Max(1, (vendor.Definition.StockSize + Columns - 1) / Columns);
        _waresTop = Bounds.Top + 92;
        _potionTop = _waresTop + _rows * Step + 8;
        _buybackTop = _potionTop + 34 + 40;
    }

    /// <summary>What is under a point: a ware, the potion button, a buyback slot, or nothing.</summary>
    public (int? Ware, bool Potion, int? Buyback) HitTest(SKPoint p, Vendor vendor)
    {
        for (var i = 0; i < vendor.Stock.Count; i++)
        {
            if (WareRect(i).Contains(p))
                return (i, false, null);
        }
        if (PotionButton.Contains(p))
            return (null, true, null);
        for (var i = 0; i < vendor.Buyback.Count; i++)
        {
            if (BuybackRect(i).Contains(p))
                return (null, false, i);
        }
        return (null, false, null);
    }

    /// <summary>Handles a click inside the panel; true when it was inside.</summary>
    public bool Click(SKPoint p, GameWorld world)
    {
        if (world.OpenVendor is not { } vendor || !Bounds.Contains(p))
            return false;
        var (ware, potion, buyback) = HitTest(p, vendor);
        if (ware is { } w)
            world.Buy(w);
        else if (potion)
            world.BuyPotion();
        else if (buyback is { } b)
            world.BuyBack(b);
        return true;
    }

    public void Draw(SKCanvas canvas, SKSize size, GameWorld world, SKPoint? pointer)
    {
        if (world.OpenVendor is not { } vendor)
            return;
        Layout(size, vendor);
        var c = world.Hero.Character;
        var gear = world.Hero.Equipment;
        using var back = new SKPaint { Color = Palette.Panel };
        using var frame = new SKPaint { IsAntialias = true, Color = Palette.Frame, Style = SKPaintStyle.Stroke, StrokeWidth = 2 };
        using var faint = new SKPaint { IsAntialias = true, Color = new SKColor(55, 46, 38), Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
        using var cellBack = new SKPaint { Color = new SKColor(28, 23, 20, 240) };
        using var ink = new SKPaint { IsAntialias = true, Color = Palette.Ink };
        using var dim = new SKPaint { IsAntialias = true, Color = Palette.Dim };
        using var gold = new SKPaint { IsAntialias = true, Color = Palette.Gold };
        using var poor = new SKPaint { IsAntialias = true, Color = InventoryPanel.Worse };
        using var title = new SKFont(WorldOverlay.Serif, 19);
        using var font = new SKFont(WorldOverlay.Serif, 13);
        using var small = new SKFont(WorldOverlay.Serif, 11);

        var (hoverWare, hoverPotion, hoverBuyback) = pointer is { } pt && Bounds.Contains(pt) ? HitTest(pt, vendor) : (null, false, null);

        canvas.DrawRect(Bounds, back);
        canvas.DrawRect(Bounds, frame);
        canvas.DrawText(vendor.Definition.Name, Bounds.Left + Pad, Bounds.Top + 30, SKTextAlign.Left, title, ink);
        canvas.DrawText(vendor.Definition.Title, Bounds.Left + Pad, Bounds.Top + 47, SKTextAlign.Left, small, dim);
        canvas.DrawText($"{gear.Gold} gold", Bounds.Right - Pad, Bounds.Top + 30, SKTextAlign.Right, font, gold);
        var haggle = Pricing.Haggle(c);
        if (Math.Abs(haggle - 1) > 0.001)
            canvas.DrawText(haggle < 1 ? $"You haggle {1 - haggle:0%} off" : $"You pay {haggle - 1:0%} more", Bounds.Right - Pad, Bounds.Top + 47, SKTextAlign.Right, small, dim);

        canvas.DrawText("Wares", Bounds.Left + Pad, _waresTop - 10, SKTextAlign.Left, font, ink);
        for (var i = 0; i < vendor.Definition.StockSize; i++)
        {
            var rect = WareRect(i);
            canvas.DrawRect(rect, cellBack);
            if (i < vendor.Stock.Count)
            {
                var item = vendor.Stock[i];
                DrawItem(canvas, rect, item, Pricing.BuyPrice(item, c), gear.Gold, small, gold, poor);
            }
            else
            {
                canvas.DrawRect(rect, faint);
            }
            if (i == hoverWare)
                Outline(canvas, rect);
        }

        // A potion charge.
        var button = PotionButton;
        var price = Pricing.PotionPrice(c);
        var full = c.Potion.Charges >= HealingPotion.MaxCharges;
        using (var buttonBack = new SKPaint { Color = hoverPotion && !full ? new SKColor(60, 40, 34, 240) : new SKColor(34, 26, 22, 240) })
            canvas.DrawRect(button, buttonBack);
        canvas.DrawRect(button, hoverPotion ? frame : faint);
        canvas.DrawText($"Healing potion charge  ({c.Potion.Charges} / {HealingPotion.MaxCharges})", button.Left + 12, button.MidY + 5, SKTextAlign.Left, font, full ? dim : ink);
        canvas.DrawText(full ? "full" : $"{price} gold", button.Right - 12, button.MidY + 5, SKTextAlign.Right, font, full ? dim : gear.Gold >= price ? gold : poor);

        canvas.DrawText("Sold this visit", Bounds.Left + Pad, _buybackTop - 10, SKTextAlign.Left, font, ink);
        canvas.DrawText("buy back for what you were paid", Bounds.Left + Pad + 104, _buybackTop - 10, SKTextAlign.Left, small, dim);
        for (var i = 0; i < Vendor.BuybackSize; i++)
        {
            var rect = BuybackRect(i);
            canvas.DrawRect(rect, cellBack);
            if (i < vendor.Buyback.Count)
            {
                var item = vendor.Buyback[i];
                DrawItem(canvas, rect, item, Pricing.SellPrice(item), gear.Gold, small, gold, poor);
            }
            else
            {
                canvas.DrawRect(rect, faint);
            }
            if (i == hoverBuyback)
                Outline(canvas, rect);
        }

        canvas.DrawText("Left-click to buy · right-click a bag item to sell · Esc or walk away to leave", Bounds.MidX, Bounds.Bottom - 8, SKTextAlign.Center, small, dim);

        if (pointer is { } p)
        {
            Item? hovered = null;
            string? where = null;
            if (hoverWare is { } w && w < vendor.Stock.Count)
                (hovered, where) = (vendor.Stock[w], $"Costs {Pricing.BuyPrice(vendor.Stock[w], c)} gold");
            else if (hoverBuyback is { } b && b < vendor.Buyback.Count)
                (hovered, where) = (vendor.Buyback[b], $"Buy back for {Pricing.SellPrice(vendor.Buyback[b])} gold");
            if (hovered is not null)
            {
                var worn = gear.SlotFor(hovered) is { } slot ? gear.In(slot) : null;
                InventoryPanel.Tooltip(canvas, size, p, hovered, worn, where, gear.PreviewEquip(hovered), "If you wear it:", Bounds);
            }
        }
    }

    private void DrawItem(SKCanvas canvas, SKRect rect, Item item, int price, int gold, SKFont small, SKPaint afford, SKPaint poor)
    {
        _drawIcon(canvas, item.Definition.Icon, SKRect.Inflate(rect, -3, -3));
        using var edge = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = item.Rarity == Rarity.Common ? 1 : 2,
            Color = Palette.Rarity(item.Rarity).WithAlpha(item.Rarity == Rarity.Common ? (byte)90 : (byte)220),
        };
        canvas.DrawRect(rect, edge);
        canvas.DrawText($"{price}g", rect.MidX, rect.Bottom + 12, SKTextAlign.Center, small, gold >= price ? afford : poor);
    }

    private static void Outline(SKCanvas canvas, SKRect rect)
    {
        using var glow = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3, Color = new SKColor(255, 225, 140) };
        canvas.DrawRect(SKRect.Inflate(rect, 2, 2), glow);
    }
}
