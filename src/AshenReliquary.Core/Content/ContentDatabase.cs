using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using AshenReliquary.Core.Abilities;
using AshenReliquary.Core.Items;
using AshenReliquary.Core.Rules;
using AshenReliquary.Core.World;

namespace AshenReliquary.Core.Content;

/// <summary>The character-creation content: ancestries, backgrounds and classes, read from the embedded JSON files.</summary>
public sealed class ContentDatabase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly Lazy<ContentDatabase> EmbeddedInstance = new(LoadEmbedded);

    public ContentDatabase(
        IEnumerable<AncestryDefinition> ancestries,
        IEnumerable<BackgroundDefinition> backgrounds,
        IEnumerable<ClassDefinition> classes,
        IEnumerable<CreatureDefinition>? creatures = null,
        IEnumerable<ZoneDefinition>? zones = null,
        IEnumerable<AbilityDefinition>? abilities = null,
        IEnumerable<ItemDefinition>? items = null,
        IEnumerable<AffixDefinition>? affixes = null,
        IEnumerable<DialogueDefinition>? dialogues = null)
    {
        Dialogues = (dialogues ?? []).ToDictionary(d => d.Id);
        Items = (items ?? []).ToDictionary(i => i.Id);
        Affixes = (affixes ?? []).ToList();
        Abilities = (abilities ?? []).ToDictionary(a => a.Id);
        Ancestries = ancestries.ToDictionary(a => a.Id);
        Backgrounds = backgrounds.ToDictionary(b => b.Id);
        Classes = classes.ToDictionary(c => c.Id);
        Creatures = (creatures ?? []).ToDictionary(c => c.Id);
        Zones = (zones ?? []).ToDictionary(z => z.Id);
    }

    /// <summary>The content that ships with the game, loaded once.</summary>
    public static ContentDatabase Embedded => EmbeddedInstance.Value;

    public IReadOnlyDictionary<string, AncestryDefinition> Ancestries { get; }
    public IReadOnlyDictionary<string, BackgroundDefinition> Backgrounds { get; }
    public IReadOnlyDictionary<string, ClassDefinition> Classes { get; }
    public IReadOnlyDictionary<string, CreatureDefinition> Creatures { get; }
    public IReadOnlyDictionary<string, ZoneDefinition> Zones { get; }
    public IReadOnlyDictionary<string, AbilityDefinition> Abilities { get; }
    public IReadOnlyDictionary<string, ItemDefinition> Items { get; }
    public IReadOnlyList<AffixDefinition> Affixes { get; }
    public IReadOnlyDictionary<string, DialogueDefinition> Dialogues { get; }

    public ItemDefinition Item(string id) => Find(Items, id, "item");

    /// <summary>A loot roller over this content's items and affixes.</summary>
    public LootGenerator Loot => _loot ??= new LootGenerator(Items, Affixes);

    private LootGenerator? _loot;

    /// <summary>A class's starting ability bar: slot ("lmb", "rmb", "1".."6") to ability.</summary>
    public IReadOnlyDictionary<string, AbilityDefinition> AbilityBar(string classId) =>
        Abilities.Values.Where(a => a.Class == classId).ToDictionary(a => a.Slot);

    public AncestryDefinition Ancestry(string id) => Find(Ancestries, id, "ancestry");
    public BackgroundDefinition Background(string id) => Find(Backgrounds, id, "background");
    public ClassDefinition Class(string id) => Find(Classes, id, "class");
    public CreatureDefinition Creature(string id) => Find(Creatures, id, "creature");
    public ZoneDefinition Zone(string id) => Find(Zones, id, "zone");

    public static ContentDatabase LoadEmbedded() => new(
        Read<List<AncestryDefinition>>("ancestries.json"),
        Read<List<BackgroundDefinition>>("backgrounds.json"),
        Read<List<ClassDefinition>>("classes.json"),
        Read<List<CreatureDefinition>>("creatures.json"),
        ZoneFiles.Select(Read<ZoneDefinition>),
        Read<List<AbilityDefinition>>("abilities.json"),
        Read<List<ItemDefinition>>("items.json"),
        Read<List<AffixDefinition>>("affixes.json"),
        Read<List<DialogueDefinition>>("dialogues.json"));

    /// <summary>Every embedded zone file (zones/*.json in the source tree).</summary>
    private static IEnumerable<string> ZoneFiles => Assembly.GetExecutingAssembly().GetManifestResourceNames()
        .Where(n => n.StartsWith("AshenReliquary.Content.", StringComparison.Ordinal) && IsZone(n))
        .Select(n => n["AshenReliquary.Content.".Length..]);

    private static bool IsZone(string resourceName) =>
        resourceName.EndsWith(".json", StringComparison.Ordinal)
        && !KnownTables.Contains(resourceName["AshenReliquary.Content.".Length..]);

    private static readonly HashSet<string> KnownTables =
        ["ancestries.json", "backgrounds.json", "classes.json", "creatures.json", "abilities.json", "items.json", "affixes.json", "dialogues.json"];

    private static T Read<T>(string fileName)
    {
        var name = $"AshenReliquary.Content.{fileName}";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded content '{name}' is missing.");
        return JsonSerializer.Deserialize<T>(stream, JsonOptions)
            ?? throw new InvalidOperationException($"Embedded content '{name}' is empty.");
    }

    private static T Find<T>(IReadOnlyDictionary<string, T> table, string id, string kind) =>
        table.TryGetValue(id, out var value) ? value : throw new RulesException($"Unknown {kind} '{id}'.");
}
