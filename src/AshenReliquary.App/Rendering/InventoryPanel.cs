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
        (EquipSlot.Belt, "Belt", 1, 3), (EquipSlot.MainHand, "Weapon", 1, 4),
    ];

    private readonly Action<SKCanvas, int, SKRect> _drawIcon;
    private float _slotSize = 44, _cell = 46, _bagTop;

    public InventoryPanel(Action<SKCanvas, int, SKRect> drawIcon) => _drawIcon = drawIcon;

    public SKRect Bounds { get; private set; }

    private float WornBottom => Bounds.Top + Header + 5 * SlotStep;

    private SKRect SlotRect(int column, int row) =>
        SKRect.Create(Bounds.Left + 20 + column * 190, Bounds.Top + Header + row * SlotStep, _slotSize, _slotSize);

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

    /// <summary>Handles a click inside the panel; returns true when it was inside.</summary>
    public bool Click(SKPoint p, bool right, GameWorld world)
    {
        if (!Bounds.Contains(p))
            return false;
        var (slot, bag) = HitTest(p);
        var gear = world.Hero.Equipment;
        if (bag is { } index && gear.Bag[index] is not null)
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
                _drawIcon(canvas, item.Definition.Icon, SKRect.Inflate(rect, -3, -3));
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
        canvas.DrawText($"{gear.Gold} gold", Bounds.Right - 20, _bagTop - 8, SKTextAlign.Right, font, gold);
        for (var i = 0; i < Equipment.BagSize; i++)
        {
            var rect = BagRect(i);
            canvas.DrawRect(rect, cellBack);
            if (gear.Bag[i] is { } item)
            {
                _drawIcon(canvas, item.Definition.Icon, SKRect.Inflate(rect, -2, -2));
                RarityFrame(canvas, rect, item.Rarity);
            }
            else
            {
                canvas.DrawRect(rect, faint);
            }
            if (i == hoverBag)
                Highlight(canvas, rect, new SKColor(255, 225, 140), null);
        }
        var trading = world.OpenVendor is not null;
        canvas.DrawText(trading ? "Bag: left-click wear · right-click sell      Worn: left-click take off"
            : "Bag: left-click wear · right-click drop      Worn: left-click take off", Bounds.MidX, Bounds.Bottom - 8, SKTextAlign.Center, small, dim);

        // Tooltip, outside the panel so it never covers the slots being compared.
        if (pointer is { } p)
        {
            if (hoveredBagItem is { } bagItem)
            {
                var worn = targetSlot is { } t ? gear.In(t) : null;
                var where = trading ? $"In your bag · sells for {Pricing.SellPrice(bagItem)} gold" : "In your bag";
                Tooltip(canvas, size, p, bagItem, worn, where, gear.PreviewEquip(bagItem), "If you wear it:", Bounds);
            }
            else if (hoverSlot is { } s && gear.In(s) is { } wornItem)
            {
                Tooltip(canvas, size, p, wornItem, null, $"Worn · {s}", gear.PreviewUnequip(s), "If you take it off:", Bounds);
            }
        }
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
