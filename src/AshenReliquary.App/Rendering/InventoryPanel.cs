using AshenReliquary.Core.Abilities;
using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Items;
using AshenReliquary.Core.Rules;
using AshenReliquary.Core.World;
using SkiaSharp;

namespace AshenReliquary.App.Rendering;

/// <summary>
/// The inventory and character panel (I). Top: the worn slots and a stats summary. Below a divider: the 10x6 bag,
/// its cells sized to whatever height is left, so the two sections never overlap. Hovering shows which cell is
/// meant (a bright outline), the worn slot a bag item would replace (green), and a tooltip outside the panel with
/// what wearing or removing the item would do to your statistics.
/// Left-click a bag item to wear it, left-click a worn item to take it off, right-click a bag item to drop it.
/// Ctrl-click marks an item junk, then locked, then neither; Shift-click sends it to the open stash or trader. The bag's
/// <b>Sort</b> orders it for the hero (<see cref="ItemJudge"/>) and <b>Drop junk</b> (or <b>Sell junk</b> at a trader)
/// clears the junk in one go. A green ▲ marks an upgrade on the hero, a red ▼ a downgrade; gear the class can't use
/// well is tinted red. Items can also be dragged (see <see cref="WorldView"/>).
/// </summary>
internal sealed class InventoryPanel
{
    public const float Width = 560;
    private const float Header = 58, SlotStep = 50, Gap = 4;

    private static readonly (EquipSlot Slot, string Label, int Column, int Row)[] Slots =
    [
        (EquipSlot.Head, "Head", 0, 0), (EquipSlot.Chest, "Chest", 0, 1), (EquipSlot.Hands, "Hands", 0, 2),
        (EquipSlot.Legs, "Legs", 0, 3), (EquipSlot.Feet, "Feet", 0, 4),
        (EquipSlot.Amulet, "Amulet", 1, 0), (EquipSlot.Ring1, "Ring", 1, 1), (EquipSlot.Ring2, "Ring", 1, 2),
        (EquipSlot.Belt, "Belt", 1, 3), (EquipSlot.MainHand, "Weapon", 1, 4), (EquipSlot.OffHand, "Off hand", 1, 5),
    ];

    /// <summary>Rows of worn slots: the off hand sits under the weapon.</summary>
    private const int WornRows = 6;

    private readonly Action<SKCanvas, Core.Items.ItemDefinition, SKRect> _drawIcon;
    private float _slotSize = 44, _cell = 46, _bagTop;

    public InventoryPanel(Action<SKCanvas, Core.Items.ItemDefinition, SKRect> drawIcon) => _drawIcon = drawIcon;

    public SKRect Bounds { get; private set; }

    private SKRect SortButton => SKRect.Create(Bounds.Left + 130, _bagTop - 24, 56, 20);
    private SKRect JunkButton => SKRect.Create(Bounds.Left + 192, _bagTop - 24, 168, 20);

    /// <summary>When the Drop junk button was clicked once and is waiting for the second click to confirm.</summary>
    private DateTime _junkArmedUntil = DateTime.MinValue;

    private readonly VerdictCache _verdicts = new();

    /// <summary>How an item suits the hero, from the cache.</summary>
    public ItemVerdict VerdictFor(GameWorld world, Item item) => _verdicts.For(world, item);

    private float WornBottom => Bounds.Top + Header + WornRows * SlotStep;

    private SKRect SlotRect(int column, int row) =>
        SKRect.Create(Bounds.Left + 20 + column * 190, Bounds.Top + Header + row * SlotStep, _slotSize, _slotSize);

    /// <summary>The centres of the Sort and junk buttons and of a worn slot, as last laid out (for tests).</summary>
    public SKPoint SortCentre => new(SortButton.MidX, SortButton.MidY);
    public SKPoint JunkCentre => new(JunkButton.MidX, JunkButton.MidY);
    public SKPoint SlotCentre(EquipSlot slot) => Slots.Where(s => s.Slot == slot).Select(s => SlotRect(s.Column, s.Row)).Select(r => new SKPoint(r.MidX, r.MidY)).First();

    /// <summary>The centre of bag cell <paramref name="index"/> as last laid out (for tests that click it).</summary>
    public SKPoint BagCellCentre(int index) => new(BagRect(index).MidX, BagRect(index).MidY);

    private SKRect BagRect(int index) =>
        SKRect.Create(Bounds.Left + 20 + index % Equipment.BagColumns * (_cell + Gap),
            _bagTop + index / Equipment.BagColumns * (_cell + Gap), _cell, _cell);

    /// <summary>Fits the panel to the window: the bag gets whatever height is left under the worn section.</summary>
    public void Layout(SKSize size)
    {
        var height = Math.Max(500, size.Height - 32 - 150);
        Bounds = SKRect.Create(size.Width - Width - 16, 16, Width, height);
        _bagTop = WornBottom + 34;
        var room = Bounds.Bottom - 26 - _bagTop;
        _cell = Math.Clamp(room / Equipment.BagRows - Gap, 26, 48);
    }

    /// <summary>
    /// Set while the smith's rune bench is open: right-clicking a bag or worn item puts it on the bench instead of
    /// selling it.
    /// </summary>
    public Func<Item, bool>? Bench { get; set; }

    /// <summary>The items on the smith's bench, tagged in the bag and on the paper doll.</summary>
    public IReadOnlyCollection<Item>? Benched { get; set; }

    /// <summary>What is under a point: a worn slot, a bag cell, or nothing.</summary>
    public (EquipSlot? Slot, int? Bag) HitTest(SKPoint p)
    {
        foreach (var (slot, _, column, row) in Slots)
        {
            if (SlotRect(column, row).Contains(p))
                return (slot, null);
        }
        for (var i = 0; i < Equipment.BagSize; i++)
        {
            if (BagRect(i).Contains(p))
                return (null, i);
        }
        return (null, null);
    }

    /// <summary>
    /// Handles a click inside the panel; returns true when it was inside. <paramref name="ctrl"/> cycles a bag item's
    /// mark; <paramref name="shift"/> sends a bag item to the open stash or trader.
    /// </summary>
    public bool Click(SKPoint p, bool right, GameWorld world, bool ctrl = false, bool shift = false)
    {
        if (!Bounds.Contains(p))
            return false;
        var (slot, bag) = HitTest(p);
        var gear = world.Hero.Equipment;
        if (!right && SortButton.Contains(p))
        {
            world.SortBag();
            return true;
        }
        if (!right && JunkButton.Contains(p))
        {
            ClickJunk(world);
            return true;
        }
        if (!right && ctrl && bag is { } marked && gear.Bag[marked] is { } toMark)
        {
            toMark.CycleMark();
            return true;
        }
        if (!right && shift && bag is { } sent && gear.Bag[sent] is not null)
        {
            if (world.StashOpen)
                world.Store(sent);
            else if (world.OpenVendor is not null)
                world.Sell(sent);
            return true;
        }
        if (right && Bench is { } bench && (bag is { } b ? gear.Bag[b] : slot is { } w ? gear.In(w) : null) is { } benched)
        {
            bench(benched);
        }
        else if (bag is { } index && gear.Bag[index] is not null)
        {
            if (right && world.OpenVendor is not null)
                world.Sell(index);
            else if (right && world.StashOpen)
                world.Store(index);
            else if (right)
                world.DropFromBag(index);
            else
                gear.EquipFromBag(index);
        }
        else if (slot is { } s && !right)
        {
            gear.Unequip(s);
        }
        return true;
    }

    /// <summary>
    /// The junk button: at a trader it sells the junk at once; elsewhere the first click asks ("Drop 7? Click again")
    /// and a second within 3 s drops it.
    /// </summary>
    private void ClickJunk(GameWorld world)
    {
        if (world.Junk.Count == 0)
        {
            world.Announce("Nothing is marked as junk. Ctrl-click an item to mark it.");
            return;
        }
        if (world.OpenVendor is not null)
        {
            world.SellJunk();
            return;
        }
        if (DateTime.UtcNow < _junkArmedUntil)
        {
            world.DropJunk();
            _junkArmedUntil = DateTime.MinValue;
            return;
        }
        _junkArmedUntil = DateTime.UtcNow.AddSeconds(3);
    }

    public void Draw(SKCanvas canvas, SKSize size, GameWorld world, SKPoint? pointer, SKImage? portrait = null)
    {
        Layout(size);
        var c = world.Hero.Character;
        var gear = world.Hero.Equipment;
        using var back = new SKPaint { Color = Palette.Panel };
        using var frame = new SKPaint { IsAntialias = true, Color = Palette.Frame, Style = SKPaintStyle.Stroke, StrokeWidth = 2 };
        using var faint = new SKPaint { IsAntialias = true, Color = new SKColor(55, 46, 38), Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
        using var cellBack = new SKPaint { Color = new SKColor(28, 23, 20, 240) };
        using var ink = new SKPaint { IsAntialias = true, Color = Palette.Ink };
        using var dim = new SKPaint { IsAntialias = true, Color = Palette.Dim };
        using var gold = new SKPaint { IsAntialias = true, Color = Palette.Gold };
        using var title = new SKFont(WorldOverlay.Serif, 19);
        using var font = new SKFont(WorldOverlay.Serif, 13);
        using var small = new SKFont(WorldOverlay.Serif, 11);

        var (hoverSlot, hoverBag) = pointer is { } pt && Bounds.Contains(pt) ? HitTest(pt) : (null, null);
        var hoveredBagItem = hoverBag is { } hb ? gear.Bag[hb] : null;
        var targetSlot = hoveredBagItem is not null ? gear.SlotFor(hoveredBagItem) : null;

        canvas.DrawRect(Bounds, back);
        canvas.DrawRect(Bounds, frame);
        var textLeft = Bounds.Left + 20;
        if (portrait is not null)
        {
            var face = SKRect.Create(Bounds.Left + 14, Bounds.Top + 8, 44, 44);
            canvas.DrawImage(portrait, face, new SKSamplingOptions(SKCubicResampler.Mitchell));
            canvas.DrawRect(face, frame);
            textLeft = face.Right + 10;
        }
        canvas.DrawText(c.Name, textLeft, Bounds.Top + 28, SKTextAlign.Left, title, ink);
        var heritage = c.Heritage is { } h ? $"{h.Name} " : "";
        canvas.DrawText($"Level {c.Level} {heritage}{c.Ancestry.Name} {c.Background.Name} {c.Class.Name}", textLeft, Bounds.Top + 46, SKTextAlign.Left, small, dim);

        // Worn.
        foreach (var (slot, label, column, row) in Slots)
        {
            var rect = SlotRect(column, row);
            canvas.DrawRect(rect, cellBack);
            if (gear.In(slot) is { } item)
            {
                _drawIcon(canvas, item.Definition, SKRect.Inflate(rect, -3, -3));
                RarityFrame(canvas, rect, item.Rarity);
                using var name = new SKPaint { IsAntialias = true, Color = Palette.Rarity(item.Rarity) };
                canvas.DrawText(Fit(item.Name, 21), rect.Right + 8, rect.MidY - 2, SKTextAlign.Left, small, name);
                canvas.DrawText(label, rect.Right + 8, rect.MidY + 12, SKTextAlign.Left, small, dim);
            }
            else
            {
                canvas.DrawRect(rect, faint);
                canvas.DrawText(label, rect.MidX, rect.MidY + 4, SKTextAlign.Center, small, dim);
            }
            if (gear.In(slot) is { } worn && Benched?.Contains(worn) == true)
                Highlight(canvas, rect, Palette.Frame, "bench");
            if (slot == targetSlot)
                Highlight(canvas, rect, new SKColor(110, 220, 110), "replaces");
            if (slot == hoverSlot)
                Highlight(canvas, rect, new SKColor(255, 225, 140), null);
        }

        // Stats summary, right of the worn slots.
        var x = Bounds.Left + 400;
        var y = Bounds.Top + Header + 10;
        foreach (var line in StatLines(world))
        {
            canvas.DrawText(line, x, y, SKTextAlign.Left, small, line.EndsWith(':') ? ink : dim);
            y += 14;
        }

        // Divider, then the bag.
        var dividerY = WornBottom + 8;
        canvas.DrawLine(Bounds.Left + 14, dividerY, Bounds.Right - 14, dividerY, frame);
        canvas.DrawText("Bag", Bounds.Left + 20, _bagTop - 8, SKTextAlign.Left, font, ink);
        canvas.DrawText($"{gear.Bag.Count(i => i is not null)} / {Equipment.BagSize}", Bounds.Left + 56, _bagTop - 8, SKTextAlign.Left, small, dim);
        var junk = world.Junk;
        var selling = world.OpenVendor is not null;
        var armed = DateTime.UtcNow < _junkArmedUntil;
        Button(canvas, SortButton, "Sort", pointer, small, false);
        Button(canvas, JunkButton, junk.Count == 0 ? "Drop junk (none marked)"
            : selling ? $"Sell junk ({junk.Count}): {junk.Sum(Pricing.SellPrice)} gold"
            : armed ? $"Drop {junk.Count}? Click again" : $"Drop junk ({junk.Count})", pointer, small, armed);
        canvas.DrawText($"{gear.Gold} gold", Bounds.Right - 20, _bagTop - 8, SKTextAlign.Right, font, gold);
        for (var i = 0; i < Equipment.BagSize; i++)
        {
            var rect = BagRect(i);
            canvas.DrawRect(rect, cellBack);
            if (gear.Bag[i] is { } item)
            {
                _drawIcon(canvas, item.Definition, SKRect.Inflate(rect, -2, -2));
                Judgement(canvas, rect, VerdictFor(world, item));
                RarityFrame(canvas, rect, item.Rarity);
                MarkCorner(canvas, rect, item.Mark);
                if (item.Count > 1)
                    StackCount(canvas, rect, item.Count, small);
            }
            else
            {
                canvas.DrawRect(rect, faint);
            }
            if (gear.Bag[i] is { } benched && Benched?.Contains(benched) == true)
                Highlight(canvas, rect, Palette.Frame, "bench");
            if (i == hoverBag)
                Highlight(canvas, rect, new SKColor(255, 225, 140), null);
        }
        var trading = world.OpenVendor is not null;
        canvas.DrawText(Bench is not null ? "Bag: left-click wear      Worn: left-click take off      Either: right-click to the bench"
            : trading ? "Left-click wear · right-click sell · Ctrl-click junk/lock · drag to move or sell"
            : world.StashOpen ? "Left-click wear · right-click stash · Ctrl-click junk/lock · drag to move or stash"
            : "Left-click wear · right-click drop · Ctrl-click junk/lock · drag to move or drop", Bounds.MidX, Bounds.Bottom - 8, SKTextAlign.Center, small, dim);

        // Tooltip, outside the panel so it never covers the slots being compared.
        if (pointer is { } p)
        {
            if (hoveredBagItem is { } bagItem)
            {
                var worn = targetSlot is { } t ? gear.In(t) : null;
                var where = trading && Bench is null ? $"In your bag · sells for {Pricing.SellPrice(bagItem)} gold" : "In your bag";
                Tooltip(canvas, size, p, bagItem, worn, where, gear.PreviewEquip(bagItem), "If you wear it:", Bounds);
            }
            else if (hoverSlot is { } s && gear.In(s) is { } wornItem)
            {
                Tooltip(canvas, size, p, wornItem, null, $"Worn · {s}", gear.PreviewUnequip(s), "If you take it off:", Bounds);
            }
        }
    }

    /// <summary>A bag cell's verdict: a red wash over gear the class can't use well, a green ▲ for an upgrade, a red ▼ for a downgrade.</summary>
    public static void Judgement(SKCanvas canvas, SKRect rect, ItemVerdict verdict)
    {
        if (verdict.Fit == ItemFit.PoorFit)
        {
            using var wash = new SKPaint { Color = new SKColor(150, 20, 15, 95) };
            canvas.DrawRect(rect, wash);
            return;
        }
        if (verdict.Comparison is not (ItemComparison.Better or ItemComparison.Worse))
            return;
        var up = verdict.Comparison == ItemComparison.Better;
        var r = Math.Max(4f, rect.Width * 0.14f);
        var (cx, cy) = (rect.Right - r - 3, rect.Top + r + 3);
        using var path = new SKPath();
        if (up)
        {
            path.MoveTo(cx, cy - r);
            path.LineTo(cx + r, cy + r * 0.7f);
            path.LineTo(cx - r, cy + r * 0.7f);
        }
        else
        {
            path.MoveTo(cx, cy + r);
            path.LineTo(cx + r, cy - r * 0.7f);
            path.LineTo(cx - r, cy - r * 0.7f);
        }
        path.Close();
        using var fill = new SKPaint { IsAntialias = true, Color = up ? Better : Worse };
        using var edge = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.2f, Color = SKColors.Black.WithAlpha(200) };
        canvas.DrawPath(path, fill);
        canvas.DrawPath(path, edge);
    }

    /// <summary>A player's mark: a dull red corner for junk, a gold one for locked.</summary>
    public static void MarkCorner(SKCanvas canvas, SKRect rect, ItemMark mark)
    {
        if (mark == ItemMark.None)
            return;
        var r = Math.Max(7f, rect.Width * 0.3f);
        using var path = new SKPath();
        path.MoveTo(rect.Left, rect.Bottom);
        path.LineTo(rect.Left, rect.Bottom - r);
        path.LineTo(rect.Left + r, rect.Bottom);
        path.Close();
        using var paint = new SKPaint { IsAntialias = true, Color = mark == ItemMark.Junk ? new SKColor(170, 45, 35, 230) : new SKColor(225, 185, 80, 235) };
        canvas.DrawPath(path, paint);
    }

    /// <summary>A small text button; <paramref name="hot"/> draws it lit (a confirmation waiting).</summary>
    public static void Button(SKCanvas canvas, SKRect rect, string label, SKPoint? pointer, SKFont font, bool hot)
    {
        var over = pointer is { } p && rect.Contains(p);
        using var back = new SKPaint { Color = hot ? new SKColor(110, 40, 30, 240) : over ? new SKColor(60, 40, 34, 240) : new SKColor(34, 26, 22, 240) };
        using var edge = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1, Color = over || hot ? Palette.Frame : new SKColor(70, 58, 46) };
        using var ink = new SKPaint { IsAntialias = true, Color = over || hot ? Palette.Gold : Palette.Ink };
        canvas.DrawRect(rect, back);
        canvas.DrawRect(rect, edge);
        canvas.DrawText(label, rect.MidX, rect.MidY + 4, SKTextAlign.Center, font, ink);
    }

    /// <summary>How many are in a stack, in the cell's bottom-right corner.</summary>
    public static void StackCount(SKCanvas canvas, SKRect rect, int count, SKFont font)
    {
        using var stroke = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3, Color = SKColors.Black };
        using var ink = new SKPaint { IsAntialias = true, Color = Palette.Ink };
        canvas.DrawText(count.ToString(), rect.Right - 3, rect.Bottom - 3, SKTextAlign.Right, font, stroke);
        canvas.DrawText(count.ToString(), rect.Right - 3, rect.Bottom - 3, SKTextAlign.Right, font, ink);
    }

    private static void Highlight(SKCanvas canvas, SKRect rect, SKColor color, string? label)
    {
        using var glow = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3, Color = color };
        canvas.DrawRect(SKRect.Inflate(rect, 2, 2), glow);
        if (label is null)
            return;
        using var font = new SKFont(WorldOverlay.Serif, 10);
        using var back = new SKPaint { Color = color.WithAlpha(220) };
        using var ink = new SKPaint { IsAntialias = true, Color = SKColors.Black };
        var tag = SKRect.Create(rect.Left, rect.Bottom - 12, rect.Width, 12);
        canvas.DrawRect(tag, back);
        canvas.DrawText(label, tag.MidX, tag.Bottom - 2.5f, SKTextAlign.Center, font, ink);
    }

    private static IEnumerable<string> StatLines(GameWorld world)
    {
        var c = world.Hero.Character;
        var w = c.Weapon;
        var avg = w.DiceCount * (w.Definition.DamageDie + 1) / 2.0 + c.DamageBonus().Total;
        yield return "Defence:";
        yield return $"HP {c.HitPoints} / {c.MaxHitPoints}   AC {c.ArmorClass().Total}";
        yield return $"Fort {c.Save(StatisticKind.Fortitude).Total:+0} Ref {c.Save(StatisticKind.Reflex).Total:+0} Will {c.Save(StatisticKind.Will).Total:+0}";
        var resist = string.Join(", ", c.Defenses.Resistances.Where(r => r.Value > 0).Select(r => $"{r.Key.ToString().ToLowerInvariant()} {r.Value}"));
        if (resist.Length > 0)
            yield return $"Resist {resist}";
        yield return "Offence:";
        yield return $"Attack {c.AttackModifier().Total:+0}";
        yield return $"{w.DiceCount}d{w.Definition.DamageDie}{c.DamageBonus().Total:+0;-0;+0} (avg {avg:0.#})";
        var aps = w.Definition.AttacksPerSecond * (1 + Math.Min(c.Gear.AttackSpeed, ActionTiming.AttackSpeedCap));
        yield return $"{aps:0.##} attacks/s";
        if (world.Hero.Resource.Kind == ResourceKind.Mana)
            yield return $"Spell {c.SpellAttack().Total:+0}  DC {c.SpellDc().Total}";
        yield return "Movement:";
        yield return $"Speed {c.SpeedFeet} ft{(c.Gear.MoveSpeed > 0 ? $" +{c.Gear.MoveSpeed * 100:0}%" : "")}";
    }

    private static void RarityFrame(SKCanvas canvas, SKRect rect, Rarity rarity)
    {
        using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = rarity == Rarity.Common ? 1 : 2, Color = Palette.Rarity(rarity).WithAlpha(rarity == Rarity.Common ? (byte)90 : (byte)220) };
        canvas.DrawRect(rect, paint);
    }

    private static string Fit(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";

    public static readonly SKColor Better = new(110, 220, 110);
    public static readonly SKColor Worse = new(235, 90, 80);
    public static readonly SKColor Same = new(225, 200, 90);

    /// <summary>
    /// The item tooltip: name in its rarity colour, where it is, its lines, what it would replace, and (when a
    /// preview is given) every statistic before and after, green for better, red for worse, yellow for the same.
    /// Inside the panel the tooltip goes to the panel's left, so it never hides the slots being compared.
    /// </summary>
    public static void Tooltip(SKCanvas canvas, SKSize size, SKPoint at, Item item, Item? compare, string? where = null,
        IReadOnlyList<StatChange>? preview = null, string previewTitle = "If you wear it:", SKRect? avoid = null)
    {
        using var nameFont = new SKFont(WorldOverlay.Serif, 15);
        using var font = new SKFont(WorldOverlay.Serif, 12);
        using var bold = new SKFont(WorldOverlay.Serif, 12.5f);
        var lines = new List<(string Left, string? Right, SKColor Color, SKFont Font)> { (item.Name, null, Palette.Rarity(item.Rarity), nameFont) };
        if (where is not null)
            lines.Add((where, null, new SKColor(150, 190, 230), font));
        lines.AddRange(item.Describe().Select(l => (l, (string?)null, Palette.Dim, font)));
        if (compare is not null)
        {
            lines.Add(("", null, Palette.Dim, font));
            lines.Add(($"Replaces: {compare.Name}", null, Palette.Rarity(compare.Rarity), font));
        }
        if (preview is { Count: > 0 })
        {
            lines.Add(("", null, Palette.Dim, font));
            lines.Add((previewTitle, null, Palette.Ink, bold));
            foreach (var change in preview)
            {
                var color = change.Direction > 0 ? Better : change.Direction < 0 ? Worse : Same;
                var delta = change.Direction == 0 ? "" : $"  ({(change.Delta > 0 ? "+" : "")}{change.Delta.ToString(change.After.Format.Contains('%') ? "0" : change.After.Format.Contains('.') ? "0.0#" : "0", System.Globalization.CultureInfo.InvariantCulture)})";
                lines.Add(($"  {change.Name}", $"{change.Before.Text} → {change.After.Text}{delta}", color, font));
            }
        }

        const float gutter = 18;
        var leftWidth = lines.Max(l => l.Font.MeasureText(l.Left));
        var rightWidth = lines.Where(l => l.Right is not null).Select(l => font.MeasureText(l.Right!)).DefaultIfEmpty(0).Max();
        var width = Math.Max(leftWidth, lines.Where(l => l.Right is not null).Select(l => font.MeasureText(l.Left)).DefaultIfEmpty(0).Max() + gutter + rightWidth) + 24;
        var height = lines.Count * 16 + 14;
        float x, y;
        if (avoid is { } panel && panel.Contains(at))
        {
            // Beside the panel: to its left, or to its right when there is no room (a panel on the left edge).
            x = panel.Left - width - 10 >= 8 ? panel.Left - width - 10 : panel.Right + 10;
            y = Math.Clamp(at.Y - 24, 8, size.Height - height - 8);
        }
        else
        {
            x = Math.Min(at.X + 18, size.Width - width - 8);
            y = Math.Min(at.Y + 18, size.Height - height - 8);
        }
        x = Math.Max(8, x);
        var box = SKRect.Create(x, y, width, height);
        using var back = new SKPaint { Color = new SKColor(8, 6, 6, 246) };
        using var edge = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, Color = Palette.Rarity(item.Rarity).WithAlpha(180) };
        canvas.DrawRect(box, back);
        canvas.DrawRect(box, edge);
        using var paint = new SKPaint { IsAntialias = true };
        var line = y + 21;
        foreach (var (left, right, color, f) in lines)
        {
            paint.Color = right is null ? color : Palette.Dim;
            canvas.DrawText(left, x + 12, line, SKTextAlign.Left, f, paint);
            if (right is not null)
            {
                paint.Color = color;
                canvas.DrawText(right, x + width - 12, line, SKTextAlign.Right, f, paint);
            }
            line += 16;
        }
    }
}

/// <summary>
/// Items' verdicts for the hero (<see cref="GameWorld.Judge"/>), worked out again only when the worn gear, the bag or the
/// level changes: judging compares stats by trying the item on, too much to do for every cell every frame.
/// </summary>
internal sealed class VerdictCache
{
    private readonly Dictionary<Item, ItemVerdict> _verdicts = [];
    private int _key = int.MinValue;

    public ItemVerdict For(GameWorld world, Item item)
    {
        var gear = world.Hero.Equipment;
        var key = HashCode.Combine(gear.BagVersion, world.Hero.Character.Level,
            gear.Worn.Aggregate(0, (h, kv) => HashCode.Combine(h, kv.Key, kv.Value.Id)));
        if (key != _key)
        {
            _verdicts.Clear();
            _key = key;
        }
        if (!_verdicts.TryGetValue(item, out var verdict))
            _verdicts[item] = verdict = world.Judge(item);
        return verdict;
    }
}
