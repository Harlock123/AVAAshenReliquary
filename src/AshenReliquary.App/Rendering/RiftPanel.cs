using AshenReliquary.Core.World;
using SkiaSharp;

namespace AshenReliquary.App.Rendering;

/// <summary>
/// A woken shrine's Rift: every other woken shrine, in this zone or another, as a list. Click one to step through;
/// Esc or walking away closes it.
/// </summary>
internal sealed class RiftPanel
{
    public const float Width = 420;
    private const float RowHeight = 44, Top = 78, Pad = 20;

    public SKRect Bounds { get; private set; }

    private SKRect RowRect(int index) => SKRect.Create(Bounds.Left + Pad, Bounds.Top + Top + index * RowHeight, Width - 2 * Pad, RowHeight - 6);

    public void Layout(SKSize size, int rows) =>
        Bounds = SKRect.Create((size.Width - Width) / 2, Math.Max(16, size.Height * 0.18f), Width, Top + Math.Max(1, rows) * RowHeight + Pad);

    public void Draw(SKCanvas canvas, SKSize size, GameWorld world, SKPoint? pointer)
    {
        var destinations = world.RiftDestinations;
        Layout(size, destinations.Count);
        using var back = new SKPaint { Color = Palette.Panel };
        using var frame = new SKPaint { Color = new SKColor(120, 190, 255, 160), Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
        canvas.DrawRect(Bounds, back);
        canvas.DrawRect(Bounds, frame);
        using var titleFont = new SKFont(WorldOverlay.Serif, 22);
        using var font = new SKFont(WorldOverlay.Serif, 15);
        using var small = new SKFont(WorldOverlay.Serif, 12);
        using var blue = new SKPaint { IsAntialias = true, Color = new SKColor(150, 200, 255) };
        using var ink = new SKPaint { IsAntialias = true, Color = Palette.Ink };
        using var dim = new SKPaint { IsAntialias = true, Color = Palette.Dim };
        canvas.DrawText(world.OpenShrine?.Name ?? "Rift", Bounds.Left + Pad, Bounds.Top + 34, SKTextAlign.Left, titleFont, blue);
        canvas.DrawText("Step through to any shrine you have woken.", Bounds.Left + Pad, Bounds.Top + 56, SKTextAlign.Left, small, dim);
        if (destinations.Count == 0)
        {
            canvas.DrawText("No other shrine is awake yet.", Bounds.Left + Pad, Bounds.Top + Top + 20, SKTextAlign.Left, font, dim);
            return;
        }
        using var hover = new SKPaint { Color = new SKColor(120, 190, 255, 40) };
        for (var i = 0; i < destinations.Count; i++)
        {
            var (zone, shrine) = destinations[i];
            var rect = RowRect(i);
            if (pointer is { } p && rect.Contains(p))
                canvas.DrawRect(rect, hover);
            canvas.DrawText(shrine.Name, rect.Left + 10, rect.Top + 18, SKTextAlign.Left, font, ink);
            canvas.DrawText(zone.Id == world.Zone.Id ? $"{zone.Name} (here)" : zone.Name, rect.Left + 10, rect.Top + 33, SKTextAlign.Left, small, dim);
        }
    }

    /// <summary>Travels to the shrine under the point; true when the click landed on the panel.</summary>
    public bool Click(SKPoint p, GameWorld world)
    {
        if (!Bounds.Contains(p))
            return false;
        var destinations = world.RiftDestinations;
        for (var i = 0; i < destinations.Count; i++)
        {
            if (RowRect(i).Contains(p))
            {
                world.RiftTravel(destinations[i].Shrine.Id);
                break;
            }
        }
        return true;
    }
}
