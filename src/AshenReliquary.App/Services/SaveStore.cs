using AshenReliquary.Core.Saving;

namespace AshenReliquary.App.Services;

/// <summary>
/// The save folder: one JSON file per character. On Linux it is ~/.local/share/AshenReliquary/saves; ASHEN_SAVE_DIR
/// overrides it (tests use a temporary folder). Writes go to a temporary file first and are then swapped in, so a
/// crash mid-save never leaves a half-written save.
/// </summary>
public static class SaveStore
{
    /// <summary>Tests point this at a temporary folder so they never touch the player's real saves.</summary>
    public static string? FolderOverride { get; set; }

    public static string Folder =>
        FolderOverride ?? (Environment.GetEnvironmentVariable("ASHEN_SAVE_DIR") is { Length: > 0 } dir
            ? dir
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AshenReliquary", "saves"));

    public static string PathFor(string saveId) => Path.Combine(Folder, saveId + ".json");

    public static void Write(SaveGame save)
    {
        Directory.CreateDirectory(Folder);
        var path = PathFor(save.SaveId);
        var temp = path + ".tmp";
        File.WriteAllText(temp, save.ToJson());
        File.Move(temp, path, overwrite: true);
    }

    /// <summary>Every readable save, newest first. Unreadable files are skipped, never deleted.</summary>
    public static IReadOnlyList<SaveGame> List()
    {
        if (!Directory.Exists(Folder))
            return [];
        var saves = new List<SaveGame>();
        foreach (var file in Directory.EnumerateFiles(Folder, "*.json"))
        {
            try
            {
                saves.Add(SaveGame.FromJson(File.ReadAllText(file)));
            }
            catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException)
            {
                // A damaged or future-version file: leave it alone.
            }
        }
        return saves.OrderByDescending(s => s.SavedAt).ToList();
    }

    public static void Delete(string saveId)
    {
        var path = PathFor(saveId);
        if (File.Exists(path))
            File.Delete(path);
    }

    /// <summary>
    /// The number for the next copy of save <paramref name="original"/>: one more than the highest its copies have, or 2
    /// for the first (the character's own save counts as the first).
    /// </summary>
    public static int NextCopyNumber(string original) =>
        List().Where(s => s.CopyOf == original).Select(s => s.CopyNumber ?? 1).DefaultIfEmpty(1).Max() + 1;

    /// <summary>The file name for copy <paramref name="number"/> of save <paramref name="original"/>: "elirk-499135-2".</summary>
    public static string CopyId(string original, int number) => $"{original}-{number}";

    /// <summary>A file name for a new character: its name made safe, plus a short unique suffix.</summary>
    public static string NewSaveId(string name)
    {
        var slug = new string(name.ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray()).Trim('-');
        if (slug.Length == 0)
            slug = "hero";
        return $"{slug[..Math.Min(slug.Length, 24)]}-{Guid.NewGuid().ToString("N")[..6]}";
    }
}
