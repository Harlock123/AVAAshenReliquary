using AshenReliquary.App.Services;

namespace AshenReliquary.App.Tests;

public class DisplayScaleTests
{
    private static Func<string, string?> Env(params (string Name, string Value)[] vars) =>
        name => vars.FirstOrDefault(v => v.Name == name).Value;

    [Fact]
    public void Gdk_scale_becomes_the_global_factor()
    {
        Assert.Equal(2, DisplayScale.FactorFor(Env((DisplayScale.GdkVariable, "2"))));
        Assert.Equal(1.5, DisplayScale.FactorFor(Env((DisplayScale.GdkVariable, "1.5"))));
    }

    [Fact]
    public void No_hint_or_a_1x_hint_leaves_Avalonia_to_detect_it()
    {
        Assert.Null(DisplayScale.FactorFor(Env()));
        Assert.Null(DisplayScale.FactorFor(Env((DisplayScale.GdkVariable, "1"))));
        Assert.Null(DisplayScale.FactorFor(Env((DisplayScale.GdkVariable, "lots"))));
    }

    [Fact]
    public void A_players_own_Avalonia_setting_wins()
    {
        Assert.Null(DisplayScale.FactorFor(Env((DisplayScale.GdkVariable, "2"), (DisplayScale.GlobalVariable, "1.25"))));
        Assert.Null(DisplayScale.FactorFor(Env((DisplayScale.GdkVariable, "2"), (DisplayScale.ScreenVariable, "eDP-1=1.5"))));
    }
}
