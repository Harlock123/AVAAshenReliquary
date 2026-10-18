using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Items;
using AshenReliquary.Core.World;
using SkiaSharp;
using Runes = AshenReliquary.Core.Items.Runework;

namespace AshenReliquary.App.Rendering;

/// <summary>
/// A trader's table, on the left while the inventory is open on the right. Wares with their prices, a potion
/// charge, and what you sold this visit (buy it back for what you were paid). Left-click buys; right-click a bag
/// item (in the inventory) sells it. Tooltips show the price and what wearing the item would change.
/// A smith (<see cref="VendorDefinition.Runework"/>) adds a Runework tab: a bench with the item to work on and one to
/// take runes from (right-click gear in the inventory to put it there), and a button for each rune he can etch or move.
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

    /// <summary>The Runework tab is showing (a smith only).</summary>
    public bool Runework { get; private set; }

    /// <summary>What is on the bench, while the Runework tab shows.</summary>
    public IReadOnlyCollection<Item> Benched => Runework ? new[] { _work, _donor }.OfType<Item>().ToArray() : [];

    private Vendor? _vendor;
    private Item? _work, _donor;
    private readonly List<(SKRect Rect, Action Act)> _buttons = [];

    private SKRect WaresTab => SKRect.Create(Bounds.Left + Pad, Bounds.Top + 58, 100, 26);
    private SKRect RuneworkTab => SKRect.Create(Bounds.Left + Pad + 106, Bounds.Top + 58, 100, 26);
    private SKRect WorkCell => SKRect.Create(Bounds.Left + Pad, Bounds.Top + 128, 64, 64);
    private SKRect DonorCell => SKRect.Create(Bounds.Left + Pad + 210, Bounds.Top + 128, 64, 64);

    /// <summary>
    /// Puts one of the hero's items on the bench: the first goes to "work on", the next to "take runes from" (each
    /// replacing what was there). Things that take no runes are refused with a word from the smith.
    /// </summary>
    public bool PutOnBench(Item item, GameWorld world)
    {
        if (Runes.Kinds(item.Definition).Count == 0)
        {
            world.Announce($"{world.OpenVendor?.Definition.Name ?? "The smith"}: \"Only weapons and body armour take runes.\"");
            return false;
        }
        if (_work is null || _work == item)
            _work = item;
        else if (_donor == item)
            (_work, _donor) = (_donor, _work);
        else
            _donor = item;
        return true;
    }

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
        _waresTop = Bounds.Top + (vendor.Definition.Runework ? 112 : 92);
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
        if (vendor.Definition.Runework && WaresTab.Contains(p))
            Runework = false;
        else if (vendor.Definition.Runework && RuneworkTab.Contains(p))
            Runework = true;
        if (Runework)
        {
            if (WorkCell.Contains(p))
                (_work, _donor) = (_donor, null);
            else if (DonorCell.Contains(p))
                _donor = null;
            else if (_buttons.FirstOrDefault(b => b.Rect.Contains(p)) is { Act: { } act })
                act();
            return true;
        }
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
        if (vendor != _vendor)
        {
            _vendor = vendor;
            Runework = false;
            _work = _donor = null;
        }
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

        if (vendor.Definition.Runework)
        {
            Tab(canvas, WaresTab, "Wares", !Runework, pointer, font);
            Tab(canvas, RuneworkTab, "Runework", Runework, pointer, font);
        }
        if (Runework)
        {
            DrawRunework(canvas, size, world, pointer);
            return;
        }

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

    private static void Tab(SKCanvas canvas, SKRect rect, string label, bool on, SKPoint? pointer, SKFont font)
    {
        var over = pointer is { } p && rect.Contains(p);
        using var back = new SKPaint { Color = on ? new SKColor(80, 60, 30, 240) : over ? new SKColor(60, 40, 34, 240) : new SKColor(34, 26, 22, 240) };
        using var edge = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1, Color = on ? Palette.Frame : new SKColor(55, 46, 38) };
        using var ink = new SKPaint { IsAntialias = true, Color = on ? Palette.Gold : Palette.Dim };
        canvas.DrawRect(rect, back);
        canvas.DrawRect(rect, edge);
        canvas.DrawText(label, rect.MidX, rect.MidY + 5, SKTextAlign.Center, font, ink);
    }

    /// <summary>The Runework tab: the bench's two items, then what can be etched onto the first and moved onto it from the second.</summary>
    private void DrawRunework(SKCanvas canvas, SKSize size, GameWorld world, SKPoint? pointer)
    {
        var c = world.Hero.Character;
        var gear = world.Hero.Equipment;
        if (_work is not null && !world.Owns(_work))
            _work = null;
        if (_donor is not null && !world.Owns(_donor))
            _donor = null;
        if (_work is null && _donor is not null)
            (_work, _donor) = (_donor, null);
        _buttons.Clear();

        using var ink = new SKPaint { IsAntialias = true, Color = Palette.Ink };
        using var dim = new SKPaint { IsAntialias = true, Color = Palette.Dim };
        using var gold = new SKPaint { IsAntialias = true, Color = Palette.Gold };
        using var poor = new SKPaint { IsAntialias = true, Color = InventoryPanel.Worse };
        using var cellBack = new SKPaint { Color = new SKColor(28, 23, 20, 240) };
        using var faint = new SKPaint { IsAntialias = true, Color = new SKColor(55, 46, 38), Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
        using var font = new SKFont(WorldOverlay.Serif, 13);
        using var small = new SKFont(WorldOverlay.Serif, 11);

        void Bench(SKRect cell, string label, Item? item)
        {
            canvas.DrawText(label, cell.Left, cell.Top - 9, SKTextAlign.Left, font, ink);
            canvas.DrawRect(cell, cellBack);
            canvas.DrawRect(cell, faint);
            if (item is null)
            {
                canvas.DrawText("empty", cell.MidX, cell.MidY + 4, SKTextAlign.Center, small, dim);
                return;
            }
            _drawIcon(canvas, item.Definition.Icon, SKRect.Inflate(cell, -4, -4));
            using var edge = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2, Color = Palette.Rarity(item.Rarity) };
            canvas.DrawRect(cell, edge);
            using var name = new SKPaint { IsAntialias = true, Color = Palette.Rarity(item.Rarity) };
            canvas.DrawText(Fit(item.Name, small, 190), cell.Left, cell.Bottom + 15, SKTextAlign.Left, small, name);
            canvas.DrawText($"item level {item.ItemLevel}{(gear.Worn.Values.Contains(item) ? " · worn" : "")}", cell.Left, cell.Bottom + 29, SKTextAlign.Left, small, dim);
            if (pointer is { } p && cell.Contains(p))
                Outline(canvas, cell);
        }
        Bench(WorkCell, "Work on", _work);
        Bench(DonorCell, "Take runes from", _donor);

        // A row per rune: what it does, then the price, or why not.
        var y = WorkCell.Bottom + 62;
        void Button(string label, string? why, int price, Action act)
        {
            var rect = SKRect.Create(Bounds.Left + Pad, y, Width - 2 * Pad, 32);
            var over = pointer is { } p && rect.Contains(p);
            using var back = new SKPaint { Color = why is null && over ? new SKColor(60, 40, 34, 240) : new SKColor(34, 26, 22, 240) };
            canvas.DrawRect(rect, back);
            canvas.DrawRect(rect, faint);
            canvas.DrawText(label, rect.Left + 12, rect.MidY + 5, SKTextAlign.Left, font, why is null ? ink : dim);
            if (why is not null)
                canvas.DrawText(Fit(why, small, rect.Width - font.MeasureText(label) - 40), rect.Right - 12, rect.MidY + 4, SKTextAlign.Right, small, dim);
            else
                canvas.DrawText($"{price} gold", rect.Right - 12, rect.MidY + 5, SKTextAlign.Right, font, gear.Gold >= price ? gold : poor);
            _buttons.Add((rect, act));
            y += 38;
        }

        if (_work is { } work)
        {
            canvas.DrawText("Etch", Bounds.Left + Pad, y - 8, SKTextAlign.Left, font, ink);
            foreach (var kind in Runes.Kinds(work.Definition))
            {
                var k = kind;
                var next = work.Rune(kind) + 1;
                Button($"Etch {Runes.Describe(kind, Math.Min(next, 3))}", Runes.CannotEtch(work, kind),
                    Runes.EtchPrice(work, kind, c), () => world.Etch(work, k));
            }
            y += 22;
        }
        if (_work is { } to && _donor is { } from)
        {
            canvas.DrawText("Move onto the first", Bounds.Left + Pad, y - 8, SKTextAlign.Left, font, ink);
            var moves = Runes.Kinds(from.Definition).Where(k => from.Rune(k) > 0).ToList();
            if (moves.Count == 0)
                canvas.DrawText($"{Fit(from.Name, small, 260)} has no runes to move.", Bounds.Left + Pad, y + 14, SKTextAlign.Left, small, dim);
            foreach (var kind in moves)
            {
                var k = kind;
                Button($"Move its {Runes.Describe(kind, from.Rune(kind))} rune", Runes.CannotMove(from, to, kind),
                    Runes.MovePrice(from, kind, c), () => world.MoveRune(from, to, k));
            }
        }
        if (_work is null)
        {
            var lines = new[] { "Right-click a weapon or body armour in your bag,", "or one you are wearing, to put it on the bench.",
                "Put a second item beside it to move its runes across." };
            for (var i = 0; i < lines.Length; i++)
                canvas.DrawText(lines[i], Bounds.Left + Pad, y + 8 + i * 18, SKTextAlign.Left, font, dim);
        }

        canvas.DrawText("Right-click your gear to put it on the bench · click a bench item to take it off", Bounds.MidX, Bounds.Bottom - 8, SKTextAlign.Center, small, dim);

        if (pointer is { } at)
        {
            var hovered = WorkCell.Contains(at) ? _work : DonorCell.Contains(at) ? _donor : null;
            if (hovered is not null)
                InventoryPanel.Tooltip(canvas, size, at, hovered, null, "On the bench", null, avoid: Bounds);
        }
    }

    private static string Fit(string text, SKFont font, float width)
    {
        if (font.MeasureText(text) <= width)
            return text;
        while (text.Length > 1 && font.MeasureText(text + "…") > width)
            text = text[..^1];
        return text + "…";
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
