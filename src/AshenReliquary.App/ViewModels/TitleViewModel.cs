using System.Collections.ObjectModel;
using AshenReliquary.App.Services;
using AshenReliquary.Core.Content;
using AshenReliquary.Core.Saving;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AshenReliquary.App.ViewModels;

/// <summary>One saved character in the title screen's list.</summary>
public sealed class SaveEntry(SaveGame save)
{
    private static readonly ContentDatabase Content = ContentDatabase.Embedded;

    public SaveGame Save { get; } = save;

    /// <summary>The character's name, and a saved copy's number ("Elirk-2"; an older copy, when it was taken).</summary>
    public string Name => Save.CopyNumber is { } number ? $"{Save.Choices.Name}-{number}"
        : Save.Label is { } label ? $"{Save.Choices.Name} · {label}" : Save.Choices.Name;

    public string Detail
    {
        get
        {
            var ancestry = Content.Ancestries.TryGetValue(Save.Choices.AncestryId, out var a) ? a.Name : Save.Choices.AncestryId;
            var cls = Content.Classes.TryGetValue(Save.Choices.ClassId, out var c) ? c.Name : Save.Choices.ClassId;
            var zone = Content.Zones.TryGetValue(Save.ZoneId, out var z) ? z.Name : Save.ZoneId;
            return $"Level {Save.Level} {ancestry} {cls} · {zone}";
        }
    }

    public string When
    {
        get
        {
            var ago = DateTimeOffset.Now - Save.SavedAt;
            var played = TimeSpan.FromSeconds(Save.PlaySeconds);
            var when = ago.TotalMinutes < 1 ? "just now"
                : ago.TotalHours < 1 ? $"{(int)ago.TotalMinutes} min ago"
                : ago.TotalDays < 1 ? $"{(int)ago.TotalHours} h ago"
                : Save.SavedAt.LocalDateTime.ToString("d MMM yyyy, HH:mm");
            return $"Saved {when} · played {(int)played.TotalHours}:{played.Minutes:00}";
        }
    }

    public Bitmap? Portrait => Portraits.Find(Save.PortraitId) is { } p ? Portraits.Bitmap(p) : null;
}

/// <summary>
/// The title screen: continue the most recent game, load any saved character, delete one (after a confirmation),
/// start a new character, or quit.
/// </summary>
public sealed partial class TitleViewModel : ObservableObject
{
    public TitleViewModel() => Refresh();

    public ObservableCollection<SaveEntry> Saves { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private SaveEntry? _selected;

    [ObservableProperty] private bool _confirmingDelete;
    [ObservableProperty] private string _message = "";

    public bool HasSaves => Saves.Count > 0;
    public bool HasSelection => Selected is not null;

    public event Action<SaveGame>? LoadRequested;
    public event Action? NewRequested;
    public event Action? QuitRequested;

    public void Refresh()
    {
        Saves.Clear();
        foreach (var save in SaveStore.List())
            Saves.Add(new SaveEntry(save));
        Selected = Saves.FirstOrDefault();
        ConfirmingDelete = false;
        OnPropertyChanged(nameof(HasSaves));
    }

    partial void OnSelectedChanged(SaveEntry? value) => ConfirmingDelete = false;

    [RelayCommand]
    private void Load()
    {
        if (Selected is { } entry)
            LoadRequested?.Invoke(entry.Save);
    }

    [RelayCommand]
    private void NewCharacter() => NewRequested?.Invoke();

    [RelayCommand]
    private void Quit() => QuitRequested?.Invoke();

    /// <summary>The first press asks; the second (Delete forever) removes the save file.</summary>
    [RelayCommand]
    private void Delete()
    {
        if (Selected is not { } entry)
            return;
        if (!ConfirmingDelete)
        {
            ConfirmingDelete = true;
            return;
        }
        SaveStore.Delete(entry.Save.SaveId);
        Message = $"{entry.Name} was deleted.";
        Refresh();
    }

    [RelayCommand]
    private void CancelDelete() => ConfirmingDelete = false;
}
