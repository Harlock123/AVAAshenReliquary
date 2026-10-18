using Avalonia.Headless.XUnit;
using AshenReliquary.App.Services;

namespace AshenReliquary.App.Tests;

public class BattleDemoTests(ITestOutputHelper output)
{
    /// <summary>"--battle": left alone in the Fens for two minutes, the hero walks from pack to pack and kills them, and never falls.</summary>
    [AvaloniaTheory]
    [InlineData("fighter")]
    [InlineData("wizard")]
    public void The_demo_hero_clears_packs_and_never_dies(string classId)
    {
        var kit = TestKit.Open(classId, zoneId: "rotwater_fens");
        var world = kit.World;
        kit.Window.Demo = new BattleDemo(world, classId);
        var standing = world.Monsters.Count(m => !m.IsDead);
        var (startX, startY) = (world.Hero.X, world.Hero.Y);
        var fell = false;
        for (var t = 0; t < 240; t++)
        {
            kit.Play(0.5);
            fell |= world.Hero.IsDead;
        }
        var killed = standing - world.Monsters.Count(m => !m.IsDead);
        output.WriteLine($"{classId}: {killed} of {standing} killed, walked {Math.Sqrt((world.Hero.X - startX) * (world.Hero.X - startX) + (world.Hero.Y - startY) * (world.Hero.Y - startY)):0} tiles");
        Assert.True(killed >= 6, $"only {killed} monsters died");
        Assert.False(fell);
    }
}
