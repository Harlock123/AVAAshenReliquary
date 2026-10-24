using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Items;
using AshenReliquary.Core.World;
using SkiaSharp;
using Runes = AshenReliquary.Core.Items.Runework;

namespace AshenReliquary.App.Rendering;

/// <summary>
/// A trader's table, on the left while the inventory is open on the right. Wares with their prices, a potion
/// charge, the trader's supplies (belt items, never sold out), and what you sold this visit (buy it back for what you
/// were paid). Left-click buys; right-click a bag
/// item (in the inventory) sells it. Tooltips show the price and what wearing the item would change.
/// A smith (<see cref="VendorDefinition.Runework"/>) adds a Runework tab: a bench with the item to work on and one to
/// take runes from (right-click gear in the inventory to put it there), and a button for each rune he can etch or move;
/// and a Crafting tab: the hero's Ashen Dust and Rune Fragments, a piece of gear on the forge to salvage or work (raise
/// it, add or reroll an affix, each on a Crafting check), and belt items to brew.
/// </summary>
internal sealed class VendorPanel
{
    public const float Width = 440;
    private const int Columns = 6;
    private const float Cell = 52, Step = 68, Pad = 20;

    private readonly Action<SKCanvas, Core.Items.ItemDefinition, SKRect> _drawIcon;

    public VendorPanel(Action<SKCanvas, Core.Items.ItemDefinition, SKRect> drawIcon) => _drawIcon = drawIcon;

    public SKRect Bounds { get; private set; }
    private float _waresTop, _potionTop, _suppliesTop, _buybackTop;
    private int _rows;

    /// <summary>The Runework tab is showing (a smith only).</summary>
    public bool Runework { get; private set; }

    /// <summary>The Crafting tab is showing (a smith only).</summary>
    public bool Crafting { get; private set; }

    /// <summary>What is on the bench, while the Runework or Crafting tab shows.</summary>
    public IReadOnlyCollection<Item> Benched => Runework ? new[] { _work, _donor }.OfType<Item>().ToArray()
        : Crafting && _forged is not null ? [_forged] : [];

    /// <summary>The item on the forge (the Crafting tab).</summary>
    private Item? _forged;

    private Vendor? _vendor;
    private Item? _work, _donor;
    private readonly List<(SKRect Rect, Action Act)> _buttons = [];

    private SKRect WaresTab => SKRect.Create(Bounds.Left + Pad, Bounds.Top + 58, 100, 26);
    private SKRect RuneworkTab => SKRect.Create(Bounds.Left + Pad + 106, Bounds.Top + 58, 100, 26);
    private SKRect CraftingTab => SKRect.Create(Bounds.Left + Pad + 212, Bounds.Top + 58, 100, 26);
    private SKRect WorkCell => SKRect.Create(Bounds.Left + Pad, Bounds.Top + 128, 64, 64);
    private SKRect DonorCell => SKRect.Create(Bounds.Left + Pad + 210, Bounds.Top + 128, 64, 64);

    /// <summary>
    /// Puts one of the hero's items on the bench: the first goes to "work on", the next to "take runes from" (each
    /// replacing what was there). Things that take no runes are refused with a word from the smith.
    /// </summary>
    public bool PutOnBench(Item item, GameWorld world)
    {
        if (Crafting)
        {
            if (item.Definition.Slot is null)
            {
                world.Announce($"{world.OpenVendor?.Definition.Name ?? "The smith"}: \"Bring me gear to work.\"");
                return false;
            }
            _forged = item;
            return true;
        }
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
    private SKRect SupplyRect(int index) => CellRect(_suppliesTop, index);

    /// <summary>The centre of supply <paramref name="index"/> as last laid out (for tests that click it).</summary>
    public SKPoint SupplyCentre(int index) => new(SupplyRect(index).MidX, SupplyRect(index).MidY);

    private SKRect CellRect(float top, int index) =>
        SKRect.Create(Bounds.Left + Pad + index % Columns * Step, top + index / Columns * Step, Cell, Cell);

    public void Layout(SKSize size, Vendor vendor)
    {
        var height = Math.Max(500, size.Height - 32 - 150);
        Bounds = SKRect.Create(16, 16, Width, height);
        _rows = Math.Max(1, (vendor.Definition.StockSize + Columns - 1) / Columns);
        _waresTop = Bounds.Top + (vendor.Definition.Runework ? 112 : 92);
        _potionTop = _waresTop + _rows * Step + 8;
        _suppliesTop = _potionTop + 34 + 40;
        _buybackTop = (vendor.Definition.Supplies is { Count: > 0 } ? _suppliesTop + Step + 30 : _potionTop + 34) + 40;
    }

    /// <summary>What is under a point: a ware, the potion button, a supply, a buyback slot, or nothing.</summary>
    public (int? Ware, bool Potion, int? Buyback, int? Supply) HitTest(SKPoint p, Vendor vendor)
    {
        for (var i = 0; i < vendor.Stock.Count; i++)
        {
            if (WareRect(i).Contains(p))
                return (i, false, null, null);
        }
        if (PotionButton.Contains(p))
            return (null, true, null, null);
        for (var i = 0; i < (vendor.Definition.Supplies?.Count ?? 0); i++)
        {
            if (SupplyRect(i).Contains(p))
                return (null, false, null, i);
        }
        for (var i = 0; i < vendor.Buyback.Count; i++)
        {
            if (BuybackRect(i).Contains(p))
                return (null, false, i, null);
        }
        return (null, false, null, null);
    }

    /// <summary>Handles a click inside the panel; true when it was inside.</summary>
    public bool Click(SKPoint p, GameWorld world)
    {
        if (world.OpenVendor is not { } vendor || !Bounds.Contains(p))
            return false;
        if (vendor.Definition.Runework && WaresTab.Contains(p))
            (Runework, Crafting) = (false, false);
        else if (vendor.Definition.Runework && RuneworkTab.Contains(p))
            (Runework, Crafting) = (true, false);
        else if (vendor.Definition.Runework && CraftingTab.Contains(p))
            (Runework, Crafting) = (false, true);
        if (Crafting)
        {
            if (ForgeCell.Contains(p))
                _forged = null;
            else if (_buttons.FirstOrDefault(b => b.Rect.Contains(p)) is { Act: { } act })
                act();
            return true;
        }
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
        var (ware, potion, buyback, supply) = HitTest(p, vendor);
        if (ware is { } w)
            world.Buy(w);
        else if (potion)
            world.BuyPotion();
        else if (supply is { } s && s < world.Supplies.Count)
            world.BuySupply(world.Supplies[s]);
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
            Runework = Crafting = false;
            _work = _donor = _forged = null;
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

        var (hoverWare, hoverPotion, hoverBuyback, hoverSupply) = pointer is { } pt && Bounds.Contains(pt) ? HitTest(pt, vendor) : (null, false, null, null);

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
            Tab(canvas, WaresTab, "Wares", !Runework && !Crafting, pointer, font);
            Tab(canvas, RuneworkTab, "Runework", Runework, pointer, font);
            Tab(canvas, CraftingTab, "Crafting", Crafting, pointer, font);
        }
        if (Crafting)
        {
            DrawCrafting(canvas, size, world, pointer);
            return;
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
                DrawItem(canvas, rect, item, world.BuyPrice(item), gear.Gold, small, gold, poor);
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
        var price = world.PotionPrice;
        var full = c.Potion.Full;
        using (var buttonBack = new SKPaint { Color = hoverPotion && !full ? new SKColor(60, 40, 34, 240) : new SKColor(34, 26, 22, 240) })
            canvas.DrawRect(button, buttonBack);
        canvas.DrawRect(button, hoverPotion ? frame : faint);
        var into = c.Potion.Charges < HealingPotion.MaxCharges ? $"{c.Potion.Charges} / {HealingPotion.MaxCharges}"
            : $"case {c.Potion.Spares} / {HealingPotion.CaseCapacity}";
        canvas.DrawText($"Healing draught  ({into})", button.Left + 12, button.MidY + 5, SKTextAlign.Left, font, full ? dim : ink);
        canvas.DrawText(full ? "full" : $"{price} gold", button.Right - 12, button.MidY + 5, SKTextAlign.Right, font, full ? dim : gear.Gold >= price ? gold : poor);

        // Supplies: belt items, never sold out, priced by the hero's level.
        var supplies = world.Supplies;
        if (supplies.Count > 0)
        {
            canvas.DrawText("Supplies", Bounds.Left + Pad, _suppliesTop - 10, SKTextAlign.Left, font, ink);
            canvas.DrawText(supplies.All(i => i.IsBeltItem) ? "for the belt: keys 7 to 0" : "for the belt (keys 7 to 0) and the forge",
                Bounds.Left + Pad + 64, _suppliesTop - 10, SKTextAlign.Left, small, dim);
            for (var i = 0; i < supplies.Count; i++)
            {
                var rect = SupplyRect(i);
                canvas.DrawRect(rect, cellBack);
                _drawIcon(canvas, supplies[i], SKRect.Inflate(rect, -4, -4));
                canvas.DrawRect(rect, faint);
                var cost = world.SupplyPrice(supplies[i]);
                canvas.DrawText($"{cost}", rect.MidX, rect.Bottom + 13, SKTextAlign.Center, small, gear.Gold >= cost ? gold : poor);
                var held = gear.CountOf(supplies[i]);
                if (held > 0)
                    InventoryPanel.StackCount(canvas, rect, held, small);
                if (i == hoverSupply)
                    Outline(canvas, rect);
            }
        }

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
                (hovered, where) = (vendor.Stock[w], $"Costs {world.BuyPrice(vendor.Stock[w])} gold");
            else if (hoverBuyback is { } b && b < vendor.Buyback.Count)
                (hovered, where) = (vendor.Buyback[b], $"Buy back for {Pricing.SellPrice(vendor.Buyback[b])} gold");
            else if (hoverSupply is { } s && s < supplies.Count)
                (hovered, where) = (new Item(supplies[s], Rarity.Common, c.Level), $"Costs {world.SupplyPrice(supplies[s])} gold · you have {gear.CountOf(supplies[s])}");
            if (hovered is not null)
            {
                var worn = gear.SlotFor(hovered) is { } slot ? gear.In(slot) : null;
                InventoryPanel.Tooltip(canvas, size, p, hovered, worn, where, gear.PreviewEquip(hovered), "If you wear it:", Bounds, gear);
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
            _drawIcon(canvas, item.Definition, SKRect.Inflate(cell, -4, -4));
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

    private SKRect ForgeCell => SKRect.Create(Bounds.Left + Pad, Bounds.Top + 150, 64, 64);

    /// <summary>
    /// The Crafting tab: the materials held, the piece on the forge with what salvaging it gives and the recipes that
    /// can be worked on it (each with its DC, materials and fee, or why not), then belt items to brew.
    /// </summary>
    private void DrawCrafting(SKCanvas canvas, SKSize size, GameWorld world, SKPoint? pointer)
    {
        var c = world.Hero.Character;
        var gear = world.Hero.Equipment;
        if (_forged is not null && !world.Owns(_forged))
            _forged = null;
        _buttons.Clear();
        using var ink = new SKPaint { IsAntialias = true, Color = Palette.Ink };
        using var dim = new SKPaint { IsAntialias = true, Color = Palette.Dim };
        using var gold = new SKPaint { IsAntialias = true, Color = Palette.Gold };
        using var poor = new SKPaint { IsAntialias = true, Color = InventoryPanel.Worse };
        using var dust = new SKPaint { IsAntialias = true, Color = new SKColor(200, 190, 170) };
        using var cellBack = new SKPaint { Color = new SKColor(28, 23, 20, 240) };
        using var faint = new SKPaint { IsAntialias = true, Color = new SKColor(55, 46, 38), Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
        using var font = new SKFont(WorldOverlay.Serif, 13);
        using var small = new SKFont(WorldOverlay.Serif, 11);

        var crafting = c.Skill("Crafting").Total;
        // The gunsmith's materials, shown once there are any or a gun is in hand.
        var materials = world.MaterialsNeeded(CraftRecipe.Brew, round: true).Concat(world.MaterialsNeeded(CraftRecipe.Refit, round: false))
            .Select(m => m.Item).Distinct().ToList();
        var gunsmithing = world.WieldsFirearm || materials.Any(m => gear.CountOf(m) > 0);
        var stock = gunsmithing ? "   ·   " + string.Join(" · ", materials.Select(m => $"{m.Name} {gear.CountOf(m)}")) : "";
        canvas.DrawText(Fit($"Ashen Dust {gear.Dust}   ·   Rune Fragments {gear.Fragments}{stock}", font, Width - 2 * Pad), Bounds.Left + Pad, Bounds.Top + 112,
            SKTextAlign.Left, font, dust);
        canvas.DrawText($"Your Crafting {crafting:+0;-0;+0} ({c.SkillRank("Crafting")})", Bounds.Left + Pad, Bounds.Top + 128, SKTextAlign.Left, small, dim);

        var cell = ForgeCell;
        canvas.DrawRect(cell, cellBack);
        canvas.DrawRect(cell, faint);
        if (_forged is { } item)
        {
            _drawIcon(canvas, item.Definition, SKRect.Inflate(cell, -4, -4));
            using var edge = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2, Color = Palette.Rarity(item.Rarity) };
            canvas.DrawRect(cell, edge);
            using var name = new SKPaint { IsAntialias = true, Color = Palette.Rarity(item.Rarity) };
            canvas.DrawText(Fit(item.Name, font, Width - 2 * Pad - 80), cell.Right + 12, cell.Top + 18, SKTextAlign.Left, font, name);
            canvas.DrawText($"item level {item.ItemLevel} · DC {Core.Items.Crafting.Dc(item.ItemLevel)}{(gear.Worn.Values.Contains(item) ? " · worn" : "")}",
                cell.Right + 12, cell.Top + 34, SKTextAlign.Left, small, dim);
            if (pointer is { } p && cell.Contains(p))
                Outline(canvas, cell);
        }
        else
        {
            canvas.DrawText("empty", cell.MidX, cell.MidY + 4, SKTextAlign.Center, small, dim);
            canvas.DrawText("Right-click a piece of gear to put it on the forge.", cell.Right + 12, cell.MidY + 4, SKTextAlign.Left, small, dim);
        }

        var y = cell.Bottom + 16;
        // Buttons shrink to fit a short window: the forge's work, the belt's brews and (for a gun) its rounds.
        var rows = (_forged is { } onForge ? 3 + Math.Max(1, onForge.Affixes.Count) + (onForge.Definition.IsFirearm ? 1 : 0) : 0)
            + world.Brewable.Count(b => !world.IsRound(b)) + (gunsmithing ? world.Brewable.Count(world.IsRound) : 0);
        var headers = gunsmithing ? 2 : 1;
        var step = Math.Clamp((Bounds.Bottom - 24 - y - headers * 24) / Math.Max(1, rows), 24f, 34f);
        void Button(string label, string? why, string cost, bool afford, Action act)
        {
            var rect = SKRect.Create(Bounds.Left + Pad, y, Width - 2 * Pad, step - 4);
            var over = pointer is { } p && rect.Contains(p);
            using var back = new SKPaint { Color = why is null && over ? new SKColor(60, 40, 34, 240) : new SKColor(34, 26, 22, 240) };
            canvas.DrawRect(rect, back);
            canvas.DrawRect(rect, faint);
            canvas.DrawText(label, rect.Left + 12, rect.MidY + 5, SKTextAlign.Left, font, why is null ? ink : dim);
            if (why is not null)
                canvas.DrawText(Fit(why, small, rect.Width - font.MeasureText(label) - 40), rect.Right - 12, rect.MidY + 4, SKTextAlign.Right, small, dim);
            else
                canvas.DrawText(cost, rect.Right - 12, rect.MidY + 4, SKTextAlign.Right, small, afford ? gold : poor);
            if (why is null)
                _buttons.Add((rect, act));
            y += step;
        }
        string Cost(int d, int f, int g) => $"{d} dust{(f > 0 ? $", {f} frag." : "")}, {g} gold";
        string MaterialCost(IReadOnlyList<(Core.Items.ItemDefinition Item, int Count)> needs, int g) =>
            string.Join(", ", needs.Select(n => $"{n.Count} {n.Item.Name.ToLowerInvariant()}")) + $", {g} gold";
        bool AffordsMaterials(IReadOnlyList<(Core.Items.ItemDefinition Item, int Count)> needs, int g) =>
            gear.Gold >= g && needs.All(n => gear.CountOf(n.Item) >= n.Count);
        bool Affords((int Dust, int Fragments, int Gold) cost) => gear.Dust >= cost.Dust && gear.Fragments >= cost.Fragments && gear.Gold >= cost.Gold;

        if (_forged is { } work)
        {
            var (gotDust, gotFragments) = Core.Items.Crafting.Salvage(work);
            Button("Salvage it", Core.Items.Crafting.CannotSalvage(work) ?? (gear.Worn.Values.Contains(work) ? "Take it off first." : null),
                $"+{gotDust} dust{(gotFragments > 0 ? $", +{gotFragments} frag." : "")}", true, () => world.Salvage(work));
            foreach (var (recipe, label) in new[] { (CraftRecipe.Raise, "Raise to uncommon"), (CraftRecipe.AddAffix, "Add an affix") })
            {
                var r = recipe;
                var cost = Core.Items.Crafting.Cost(recipe, work.ItemLevel);
                Button(label, Core.Items.Crafting.CannotCraft(recipe, work), Cost(cost.Dust, cost.Fragments, cost.Gold), Affords(cost),
                    () => world.Craft(r, work));
            }
            var reroll = Core.Items.Crafting.Cost(CraftRecipe.Reroll, work.ItemLevel);
            if (Core.Items.Crafting.CannotCraft(CraftRecipe.Reroll, work) is { } noReroll)
                Button("Reroll an affix", noReroll, "", false, () => { });
            for (var i = 0; i < work.Affixes.Count && work.Definition is { IsSpecial: false }; i++)
            {
                var index = i;
                Button($"Reroll: {Fit(work.Affixes[i].Describe(), font, 200)}", null, Cost(reroll.Dust, reroll.Fragments, reroll.Gold), Affords(reroll),
                    () => world.Craft(CraftRecipe.Reroll, work, affix: index));
            }
            if (work.Definition.IsFirearm)
            {
                var refit = Core.Items.Crafting.Cost(CraftRecipe.Refit, work.ItemLevel);
                var needs = world.MaterialsNeeded(CraftRecipe.Refit, round: false);
                Button("Refit: one round more", Core.Items.Crafting.CannotCraft(CraftRecipe.Refit, work), MaterialCost(needs, refit.Gold),
                    AffordsMaterials(needs, refit.Gold), () => world.Craft(CraftRecipe.Refit, work));
            }
        }

        y += 10;
        canvas.DrawText("Brew for the belt (two at a time)", Bounds.Left + Pad, y + 4, SKTextAlign.Left, font, ink);
        y += 14;
        foreach (var brew in world.Brewable.Where(b => !world.IsRound(b)))
        {
            var b = brew;
            var cost = Core.Items.Crafting.Cost(CraftRecipe.Brew, brew.MinLevel);
            Button($"{brew.Name} · DC {Core.Items.Crafting.Dc(Math.Max(1, brew.MinLevel))}", null, Cost(cost.Dust, cost.Fragments, cost.Gold), Affords(cost),
                () => world.Craft(CraftRecipe.Brew, brew: b));
        }
        if (gunsmithing)
        {
            y += 10;
            var many = Core.Items.Crafting.RoundsPerCasting + c.RoundsBonus;
            canvas.DrawText($"Cast rounds for your gun ({many} at a time)", Bounds.Left + Pad, y + 4, SKTextAlign.Left, font, ink);
            y += 14;
            var needs = world.MaterialsNeeded(CraftRecipe.Brew, round: true);
            foreach (var round in world.Brewable.Where(world.IsRound))
            {
                var r = round;
                var cost = Core.Items.Crafting.Cost(CraftRecipe.Brew, round.MinLevel, round: true);
                Button($"{round.Name} · DC {Core.Items.Crafting.Dc(Math.Max(1, round.MinLevel))}", null, MaterialCost(needs, cost.Gold),
                    AffordsMaterials(needs, cost.Gold), () => world.Craft(CraftRecipe.Brew, brew: r));
            }
        }

        canvas.DrawText("Right-click gear to put it on the forge · a critical success adds an affix", Bounds.MidX, Bounds.Bottom - 8, SKTextAlign.Center, small, dim);
        if (pointer is { } at && cell.Contains(at) && _forged is not null)
            InventoryPanel.Tooltip(canvas, size, at, _forged, null, "On the forge", null, avoid: Bounds, gear: gear);
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
        _drawIcon(canvas, item.Definition, SKRect.Inflate(rect, -3, -3));
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
