using Avalonia;
using Avalonia.Headless.XUnit;
using AshenReliquary.Core.Abilities;
using AshenReliquary.Core.Characters;

namespace AshenReliquary.App.Tests;

/// <summary>M3 step 6 on screen: a dedication picked on the level-up screen, in the narrow window, and its pool on the HUD.</summary>
public class DedicationUiTests
{
    private static string? Shot(string name) =>
        Environment.GetEnvironmentVariable("ASHEN_SHOTS") is { Length: > 0 } dir ? Path.Combine(dir, name + ".png") : null;

    [AvaloniaFact]
    public void A_fighter_takes_wizard_dedication_on_the_level_up_screen_and_casts_force_bolt_from_its_mana()
    {
        var kit = TestKit.Open();
        kit.Window.MinWidth = 0;
        kit.Window.Width = 731;
        kit.Window.Height = 946;
        var world = kit.World;
        TestHooks.GainXp(world, 1000);
        kit.Window.View.LevelUpOpen = true;
        kit.Play(0.1);
        var panel = kit.Window.View.LevelUp;
        var character = world.Hero.Character;

        void Pick(string title)
        {
            var (rect, _) = panel.Options.First(o => o.Title == title);
            Assert.True(rect.Left >= 0 && rect.Right <= 731 && rect.Bottom <= 946, $"{title} is drawn off the window: {rect}");
            kit.Click(new Point(rect.MidX, rect.MidY));
        }
        Assert.Equal(5, panel.Options.Count(o => o.Title.EndsWith(" Dedication")));
        Assert.DoesNotContain(panel.Options, o => o.Title == "Fighter Dedication");
        Pick("Wizard Dedication");
        Pick(character.FeatOptions(FeatType.Skill, 2).First().Name);
        kit.Capture(Shot("level-up-dedication"));
        Assert.Empty(panel.Missing(character, 2));
        kit.Click(new Point(panel.ConfirmButton.MidX, panel.ConfirmButton.MidY));
        Assert.Empty(character.PendingLevels);
        Assert.Equal("wizard", character.DedicationClass?.Id);

        kit.Window.View.LevelUpOpen = false;
        kit.Play(0.1);
        Assert.NotNull(world.SlotOf("force_bolt"));
        Assert.Equal(ResourceKind.Mana, world.Hero.Dedication?.Kind);
        var tip = Rendering.Hud.AbilityTip(world, world.Bar[world.SlotOf("force_bolt")!], "").Select(l => l.Text).ToList();
        Assert.Contains(tip, t => t.Contains("12 Mana"));
        kit.Capture(Shot("dedication-hud"));
    }
}
