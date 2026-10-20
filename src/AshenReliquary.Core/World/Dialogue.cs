namespace AshenReliquary.Core.World;

/// <summary>
/// A conversation (or an object's narration), read from <c>dialogues.json</c>. It opens at the first of
/// <see cref="Openings"/> whose requirements hold, else at <see cref="Start"/>, and moves from node to node as choices
/// are picked; a choice with no <see cref="DialogueChoice.To"/> ends it.
/// </summary>
public sealed record DialogueDefinition(string Id, string Start, IReadOnlyDictionary<string, DialogueNode> Nodes,
    IReadOnlyList<DialogueOpening>? Openings = null);

/// <summary>Opens the dialogue at <see cref="Node"/> when <see cref="Requires"/> holds (a later visit, say).</summary>
public sealed record DialogueOpening(string Node, IReadOnlyDictionary<string, string> Requires);

/// <summary>
/// One beat: the text shown, what happens on reaching it, and the answers. A node with no choices offers a plain
/// "Leave".
/// </summary>
public sealed record DialogueNode(string Text, IReadOnlyList<DialogueChoice>? Choices = null, IReadOnlyList<DialogueEffect>? Effects = null);

/// <summary>
/// An answer. It shows only while <see cref="Requires"/> holds (flag name to value; "" for "not set"). With a
/// <see cref="Check"/> it rolls before moving on; otherwise it goes to <see cref="To"/>, or ends the dialogue.
/// </summary>
public sealed record DialogueChoice(string Text, string? To = null, IReadOnlyDictionary<string, string>? Requires = null,
    SkillCheckDefinition? Check = null, IReadOnlyList<DialogueEffect>? Effects = null);

/// <summary>
/// A check against a DC: <see cref="Skill"/> is a skill ("Diplomacy", "Warfare Lore"), "Perception", or a save
/// ("Fortitude", "Reflex", "Will"). Success or better goes to <see cref="Success"/>; failure or worse to <see cref="Failure"/>.
/// </summary>
public sealed record SkillCheckDefinition(string Skill, int Dc, string Success, string Failure);

/// <summary>
/// Something a node or choice does: set a world flag, write or update a journal entry, or give XP, gold or an item
/// (an item base id, common, at the hero's level). <see cref="PayPerLevel"/> charges that much gold per hero level
/// first (an answer the hero can't afford does nothing; "{cost}" in its text shows the price); <see cref="Respec"/>
/// lets every level from 2 be chosen again. <see cref="Dust"/> and <see cref="Fragments"/> give crafting materials (a
/// machine scrapped for parts).
/// </summary>
public sealed record DialogueEffect(string? Flag = null, string? Value = null, JournalEntryDefinition? Journal = null,
    int Xp = 0, int Gold = 0, string? Item = null, int PayPerLevel = 0, bool Respec = false, string? Boon = null, string? TakeItem = null,
    int Dust = 0, int Fragments = 0);

/// <summary>A journal entry: <see cref="Kind"/> is "main" or "side". Writing the same id again replaces the text.</summary>
public sealed record JournalEntryDefinition(string Id, string Title, string Text, string Kind = "side");

/// <summary>A journal entry as written, and when.</summary>
public sealed record JournalEntry(string Id, string Title, string Text, string Kind, double At);

/// <summary>
/// Someone to talk to, placed in a zone: drawn like a trader, from Flare's avatar layers. A <see cref="Still"/> one
/// doesn't idle (a scarecrow on its pole).
/// </summary>
public sealed record NpcDefinition(string Id, string Name, string Title, int[] At, IReadOnlyDictionary<string, string> Look,
    string Dialogue, int Facing = 6, string? Portrait = null, bool Still = false, IReadOnlyDictionary<string, string>? Requires = null);

/// <summary>
/// Something to look at or use, placed in a zone: a well, a letter, a notice board. <see cref="Tile"/> is a tileset
/// object to draw there (null when the map already shows it). Clicking it starts its dialogue, as narration, or does
/// its <see cref="Action"/> instead ("stash" opens the stash).
/// </summary>
public sealed record InteractableDefinition(string Id, string Name, int[] At, string? Dialogue = null, int? Tile = null, bool Blocks = true,
    string? Action = null);

/// <summary>An NPC or object in play: where it stands and what it says.</summary>
public sealed class Interactive
{
    internal Interactive(string id, string name, string? title, int[] at, string? dialogue, NpcDefinition? npc, InteractableDefinition? thing)
    {
        Id = id;
        Name = name;
        Title = title;
        X = at[0] + 0.5;
        Y = at[1] + 0.5;
        Dialogue = dialogue;
        Npc = npc;
        Thing = thing;
    }

    public string Id { get; }
    public string Name { get; }
    public string? Title { get; }
    public double X { get; }
    public double Y { get; }
    public string? Dialogue { get; }

    /// <summary>Set for a person (drawn as a paper doll, with a portrait in conversation).</summary>
    public NpcDefinition? Npc { get; }

    /// <summary>Set for an object.</summary>
    public InteractableDefinition? Thing { get; }

    public double DistanceTo(double x, double y) => Math.Sqrt((X - x) * (X - x) + (Y - y) * (Y - y));
}

/// <summary>An answer as offered: its text, and for a check what it rolls, against what, and the odds of success.</summary>
public sealed record ChoiceView(string Text, string? CheckLabel = null, int? Modifier = null, int? Dc = null, double? Chance = null);

/// <summary>The conversation on screen: who speaks, what they say, the answers on offer and the last check's result.</summary>
public sealed record DialogueView(string Speaker, string? Portrait, string Text, IReadOnlyList<ChoiceView> Choices, string? LastRoll);
