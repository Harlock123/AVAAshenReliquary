using System.Collections.ObjectModel;
using AshenReliquary.App.Services;
using Avalonia.Media.Imaging;
using AshenReliquary.Core.Characters;
using AshenReliquary.Core.Content;
using AshenReliquary.Core.Items;
using AshenReliquary.Core.Rules;
using AshenReliquary.Core.World;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AshenReliquary.App.ViewModels;

/// <summary>A class in the list; only the vertical slice's classes can be picked yet.</summary>
public sealed class ClassOption(ClassDefinition definition, bool playable)
{
    public ClassDefinition Definition { get; } = definition;
    public string Name => Definition.Also is { } also ? $"{Definition.Name} ({also})" : Definition.Name;
    public bool Playable { get; } = playable;
    public string Detail => Playable
        ? $"{Definition.HitPointsPerLevel} HP per level · {Definition.Resource} · {string.Join(" / ", Definition.Subclasses)}"
        : "Coming in M3";
}

/// <summary>One boost toggle in the table: selected, clickable, and what it shows.</summary>
public sealed partial class BoostCell(Action toggle) : ObservableObject
{
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _isEnabled;
    [ObservableProperty] private bool _isFlaw;
    [ObservableProperty] private string _text = "";

    public IRelayCommand Toggle { get; } = new RelayCommand(toggle);
}

/// <summary>
/// One skill in the trained-skills picker: fixed ones (from the background or class) show as trained and can't be
/// toggled; the rest are the player's picks.
/// </summary>
public sealed partial class SkillOption(string name, Action toggle) : ObservableObject
{
    public string Name { get; } = name;
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _isEnabled;

    public IRelayCommand Toggle { get; } = new RelayCommand(toggle);
}

/// <summary>One attribute's row: its boost cells in each group and the resulting modifier.</summary>
public sealed partial class AttributeRow : ObservableObject
{
    public AttributeRow(AttributeKind kind, CharacterCreationViewModel owner)
    {
        Kind = kind;
        Ancestry = new BoostCell(() => owner.ToggleAncestry(kind));
        Background = new BoostCell(() => owner.SetBackgroundBoost(kind));
        BackgroundFree = new BoostCell(() => owner.SetBackgroundFree(kind));
        Key = new BoostCell(() => owner.SetKey(kind));
        Free = new BoostCell(() => owner.ToggleFree(kind));
    }

    public AttributeKind Kind { get; }
    public string Name => Kind.ToString();
    public string Abbreviation => Kind.Abbreviation();
    public BoostCell Ancestry { get; }
    public BoostCell Background { get; }
    public BoostCell BackgroundFree { get; }
    public BoostCell Key { get; }
    public BoostCell Free { get; }
    [ObservableProperty] private string _value = "+0";
}

/// <summary>
/// The character creation screen, in Pathfinder order: ancestry and heritage, background, class, then four free
/// boosts. Every change rebuilds the character through <see cref="CharacterBuilder"/>, so the preview shows the
/// real numbers and Begin only enables for a build the rules accept.
/// </summary>
public sealed partial class CharacterCreationViewModel : ObservableObject
{
    private readonly ContentDatabase _content = ContentDatabase.Embedded;
    private readonly List<AttributeKind> _ancestryFree = [];
    private readonly List<AttributeKind> _free = [];
    private AttributeKind _backgroundBoost;
    private AttributeKind _backgroundFree;
    private AttributeKind _key;
    private readonly List<string> _skills = [];
    private bool _loading;

    public CharacterCreationViewModel()
    {
        Ancestries = _content.Ancestries.Values.ToList();
        Backgrounds = _content.Backgrounds.Values.ToList();
        Classes = _content.Classes.Values.Select(c => new ClassOption(c, GameSession.IsPlayable(c.Id)))
            .OrderByDescending(c => c.Playable).ToList();
        Rows = new ObservableCollection<AttributeRow>(Enum.GetValues<AttributeKind>().Select(k => new AttributeRow(k, this)));
        SkillOptions = new ObservableCollection<SkillOption>(Skills.Attribute.Keys.Order().Select(s => new SkillOption(s, () => ToggleSkill(s))));
        _ancestry = Ancestries.First(a => a.Id == "human");
        _background = Backgrounds.First(b => b.Id == "deserter");
        _selectedClass = Classes.First(c => c.Definition.Id == "fighter");
        _heritage = _ancestry.Heritages?.FirstOrDefault();
        Heritages = new ObservableCollection<HeritageDefinition>(_ancestry.Heritages ?? []);
        AncestryFeats = new ObservableCollection<FeatDefinition>(CharacterBuilder.AncestryFeats(_content, _ancestry.Id));
        _ancestryFeat = AncestryFeats.FirstOrDefault();
        Subclasses = new ObservableCollection<FeatDefinition>(SubclassesOf(_selectedClass.Definition.Id));
        _subclass = Subclasses.FirstOrDefault();
        Recommend();
    }

    public IReadOnlyList<AncestryDefinition> Ancestries { get; }
    public IReadOnlyList<BackgroundDefinition> Backgrounds { get; }
    public IReadOnlyList<ClassOption> Classes { get; }
    public ObservableCollection<HeritageDefinition> Heritages { get; }
    public ObservableCollection<AttributeRow> Rows { get; }
    public ObservableCollection<SkillOption> SkillOptions { get; }
    public ObservableCollection<FeatDefinition> AncestryFeats { get; }

    /// <summary>The selected class's subclasses (spec, section 7: chosen at level 1).</summary>
    public ObservableCollection<FeatDefinition> Subclasses { get; }

    private IEnumerable<FeatDefinition> SubclassesOf(string classId) =>
        _content.Feats.Values.Where(f => f.Type == FeatType.Subclass && f.Class == classId);
    public ObservableCollection<string> Preview { get; } = [];
    public ObservableCollection<string> Abilities { get; } = [];

    [ObservableProperty] private string _name = "The Ashbound";
    [ObservableProperty] private AncestryDefinition _ancestry;
    [ObservableProperty] private HeritageDefinition? _heritage;
    [ObservableProperty] private BackgroundDefinition _background;
    [ObservableProperty] private ClassOption _selectedClass;
    [ObservableProperty] private string _problem = "";
    [ObservableProperty] private bool _canBegin;
    [ObservableProperty] private string _boostSummary = "";
    [ObservableProperty] private string _ancestryHeader = "Ancestry";
    [ObservableProperty] private string _freeHeader = "Free (4)";
    [ObservableProperty] private int _portraitIndex;
    [ObservableProperty] private FeatDefinition? _ancestryFeat;
    [ObservableProperty] private FeatDefinition? _subclass;
    [ObservableProperty] private string _skillHeader = "Trained skills";

    partial void OnAncestryFeatChanged(FeatDefinition? value) => Refresh();
    partial void OnSubclassChanged(FeatDefinition? value) => Refresh();

    /// <summary>How many skills the player picks: the class's number plus Intelligence.</summary>
    private int SkillPicks => CharacterBuilder.SkillPicks(SelectedClass.Definition, CurrentAttributes());

    /// <summary>The skills the background and class train anyway.</summary>
    private HashSet<string> FixedSkills => [Background.Skill, .. SelectedClass.Definition.TrainedSkills ?? []];

    public void ToggleSkill(string skill)
    {
        if (FixedSkills.Contains(skill))
            return;
        if (!_skills.Remove(skill) && _skills.Count < SkillPicks)
            _skills.Add(skill);
        Refresh();
    }

    /// <summary>The portraits for the chosen ancestry; ‹ › cycle through them.</summary>
    public IReadOnlyList<Portrait> AncestryPortraits => Portraits.For(Ancestry.Id);

    public Portrait? SelectedPortrait => AncestryPortraits.Count == 0 ? null : AncestryPortraits[Math.Clamp(PortraitIndex, 0, AncestryPortraits.Count - 1)];

    public Bitmap? PortraitImage => SelectedPortrait is { } p ? Portraits.Bitmap(p) : null;

    public bool HasPortrait => SelectedPortrait is not null;

    public string PortraitCaption => SelectedPortrait is { } p
        ? $"{PortraitIndex + 1} / {AncestryPortraits.Count} · art by {p.Artist}"
        : $"No {Ancestry.Name.ToLowerInvariant()} portrait yet";

    partial void OnPortraitIndexChanged(int value)
    {
        OnPropertyChanged(nameof(SelectedPortrait));
        OnPropertyChanged(nameof(PortraitImage));
        OnPropertyChanged(nameof(PortraitCaption));
        OnPropertyChanged(nameof(HasPortrait));
    }

    [RelayCommand]
    private void NextPortrait()
    {
        if (AncestryPortraits.Count > 0)
            PortraitIndex = (PortraitIndex + 1) % AncestryPortraits.Count;
    }

    [RelayCommand]
    private void PreviousPortrait()
    {
        if (AncestryPortraits.Count > 0)
            PortraitIndex = (PortraitIndex + AncestryPortraits.Count - 1) % AncestryPortraits.Count;
    }

    /// <summary>Raised by Begin with the finished choices and the chosen portrait (null when none exists yet).</summary>
    public event Action<CharacterChoices, Portrait?>? Started;

    /// <summary>
    /// The act the hero begins at (M4 step 10): 1 on the gallows road, or 2 to 4 with the acts before it done, at its
    /// level (see <see cref="GameWorld.ActStarts"/>).
    /// </summary>
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(StartActLabel))] private int _startAct = 1;

    public string StartActLabel => StartAct switch
    {
        2 => "Begin at: Act 2 · the Iron March, level 6",
        3 => "Begin at: Act 3 · the Frostbitten Pass, level 11",
        4 => "Begin at: Act 4 · the Ember Gate, level 15",
        _ => "Begin at: Act 1 · the gallows",
    };

    [RelayCommand]
    private void NextStartAct() => StartAct = StartAct % GameWorld.ActStarts.Count + 1;

    /// <summary>Raised by the Guide button, with the guide page for the class being made.</summary>
    public event Action<string>? GuideRequested;

    [RelayCommand]
    private void Guide() => GuideRequested?.Invoke($"class:{SelectedClass.Definition.Id}");

    public string AncestryDetail => $"{Ancestry.HitPoints} HP · {Ancestry.SpeedFeet} ft · {Ancestry.Senses}. {Ancestry.Trait}.";
    public string BackgroundDetail => $"Trained in {Background.Skill} and {Background.Lore}; skill feat {Background.SkillFeat}.";

    partial void OnNameChanged(string value) => Refresh();

    partial void OnAncestryChanged(AncestryDefinition value)
    {
        Heritages.Clear();
        foreach (var h in value.Heritages ?? [])
            Heritages.Add(h);
        Heritage = Heritages.FirstOrDefault();
        AncestryFeats.Clear();
        foreach (var feat in CharacterBuilder.AncestryFeats(_content, value.Id))
            AncestryFeats.Add(feat);
        AncestryFeat = AncestryFeats.FirstOrDefault();
        OnPropertyChanged(nameof(AncestryDetail));
        OnPropertyChanged(nameof(AncestryPortraits));
        PortraitIndex = 0;
        OnPortraitIndexChanged(0);
        if (!_loading)
            Recommend();
    }

    partial void OnHeritageChanged(HeritageDefinition? value) => Refresh();

    partial void OnBackgroundChanged(BackgroundDefinition value)
    {
        OnPropertyChanged(nameof(BackgroundDetail));
        if (!_loading)
            Recommend();
    }

    partial void OnSelectedClassChanged(ClassOption value)
    {
        Subclasses.Clear();
        foreach (var subclass in SubclassesOf(value.Definition.Id))
            Subclasses.Add(subclass);
        Subclass = Subclasses.FirstOrDefault();
        if (!_loading)
            Recommend();
    }

    /// <summary>Fills every boost with the recommended build for the current ancestry, background and class.</summary>
    [RelayCommand]
    public void Recommend()
    {
        var choices = CharacterBuilder.Recommend(_content, Name, Ancestry.Id, Background.Id, SelectedClass.Definition.Id, null, Heritage?.Id);
        _ancestryFree.Clear();
        _ancestryFree.AddRange(choices.AncestryFreeBoosts);
        _backgroundBoost = choices.BackgroundBoost;
        _backgroundFree = choices.BackgroundFreeBoost;
        _key = choices.KeyAttribute;
        _free.Clear();
        _free.AddRange(choices.FreeBoosts);
        // The recommended skills: what the builder picks when none are given, less those trained anyway.
        _skills.Clear();
        try
        {
            var built = CharacterBuilder.Create(_content, choices);
            _skills.AddRange(built.SkillRanks.Keys.Where(s => !FixedSkills.Contains(s)).Order());
        }
        catch (RulesException)
        {
        }
        Refresh();
    }

    [RelayCommand]
    private void Begin()
    {
        if (BuildChoices() is { } choices && TryBuild(choices, out _))
            Started?.Invoke(choices, SelectedPortrait);
    }

    public void ToggleAncestry(AttributeKind kind)
    {
        if (Ancestry.Boosts.Contains(kind))
            return;
        if (!_ancestryFree.Remove(kind) && _ancestryFree.Count < Ancestry.FreeBoosts)
            _ancestryFree.Add(kind);
        Refresh();
    }

    public void SetBackgroundBoost(AttributeKind kind)
    {
        if (!Background.BoostChoices.Contains(kind))
            return;
        _backgroundBoost = kind;
        if (_backgroundFree == kind)
            _backgroundFree = Enum.GetValues<AttributeKind>().First(a => a != kind);
        Refresh();
    }

    public void SetBackgroundFree(AttributeKind kind)
    {
        if (kind == _backgroundBoost)
            return;
        _backgroundFree = kind;
        Refresh();
    }

    public void SetKey(AttributeKind kind)
    {
        if (!SelectedClass.Definition.KeyAttributes.Contains(kind))
            return;
        _key = kind;
        Refresh();
    }

    public void ToggleFree(AttributeKind kind)
    {
        if (!_free.Remove(kind) && _free.Count < CharacterBuilder.FreeBoostCount)
            _free.Add(kind);
        Refresh();
    }

    private CharacterChoices? BuildChoices() => new(Name, Ancestry.Id, Background.Id, SelectedClass.Definition.Id, _key,
        _ancestryFree.ToList(), _backgroundBoost, _backgroundFree, _free.ToList(), Heritage?.Id, _skills.ToList(), AncestryFeat?.Id,
        Subclass?.Id);

    /// <summary>The attributes the boost table gives right now (for the skill-pick count, which follows Intelligence).</summary>
    private AttributeSet CurrentAttributes()
    {
        var values = new AttributeSet();
        foreach (var k in Enum.GetValues<AttributeKind>())
        {
            if (Ancestry.Boosts.Contains(k) || _ancestryFree.Contains(k)) values.Boost(k);
            if (Ancestry.Flaws.Contains(k)) values.Flaw(k);
            if (_backgroundBoost == k) values.Boost(k);
            if (_backgroundFree == k) values.Boost(k);
            if (_key == k) values.Boost(k);
            if (_free.Contains(k)) values.Boost(k);
        }
        return values;
    }

    private bool TryBuild(CharacterChoices choices, out Character? character)
    {
        character = null;
        if (!SelectedClass.Playable)
        {
            Problem = $"{SelectedClass.Name} arrives in M3. Pick the Fighter or the Wizard for now.";
            return false;
        }
        if (_ancestryFree.Count < Ancestry.FreeBoosts)
        {
            Problem = $"Choose {Ancestry.FreeBoosts - _ancestryFree.Count} more ancestry boost(s).";
            return false;
        }
        if (_free.Count < CharacterBuilder.FreeBoostCount)
        {
            Problem = $"Choose {CharacterBuilder.FreeBoostCount - _free.Count} more free boost(s).";
            return false;
        }
        if (_skills.Count < SkillPicks)
        {
            Problem = $"Choose {SkillPicks - _skills.Count} more trained skill(s).";
            return false;
        }
        try
        {
            character = CharacterBuilder.Create(_content, choices);
            Problem = "";
            return true;
        }
        catch (RulesException e)
        {
            Problem = e.Message;
            return false;
        }
    }

    /// <summary>Recomputes the boost table, the attribute values and the preview from the current choices.</summary>
    private void Refresh()
    {
        AncestryHeader = Ancestry.FreeBoosts == 1 ? "Ancestry" : $"Anc. ({Ancestry.FreeBoosts})";
        var values = new AttributeSet();
        foreach (var row in Rows)
        {
            var k = row.Kind;
            var fixedBoost = Ancestry.Boosts.Contains(k);
            var flaw = Ancestry.Flaws.Contains(k);
            var free = _ancestryFree.Contains(k);
            row.Ancestry.IsSelected = fixedBoost || free;
            row.Ancestry.IsFlaw = flaw;
            row.Ancestry.IsEnabled = !fixedBoost && (free || _ancestryFree.Count < Ancestry.FreeBoosts);
            row.Ancestry.Text = flaw ? "−1" : fixedBoost ? "+1 ✦" : free ? "+1" : "·";

            var bgOption = Background.BoostChoices.Contains(k);
            row.Background.IsSelected = _backgroundBoost == k;
            row.Background.IsEnabled = bgOption;
            row.Background.Text = _backgroundBoost == k ? "+1" : bgOption ? "·" : "";

            row.BackgroundFree.IsSelected = _backgroundFree == k;
            row.BackgroundFree.IsEnabled = _backgroundBoost != k;
            row.BackgroundFree.Text = _backgroundFree == k ? "+1" : "·";

            var keyOption = SelectedClass.Definition.KeyAttributes.Contains(k);
            row.Key.IsSelected = _key == k;
            row.Key.IsEnabled = keyOption;
            row.Key.Text = _key == k ? "+1 key" : keyOption ? "·" : "";

            var picked = _free.Contains(k);
            row.Free.IsSelected = picked;
            row.Free.IsEnabled = picked || _free.Count < CharacterBuilder.FreeBoostCount;
            row.Free.Text = picked ? "+1" : "·";

            if (fixedBoost || free) values.Boost(k);
            if (flaw) values.Flaw(k);
            if (_backgroundBoost == k) values.Boost(k);
            if (_backgroundFree == k) values.Boost(k);
            if (_key == k) values.Boost(k);
            if (picked) values.Boost(k);
        }
        foreach (var row in Rows)
            row.Value = $"{values[row.Kind]:+0;-0;+0}";
        FreeHeader = $"Free ({_free.Count}/{CharacterBuilder.FreeBoostCount})";

        // Skills: drop picks now trained anyway or beyond the count (Intelligence went down), then show the toggles.
        var fixedSkills = FixedSkills;
        _skills.RemoveAll(fixedSkills.Contains);
        var picks = SkillPicks;
        if (_skills.Count > picks)
            _skills.RemoveRange(picks, _skills.Count - picks);
        foreach (var option in SkillOptions)
        {
            var isFixed = fixedSkills.Contains(option.Name);
            option.IsSelected = isFixed || _skills.Contains(option.Name);
            option.IsEnabled = !isFixed && (option.IsSelected || _skills.Count < picks);
        }
        SkillHeader = $"Trained skills ({_skills.Count}/{picks} to pick)";

        Preview.Clear();
        Abilities.Clear();
        var choices = BuildChoices()!;
        CanBegin = TryBuild(choices, out var character);
        if (character is null)
            return;
        var gear = GameSession.Dress(character, new Equipment(character), choices.ClassId, choices.Subclass);
        character.RestAtShrine();
        foreach (var stat in StatSnapshot.Take(character).Where(s => s.Core))
            Preview.Add(stat.Name == "Speed" ? $"Speed: {character.SpeedFeet} ft" : $"{stat.Name}: {stat.Text}");
        Preview.Add($"Perception: {character.Perception().Total:+0}");
        Preview.Add($"Class DC: {character.ClassDc().Total}");
        Preview.Add($"Resource: {character.Class.Resource}");
        if (character.SneakAttackDice > 0)
            Preview.Add($"Sneak attack: {character.SneakAttackDice}d6");
        if (character.PreyDice > 0)
            Preview.Add($"Hunt Prey: {character.PreyDice}d8");
        if (character.UndeadBane > 0)
            Preview.Add($"Undead bane: +{character.UndeadBane} vitality");
        if (character.TaintFuel)
            Preview.Add("Taint drain: your hits fill your Taint");
        if (character.Class.GraveCharges)
        {
            Preview.Add($"Grave Charges: up to {Core.World.GameWorld.BaseGraveCharges + character.GraveChargeBonus}");
            Preview.Add($"Skeletons: {Core.World.GameWorld.BaseMinions + character.MinionBonus} at once");
        }
        if (character.Companion is { } companion && _content.Creatures.TryGetValue(companion, out var beast))
            Preview.Add($"Companion: a {beast.Name.ToLowerInvariant()}");
        foreach (var (slot, ability) in _content.AbilityBar(choices.ClassId).OrderBy(kv => kv.Key switch { "lmb" => 0, "rmb" => 1, _ => 2 }))
            Abilities.Add($"{(slot == "lmb" ? "LMB" : slot == "rmb" ? "RMB" : slot)}  {ability.Name}: {ability.Description}");
        foreach (var id in character.GrantedAbilities)
        {
            if (_content.Abilities.TryGetValue(id, out var granted))
                Abilities.Add($"{character.Subclass?.Name ?? "Feat"}  {granted.Name}: {granted.Description}");
        }
        Abilities.Add($"Starting gear: {string.Join(", ", gear.Worn.Values.Select(i => i.Name))}.");
    }

    /// <summary>For tests: pick by id without going through the UI.</summary>
    public void Choose(string ancestryId, string backgroundId, string classId, string? subclassId = null)
    {
        _loading = true;
        Ancestry = Ancestries.First(a => a.Id == ancestryId);
        Background = Backgrounds.First(b => b.Id == backgroundId);
        SelectedClass = Classes.First(c => c.Definition.Id == classId);
        if (subclassId is not null)
            Subclass = Subclasses.First(s => s.Id == subclassId);
        _loading = false;
        Recommend();
    }
}
