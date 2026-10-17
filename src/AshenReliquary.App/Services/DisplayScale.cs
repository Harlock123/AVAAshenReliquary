using System.Globalization;

namespace AshenReliquary.App.Services;

/// <summary>
/// Picks up the desktop's HiDPI scale on Linux. Avalonia's X11 backend takes its scale from <c>Xft.dpi</c>, which
/// Wayland desktops often leave unset; a compositor that hands XWayland raw pixels (Hyprland's
/// <c>force_zero_scaling</c>, as on Omarchy) then gets a 1x window with tiny text. Those desktops do set
/// <c>GDK_SCALE</c> for X11 apps, so that becomes Avalonia's global scale factor. A player's own
/// <c>AVALONIA_GLOBAL_SCALE_FACTOR</c> or <c>AVALONIA_SCREEN_SCALE_FACTORS</c> is left alone.
/// </summary>
public static class DisplayScale
{
    public const string GlobalVariable = "AVALONIA_GLOBAL_SCALE_FACTOR";
    public const string ScreenVariable = "AVALONIA_SCREEN_SCALE_FACTORS";
    public const string GdkVariable = "GDK_SCALE";

    public static void Apply()
    {
        if (!OperatingSystem.IsLinux()) return;
        if (FactorFor(Environment.GetEnvironmentVariable) is { } factor)
            Environment.SetEnvironmentVariable(GlobalVariable, factor.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>The scale factor to give Avalonia, or null to leave its own detection in charge.</summary>
    public static double? FactorFor(Func<string, string?> environment)
    {
        if (!string.IsNullOrEmpty(environment(GlobalVariable)) || !string.IsNullOrEmpty(environment(ScreenVariable)))
            return null;
        if (!double.TryParse(environment(GdkVariable), NumberStyles.Float, CultureInfo.InvariantCulture, out var gdk))
            return null;
        return gdk > 1 && gdk <= 8 ? gdk : null;
    }
}
