using AshenReliquary.Core.World;
using SkiaSharp;

namespace AshenReliquary.App.Rendering;

/// <summary>Things drawn over the world in map space: floating combat text and monster health bars.</summary>
internal static class WorldOverlay
{
    public static readonly SKTypeface Serif = SKTypeface.FromFamilyName("serif", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
        ?? SKTypeface.Default;

    public static void DrawFloatingText(WorldView view, SKCanvas canvas, GameWorld world)
    {
        var z = (float)view.Zoom;
        using var fill = new SKPaint { IsAntialias = true };
        using var stroke = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3, Color = new SKColor(0, 0, 0, 200) };
        foreach (var text in world.Texts)
        {
            var age = (float)((world.Time - text.BornAt) / FloatingText.Lifetime);
            var at = view.ToScreen(text.X, text.Y);
            var (color, size) = text.Kind switch
            {
                FloatingTextKind.Critical => (new SKColor(255, 170, 60), 30f),
                FloatingTextKind.DamageTaken => (new SKColor(230, 70, 60), 22f),
                FloatingTextKind.Miss => (new SKColor(170, 170, 170), 17f),
                FloatingTextKind.Heal => (new SKColor(120, 230, 120), 20f),
                FloatingTextKind.Xp => (new SKColor(190, 160, 255), 18f),
                FloatingTextKind.Info => (new SKColor(240, 210, 120), 22f),
                _ => (new SKColor(245, 240, 225), 22f),
            };
            var alpha = (byte)(255 * Math.Clamp(1 - age * age, 0, 1));
            using var font = new SKFont(Serif, size * Math.Max(0.8f, z * 1.4f));
            var x = at.X + (text.Kind == FloatingTextKind.DamageTaken ? -18 : 14) * z;
            var y = at.Y - 150 * z - age * 60;
            stroke.Color = stroke.Color.WithAlpha((byte)(alpha * 0.8));
            fill.Color = color.WithAlpha(alpha);
            canvas.DrawText(text.Text, x, y, SKTextAlign.Center, font, stroke);
            canvas.DrawText(text.Text, x, y, SKTextAlign.Center, font, fill);
        }
    }

    public static void DrawHealthBar(SKCanvas canvas, SKPoint foot, float zoom, float fraction, bool champion)
    {
        var width = 70 * zoom;
        var rect = SKRect.Create(foot.X - width / 2, foot.Y - 165 * zoom, width, 6);
        using var back = new SKPaint { Color = new SKColor(0, 0, 0, 180) };
        using var bar = new SKPaint { Color = champion ? new SKColor(220, 120, 40) : new SKColor(170, 30, 25) };
        canvas.DrawRect(SKRect.Inflate(rect, 1, 1), back);
        canvas.DrawRect(SKRect.Create(rect.Left, rect.Top, rect.Width * Math.Clamp(fraction, 0, 1), rect.Height), bar);
    }
}
