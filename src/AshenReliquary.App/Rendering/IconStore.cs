using Avalonia.Platform;
using SkiaSharp;

namespace AshenReliquary.App.Rendering;

/// <summary>
/// The game-icons.net icons (Assets/Icons, made by tools/build_icons.py): ivory glyphs on transparency, tinted when
/// drawn. Ids are ability ids, condition names, "persistent-Fire" and the like, and HUD extras ("hero-point").
/// </summary>
internal sealed class IconStore
{
    private const string Root = "avares://AVAAshenReliquary/Assets/Icons/";
    private readonly Dictionary<string, SKImage?> _images = [];
    private readonly SKSamplingOptions _sampling = new(SKFilterMode.Linear, SKMipmapMode.Linear);

    public SKImage? Get(string id)
    {
        if (_images.TryGetValue(id, out var image))
            return image;
        var uri = new Uri(Root + id + ".png");
        if (AssetLoader.Exists(uri))
        {
            using var stream = AssetLoader.Open(uri);
            using var data = SKData.Create(stream);
            using var encoded = SKImage.FromEncodedData(data);
            image = encoded?.ToRasterImage(ensurePixelData: true);
        }
        _images[id] = image;
        return image;
    }

    /// <summary>Draws an icon tinted with <paramref name="tint"/> (white keeps the ivory). Missing ids draw nothing.</summary>
    public void Draw(SKCanvas canvas, string id, SKRect rect, SKColor tint, byte alpha = 255)
    {
        if (Get(id) is not { } image)
            return;
        using var paint = new SKPaint
        {
            ColorFilter = SKColorFilter.CreateBlendMode(tint.WithAlpha(255), SKBlendMode.Modulate),
            Color = SKColors.White.WithAlpha(alpha),
        };
        canvas.DrawImage(image, rect, _sampling, paint);
    }
}

/// <summary>One line of a tooltip: text, its colour, and an optional right-aligned value.</summary>
internal readonly record struct TipLine(string Text, SKColor Color, string? Right = null, bool Title = false);

/// <summary>The one tooltip box the HUD, character sheet and minimap share.</summary>
internal static class TooltipBox
{
    public static void Draw(SKCanvas canvas, SKSize size, SKPoint at, IReadOnlyList<TipLine> lines, SKColor edge)
    {
        if (lines.Count == 0)
            return;
        using var title = new SKFont(WorldOverlay.Serif, 15);
        using var font = new SKFont(WorldOverlay.Serif, 12);
        float width = 0;
        foreach (var l in lines)
        {
            var f = l.Title ? title : font;
            width = Math.Max(width, f.MeasureText(l.Text) + (l.Right is { } r ? font.MeasureText(r) + 24 : 0));
        }
        width = Math.Min(width + 24, 440);
        var height = lines.Count * 16 + 16;
        var x = Math.Clamp(at.X + 16, 8, size.Width - width - 8);
        var y = at.Y - height - 12 < 8 ? at.Y + 20 : at.Y - height - 12;
        y = Math.Clamp(y, 8, size.Height - height - 8);
        if (x < at.X && x + width > at.X - 4 && y < at.Y && y + height > at.Y)
            x = Math.Max(8, at.X - width - 16);
        var box = SKRect.Create(x, y, width, height);
        using var back = new SKPaint { Color = new SKColor(8, 6, 6, 246) };
        using var frame = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, Color = edge.WithAlpha(190) };
        canvas.DrawRect(box, back);
        canvas.DrawRect(box, frame);
        using var paint = new SKPaint { IsAntialias = true };
        var line = y + 22;
        foreach (var l in lines)
        {
            paint.Color = l.Color;
            canvas.DrawText(Fit(l.Text, l.Title ? title : font, width - 24 - (l.Right is { } rr ? font.MeasureText(rr) + 16 : 0)),
                x + 12, line, SKTextAlign.Left, l.Title ? title : font, paint);
            if (l.Right is { } right)
                canvas.DrawText(right, x + width - 12, line, SKTextAlign.Right, font, paint);
            line += 16;
        }
    }

    private static string Fit(string text, SKFont font, float max)
    {
        if (font.MeasureText(text) <= max)
            return text;
        while (text.Length > 1 && font.MeasureText(text + "…") > max)
            text = text[..^1];
        return text + "…";
    }
}
