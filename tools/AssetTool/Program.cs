using AshenReliquary.Core.Content.Flare;
using SkiaSharp;

// Usage:
//   AssetTool tiles <flare-root> <tilesetdef> <out.png> [first-id] [last-id]
//       A numbered contact sheet of a tileset, for picking tile ids when authoring maps.
//   AssetTool sprite <flare-root> <animation.txt> <animation> <out.png>
//       Every frame of one animation in all 8 directions, with each frame's anchor marked.
if (args.Length < 3)
{
    Console.Error.WriteLine("usage: AssetTool tiles <flare-root> <tilesetdef> <out.png> [first] [last] | sprite <flare-root> <anim.txt> <name> <out.png>");
    return 2;
}

var root = args[1];
using var font = new SKFont(SKTypeface.Default, 18);
using var ink = new SKPaint { Color = SKColors.Yellow, IsAntialias = true };
using var outline = new SKPaint { Color = SKColors.Black, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 4 };
using var cross = new SKPaint { Color = SKColors.Red, StrokeWidth = 2 };

switch (args[0])
{
    case "portrait":
    {
        // AssetTool portrait <in.png> <out.png> [--size 320] [--top 0.04] [--zoom 1.0] [--auto]
        // Makes a square face portrait in the game's palette:
        //   default: a square from the top of the painting (from --top, a fraction of the height);
        //   --zoom >1 widens the square past the painting's edges (a head taller than the canvas is wide), filling
        //             the overflow with a blurred, enlarged copy of the painting;
        //   --auto    finds the painted area itself, after turning a white page (flood-filled from the edges) and any
        //             transparency into the game's dark backdrop.
        // Then the Act 1 grade (30% desaturated, warmed) and a dark vignette are baked in.
        var size = Option("--size", 320);
        var top = Option2("--top", 0.04f);
        var zoom = Option2("--zoom", 1.0f);
        var auto = args.Contains("--auto");
        using var original = Load(args[1]);
        using var source = auto ? StripPage(original) : original.Copy();

        SKRect region;
        if (auto)
        {
            var box = ContentBounds(source);
            var side = Math.Max(box.Width, box.Height) * 1.06f * zoom;
            region = SKRect.Create(box.MidX - side / 2, box.Top - side * 0.03f, side, side);
        }
        else
        {
            var side = Math.Min(source.Width, source.Height) * zoom;
            var y = Math.Clamp(source.Height * top, 0, Math.Max(0, source.Height - side));
            region = SKRect.Create((source.Width - side) / 2, y, side, side);
        }

        using var output = new SKBitmap(size, size);
        using (var canvas = new SKCanvas(output))
        {
            var scale = size / region.Width;
            // Backdrop: the dark gradient of the creation screen, then (for paintings) a blurred cover of the painting.
            using (var backdrop = new SKPaint
            {
                Shader = SKShader.CreateRadialGradient(new SKPoint(size / 2f, size * 0.4f), size * 0.8f,
                    [new SKColor(52, 38, 28), new SKColor(12, 10, 9)], SKShaderTileMode.Clamp),
            })
                canvas.DrawRect(SKRect.Create(size, size), backdrop);
            using var image = SKImage.FromBitmap(source);
            var placed = SKRect.Create((0 - region.Left) * scale, (0 - region.Top) * scale, source.Width * scale, source.Height * scale);
            if (!auto && !SKRect.Create(source.Width, source.Height).Contains(region))
            {
                using var blur = new SKPaint { ImageFilter = SKImageFilter.CreateBlur(size / 18f, size / 18f) };
                var cover = Math.Max((float)size / source.Width, (float)size / source.Height) * 1.15f;
                canvas.DrawImage(image, SKRect.Create((size - source.Width * cover) / 2, (size - source.Height * cover) / 2, source.Width * cover, source.Height * cover), new SKSamplingOptions(SKFilterMode.Linear), blur);
            }
            canvas.DrawImage(image, placed, new SKSamplingOptions(SKCubicResampler.Mitchell));
        }
        using var graded = Grade(output, size);
        Save(graded, args[2]);
        Console.WriteLine($"{Path.GetFileName(args[1])} -> {args[2]}");
        break;
    }
    case "icon":
    {
        // AssetTool icon <in.svg> <out.png> [--size 96]: a game-icons.net SVG is a black 512x512 square plus white
        // path(s). The white paths are parsed with Skia's own SVG path reader and filled with one ivory tint on a
        // transparent background, which the game can recolour. No SVG library needed.
        var size = Option("--size", 96);
        var svg = File.ReadAllText(args[1]);
        var box = System.Text.RegularExpressions.Regex.Match(svg, "viewBox=\"([\\d. -]+)\"");
        var view = box.Success ? box.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(float.Parse).ToArray() : [0f, 0, 512, 512];
        var paths = System.Text.RegularExpressions.Regex.Matches(svg, "<path[^>]*fill=\"#fff(?:fff)?\"[^>]*d=\"([^\"]+)\"|<path[^>]*d=\"([^\"]+)\"[^>]*fill=\"#fff(?:fff)?\"")
            .Select(m => m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value).ToList();
        if (paths.Count == 0)
            throw new InvalidDataException($"{args[1]} has no white path to draw.");
        using var icon = new SKBitmap(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(icon))
        using (var glyph = new SKPaint { IsAntialias = true, Color = new SKColor(236, 224, 200) })
        {
            canvas.Clear(SKColors.Transparent);
            canvas.Scale(size / view[2], size / view[3]);
            canvas.Translate(-view[0], -view[1]);
            foreach (var d in paths)
            {
                using var path = SKPath.ParseSvgPathData(d) ?? throw new InvalidDataException($"Bad path data in {args[1]}.");
                canvas.DrawPath(path, glyph);
            }
        }
        Save(icon, args[2]);
        Console.WriteLine($"{Path.GetFileName(args[1])} -> {args[2]}");
        break;
    }
    case "grid":
    {
        // AssetTool grid <folder> <out.png> [cell]: every PNG in a folder, labelled by file name.
        var files = Directory.GetFiles(args[1], "*.png").Order().ToList();
        var cell = args.Length > 3 ? int.Parse(args[3]) : 200;
        const int columns = 8;
        using var sheet = new SKBitmap(columns * cell, (files.Count + columns - 1) / columns * (cell + 22));
        using var canvas = new SKCanvas(sheet);
        canvas.Clear(new SKColor(40, 40, 48));
        for (var i = 0; i < files.Count; i++)
        {
            using var image = Load(files[i]);
            var scale = Math.Min((float)cell / image.Width, (float)cell / image.Height);
            float x = i % columns * cell, y = i / columns * (cell + 22) + 22;
            canvas.DrawBitmap(image, SKRect.Create(x, y, image.Width * scale, image.Height * scale));
            canvas.DrawText(Path.GetFileNameWithoutExtension(files[i]), x + 4, y - 5, SKTextAlign.Left, font, ink);
        }
        Save(sheet, args[2]);
        Console.WriteLine($"{files.Count} images -> {args[2]}");
        break;
    }
    case "tiles":
    {
        var tileset = FlareTileset.Parse(File.ReadAllText(Path.Combine(root, args[2])));
        var first = args.Length > 4 ? int.Parse(args[4]) : int.MinValue;
        var last = args.Length > 5 ? int.Parse(args[5]) : int.MaxValue;
        var tiles = tileset.Tiles.Values.Where(t => t.Id >= first && t.Id <= last).OrderBy(t => t.Id).ToList();
        var images = tileset.Images.ToDictionary(i => i, i => Load(Path.Combine(root, i)));
        const int cell = 220, columns = 10;
        var cellHeight = Math.Max(160, tiles.Max(t => t.Height) + 30);
        using var sheet = new SKBitmap(cell * columns, cellHeight * ((tiles.Count + columns - 1) / columns));
        using var canvas = new SKCanvas(sheet);
        canvas.Clear(new SKColor(40, 40, 48));
        for (var i = 0; i < tiles.Count; i++)
        {
            var t = tiles[i];
            var x = i % columns * cell + (cell - t.Width) / 2f;
            var y = i / columns * cellHeight + 24f;
            canvas.DrawBitmap(images[t.Image], new SKRect(t.X, t.Y, t.X + t.Width, t.Y + t.Height), new SKRect(x, y, x + t.Width, y + t.Height));
            var label = $"{t.Id}";
            canvas.DrawText(label, i % columns * cell + 6, i / columns * cellHeight + 20, font, outline);
            canvas.DrawText(label, i % columns * cell + 6, i / columns * cellHeight + 20, font, ink);
        }
        Save(sheet, args[3]);
        Console.WriteLine($"{tiles.Count} tiles -> {args[3]}");
        break;
    }
    case "sprite":
    {
        var set = FlareAnimationSet.Parse(File.ReadAllText(Path.Combine(root, args[2])));
        var anim = set[args[3]] ?? throw new ArgumentException($"No animation '{args[3]}'. Have: {string.Join(", ", set.Animations.Keys)}");
        using var image = Load(Path.Combine(root, set.Image));
        const int cell = 220;
        using var sheet = new SKBitmap(cell * anim.FrameCount, cell * 8);
        using var canvas = new SKCanvas(sheet);
        canvas.Clear(new SKColor(60, 60, 70));
        for (var d = 0; d < 8; d++)
        for (var f = 0; f < anim.FrameCount; f++)
        {
            var fr = anim.Frame(f, d);
            float ax = f * cell + cell / 2f, ay = d * cell + cell * 0.8f;
            canvas.DrawBitmap(image, new SKRect(fr.X, fr.Y, fr.X + fr.Width, fr.Y + fr.Height),
                new SKRect(ax - fr.OffsetX, ay - fr.OffsetY, ax - fr.OffsetX + fr.Width, ay - fr.OffsetY + fr.Height));
            canvas.DrawLine(ax - 6, ay, ax + 6, ay, cross);
            canvas.DrawLine(ax, ay - 6, ax, ay + 6, cross);
            if (f == 0)
                canvas.DrawText($"dir {d}", 6, d * cell + 20, font, ink);
        }
        Save(sheet, args[4]);
        Console.WriteLine($"{anim.Name}: {anim.FrameCount} frames x 8 directions -> {args[4]}");
        break;
    }
    default:
        Console.Error.WriteLine($"Unknown command '{args[0]}'.");
        return 2;
}
return 0;

int Option(string name, int fallback) => Array.IndexOf(args, name) is var i and >= 0 ? int.Parse(args[i + 1]) : fallback;
float Option2(string name, float fallback) => Array.IndexOf(args, name) is var i and >= 0
    ? float.Parse(args[i + 1], System.Globalization.CultureInfo.InvariantCulture) : fallback;

static SKBitmap Grade(SKBitmap source, int size)
{
    var result = new SKBitmap(size, size);
    using var canvas = new SKCanvas(result);
    const float s = 0.72f;
    float rw = 0.2126f, gw = 0.7152f, bw = 0.0722f;
    float[] matrix =
    [
        (rw * (1 - s) + s) * 0.98f, gw * (1 - s) * 0.98f, bw * (1 - s) * 0.98f, 0, 0,
        rw * (1 - s) * 0.93f, (gw * (1 - s) + s) * 0.93f, bw * (1 - s) * 0.93f, 0, 0,
        rw * (1 - s) * 0.84f, gw * (1 - s) * 0.84f, (bw * (1 - s) + s) * 0.84f, 0, 0,
        0, 0, 0, 1, 0,
    ];
    using var grade = new SKPaint { ColorFilter = SKColorFilter.CreateColorMatrix(matrix) };
    canvas.DrawBitmap(source, 0, 0, grade);
    using var vignette = new SKPaint
    {
        Shader = SKShader.CreateRadialGradient(new SKPoint(size / 2f, size * 0.45f), size * 0.75f,
            [SKColors.Transparent, SKColors.Transparent, new SKColor(0, 0, 0, 190)], [0f, 0.55f, 1f], SKShaderTileMode.Clamp),
    };
    canvas.DrawRect(SKRect.Create(size, size), vignette);
    return result;
}

/// <summary>Turns a near-white page around a painting (flood-filled from the edges) into transparency.</summary>
static SKBitmap StripPage(SKBitmap source)
{
    var result = source.Copy(SKColorType.Rgba8888);
    int w = result.Width, h = result.Height;
    var seen = new bool[w * h];
    var queue = new Queue<(int X, int Y)>();
    void Seed(int x, int y)
    {
        var c = result.GetPixel(x, y);
        if (!seen[y * w + x] && (c.Alpha < 40 || (c.Red > 232 && c.Green > 232 && c.Blue > 232)))
        {
            seen[y * w + x] = true;
            queue.Enqueue((x, y));
        }
    }
    for (var x = 0; x < w; x++) { Seed(x, 0); Seed(x, h - 1); }
    for (var y = 0; y < h; y++) { Seed(0, y); Seed(w - 1, y); }
    while (queue.Count > 0)
    {
        var (x, y) = queue.Dequeue();
        result.SetPixel(x, y, SKColors.Transparent);
        if (x > 0) Seed(x - 1, y);
        if (x < w - 1) Seed(x + 1, y);
        if (y > 0) Seed(x, y - 1);
        if (y < h - 1) Seed(x, y + 1);
    }

    // Many portraits sit on a flat grey or beige panel inside the page. Find the colour that most often borders the
    // hole just made; if it is a flat, unsaturated colour, flood that panel away too (textured paint like a grey
    // beard varies too much to be taken).
    var border = new Dictionary<int, int>();
    for (var y = 1; y < h - 1; y++)
    for (var x = 1; x < w - 1; x++)
    {
        var c = result.GetPixel(x, y);
        if (c.Alpha < 40)
            continue;
        if (result.GetPixel(x - 1, y).Alpha >= 40 && result.GetPixel(x + 1, y).Alpha >= 40
            && result.GetPixel(x, y - 1).Alpha >= 40 && result.GetPixel(x, y + 1).Alpha >= 40)
            continue;
        var key = (c.Red / 8 << 16) | (c.Green / 8 << 8) | c.Blue / 8;
        border[key] = border.GetValueOrDefault(key) + 1;
    }
    if (border.Count > 0)
    {
        var (mode, count) = border.MaxBy(kv => kv.Value);
        var panel = new SKColor((byte)((mode >> 16) * 8 + 4), (byte)(((mode >> 8) & 255) * 8 + 4), (byte)((mode & 255) * 8 + 4));
        var saturation = Math.Max(panel.Red, Math.Max(panel.Green, panel.Blue)) - Math.Min(panel.Red, Math.Min(panel.Green, panel.Blue));
        if (count > (w + h) / 2 && saturation < 40)
        {
            bool Near(SKColor c) => c.Alpha >= 40 && Math.Abs(c.Red - panel.Red) < 16 && Math.Abs(c.Green - panel.Green) < 16 && Math.Abs(c.Blue - panel.Blue) < 16;
            for (var y = 1; y < h - 1; y++)
            for (var x = 1; x < w - 1; x++)
            {
                if (result.GetPixel(x, y).Alpha < 40 && !seen[y * w + x])
                    continue;
                foreach (var (nx, ny) in new[] { (x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1) })
                {
                    if (result.GetPixel(x, y).Alpha < 40 && Near(result.GetPixel(nx, ny)) && !seen[ny * w + nx])
                    {
                        seen[ny * w + nx] = true;
                        queue.Enqueue((nx, ny));
                    }
                }
            }
            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();
                result.SetPixel(x, y, SKColors.Transparent);
                foreach (var (nx, ny) in new[] { (x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1) })
                {
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h || seen[ny * w + nx] || !Near(result.GetPixel(nx, ny)))
                        continue;
                    seen[ny * w + nx] = true;
                    queue.Enqueue((nx, ny));
                }
            }
        }
    }
    return result;
}

/// <summary>The bounding box of everything that isn't transparent.</summary>
static SKRect ContentBounds(SKBitmap bitmap)
{
    int left = bitmap.Width, top = bitmap.Height, right = 0, bottom = 0;
    for (var y = 0; y < bitmap.Height; y++)
    for (var x = 0; x < bitmap.Width; x++)
    {
        if (bitmap.GetPixel(x, y).Alpha < 40)
            continue;
        left = Math.Min(left, x); right = Math.Max(right, x);
        top = Math.Min(top, y); bottom = Math.Max(bottom, y);
    }
    return right < left ? SKRect.Create(bitmap.Width, bitmap.Height) : new SKRect(left, top, right + 1, bottom + 1);
}

// Decode returns null rather than throwing for a missing or unreadable file; exit cleanly instead of crashing on it.
static SKBitmap Load(string path)
{
    if (SKBitmap.Decode(path) is { } bitmap)
        return bitmap;
    Console.Error.WriteLine($"Cannot read image '{path}'.");
    Environment.Exit(1);
    return null!;
}

static void Save(SKBitmap bitmap, string path)
{
    using var data = bitmap.Encode(SKEncodedImageFormat.Png, 90);
    File.WriteAllBytes(path, data.ToArray());
}
