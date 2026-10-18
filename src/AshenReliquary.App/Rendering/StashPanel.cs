using AshenReliquary.Core.Items;
using AshenReliquary.Core.World;
using SkiaSharp;

namespace AshenReliquary.App.Rendering;

/// <summary>
/// The hub's stash, on the left while the inventory is open on the right: 60 slots that stay put while the hero
/// travels. Left-click a stashed item to take it into the bag; right-click a bag item (in the inventory) to stash it.
/// </summary>
internal sealed class StashPanel
{
    public const float Width = 440;
    private const float Cell = 34, Step = 40, Pad = 20;

    private readonly Action<SKCanvas, int, SKRect> _drawIcon;

    public StashPanel(Action<SKCanvas, int, SKRect> drawIcon) => _drawIcon = drawIcon;

    public SKRect Bounds { get; private set; }

    private SKRect CellRect(int index) =>
        SKRect.Create(Bounds.Left + Pad + index % Stash.Columns * Step, Bounds.Top + 76 + index / Stash.Columns * Step, Cell, Cell);

    private void Layout() => Bounds = SKRect.Create(16, 16, Width, 76 + Stash.Rows * Step + 50);

    private int? HitTest(SKPoint p)
    {
        for (var i = 0; i < Stash.Size; i++)
        {
            if (CellRect(i).Contains(p))
                return i;
        }
        return null;
    }

    /// <summary>Handles a click inside the panel; true when it was inside.</summary>
    public bool Click(SKPoint p, GameWorld world)
    {
        if (!world.StashOpen || !Bounds.Contains(p))
            return false;
        if (HitTest(p) is { } index)
            world.Retrieve(index);
        return true;
    }

    public void Draw(SKCanvas canvas, SKSize size, GameWorld world, SKPoint? pointer)
    {
        Layout();
        using var back = new SKPaint { Color = Palette.Panel };
        using var frame = new SKPaint { IsAntialias = true, Color = Palette.Frame, Style = SKPaintStyle.Stroke, StrokeWidth = 2 };
        using var faint = new SKPaint { IsAntialias = true, Color = new SKColor(55, 46, 38), Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
        using var cellBack = new SKPaint { Color = new SKColor(28, 23, 20, 240) };
        using var ink = new SKPaint { IsAntialias = true, Color = Palette.Ink };
        using var dim = new SKPaint { IsAntialias = true, Color = Palette.Dim };
        using var title = new SKFont(WorldOverlay.Serif, 19);
        using var small = new SKFont(WorldOverlay.Serif, 11);
        canvas.DrawRect(Bounds, back);
        canvas.DrawRect(Bounds, frame);
        canvas.DrawText("Stash", Bounds.Left + Pad, Bounds.Top + 30, SKTextAlign.Left, title, ink);
        canvas.DrawText("Kept safe in Fort Gallowmere while you travel.", Bounds.Left + Pad, Bounds.Top + 48, SKTextAlign.Left, small, dim);

        var hover = pointer is { } pt && Bounds.Contains(pt) ? HitTest(pt) : null;
        var slots = world.Stash.Slots;
        for (var i = 0; i < Stash.Size; i++)
        {
            var rect = CellRect(i);
            canvas.DrawRect(rect, cellBack);
            if (slots[i] is { } item)
            {
                _drawIcon(canvas, item.Definition.Icon, SKRect.Inflate(rect, -2, -2));
                using var edge = new SKPaint
                {
                    IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = item.Rarity == Rarity.Common ? 1 : 2,
                    Color = Palette.Rarity(item.Rarity).WithAlpha(item.Rarity == Rarity.Common ? (byte)90 : (byte)220),
                };
                canvas.DrawRect(rect, edge);
            }
            else
            {
                canvas.DrawRect(rect, faint);
            }
            if (i == hover)
            {
                using var glow = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3, Color = new SKColor(255, 225, 140) };
                canvas.DrawRect(SKRect.Inflate(rect, 2, 2), glow);
            }
        }
        canvas.DrawText("Left-click to take · right-click a bag item to stash it · Esc or walk away to close", Bounds.MidX, Bounds.Bottom - 12,
            SKTextAlign.Center, small, dim);

        if (pointer is { } p && hover is { } h && slots[h] is { } hovered)
        {
            var gear = world.Hero.Equipment;
            var worn = gear.SlotFor(hovered) is { } slot ? gear.In(slot) : null;
            InventoryPanel.Tooltip(canvas, size, p, hovered, worn, "In the stash", gear.PreviewEquip(hovered), "If you wear it:", Bounds);
        }
    }
}
