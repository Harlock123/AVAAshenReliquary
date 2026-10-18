using System.Text.Json;
using AshenReliquary.App.Audio;
using AshenReliquary.Core.World;

namespace AshenReliquary.App.Services;

/// <summary>
/// Decides what plays. Each frame it plays the world's sound events (a random variant from audio.json, quieter with
/// distance from the hero and panned by where they are on screen), footsteps while the hero runs, and the music:
/// the title theme off the road, exploration tracks on it, and the battle theme while monsters near the hero are
/// hunting (held for a few seconds after). An ambience loop sits under the music in zones.
/// </summary>
public sealed class AudioDirector
{
    public const double CombatRange = 16;
    public const double CombatHoldSeconds = 5;
    public const double FootstepSeconds = 0.34;
    public const double RepeatGuardSeconds = 0.06;

    private readonly IAudioEngine _engine;
    private readonly string _root;
    private readonly Dictionary<string, string[]> _effects;
    private readonly Dictionary<string, string[]> _music;
    private readonly Dictionary<string, string[]> _ambience;
    private readonly Dictionary<string, double> _lastPlayed = [];
    private readonly Random _random = new();
    private double _clock;
    private double _combatUntil = double.NegativeInfinity;
    private double _footstepIn;
    private string? _musicGroup;

    public AudioDirector(IAudioEngine engine, string? root = null)
    {
        _engine = engine;
        _root = root ?? Path.Combine(AppContext.BaseDirectory, "Assets", "Audio");
        var listing = File.Exists(Path.Combine(_root, "audio.json"))
            ? JsonDocument.Parse(File.ReadAllText(Path.Combine(_root, "audio.json"))).RootElement
            : default;
        _effects = Groups(listing, "effects");
        _music = Groups(listing, "music");
        _ambience = Groups(listing, "ambience");
        Apply(Settings.Load());
    }

    public IAudioEngine Engine => _engine;

    /// <summary>The music group now chosen ("title", "explore", "combat", "boss"), for tests and the F3 overlay.</summary>
    public string? MusicGroup => _musicGroup;

    public IReadOnlyDictionary<string, string[]> Effects => _effects;

    public void Apply(Settings settings)
    {
        _engine.SetVolumes(settings.Master, settings.Effects, settings.Music);
        _engine.SetAmbienceVolume(settings.Ambience);
    }

    /// <summary>A sound by its audio.json id, centred and at full volume (menus, the inventory).</summary>
    public void Play(string id, float gain = 1f, float pan = 0f)
    {
        if (!_effects.TryGetValue(id, out var files) || files.Length == 0)
            return;
        if (_lastPlayed.TryGetValue(id, out var last) && _clock - last < RepeatGuardSeconds)
            return;
        _lastPlayed[id] = _clock;
        _engine.PlayEffect(files[_random.Next(files.Length)], gain, pan);
    }

    /// <summary>Off the road: the title theme, no ambience.</summary>
    public void Title(double dt)
    {
        _clock += dt;
        SetMusic("title");
        _engine.StopAmbience();
    }

    public void Update(GameWorld world, double dt, bool paused)
    {
        _clock += dt;
        var hero = world.Hero;
        foreach (var sound in world.DrainSounds())
        {
            var distance = Math.Sqrt((sound.X - hero.X) * (sound.X - hero.X) + (sound.Y - hero.Y) * (sound.Y - hero.Y));
            var gain = (float)Math.Clamp(1.15 - distance / 18, 0.12, 1);
            // Screen x grows with (x - y) in the isometric view: that is left and right.
            var pan = (float)Math.Clamp(((sound.X - sound.Y) - (hero.X - hero.Y)) / 9, -1, 1);
            Play(IdFor(sound), gain, pan);
        }

        if (!paused && !hero.IsDead && hero.Animation == "run")
        {
            _footstepIn -= dt;
            if (_footstepIn <= 0)
            {
                Play("footstep", 0.3f);
                _footstepIn = FootstepSeconds;
            }
        }
        else
        {
            _footstepIn = 0.05;
        }

        var hunted = world.Monsters.Any(m => !m.IsDead && m.State is MonsterState.Chase or MonsterState.Attack && m.DistanceTo(hero) <= CombatRange);
        if (hunted)
            _combatUntil = _clock + CombatHoldSeconds;
        // A Herald fight has its own track while the boss is awake.
        SetMusic(world.Boss is not null && _music.ContainsKey("boss") ? "boss" : _clock < _combatUntil ? "combat" : "explore");
        if (_ambience.TryGetValue("road", out var road) && road.Length > 0 && _engine.CurrentAmbience != road[0])
            _engine.PlayAmbience(road[0]);
    }

    /// <summary>The audio.json effect id for a world sound.</summary>
    public static string IdFor(SoundEvent sound) => sound.Cue switch
    {
        SoundCue.Swing or SoundCue.Miss => "swing",
        SoundCue.Hit => "hit",
        SoundCue.Crit => "crit",
        SoundCue.HeroHurt => "hero-hurt",
        SoundCue.MonsterAggro => sound.Variant == "skeleton" ? "skeleton" : sound.Variant == "grave" ? "grave-rise" : "zombie",
        SoundCue.MonsterDeath => sound.Variant == "skeleton" ? "skeleton-death" : sound.Variant == "grave" ? "grave-rise" : "zombie-death",
        SoundCue.Bow => "bow",
        SoundCue.WitchCast => "witch-cast",
        SoundCue.GraveRise => "grave-rise",
        SoundCue.Ignition => "ignition",
        SoundCue.ForceBolt => "force-bolt",
        SoundCue.Impact => "impact",
        SoundCue.LootDrop => "loot-drop",
        SoundCue.Gold => "gold",
        SoundCue.Pickup => sound.Variant is "metal" or "jewel" ? $"pickup-{sound.Variant}" : "pickup-cloth",
        SoundCue.Potion => "potion",
        SoundCue.LevelUp => "level-up",
        SoundCue.Shrine => "shrine",
        SoundCue.Death => "death",
        SoundCue.ZoneComplete => "zone-complete",
        SoundCue.CacheOpen => "inventory",
        _ => "click",
    };

    /// <summary>Switches music group; within a group a new track is picked at random (the engine crossfades).</summary>
    private void SetMusic(string group)
    {
        if (group == _musicGroup && _engine.CurrentMusic is not null)
            return;
        _musicGroup = group;
        if (_music.TryGetValue(group, out var tracks) && tracks.Length > 0)
            _engine.PlayMusic(tracks[_random.Next(tracks.Length)], loop: true);
    }

    private Dictionary<string, string[]> Groups(JsonElement listing, string name)
    {
        var groups = new Dictionary<string, string[]>();
        if (listing.ValueKind != JsonValueKind.Object || !listing.TryGetProperty(name, out var element))
            return groups;
        foreach (var group in element.EnumerateObject())
            groups[group.Name] = group.Value.EnumerateArray().Select(f => Path.Combine(_root, f.GetString()!)).ToArray();
        return groups;
    }
}
