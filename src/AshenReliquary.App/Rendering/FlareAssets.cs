using Avalonia.Platform;
using AshenReliquary.Core.Content.Flare;
using SkiaSharp;

namespace AshenReliquary.App.Rendering;

/// <summary>
/// Loads the Flare art and its definition files from the app's embedded resources, decoding each image
/// once. Paths are relative to Assets/Flare, exactly as Flare's own files write them.
/// </summary>
public sealed class FlareAssets
{
    private const string Root = "avares://AVAAshenReliquary/Assets/Flare/";

    /// <summary>Flare's hero_layers.txt names directions by map compass; this is the sprite index of each name.</summary>
    private static readonly Dictionary<string, int> CompassToSprite = new()
    {
        ["SW"] = 0, ["W"] = 1, ["NW"] = 2, ["N"] = 3, ["NE"] = 4, ["E"] = 5, ["SE"] = 6, ["S"] = 7,
    };

    private static readonly Lazy<FlareAssets> SharedInstance = new(() => new FlareAssets());

    private readonly Dictionary<string, SKImage> _images = [];
    private readonly Dictionary<string, FlareAnimationSet> _animations = [];
    private readonly Dictionary<string, FlareTileset> _tilesets = [];
    private readonly string[][] _layerOrder = new string[8][];

    private FlareAssets()
    {
        foreach (var line in ReadText("hero_layers.txt").Split('\n'))
        {
            var text = line.Trim();
            if (!text.StartsWith("layer=", StringComparison.Ordinal))
                continue;
            var parts = text["layer=".Length..].Split(',');
            if (CompassToSprite.TryGetValue(parts[0], out var direction))
                _layerOrder[direction] = parts[1..];
        }
    }

    public static FlareAssets Shared => SharedInstance.Value;

    /// <summary>The order the hero's paper-doll slots are drawn in when facing <paramref name="direction"/>.</summary>
    public IReadOnlyList<string> LayerOrder(int direction) => _layerOrder[direction] ?? ["main", "feet", "legs", "hands", "chest", "off", "head"];

    public string ReadText(string path)
    {
        using var stream = AssetLoader.Open(new Uri(Root + path));
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public SKImage Image(string path)
    {
        if (_images.TryGetValue(path, out var image))
            return image;
        using var stream = AssetLoader.Open(new Uri(Root + path));
        using var data = SKData.Create(stream);
        // FromEncodedData alone is lazy: Skia would re-decode the whole sprite sheet whenever its cache drops it.
        // Decode once into a plain raster image instead.
        using var encoded = SKImage.FromEncodedData(data) ?? throw new InvalidDataException($"Could not decode {path}.");
        image = encoded.ToRasterImage(ensurePixelData: true);
        _images[path] = image;
        return image;
    }

    public FlareTileset Tileset(string name)
    {
        if (!_tilesets.TryGetValue(name, out var set))
            _tilesets[name] = set = FlareTileset.Parse(ReadText($"tilesetdefs/{name}.txt"));
        return set;
    }

    public FlareAnimationSet Enemy(string sprite) => Animation($"animations/enemies/{sprite}.txt");

    public FlareAnimationSet AvatarLayer(string layer) => Animation($"animations/avatar/male/{layer}.txt");

    /// <summary>A dropped item's sprite: Flare plays its "power" animation once (tumbling to the ground) and holds the last frame.</summary>
    public FlareAnimationSet Loot(string loot) => Animation($"animations/loot/{loot}.txt");

    public const string IconSheet = "images/icons/icons.png";
    public const int IconSize = 64;
    public const int IconsPerRow = 8;

    private FlareAnimationSet Animation(string path)
    {
        if (!_animations.TryGetValue(path, out var set))
            _animations[path] = set = FlareAnimationSet.Parse(ReadText(path));
        return set;
    }
}
