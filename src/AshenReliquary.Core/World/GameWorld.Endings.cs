using AshenReliquary.Core.Content;

namespace AshenReliquary.Core.World;

/// <summary>
/// The choice at the Throne (M5 step 4): with Malachar fallen or spared, the Throne's dialogue offers the endings, each a
/// dialogue effect <c>ending</c>. Destroy the Ember Heart (Ashes to Ashes), Bind Ashur inside yourself (the Lantern
/// Rekindled: open only with the Chronicle of Saint Oriel and three of the road's side stories seen through), Claim the
/// Throne (the New Prophet), or, with Malachar spared, give him the Heart to carry into the fire (the Lantern-Bird). The
/// ending is kept in <see cref="EpilogueDefinition.EndingFlag"/>; the window then shows the epilogue and the credits.
/// </summary>
public sealed partial class GameWorld
{
    /// <summary>
    /// The twelve side stories along the road, by the flag that settles each and the values that count as seen through
    /// (any resolution counts: an egg carried off and never hatched or smashed does not).
    /// </summary>
    public static readonly IReadOnlyList<(string Flag, string[] Settled)> SideStories =
    [
        ("act1.sallow_relic", ["cured", "destroyed"]),
        ("act1.scarecrow", ["freed", "taken"]),
        ("act1.ferry", ["brothers", "lantern"]),
        ("act2.hadric_relic", ["returned", "broken"]),
        ("act2.confessor", ["repaired", "scrapped"]),
        ("act2.faction", ["guard", "outlaws", "none"]),
        ("act3.candles", ["rekindled", "snuffed"]),
        ("act3.penitent", ["forgiven", "banished"]),
        ("act3.library", ["complete"]),
        ("act4.egg", ["hatched", "smashed"]),
        ("act4.beacon", ["lit", "dark"]),
        ("daughter.toy", ["shown"]),
    ];

    /// <summary>How many side stories Bind needs seen through, besides the Chronicle.</summary>
    public const int BindStories = 3;

    /// <summary>The flag that opens Bind at the Throne (set while its conditions hold).</summary>
    public const string BindFlag = "act5.bind";

    /// <summary>How many of the twelve side stories the hero has seen through.</summary>
    public int SideStoriesSettled => SideStories.Count(s => Flag(s.Flag) is { } value && s.Settled.Contains(value));

    /// <summary>Whether Bind is open: the Chronicle read whole, and three side stories seen through.</summary>
    public bool BindOpen => Flag("act3.library") == "complete" && SideStoriesSettled >= BindStories;

    /// <summary>An ending chosen and not yet shown (the window shows the epilogue once the dialogue closes), or null.</summary>
    public string? PendingEnding { get; private set; }

    /// <summary>The ending this hero has reached, if any (kept in the save as a flag).</summary>
    public string? Ending => Flag(EpilogueDefinition.EndingFlag);

    /// <summary>Taken by the window as it shows the epilogue.</summary>
    public string? TakePendingEnding()
    {
        var ending = PendingEnding;
        PendingEnding = null;
        return ending;
    }

    /// <summary>Before a dialogue opens: the flags its choices hang on that are worked out, not set (Bind's).</summary>
    private void RefreshEndingFlags() => SetFlag(BindFlag, BindOpen ? "open" : "");

    /// <summary>An <c>ending</c> effect: the ending is reached (it may be chosen again, from a save at the Throne).</summary>
    private void ReachEnding(string ending)
    {
        SetFlag(EpilogueDefinition.EndingFlag, ending);
        PendingEnding = ending;
        if (ContentDatabase.Epilogue.Endings.GetValueOrDefault(ending) is { } end)
            Arrival = new Banner(end.Title, "The end.", Time);
    }

    /// <summary>
    /// Sets the world up as an epilogue slide's scene: the story's <paramref name="flags"/> (so the people there are the
    /// ones who stayed), no monsters, nothing on the ground, the hero (unseen: the window draws scenes without it) standing a
    /// little in front of <paramref name="near"/> so the camera frames it.
    /// </summary>
    public void StageScene(IReadOnlyDictionary<string, string> flags, string near)
    {
        RestoreStory(flags, []);
        foreach (var m in _monsters)
            m.State = MonsterState.Idle;
        _monsters.Clear();
        _ground.Clear();
        _allies.Clear();
        _hazards.Clear();
        var (x, y) = SceneSpot(near);
        (Hero.X, Hero.Y) = (x, y);
        Hero.Path.Clear();
        Hero.Goal = null;
    }

    /// <summary>Where a scene's camera stands: the NPC, object or vendor <paramref name="near"/>, else the zone's start.</summary>
    private (double X, double Y) SceneSpot(string near)
    {
        if (_interactives.FirstOrDefault(i => i.Id == near) is { } who)
            return (who.X, who.Y + 1.5);
        if (Zone.Interactables?.FirstOrDefault(i => i.Id == near) is { } thing)
            return (thing.At[0] + 0.5, thing.At[1] + 2);
        if (Zone.Npcs?.FirstOrDefault(n => n.Id == near) is { } npc)
            return (npc.At[0] + 0.5, npc.At[1] + 2);
        if (Zone.Vendors?.FirstOrDefault(v => v.Id == near) is { } vendor)
            return (vendor.At[0] + 0.5, vendor.At[1] + 2);
        return (Zone.PlayerStart[0] + 0.5, Zone.PlayerStart[1] + 0.5);
    }
}
