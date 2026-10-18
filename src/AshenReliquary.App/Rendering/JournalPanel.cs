using AshenReliquary.Core.World;
using SkiaSharp;

namespace AshenReliquary.App.Rendering;

/// <summary>
/// The journal (J): the main story, then the side stories found so far, newest first in each. An entry appears only
/// once something has been discovered.
/// </summary>
internal sealed class JournalPanel
{
    public const float Width = 520;
    private const float Pad = 22, LineHeight = 19;

    public SKRect Bounds { get; private set; }

    public void Draw(SKCanvas canvas, SKSize size, GameWorld world)
    {
        Bounds = SKRect.Create((size.Width - Width) / 2, 16, Width, Math.Max(300, size.Height - 32 - 150));
        using var back = new SKPaint { Color = Palette.Panel };
        using var frame = new SKPaint { Color = Palette.Frame, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
        canvas.DrawRect(Bounds, back);
        canvas.DrawRect(Bounds, frame);
        using var titleFont = new SKFont(WorldOverlay.Serif, 22);
        using var headFont = new SKFont(WorldOverlay.Serif, 15);
        using var textFont = new SKFont(WorldOverlay.Serif, 13);
        using var gold = new SKPaint { IsAntialias = true, Color = Palette.Gold };
        using var ink = new SKPaint { IsAntialias = true, Color = Palette.Ink };
        using var dim = new SKPaint { IsAntialias = true, Color = Palette.Dim };
        var x = Bounds.Left + Pad;
        var y = Bounds.Top + 36;
        canvas.DrawText("Journal", x, y, SKTextAlign.Left, titleFont, gold);
        canvas.DrawText("J to close", Bounds.Right - Pad, y, SKTextAlign.Right, textFont, dim);
        y += 16;
        if (world.Journal.Count == 0)
        {
            canvas.DrawText("Nothing written yet.", x, y + 30, SKTextAlign.Left, headFont, dim);
            return;
        }
        canvas.Save();
        canvas.ClipRect(Bounds);
        foreach (var (kind, heading) in new[] { ("main", "Main story"), ("side", "Side stories") })
        {
            var entries = world.Journal.Where(j => j.Kind == kind).Reverse().ToList();
            if (entries.Count == 0)
                continue;
            y += 30;
            canvas.DrawText(heading.ToUpperInvariant(), x, y, SKTextAlign.Left, textFont, dim);
            foreach (var entry in entries)
            {
                y += 26;
                canvas.DrawText(entry.Title, x, y, SKTextAlign.Left, headFont, ink);
                foreach (var line in Wrap(entry.Text, textFont, Width - 2 * Pad))
                {
                    y += LineHeight;
                    canvas.DrawText(line, x, y, SKTextAlign.Left, textFont, dim);
                }
            }
        }
        canvas.Restore();
    }

    private static IEnumerable<string> Wrap(string text, SKFont font, float width)
    {
        var line = "";
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = line.Length == 0 ? word : $"{line} {word}";
            if (font.MeasureText(candidate) > width && line.Length > 0)
            {
                yield return line;
                line = word;
            }
            else
            {
                line = candidate;
            }
        }
        if (line.Length > 0)
            yield return line;
    }
}
