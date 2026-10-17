using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using AshenReliquary.App.Audio;
using AshenReliquary.App.Services;
using AshenReliquary.Core.World;

namespace AshenReliquary.App.Tests;

/// <summary>A silent engine that remembers what it was asked to play.</summary>
internal sealed class RecordingAudioEngine : NullAudioEngine
{
    public List<(string Path, float Gain, float Pan)> Effects { get; } = [];
    public List<string> Tracks { get; } = [];

    public override void PlayEffect(string path, float gain = 1f, float pan = 0f) => Effects.Add((path, gain, pan));

    public override void PlayMusic(string path, bool loop = true)
    {
        base.PlayMusic(path, loop);
        Tracks.Add(path);
    }
}

public class AudioTests
{
    private static readonly string Root = Path.Combine(AppContext.BaseDirectory, "Assets", "Audio");

    [Fact]
    public void Every_sound_the_game_can_ask_for_exists()
    {
        var director = new AudioDirector(new RecordingAudioEngine(), Root);
        var variants = new string?[] { null, "zombie", "skeleton", "grave", "cloth", "metal", "jewel" };
        foreach (var cue in Enum.GetValues<SoundCue>())
        foreach (var variant in variants)
        {
            var id = AudioDirector.IdFor(new SoundEvent(cue, 0, 0, variant));
            Assert.True(director.Effects.ContainsKey(id), $"{cue}/{variant} -> '{id}' is not in audio.json");
        }
        foreach (var id in new[] { "footstep", "inventory", "click" })
            Assert.True(director.Effects.ContainsKey(id), id);
        Assert.All(director.Effects.Values.SelectMany(f => f), f => Assert.True(File.Exists(f), f));
    }

    [Fact]
    public void Every_shipped_audio_file_decodes()
    {
        var files = Directory.GetFiles(Root, "*.ogg", SearchOption.AllDirectories);
        Assert.True(files.Length > 70);
        foreach (var file in files)
        {
            using var decoder = AudioDecoders.Open(file);
            Assert.True(decoder.SampleRate == 44100, file);
            var buffer = new short[4096];
            Assert.True(decoder.Read(buffer) > 0, file);
            if (file.Contains($"{Path.DirectorySeparatorChar}sfx{Path.DirectorySeparatorChar}"))
                Assert.True(decoder.Channels == 1, $"{file}: effects must be mono so they can be panned");
        }
    }

    [AvaloniaFact]
    public void Music_follows_the_title_the_road_and_combat()
    {
        var engine = new RecordingAudioEngine();
        var director = new AudioDirector(engine, Root);
        director.Title(0.1);
        Assert.Equal("title", director.MusicGroup);

        var kit = TestKit.Open();
        var world = kit.World;
        director.Update(world, 0.1, paused: false);
        Assert.Equal("explore", director.MusicGroup);
        Assert.Contains("explore", engine.Tracks.Last());
        Assert.NotNull(engine.CurrentAmbience);

        var zombie = world.Monsters.Where(m => m.Definition.Id == "fen_zombie").MinBy(m => m.DistanceTo(world.Hero))!;
        world.Attack(zombie);
        for (var t = 0.0; t < 3 && director.MusicGroup != "combat"; t += 1 / 30.0)
        {
            world.Update(1 / 30.0);
            director.Update(world, 1 / 30.0, false);
        }
        Assert.Equal("combat", director.MusicGroup);
        Assert.Contains("combat", engine.Tracks.Last());

        // Win the fight, then the battle music holds for a few seconds before the road theme returns.
        foreach (var m in world.Monsters.Where(m => m.DistanceTo(world.Hero) < 20).ToList())
            TestHooks.Slay(world, m);
        for (var t = 0.0; t < AudioDirector.CombatHoldSeconds - 1; t += 1 / 30.0)
            director.Update(world, 1 / 30.0, false);
        Assert.Equal("combat", director.MusicGroup);
        for (var t = 0.0; t < 2; t += 1 / 30.0)
            director.Update(world, 1 / 30.0, false);
        Assert.Equal("explore", director.MusicGroup);
    }

    [AvaloniaFact]
    public void Sounds_fade_with_distance_and_pan_with_position()
    {
        var engine = new RecordingAudioEngine();
        var director = new AudioDirector(engine, Root);
        var kit = TestKit.Open();
        var world = kit.World;
        var hero = world.Hero;
        TestHooks.Sound(world, new SoundEvent(SoundCue.Hit, hero.X, hero.Y));
        TestHooks.Sound(world, new SoundEvent(SoundCue.Impact, hero.X + 12, hero.Y - 12));
        director.Update(world, 0.1, false);
        var near = engine.Effects.Single(e => e.Path.Contains("hit-"));
        var far = engine.Effects.Single(e => e.Path.Contains("impact-"));
        Assert.True(near.Gain > far.Gain);
        Assert.Equal(0f, near.Pan, 3);
        Assert.Equal(1f, far.Pan, 3); // (x - y) grows to the right of the screen
    }

    [AvaloniaFact]
    public void Running_makes_footsteps()
    {
        var engine = new RecordingAudioEngine();
        var director = new AudioDirector(engine, Root);
        var kit = TestKit.Open();
        var world = kit.World;
        world.MoveTo(world.Hero.X - 4, world.Hero.Y - 4);
        for (var t = 0.0; t < 1.0; t += 1 / 30.0)
        {
            world.Update(1 / 30.0);
            director.Update(world, 1 / 30.0, false);
        }
        Assert.InRange(engine.Effects.Count(e => e.Path.Contains("footstep")), 2, 4);
    }

    [AvaloniaFact]
    public void Buttons_click()
    {
        var engine = new RecordingAudioEngine();
        SaveStore.FolderOverride = Path.Combine(Path.GetTempPath(), "ashen-tests", Guid.NewGuid().ToString("N"));
        var window = new Views.MainWindow { Width = 1288, Height = 770 };
        window.Audio = new AudioDirector(engine, Root);
        window.ShowStart();
        window.Show();
        window.CreationView.FindControl<Avalonia.Controls.Button>("RecommendButton")!
            .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Assert.Contains(engine.Effects, e => e.Path.Contains("click-"));
    }
}
