using AshenReliquary.Core.World;
using SkiaSharp;

namespace AshenReliquary.App.Rendering;

/// <summary>
/// The conversation on screen, above the ability bar: the speaker's portrait (people only), their name, what they say
/// and the answers, numbered (click one or press its number). A check shows what it rolls, the DC and the odds; the
/// last roll's result sits above the text. Esc or walking away ends it.
/// </summary>
internal sealed class DialoguePanel
{
    public const float Width = 760;
    private const float Pad = 18, Portrait = 112, LineHeight = 21, ChoiceHeight = 26;

    public SKRect Bounds { get; private set; }
    private readonly List<SKRect> _choiceRects = [];

    public void Draw(SKCanvas canvas, SKSize size, DialogueView view, SKImage? portrait, SKPoint? pointer)
    {
        using var nameFont = new SKFont(WorldOverlay.Serif, 18);
        using var textFont = new SKFont(WorldOverlay.Serif, 15);
        using var small = new SKFont(WorldOverlay.Serif, 12);
        var width = Math.Min(Width, size.Width - 32);
        var textLeft = portrait is null ? Pad : Pad + Portrait + 16;
        var textWidth = width - textLeft - Pad;
        var lines = WrapText(view.Text, textFont, textWidth);
        var choiceLines = view.Choices.Select((c, i) => WrapText($"{i + 1}. {c.Text}", textFont, textWidth - 8)).ToList();
        var textHeight = 34 + (view.LastRoll is null ? 0 : 20) + lines.Count * LineHeight + 12
            + choiceLines.Sum(c => c.Count * LineHeight + (ChoiceHeight - LineHeight) + 14);
        var height = Math.Max(portrait is null ? 0 : Portrait + 2 * Pad, textHeight + Pad);
        Bounds = SKRect.Create((size.Width - width) / 2, size.Height - 150 - height, width, height);

        using var back = new SKPaint { Color = Palette.Panel };
        using var frame = new SKPaint { Color = Palette.Frame, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
        canvas.DrawRect(Bounds, back);
        canvas.DrawRect(Bounds, frame);
        if (portrait is not null)
        {
            var rect = SKRect.Create(Bounds.Left + Pad, Bounds.Top + Pad, Portrait, Portrait);
            canvas.DrawImage(portrait, rect, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
            canvas.DrawRect(rect, frame);
        }

        using var gold = new SKPaint { IsAntialias = true, Color = Palette.Gold };
        using var ink = new SKPaint { IsAntialias = true, Color = Palette.Ink };
        using var dim = new SKPaint { IsAntialias = true, Color = Palette.Dim };
        using var roll = new SKPaint { IsAntialias = true, Color = new SKColor(150, 200, 255) };
        using var hover = new SKPaint { Color = new SKColor(240, 200, 90, 36) };
        var x = Bounds.Left + textLeft;
        var y = Bounds.Top + Pad + 16;
        canvas.DrawText(view.Speaker, x, y, SKTextAlign.Left, nameFont, gold);
        y += 20;
        if (view.LastRoll is { } last)
        {
            canvas.DrawText(last, x, y + 2, SKTextAlign.Left, small, roll);
            y += 20;
        }
        foreach (var line in lines)
        {
            y += LineHeight;
            canvas.DrawText(line, x, y - 5, SKTextAlign.Left, textFont, ink);
        }
        y += 12;
        _choiceRects.Clear();
        for (var i = 0; i < view.Choices.Count; i++)
        {
            var choice = view.Choices[i];
            var rowHeight = choiceLines[i].Count * LineHeight + (ChoiceHeight - LineHeight) + (choice.CheckLabel is null ? 0 : 14);
            var rect = SKRect.Create(x - 6, y, textWidth + 6, rowHeight);
            _choiceRects.Add(rect);
            if (pointer is { } p && rect.Contains(p))
                canvas.DrawRect(rect, hover);
            var ly = y;
            foreach (var line in choiceLines[i])
            {
                ly += LineHeight;
                canvas.DrawText(line, x, ly - 2, SKTextAlign.Left, textFont, gold);
            }
            if (choice.CheckLabel is { } label)
            {
                var modifier = choice.Modifier!.Value;
                canvas.DrawText($"{label} {(modifier >= 0 ? "+" : "")}{modifier} against DC {choice.Dc}: {choice.Chance:0%} to succeed",
                    x + 18, ly + 13, SKTextAlign.Left, small, dim);
            }
            y += rowHeight + 4;
        }
    }

    /// <summary>The answer under a point, or -1; null when the point is off the panel.</summary>
    public int? HitTest(SKPoint p)
    {
        if (!Bounds.Contains(p))
            return null;
        return _choiceRects.FindIndex(r => r.Contains(p));
    }

    private static List<string> WrapText(string text, SKFont font, float width)
    {
        var lines = new List<string>();
        var line = "";
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = line.Length == 0 ? word : $"{line} {word}";
            if (font.MeasureText(candidate) > width && line.Length > 0)
            {
                lines.Add(line);
                line = word;
            }
            else
            {
                line = candidate;
            }
        }
        if (line.Length > 0)
            lines.Add(line);
        return lines;
    }
}
