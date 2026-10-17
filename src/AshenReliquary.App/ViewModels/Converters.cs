using Avalonia.Data.Converters;

namespace AshenReliquary.App.ViewModels;

public static class Converters
{
    /// <summary>Classes that can't be played yet are shown faded.</summary>
    public static readonly IValueConverter PlayableOpacity =
        new FuncValueConverter<bool, double>(playable => playable ? 1.0 : 0.45);
}
