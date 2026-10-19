using System.Text.Json;

namespace AshenReliquary.App.Services;

/// <summary>Player settings (volumes, the loot filter), kept as settings.json beside the saves folder.</summary>
public sealed record Settings
{
    public float Master { get; init; } = 0.8f;
    public float Effects { get; init; } = 0.9f;
    public float Music { get; init; } = 0.55f;
    public float Ambience { get; init; } = 0.45f;

    /// <summary>The loot filter: common gear on the ground shows no label (Alt shows them all).</summary>
    public bool HideCommonLoot { get; init; }

    private static string PathFor => Path.Combine(Path.GetDirectoryName(SaveStore.Folder.TrimEnd(Path.DirectorySeparatorChar))!, "settings.json");

    public static Settings Load()
    {
        try
        {
            return File.Exists(PathFor) ? JsonSerializer.Deserialize<Settings>(File.ReadAllText(PathFor)) ?? new Settings() : new Settings();
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            return new Settings();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PathFor)!);
            File.WriteAllText(PathFor, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Trace.WriteLine($"Could not save settings: {e.Message}");
        }
    }
}
