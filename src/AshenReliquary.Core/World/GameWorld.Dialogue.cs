using AshenReliquary.Core.Items;
using AshenReliquary.Core.Rules;

namespace AshenReliquary.Core.World;

/// <summary>World flags, the journal, the people and things to talk to, and the conversation in progress.</summary>
public sealed partial class GameWorld
{
    public const double TalkRadius = 1.8;

    private readonly Dictionary<string, string> _flags = [];
    private readonly List<JournalEntry> _journal = [];
    private readonly List<Interactive> _interactives = [];
    private IReadOnlyDictionary<string, DialogueDefinition> _dialogues = new Dictionary<string, DialogueDefinition>();

    /// <summary>The story's state: flag name to value (<c>act1.sallow_relic = cured</c>). Saved.</summary>
    public IReadOnlyDictionary<string, string> Flags => _flags;

    public string? Flag(string name) => _flags.GetValueOrDefault(name);

    /// <summary>Sets a flag; an empty value clears it.</summary>
    public void SetFlag(string name, string value)
    {
        if (value.Length == 0)
            _flags.Remove(name);
        else
            _flags[name] = value;
    }

    /// <summary>The journal, oldest first. Entries appear only once something is discovered.</summary>
    public IReadOnlyList<JournalEntry> Journal => _journal;

    /// <summary>The people and things in this zone that can be talked to or used.</summary>
    public IReadOnlyList<Interactive> Interactives => _interactives;

    /// <summary>The conversation on screen, or null.</summary>
    public DialogueView? Dialogue { get; private set; }

    private DialogueDefinition? _dialogue;
    private DialogueNode? _node;
    private Interactive? _speaker;
    private string? _lastRoll;

    private void PopulateInteractives(ZoneDefinition zone)
    {
        EndDialogue();
        CloseStash();
        _interactives.Clear();
        foreach (var npc in zone.Npcs ?? [])
            _interactives.Add(new Interactive(npc.Id, npc.Name, npc.Title, npc.At, npc.Dialogue, npc, null));
        foreach (var thing in zone.Interactables ?? [])
            _interactives.Add(new Interactive(thing.Id, thing.Name, null, thing.At, thing.Dialogue, null, thing));
    }

    /// <summary>Walk to someone or something (a click on them) and start its dialogue on arrival.</summary>
    public void Interact(Interactive who)
    {
        if (Hero.IsDead || !_interactives.Contains(who))
            return;
        Hero.Order = null;
        Hero.PickupTarget = null;
        Hero.TalkTarget = null;
        Hero.CacheTarget = null;
        Hero.ShrineTarget = null;
        if (who.DistanceTo(Hero.X, Hero.Y) <= TalkRadius)
        {
            StartDialogue(who);
            return;
        }
        Hero.InteractTarget = who;
        var (fx, fy) = InFrontOf(who.X, who.Y);
        Hero.Goal = (fx, fy);
        PlanPath(Hero, fx, fy);
    }

    /// <summary>Called each step: arriving next to the one asked for starts the talk; walking away ends it.</summary>
    private void UpdateInteraction()
    {
        if (Hero.InteractTarget is { } who && who.DistanceTo(Hero.X, Hero.Y) <= TalkRadius)
            StartDialogue(who);
        if (_speaker is { } speaker && speaker.DistanceTo(Hero.X, Hero.Y) > TalkRadius + 2.5)
            EndDialogue();
        if (StashOpenAt is { } chest && chest.DistanceTo(Hero.X, Hero.Y) > TalkRadius + 2.5)
            CloseStash();
    }

    // ----- The stash -----------------------------------------------------------------

    /// <summary>The hub's stash, kept with the character.</summary>
    public Stash Stash { get; } = new();

    /// <summary>The stash chest the hero is standing at with it open, or null.</summary>
    public Interactive? StashOpenAt { get; private set; }

    public bool StashOpen => StashOpenAt is not null;

    public void CloseStash() => StashOpenAt = null;

    /// <summary>Moves a bag item into the stash (while it is open). False when either side can't.</summary>
    public bool Store(int bagIndex)
    {
        if (!StashOpen || Hero.Equipment.Bag.ElementAtOrDefault(bagIndex) is not { } item || !Stash.TryAdd(item))
            return false;
        Hero.Equipment.TakeFromBag(bagIndex);
        return true;
    }

    /// <summary>Moves a stash item into the bag (while it is open). False when the bag is full.</summary>
    public bool Retrieve(int stashIndex)
    {
        if (!StashOpen || Stash.Slots.ElementAtOrDefault(stashIndex) is not { } item || !Hero.Equipment.TryAdd(item))
            return false;
        Stash.Take(stashIndex);
        return true;
    }

    private void StartDialogue(Interactive who)
    {
        Hero.InteractTarget = null;
        Hero.Goal = null;
        Hero.Path.Clear();
        Hero.Face(who.X, who.Y);
        if (who.Thing?.Action == "stash")
        {
            StashOpenAt = who;
            return;
        }
        if (who.Dialogue is null || !_dialogues.TryGetValue(who.Dialogue, out var dialogue))
        {
            Say($"{who.Name} has nothing to say.");
            return;
        }
        _dialogue = dialogue;
        _speaker = who;
        _lastRoll = null;
        var start = dialogue.Openings?.FirstOrDefault(o => Holds(o.Requires))?.Node ?? dialogue.Start;
        Enter(start);
    }

    public void EndDialogue()
    {
        _dialogue = null;
        _node = null;
        _speaker = null;
        Dialogue = null;
    }

    /// <summary>Picks the answer at <paramref name="index"/> among those on offer.</summary>
    public void Choose(int index)
    {
        if (_dialogue is null || _node is null)
            return;
        var choices = Offered(_node);
        if (choices.Count == 0)
        {
            EndDialogue();
            return;
        }
        if (index < 0 || index >= choices.Count)
            return;
        var choice = choices[index];
        var price = (choice.Effects ?? []).Sum(e => e.PayPerLevel) * Hero.Character.Level;
        if (price > 0 && !Hero.Equipment.TrySpend(price))
        {
            _lastRoll = $"That costs {price} gold; you have {Hero.Equipment.Gold}.";
            Dialogue = View();
            return;
        }
        foreach (var effect in choice.Effects ?? [])
            Apply(effect);
        _lastRoll = null;
        if (choice.Check is { } check)
        {
            var modifier = CheckModifier(check.Skill);
            var result = Check.Roll(Random, modifier, check.Dc);
            var passed = result.Degree >= DegreeOfSuccess.Success;
            _lastRoll = $"{check.Skill} {result.Natural} {(modifier >= 0 ? "+" : "-")} {Math.Abs(modifier)} = {result.Total} against DC {check.Dc}: "
                + result.Degree switch
                {
                    DegreeOfSuccess.CriticalSuccess => "critical success",
                    DegreeOfSuccess.Success => "success",
                    DegreeOfSuccess.Failure => "failure",
                    _ => "critical failure",
                };
            Enter(passed ? check.Success : check.Failure);
            return;
        }
        if (choice.To is null)
            EndDialogue();
        else
            Enter(choice.To);
    }

    private void Enter(string nodeId)
    {
        if (_dialogue is null || !_dialogue.Nodes.TryGetValue(nodeId, out var node))
        {
            EndDialogue();
            return;
        }
        _node = node;
        foreach (var effect in node.Effects ?? [])
            Apply(effect);
        Dialogue = View();
    }

    private IReadOnlyList<DialogueChoice> Offered(DialogueNode node) =>
        (node.Choices ?? []).Where(c => Holds(c.Requires)).ToList();

    private DialogueView View()
    {
        var cost = ((_node!.Choices ?? []).SelectMany(c => c.Effects ?? []).Select(e => e.PayPerLevel).DefaultIfEmpty(0).Max()
            * Hero.Character.Level).ToString();
        var choices = Offered(_node!).Select(c =>
        {
            var text = c.Text.Replace("{cost}", ((c.Effects ?? []).Sum(e => e.PayPerLevel) * Hero.Character.Level).ToString());
            if (c.Check is not { } check)
                return new ChoiceView(text);
            var modifier = CheckModifier(check.Skill);
            return new ChoiceView(text, check.Skill, modifier, check.Dc, Rules.Check.Chance(modifier, check.Dc));
        }).ToList();
        if (choices.Count == 0)
            choices.Add(new ChoiceView("Leave"));
        return new DialogueView(_speaker!.Name, _speaker.Npc?.Portrait,
            _node!.Text.Replace("{hero}", Hero.Character.Name).Replace("{cost}", cost), choices, _lastRoll);
    }

    private bool Holds(IReadOnlyDictionary<string, string>? requires) =>
        requires is null || requires.All(r => r.Value.Length == 0 ? !_flags.ContainsKey(r.Key) : Flag(r.Key) == r.Value);

    /// <summary>The hero's modifier for a check: a skill, Perception, or a save.</summary>
    public int CheckModifier(string skill)
    {
        var c = Hero.Character;
        return skill switch
        {
            "Perception" => c.Perception().Total,
            "Fortitude" => c.Save(StatisticKind.Fortitude).Total,
            "Reflex" => c.Save(StatisticKind.Reflex).Total,
            "Will" => c.Save(StatisticKind.Will).Total,
            _ => c.Skill(skill).Total,
        };
    }

    private void Apply(DialogueEffect effect)
    {
        if (effect.Flag is { } flag)
            SetFlag(flag, effect.Value ?? "true");
        if (effect.Journal is { } entry)
            WriteJournal(entry);
        if (effect.Xp > 0)
            GainXp(effect.Xp, Hero);
        if (effect.Gold > 0)
        {
            Hero.Equipment.AddGold(effect.Gold);
            Float(Hero, $"+{effect.Gold} gold", FloatingTextKind.Info, 1.0);
        }
        if (effect.Respec)
        {
            Hero.Character.Respec();
            RefreshBar();
            Say("The Lantern takes back your choices. Choose again.");
        }
        if (effect.Item is { } itemId && _loot?.Items.GetValueOrDefault(itemId) is { } definition)
        {
            var item = new Item(definition, Rarity.Common, Math.Max(1, Hero.Character.Level));
            if (!Hero.Equipment.TryAdd(item))
                _ground.Add(new GroundItem(Hero.X, Hero.Y, new LootDrop(item), Time));
        }
    }

    private void WriteJournal(JournalEntryDefinition entry)
    {
        var written = new JournalEntry(entry.Id, entry.Title, entry.Text, entry.Kind, Time);
        var index = _journal.FindIndex(j => j.Id == entry.Id);
        if (index >= 0)
            _journal[index] = written;
        else
            _journal.Add(written);
        Say($"Journal: {entry.Title}");
    }

    /// <summary>Puts back saved flags and journal entries.</summary>
    internal void RestoreStory(IEnumerable<KeyValuePair<string, string>> flags, IEnumerable<JournalEntry> journal)
    {
        foreach (var (name, value) in flags)
            _flags[name] = value;
        _journal.AddRange(journal);
    }
}
