using System.Text.Json;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace AshenReliquary.App.Services;

/// <summary>One character portrait: which ancestry it shows, its file under Assets/Portraits, and its artist.</summary>
public sealed record Portrait(string Id, string Ancestry, string File, string Artist, string Licence);

/// <summary>
/// The portrait catalogue (Assets/Portraits/portraits.json): Flare's and GearHead's portraits, pre-graded to the
/// game's palette by tools/AssetTool. Bitmaps load on first use.
/// </summary>
public static class Portraits
{
    private const string Root = "avares://AVAAshenReliquary/Assets/Portraits/";

    private static readonly Lazy<IReadOnlyList<Portrait>> AllPortraits = new(() =>
    {
        using var stream = AssetLoader.Open(new Uri(Root + "portraits.json"));
        return JsonSerializer.Deserialize<List<Portrait>>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
    });

    private static readonly Dictionary<string, Bitmap> Bitmaps = [];

    public static IReadOnlyList<Portrait> All => AllPortraits.Value;

    public static IReadOnlyList<Portrait> For(string ancestryId) => All.Where(p => p.Ancestry == ancestryId).ToList();

    public static Portrait? Find(string? id) => id is null ? null : All.FirstOrDefault(p => p.Id == id);

    public static Bitmap Bitmap(Portrait portrait)
    {
        if (!Bitmaps.TryGetValue(portrait.Id, out var bitmap))
        {
            using var stream = AssetLoader.Open(new Uri(Root + portrait.File));
            Bitmaps[portrait.Id] = bitmap = new Bitmap(stream);
        }
        return bitmap;
    }

    /// <summary>The raw PNG bytes, for drawing the portrait with Skia in the game view.</summary>
    public static byte[] Png(Portrait portrait)
    {
        using var stream = AssetLoader.Open(new Uri(Root + portrait.File));
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}
