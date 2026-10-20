using AshenReliquary.Core.Abilities;

namespace AshenReliquary.Core.World;

/// <summary>
/// The ability bar (spec, section 9: two mouse buttons and keys 1 to 6) and the abilities the hero has. The player puts
/// any ability into any slot (the Abilities panel, K); an ability not on the bar waits in the list. A newly gained
/// ability goes into its class slot if that is free, else the first free number slot, else the list; after that it
/// stays where the player puts it. The layout is saved; a save from before it gets the automatic layout.
/// </summary>
public sealed partial class GameWorld
{
    /// <summary>The bar's slots in order: the mouse buttons, then keys 1 to 6.</summary>
    public static readonly IReadOnlyList<string> BarSlots = ["lmb", "rmb", "1", "2", "3", "4", "5", "6"];

    private static readonly string[] NumberSlots = ["1", "2", "3", "4", "5", "6"];

    /// <summary>The ability bar: "lmb", "rmb" and "1".."6" to abilities, as the hero's feats have changed them.</summary>
    public IReadOnlyDictionary<string, AbilityDefinition> Bar => _bar;

    private readonly IReadOnlyDictionary<string, AbilityDefinition> _classBar;
    private readonly IReadOnlyDictionary<string, AbilityDefinition> _abilities;
    private readonly Dictionary<string, AbilityDefinition> _bar = [];

    /// <summary>Slot to ability id, as the player (or the automatic placing) left it.</summary>
    private readonly Dictionary<string, string> _layout = [];

    /// <summary>The abilities the bar has been offered: each is placed once, and then left to the player.</summary>
    private readonly HashSet<string> _offered = [];

    /// <summary>Every ability the hero has, as their feats have changed it: the class's (from its level), then the feats'.</summary>
    public IReadOnlyList<AbilityDefinition> KnownAbilities { get; private set; } = [];

    /// <summary>The abilities the hero has that are on no slot (they wait in the Abilities panel's list).</summary>
    public IEnumerable<AbilityDefinition> AbilitiesOffBar => KnownAbilities.Where(a => !_layout.ContainsValue(a.Id));

    /// <summary>The bar's layout for saving: slot to ability id.</summary>
    public IReadOnlyDictionary<string, string> BarLayout => _layout;

    /// <summary>The abilities already placed once (saved with the layout, so one the player took off stays off).</summary>
    public IReadOnlyCollection<string> BarOffered => _offered;

    /// <summary>The slot an ability is in, or null when it waits in the list.</summary>
    public string? SlotOf(string abilityId) => _layout.FirstOrDefault(kv => kv.Value == abilityId).Key;

    /// <summary>
    /// The basic attack, which a left click falls back on and every one-off ability is followed by: the "lmb" slot's
    /// ability if it repeats, else the first repeating ability the hero has (the class's Strike or cantrip), else a
    /// plain weapon Strike.
    /// </summary>
    public AbilityDefinition BasicAbility =>
        Bar.GetValueOrDefault("lmb") is { Repeat: true } lmb ? lmb : KnownAbilities.FirstOrDefault(a => a.Repeat) ?? DefaultStrike;

    public static readonly AbilityDefinition DefaultStrike =
        new("strike", "Strike", "any", "lmb", AbilityKind.WeaponStrike, Repeat: true, Traits: ["Attack"]);

    /// <summary>
    /// Works out the abilities the hero has and fills the bar from the layout: an ability seen for the first time goes
    /// into its class slot if free; feat abilities take the number slots no class ability uses first, so one doesn't
    /// have to move when a class ability joins later (Finishing Blow at 2), then any slot still empty; with none free
    /// it waits in the list. Abilities the hero no longer has (after a respec) leave the bar. Then the belt
    /// (<see cref="RefreshBelt"/>).
    /// </summary>
    private void RefreshBar()
    {
        RefreshDedication();
        var c = Hero.Character;
        var known = new List<(AbilityDefinition Ability, string? Home)>();
        foreach (var (slot, ability) in _classBar)
        {
            if (ability.Level <= c.Level)
                known.Add((c.Modify(ability), slot));
        }
        foreach (var id in c.GrantedAbilities)
        {
            // A subclass's level-8 ability waits for level 8, as a class ability does.
            if (_abilities.TryGetValue(id, out var ability) && ability.Level <= c.Level && known.All(k => k.Ability.Id != id))
                known.Add((c.Modify(ability), null));
        }
        KnownAbilities = known.Select(k => k.Ability).ToList();

        var ids = KnownAbilities.Select(a => a.Id).ToHashSet();
        foreach (var slot in _layout.Where(kv => !ids.Contains(kv.Value)).Select(kv => kv.Key).ToList())
            _layout.Remove(slot);
        _offered.IntersectWith(ids);

        foreach (var (ability, home) in known)
        {
            if (!_offered.Add(ability.Id))
                continue;
            var slot = home is not null && !_layout.ContainsKey(home) ? home
                : NumberSlots.Where(s => !_classBar.ContainsKey(s)).Concat(NumberSlots).FirstOrDefault(s => !_layout.ContainsKey(s));
            if (slot is not null)
                _layout[slot] = ability.Id;
        }

        _bar.Clear();
        foreach (var (slot, id) in _layout)
            _bar[slot] = KnownAbilities.First(a => a.Id == id);
        RefreshBelt();
    }

    /// <summary>
    /// Puts an ability the hero has into a bar slot. Whatever was there moves to the slot the ability came from, or to
    /// the list if it came from the list. False for an unknown slot or an ability the hero lacks.
    /// </summary>
    public bool PlaceAbility(string abilityId, string slot)
    {
        if (!BarSlots.Contains(slot) || KnownAbilities.All(a => a.Id != abilityId))
            return false;
        var from = SlotOf(abilityId);
        if (from == slot)
            return true;
        var occupant = _layout.GetValueOrDefault(slot);
        if (from is not null)
        {
            _layout.Remove(from);
            if (occupant is not null)
                _layout[from] = occupant;
        }
        _layout[slot] = abilityId;
        RefreshBar();
        return true;
    }

    /// <summary>
    /// Uses an ability the hero knows by its id, on the bar or waiting in the list (the bots: a player would put it on
    /// the bar). False when the hero doesn't know it.
    /// </summary>
    public bool UseAbility(string id, MonsterActor? target, (double X, double Y)? toward = null)
    {
        if (KnownAbilities.FirstOrDefault(a => a.Id == id) is not { } ability)
            return false;
        Use(ability, target, toward);
        return true;
    }

    /// <summary>Takes the ability off a slot, back to the list. False when the slot was empty.</summary>
    public bool ClearSlot(string slot)
    {
        if (!_layout.Remove(slot))
            return false;
        RefreshBar();
        return true;
    }

    /// <summary>Restores a saved layout (after the character is rebuilt); abilities it doesn't know are placed as new.</summary>
    internal void RestoreBar(IReadOnlyDictionary<string, string> layout, IEnumerable<string> offered)
    {
        _layout.Clear();
        _offered.Clear();
        foreach (var (slot, id) in layout)
        {
            if (BarSlots.Contains(slot) && !_layout.ContainsValue(id))
                _layout[slot] = id;
        }
        _offered.UnionWith(offered);
        _offered.UnionWith(_layout.Values);
        RefreshBar();
    }
}
