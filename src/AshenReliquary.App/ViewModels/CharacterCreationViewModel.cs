using System.Collections.ObjectModel;
using AshenReliquary.App.Services;
using Avalonia.Media.Imaging;
using AshenReliquary.Core.Characters;
using AshenReliquary.Core.Content;
using AshenReliquary.Core.Items;
using AshenReliquary.Core.Rules;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AshenReliquary.App.ViewModels;

/// <summary>A class in the list; only the vertical slice's classes can be picked yet.</summary>
public sealed class ClassOption(ClassDefinition definition, bool playable)
{
    public ClassDefinition Definition { get; } = definition;
    public string Name => Definition.Name;
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
    private bool _loading;

    public CharacterCreationViewModel()
    {
        Ancestries = _content.Ancestries.Values.ToList();
        Backgrounds = _content.Backgrounds.Values.ToList();
        Classes = _content.Classes.Values.Select(c => new ClassOption(c, GameSession.IsPlayable(c.Id)))
            .OrderByDescending(c => c.Playable).ToList();
        Rows = new ObservableCollection<AttributeRow>(Enum.GetValues<AttributeKind>().Select(k => new AttributeRow(k, this)));
        _ancestry = Ancestries.First(a => a.Id == "human");
        _background = Backgrounds.First(b => b.Id == "deserter");
        _selectedClass = Classes.First(c => c.Definition.Id == "fighter");
        _heritage = _ancestry.Heritages?.FirstOrDefault();
        Heritages = new ObservableCollection<HeritageDefinition>(_ancestry.Heritages ?? []);
        Recommend();
    }

    public IReadOnlyList<AncestryDefinition> Ancestries { get; }
    public IReadOnlyList<BackgroundDefinition> Backgrounds { get; }
    public IReadOnlyList<ClassOption> Classes { get; }
    public ObservableCollection<HeritageDefinition> Heritages { get; }
    public ObservableCollection<AttributeRow> Rows { get; }
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

    public string AncestryDetail => $"{Ancestry.HitPoints} HP · {Ancestry.SpeedFeet} ft · {Ancestry.Senses}. {Ancestry.Trait}.";
    public string BackgroundDetail => $"Trained in {Background.Skill} and {Background.Lore}; skill feat {Background.SkillFeat}.";

    partial void OnNameChanged(string value) => Refresh();

    partial void OnAncestryChanged(AncestryDefinition value)
    {
        Heritages.Clear();
        foreach (var h in value.Heritages ?? [])
            Heritages.Add(h);
        Heritage = Heritages.FirstOrDefault();
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
        _ancestryFree.ToList(), _backgroundBoost, _backgroundFree, _free.ToList(), Heritage?.Id);

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

        Preview.Clear();
        Abilities.Clear();
        var choices = BuildChoices()!;
        CanBegin = TryBuild(choices, out var character);
        if (character is null)
            return;
        var gear = GameSession.Dress(character, new Equipment(character), choices.ClassId);
        character.RestAtShrine();
        foreach (var stat in StatSnapshot.Take(character).Where(s => s.Core))
            Preview.Add(stat.Name == "Speed" ? $"Speed: {character.SpeedFeet} ft" : $"{stat.Name}: {stat.Text}");
        Preview.Add($"Perception: {character.Perception().Total:+0}");
        Preview.Add($"Class DC: {character.ClassDc().Total}");
        Preview.Add($"Resource: {character.Class.Resource}");
        foreach (var (slot, ability) in _content.AbilityBar(choices.ClassId).OrderBy(kv => kv.Key switch { "lmb" => 0, "rmb" => 1, _ => 2 }))
            Abilities.Add($"{(slot == "lmb" ? "LMB" : slot == "rmb" ? "RMB" : slot)}  {ability.Name}: {ability.Description}");
        Abilities.Add($"Starting gear: {string.Join(", ", gear.Worn.Values.Select(i => i.Name))}.");
    }

    /// <summary>For tests: pick by id without going through the UI.</summary>
    public void Choose(string ancestryId, string backgroundId, string classId)
    {
        _loading = true;
        Ancestry = Ancestries.First(a => a.Id == ancestryId);
        Background = Backgrounds.First(b => b.Id == backgroundId);
        SelectedClass = Classes.First(c => c.Definition.Id == classId);
        _loading = false;
        Recommend();
    }
}
