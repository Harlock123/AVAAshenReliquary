using System.Reflection;
using AshenReliquary.Core.Items;
using AshenReliquary.Core.World;

namespace AshenReliquary.App.Tests;

/// <summary>Test-only shortcuts into the world, for staging screenshots.</summary>
internal static class TestHooks
{
    private static readonly FieldInfo GroundField =
        typeof(GameWorld).GetField("_ground", BindingFlags.NonPublic | BindingFlags.Instance)!;

    /// <summary>Kills a monster through the normal death path.</summary>
    public static void Slay(GameWorld world, MonsterActor monster) =>
        typeof(GameWorld).GetMethod("Slay", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(world, [monster]);

    /// <summary>Queues a sound as if the world had made it.</summary>
    public static void Sound(GameWorld world, SoundEvent sound) =>
        ((List<SoundEvent>)typeof(GameWorld).GetField("_sounds", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(world)!).Add(sound);

    /// <summary>Awards XP as if a monster had died (levelling up as it goes).</summary>
    public static void GainXp(GameWorld world, int xp) =>
        typeof(GameWorld).GetMethod("GainXp", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(world, [xp, world.Hero]);

    /// <summary>Puts one of the hero's abilities on cooldown for <paramref name="seconds"/>, as if just used.</summary>
    public static void StartCooldown(GameWorld world, string abilityId, double seconds)
    {
        var readyAt = (Dictionary<string, double>)world.Hero.GetType().GetProperty("ReadyAt", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(world.Hero)!;
        readyAt[abilityId] = world.Time + seconds;
    }

    /// <summary>Marks a trap spotted, as a good Perception check would.</summary>
    public static void Reveal(GameWorld world, Trap trap) =>
        typeof(GameWorld).GetMethod("SetTrapState", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(world, [trap, TrapState.Found]);

    /// <summary>Moves the hero to a map point at once.</summary>
    public static void Place(GameWorld world, double x, double y)
    {
        typeof(Actor).GetProperty(nameof(Actor.X))!.SetValue(world.Hero, x);
        typeof(Actor).GetProperty(nameof(Actor.Y))!.SetValue(world.Hero, y);
    }

    /// <summary>Takes every drop off the ground (so a click finds what lies under the loot labels).</summary>
    public static void ClearGround(GameWorld world) => ((List<GroundItem>)GroundField.GetValue(world)!).Clear();

    /// <summary>Places drops on the ground around a point, as if a monster had died there.</summary>
    public static void Drop(GameWorld world, IEnumerable<LootDrop> drops, double x, double y)
    {
        var ground = (List<GroundItem>)GroundField.GetValue(world)!;
        var i = 0;
        foreach (var drop in drops)
        {
            ground.Add(new GroundItem(x + i % 2 * 0.9, y + i / 2 * 0.9, drop, world.Time));
            i++;
        }
    }
}
