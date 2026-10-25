using AshenReliquary.App.Rendering;
using AshenReliquary.Core.Characters;
using AshenReliquary.Core.Content;
using AshenReliquary.Core.Items;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;

namespace AshenReliquary.App.Services;

/// <summary>
/// The guide's pictures: the game-icons.net glyphs (abilities, conditions) as bitmaps, and each class drawn as a paper
/// doll in its starting kit from Flare's avatar layers, as the game draws its hero.
/// </summary>
public static class GuideArt
{
    private const string IconRoot = "avares://AVAAshenReliquary/Assets/Icons/";
    private static readonly Dictionary<string, Bitmap?> Icons = [];
    private static readonly Dictionary<string, Bitmap> Dolls = [];

    /// <summary>An icon by id (an ability id, a condition name), or null when there is none.</summary>
    public static Bitmap? Icon(string id)
    {
        if (Icons.TryGetValue(id, out var icon))
            return icon;
        var uri = new Uri(IconRoot + id + ".png");
        if (AssetLoader.Exists(uri))
        {
            using var stream = AssetLoader.Open(uri);
            icon = new Bitmap(stream);
        }
        Icons[id] = icon;
        return icon;
    }

    /// <summary>A class in its starting kit (with this subclass's weapons), standing and facing the viewer.</summary>
    public static Bitmap Doll(string classId, string? subclass)
    {
        var key = $"{classId}/{subclass}";
        if (Dolls.TryGetValue(key, out var doll))
            return doll;
        var content = ContentDatabase.Embedded;
        var choices = CharacterBuilder.Recommend(content, "Guide", "human", content.Backgrounds.Keys.First(), classId) with { Subclass = subclass };
        var hero = CharacterBuilder.Create(content, choices);
        var gear = GameSession.Dress(hero, new Equipment(hero), classId, subclass);
        Dolls[key] = doll = Render(GameSession.LayersFor(gear));
        return doll;
    }

    /// <summary>Draws the paper doll's layers (Flare's stance, first frame, facing south-east) into a bitmap.</summary>
    private static Bitmap Render(IReadOnlyDictionary<string, string> look)
    {
        const int width = 180, height = 210, direction = 5;
        var assets = FlareAssets.Shared;
        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var foot = new SKPoint(width / 2f, height - 22);
        using (var shadow = new SKPaint { IsAntialias = true, Color = new SKColor(0, 0, 0, 110) })
            canvas.DrawOval(SKRect.Create(foot.X - 34, foot.Y - 9, 68, 18), shadow);
        using var paint = new SKPaint { IsAntialias = true };
        foreach (var slot in assets.LayerOrder(direction))
        {
            if (!look.TryGetValue(slot, out var layer))
                continue;
            var set = assets.AvatarLayer(layer);
            if ((set["stance"] ?? set["run"]) is not { } anim)
                continue;
            var frame = anim.Frame(0, direction);
            if (frame.Width == 0)
                continue;
            var sheet = assets.Image(anim.Image ?? set.Image);
            canvas.DrawImage(sheet, SKRect.Create(frame.X, frame.Y, frame.Width, frame.Height),
                SKRect.Create(foot.X - frame.OffsetX, foot.Y - frame.OffsetY, frame.Width, frame.Height), paint);
        }
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = data.AsStream();
        return new Bitmap(stream);
    }
}
