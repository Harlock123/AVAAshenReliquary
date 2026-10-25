using System.Text.RegularExpressions;
using AshenReliquary.Core.Abilities;
using AshenReliquary.Core.Characters;
using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Creatures;
using AshenReliquary.Core.Items;
using AshenReliquary.Core.Progression;
using AshenReliquary.Core.Rules;

namespace AshenReliquary.Core.World;

/// <summary>A banner across the screen: a town's name on arrival, or a way on that isn't open yet.</summary>
public sealed record Banner(string Title, string? Subtitle, double At);

/// <summary>
/// The world in play: the hero, the zone they are in, its monsters and everything that happens between them. Zones
/// the hero has left are remembered (<see cref="ZoneMemory"/>) and restored on return. It integrates in fixed 60 Hz
/// steps, so frame rate never changes gameplay, and every roll comes from one seed.
/// </summary>
public sealed partial class GameWorld
{
    public const double StepSeconds = 1.0 / 60;

    /// <summary>Converts the rules' tiles a second (25 ft = 5) to this map's larger 192x96 tiles.</summary>
    public const double MapSpeedScale = 0.7;

    public const double MeleeReach = 1.1;
    /// <summary>
    /// A shrine wakes (refilling potions, and healing the first time) and becomes the respawn point when the hero comes
    /// this close. It was 1.6, a step onto the shrine itself, and play-testers walked past shrines a few tiles off the road.
    /// </summary>
    public const double ShrineRadius = 5.0;
    public const double RespawnSeconds = 3.0;
    public const double CorpseSeconds = 12.0;
    public const double PackAggroRadius = 6.0;
    public const double LeashDistance = 24.0;
    public const double FlourishLockoutSeconds = 3.0;
    /// <summary>Out of combat this long (no monster awake, no attack under way), the hero starts to recover HP.</summary>
    public const double RegenDelaySeconds = 4.0;
    /// <summary>Share of max HP recovered per second out of combat.</summary>
    public const double RegenShare = 0.05;

    /// <summary>How far around the hero the minimap reveals, in tiles.</summary>
    public const int RevealRadius = 9;
    public const double DashSpeed = 12.0;
    public const double ProjectileSpeed = 11.0;

    private readonly IReadOnlyDictionary<string, CreatureDefinition> _creatures;
    private readonly List<MonsterActor> _monsters = [];
    private readonly List<FloatingText> _texts = [];
    private readonly HashSet<string> _activatedShrines = [];
    private readonly List<Projectile> _projectiles = [];
    private readonly List<Impact> _impacts = [];
    private readonly List<GroundItem> _ground = [];
    private readonly LootGenerator? _loot;
    private readonly HashSet<int> _slain = [];
    private readonly List<SoundEvent> _sounds = [];
    private readonly List<Vendor> _vendors = [];
    private readonly int _seed;

    /// <summary>The sounds since the last call, oldest first; the app plays them and the list starts again.</summary>
    public IReadOnlyList<SoundEvent> DrainSounds()
    {
        var sounds = _sounds.ToList();
        _sounds.Clear();
        return sounds;
    }

    private void Sound(SoundCue cue, double x, double y, string? variant = null)
    {
        // Nobody drains the list in pure simulation (tests): keep it from growing forever.
        if (_sounds.Count < 256)
            _sounds.Add(new SoundEvent(cue, x, y, variant));
    }

    private void Sound(SoundCue cue, Actor at, string? variant = null) => Sound(cue, at.X, at.Y, variant);
    private bool[] _explored = [];
    private (int X, int Y) _lastRevealCell = (int.MinValue, int.MinValue);
    private int _nextSpawnIndex;

    /// <summary>Changes whenever more of the map is explored, so the minimap knows to redraw.</summary>
    public int ExploredVersion { get; private set; }

    public bool IsExplored(int x, int y) => Map.InBounds(x, y) && _explored.Length > 0 && _explored[y * Map.Width + x];

    /// <summary>The explored cells as a base64 bitset (row by row), for saving.</summary>
    public string ExploredBits()
    {
        var bytes = new byte[(_explored.Length + 7) / 8];
        for (var i = 0; i < _explored.Length; i++)
        {
            if (_explored[i])
                bytes[i / 8] |= (byte)(1 << (i % 8));
        }
        return Convert.ToBase64String(bytes);
    }

    private void RestoreExplored(string? bits)
    {
        if (string.IsNullOrEmpty(bits))
            return;
        var bytes = Convert.FromBase64String(bits);
        for (var i = 0; i < _explored.Length && i / 8 < bytes.Length; i++)
            _explored[i] = (bytes[i / 8] & (1 << (i % 8))) != 0;
        ExploredVersion++;
        _lastRevealCell = (int.MinValue, int.MinValue);
    }

    /// <summary>
    /// Marks the cells within <see cref="RevealRadius"/> of the hero as explored (only when the hero changes cell). In a
    /// dark zone (a dungeon) only what the hero can see is explored: the cells in line of sight, and the walls bordering
    /// them, so the minimap draws the rooms looked into and not the rock and rooms behind their walls.
    /// </summary>
    private void Reveal()
    {
        var cell = ((int)Math.Floor(Hero.X), (int)Math.Floor(Hero.Y));
        if (cell == _lastRevealCell)
            return;
        _lastRevealCell = cell;
        var changed = false;
        void Mark(int x, int y)
        {
            if (!Map.InBounds(x, y) || _explored[y * Map.Width + x])
                return;
            _explored[y * Map.Width + x] = true;
            changed = true;
        }
        for (var dy = -RevealRadius; dy <= RevealRadius; dy++)
        for (var dx = -RevealRadius; dx <= RevealRadius; dx++)
        {
            if (dx * dx + dy * dy > RevealRadius * RevealRadius)
                continue;
            int x = cell.Item1 + dx, y = cell.Item2 + dy;
            if (!Zone.IsDark)
            {
                Mark(x, y);
                continue;
            }
            if (Map.BlocksSight(x, y) || !Map.HasLineOfSight(Hero.X, Hero.Y, x + 0.5, y + 0.5))
                continue;
            for (var ny = y - 1; ny <= y + 1; ny++)
            for (var nx = x - 1; nx <= x + 1; nx++)
            {
                if (nx == x && ny == y || Map.BlocksSight(nx, ny))
                    Mark(nx, ny);
            }
        }
        if (changed)
            ExploredVersion++;
    }

    /// <summary>The placed monsters killed so far (their <see cref="MonsterActor.SpawnIndex"/>), for saving.</summary>
    public IReadOnlyCollection<int> Slain => _slain;

    /// <summary>Raised when a shrine is touched for the first time or becomes the respawn point again: an autosave moment.</summary>
    public event Action<ShrineDefinition>? ShrineActivated;
    private double _accumulator;

    /// <param name="zones">Every zone an exit or a Rift Shrine can lead to; without it exits only complete the zone.</param>
    public GameWorld(ZoneDefinition zone, IReadOnlyDictionary<string, CreatureDefinition> creatures, Character hero, int seed,
        IReadOnlyDictionary<string, AbilityDefinition>? bar = null, LootGenerator? loot = null,
        IReadOnlyDictionary<string, ZoneDefinition>? zones = null, IReadOnlyDictionary<string, DialogueDefinition>? dialogues = null,
        IReadOnlyDictionary<string, AbilityDefinition>? abilities = null)
    {
        _loot = loot;
        _dialogues = dialogues ?? _dialogues;
        _classBar = bar ?? new Dictionary<string, AbilityDefinition>();
        _abilities = abilities ?? new Dictionary<string, AbilityDefinition>();
        _creatures = creatures;
        _zones = zones ?? new Dictionary<string, ZoneDefinition> { [zone.Id] = zone };
        Random = new SeededRandom(seed);
        _seed = seed;
        Hero = new HeroActor(hero) { X = zone.PlayerStart[0] + 0.5, Y = zone.PlayerStart[1] + 0.5 };
        Experience = new Experience();
        _startZone = zone.Id;
        RefreshBar();
        Populate(zone, null);
        RefreshCompanion();
        RespawnPoint = (Hero.X, Hero.Y);
        hero.HeroPoints.GainForZone();
    }

    private readonly IReadOnlyDictionary<string, ZoneDefinition> _zones;
    private readonly Dictionary<string, ZoneMemory> _memories = [];

    public ZoneDefinition Zone { get; private set; } = null!;
    public TileMap Map { get; private set; } = null!;

    /// <summary>The zones visited and left, as they were left (for saving).</summary>
    public IReadOnlyDictionary<string, ZoneMemory> Memories => _memories;

    /// <summary>Raised on arriving in another zone: an autosave moment.</summary>
    public event Action<ZoneDefinition>? ZoneEntered;

    /// <summary>
    /// Sets up a zone: its map, traders, chests and monsters, then what <paramref name="memory"/> says of an earlier
    /// visit (kills, open chests, the explored map, loot left lying, the traders' tables), if it is for this layout.
    /// </summary>
    private void Populate(ZoneDefinition zone, ZoneMemory? memory)
    {
        Zone = zone;
        Map = new TileMap(zone);
        _monsters.Clear();
        _projectiles.Clear();
        _monsterShots.Clear();
        _impacts.Clear();
        _ground.Clear();
        _vendors.Clear();
        _caches.Clear();
        _slain.Clear();
        _nextSpawnIndex = 0;
        _explored = new bool[Map.Width * Map.Height];
        _lastRevealCell = (int.MinValue, int.MinValue);
        ExploredVersion++;
        ZoneComplete = false;
        OpenVendor = null;
        OpenShrine = null;
        _insideExit = null;
        foreach (var vendor in zone.Vendors ?? [])
        {
            var v = new Vendor(vendor);
            _vendors.Add(v);
            Restock(v);
        }
        _caches.AddRange((zone.Caches ?? []).Select(c => new Cache(c)));
        _hazards.Clear();
        _flooded.Clear();
        PopulateInteractives(zone);
        PopulateTraps(zone);

        var packId = 0;
        foreach (var spawn in zone.Spawns)
        {
            packId++;
            foreach (var member in spawn.Pack)
            {
                for (var i = 0; i < member.Count; i++)
                    SpawnNear(member.Creature, member.Rank, spawn.At[0] + 0.5, spawn.At[1] + 0.5, spawn.Radius, packId, spawn.Level, member.Affixes)
                        .SpawnIndex = _nextSpawnIndex++;
            }
        }

        if (memory is null || memory.Revision != zone.Revision)
            return;
        RestoreExplored(memory.Explored);
        foreach (var index in memory.Slain)
            _slain.Add(index);
        _monsters.RemoveAll(m => m.SpawnIndex >= 0 && _slain.Contains(m.SpawnIndex));
        foreach (var cache in _caches.Where(c => memory.OpenedCaches.Contains(c.Definition.Id)))
            cache.Opened = true;
        _ground.AddRange(memory.Ground);
        ZoneComplete = memory.Complete;
        foreach (var vendor in memory.Vendors)
            RestoreVendor(vendor.Id, vendor.StockedForLevel, vendor.Stock);
    }

    /// <summary>What this zone would remember if the hero left now.</summary>
    private ZoneMemory Remember() => new()
    {
        Revision = Zone.Revision,
        Slain = _slain.ToList(),
        OpenedCaches = _caches.Where(c => c.Opened).Select(c => c.Definition.Id).ToList(),
        Explored = ExploredBits(),
        Ground = _ground.ToList(),
        Complete = ZoneComplete,
        Vendors = _vendors.Select(v => new VendorMemory(v.Definition.Id, v.StockedForLevel, v.Stock.ToList())).ToList(),
    };

    /// <summary>Puts back a zone left earlier (loading a save); a zone remade since is forgotten.</summary>
    internal void RestoreMemory(string zoneId, ZoneMemory memory)
    {
        if (_zones.TryGetValue(zoneId, out var zone) && zone.Revision == memory.Revision && zoneId != Zone.Id)
            _memories[zoneId] = memory;
    }

    /// <summary>
    /// Leaves for another zone, arriving at (<paramref name="x"/>, <paramref name="y"/>). The zone left is remembered;
    /// the first visit to a zone gives a Hero Point (and a warning if it is 3 or more levels above the hero); a town
    /// shows its banner.
    /// </summary>
    private void Travel(ZoneDefinition zone, double x, double y)
    {
        _memories[Zone.Id] = Remember();
        var first = !_memories.Remove(zone.Id, out var memory);
        ClearRogueState();
        Populate(zone, memory);
        Hero.X = x;
        Hero.Y = y;
        PlaceAllies();
        Hero.Order = null;
        Hero.Action = null;
        Hero.Goal = null;
        Hero.Path.Clear();
        Hero.PickupTarget = null;
        Hero.TalkTarget = null;
        Hero.CacheTarget = null;
        Hero.CorpseTarget = null;
        Hero.TrapTarget = null;
        Hero.ShrineTarget = null;
        Hero.InteractTarget = null;
        RespawnPoint = (x, y);
        _insideExit = zone.Exits.FirstOrDefault(e => Hero.DistanceTo(e.At[0] + 0.5, e.At[1] + 0.5) <= e.Radius);
        string? warning = null;
        if (first && zone.Id != _startZone)
        {
            Hero.Character.HeroPoints.GainForZone();
            if (zone.Level - Hero.Character.Level >= 3)
                warning = $"{zone.Name} is a level {zone.Level} area. Tread carefully.";
        }
        // A town's banner shows on every arrival; a zone with a tagline shows it the first time it is entered.
        if (zone.Town || (first && zone.Tagline is not null))
            Arrival = new Banner(zone.Name, zone.Tagline, Time);
        if (warning is not null || !zone.Town)
            Say(warning ?? $"You enter {zone.Name}.");
        ZoneEntered?.Invoke(zone);
    }

    private string _startZone = "";

    /// <summary>Goes through an exit to the zone and exit it names, if that zone is known.</summary>
    private bool TravelThrough(ExitDefinition exit)
    {
        if (exit.To is null || !_zones.TryGetValue(exit.To, out var zone))
            return false;
        var arrival = zone.Exits.FirstOrDefault(e => e.Id == exit.ToExit);
        var at = arrival?.Arrive ?? arrival?.At ?? zone.PlayerStart;
        Travel(zone, at[0] + 0.5, at[1] + 0.5);
        return true;
    }

    // ----- The Step dodge ------------------------------------------------------------------

    /// <summary>The Step dodge (spec, section 10): 2 tiles in a quarter of a second, untouchable meanwhile, every 4 s.</summary>
    public const double DodgeTiles = 2.0;
    public const double DodgeSeconds = 0.25;
    public const double DodgeCooldown = 4.0;

    public double DodgeCooldownRemaining => Math.Max(0, Hero.DodgeReadyAt - Time);

    /// <summary>Steps toward a point, dropping whatever the hero was doing. False on cooldown, unable to move, or dead.</summary>
    public bool Dodge(double towardX, double towardY)
    {
        if (Hero.IsDead || !Hero.Character.Conditions.CanMove || Time < Hero.DodgeReadyAt || Hero.DodgeRemaining > 0 || HeldBySong())
            return false;
        var (dx, dy) = (towardX - Hero.X, towardY - Hero.Y);
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length < 1e-6)
            return false;
        Hero.Order = null;
        Hero.Action = null;
        Hero.Goal = null;
        Hero.Path.Clear();
        Hero.DodgeDirection = (dx / length, dy / length);
        Hero.DodgeRemaining = DodgeSeconds;
        Hero.DodgeSpeed = DodgeTiles / DodgeSeconds;
        Hero.DodgeFeinted = null;
        Hero.InvulnerableUntil = Time + DodgeSeconds;
        Hero.DodgeReadyAt = Time + DodgeCooldown;
        Hero.Face(towardX, towardY);
        Hero.Play("run", rate: 2.0, restart: true);
        Sound(SoundCue.Swing, Hero);
        return true;
    }

    private void AdvanceDodge(double dt)
    {
        var step = Math.Min(dt, Hero.DodgeRemaining);
        Hero.DodgeRemaining -= step;
        var distance = Hero.DodgeSpeed * step;
        var (nx, ny) = (Hero.X + Hero.DodgeDirection.X * distance, Hero.Y + Hero.DodgeDirection.Y * distance);
        // Stopped by a wall or a tree, or by two blocked corners it would slip between (as walking never does: a roll
        // once left a gunslinger in a pocket of trees that no path led out of).
        if (Map.IsWalkable(nx, ny) && (Math.Floor(nx) == Math.Floor(Hero.X) || Math.Floor(ny) == Math.Floor(Hero.Y)
                || Map.IsWalkable(nx, Hero.Y) && Map.IsWalkable(Hero.X, ny)))
            (Hero.X, Hero.Y) = (nx, ny);
        else
            Hero.DodgeRemaining = 0;
        FeintPassing();
        if (Hero.DodgeRemaining <= 0)
            Hero.Play("stance");
    }

    // ----- Rift Shrines ------------------------------------------------------------------

    /// <summary>The shrine whose Rift is open (its travel list on screen), or null.</summary>
    public ShrineDefinition? OpenShrine { get; private set; }

    /// <summary>A shrine opens its Rift when the hero stands this close to it.</summary>
    public const double RiftRadius = 2.0;

    /// <summary>Walk to a woken shrine (a click on it) and open its Rift: travel to any other woken shrine, in any zone.</summary>
    public void UseShrine(ShrineDefinition shrine)
    {
        if (Hero.IsDead || !Zone.Shrines.Contains(shrine))
            return;
        Hero.Order = null;
        Hero.PickupTarget = null;
        Hero.TalkTarget = null;
        Hero.CacheTarget = null;
        Hero.CorpseTarget = null;
        Hero.TrapTarget = null;
        Hero.ShrineTarget = null;
        Hero.InteractTarget = null;
        if (!_activatedShrines.Contains(shrine.Id))
        {
            Hero.ShrineTarget = shrine;
            var (sx, sy) = ShrineRespawnPoint(shrine);
            Hero.Goal = (sx, sy);
            PlanPath(Hero, sx, sy);
            return;
        }
        if (ShrineDistance(shrine) <= RiftRadius)
        {
            OpenRift(shrine);
            return;
        }
        Hero.ShrineTarget = shrine;
        var (fx, fy) = ShrineRespawnPoint(shrine);
        Hero.Goal = (fx, fy);
        PlanPath(Hero, fx, fy);
    }

    private double ShrineDistance(ShrineDefinition shrine) => Hero.DistanceTo(shrine.At[0] + 0.5, shrine.At[1] + 0.5);

    private void OpenRift(ShrineDefinition shrine)
    {
        Hero.ShrineTarget = null;
        Hero.InteractTarget = null;
        Hero.Goal = null;
        Hero.Path.Clear();
        OpenShrine = shrine;
    }

    public void CloseRift() => OpenShrine = null;

    /// <summary>Every woken shrine the open Rift can reach: (zone, shrine), this zone's first, then by zone order.</summary>
    public IReadOnlyList<(ZoneDefinition Zone, ShrineDefinition Shrine)> RiftDestinations =>
        new[] { Zone }.Concat(_zones.Values.Where(z => z.Id != Zone.Id).OrderBy(z => z.Act).ThenBy(z => z.Level))
            .SelectMany(z => z.Shrines.Where(s => _activatedShrines.Contains(s.Id) && s != OpenShrine).Select(s => (z, s)))
            .ToList();

    /// <summary>Steps through the open Rift to a woken shrine, in this zone or another.</summary>
    public bool RiftTravel(string shrineId)
    {
        if (OpenShrine is null || Hero.IsDead)
            return false;
        var target = RiftDestinations.FirstOrDefault(d => d.Shrine.Id == shrineId);
        if (target.Shrine is null)
            return false;
        var (x, y) = ShrineRespawnPoint(target.Shrine);
        OpenShrine = null;
        if (target.Zone.Id == Zone.Id)
        {
            Hero.X = x;
            Hero.Y = y;
            Hero.Goal = null;
            Hero.Path.Clear();
            RespawnPoint = (x, y);
        }
        else
        {
            Travel(target.Zone, x, y);
        }
        Say($"The Rift carries you to {target.Shrine.Name}.");
        Sound(SoundCue.Shrine, Hero);
        return true;
    }
    public IRandomSource Random { get; }
    public HeroActor Hero { get; }
    public Experience Experience { get; }
    public IReadOnlyList<MonsterActor> Monsters => _monsters;
    public IReadOnlyList<FloatingText> Texts => _texts;
    public IReadOnlyList<Projectile> Projectiles => _projectiles;
    public IReadOnlyList<Impact> Impacts => _impacts;
    public IReadOnlyList<GroundItem> GroundItems => _ground;
    public IReadOnlyList<Vendor> Vendors => _vendors;
    public IReadOnlyList<Cache> Caches => _caches;

    /// <summary>The trader whose wares are open, or null. Walking away (or dying) closes it.</summary>
    public Vendor? OpenVendor { get; private set; }

    public const double PickupRadius = 1.0;
    public const double GoldMagnetRadius = 1.3;
    public double Time { get; private set; }
    public (double X, double Y) RespawnPoint { get; private set; }
    public bool ZoneComplete { get; private set; }

    /// <summary>The last banner and when it went up (a town walked into); null until there is one this session.</summary>
    public Banner? Arrival { get; private set; }
    private ExitDefinition? _insideExit;
    public string? LastMessage { get; private set; }
    public double LastMessageAt { get; private set; } = double.NegativeInfinity;
    public IReadOnlyCollection<string> ActivatedShrines => _activatedShrines;

    // ----- Commands from the player ------------------------------------------------

    /// <summary>Makes a level's picks (the level-up screen's Confirm); new abilities go on the bar.</summary>
    public void ChooseLevel(int level, Characters.LevelChoice choice)
    {
        Hero.Character.Choose(level, choice);
        RefreshBar();
        Say($"Level {level}: your choices are made.");
    }

    /// <summary>Takes the subclass a save from before subclasses still lacks (the level-up screen); its ability goes on the bar.</summary>
    public void ChooseSubclass(string id)
    {
        Hero.Character.ChooseSubclass(id);
        RefreshBar();
        Say($"You are a {Hero.Character.Subclass!.Name}.");
    }


    /// <summary>
    /// Walk to a point (a left click on the ground). Cancels any attack order. While the Taint's whispers hold the hero,
    /// their feet go the other way.
    /// </summary>
    public void MoveTo(double x, double y)
    {
        if (Hero.IsDead || HeldBySong())
            return;
        if (Hero.Character.Taint.ReversedMovementRemaining > 0)
            (x, y) = (2 * Hero.X - x, 2 * Hero.Y - y);
        Hero.Order = null;
        Hero.PickupTarget = null;
        Hero.TalkTarget = null;
        Hero.CacheTarget = null;
        Hero.CorpseTarget = null;
        Hero.TrapTarget = null;
        Hero.ShrineTarget = null;
        Hero.InteractTarget = null;
        Hero.Goal = (x, y);
        PlanPath(Hero, x, y);
    }

    /// <summary>
    /// A left click on a monster: the "lmb" slot's ability (walk into range, then keep attacking until it dies), or the
    /// basic attack when that slot is empty.
    /// </summary>
    public void Attack(MonsterActor monster) => Use(Bar.GetValueOrDefault("lmb") ?? BasicAbility, monster);

    /// <summary>
    /// Uses the ability in a bar slot on a monster (right click, keys 1 to 6); <paramref name="toward"/> is the point a
    /// Tumble rolls to (the pointer). False when the slot is empty.
    /// </summary>
    public bool UseSlot(string slot, MonsterActor? target, (double X, double Y)? toward = null)
    {
        if (!Bar.TryGetValue(slot, out var ability))
            return false;
        Use(ability, target, toward);
        return true;
    }

    private void Use(AbilityDefinition ability, MonsterActor? target, (double X, double Y)? toward = null)
    {
        if (Hero.IsDead || HeldBySong())
            return;
        if (ability.IsSelf)
        {
            UseOnSelf(ability, toward ?? (target is not null ? (target.X, target.Y) : null));
            return;
        }
        // Corpse Explosion (a blast) may be aimed at a rising corpse: it blows the body apart before it stands.
        if (target is null || (target.IsDead && !(target.IsRising && ability.Kind == AbilityKind.Blast)))
        {
            Say($"{ability.Name} needs a target.");
            return;
        }
        if (ability.Kind == AbilityKind.Mark)
        {
            Mark(ability, target);
            return;
        }
        if (ability.Kind == AbilityKind.Command)
        {
            SendCompanion(ability, target);
            return;
        }
        Hero.Order = new HeroOrder(ability, target);
        Hero.PickupTarget = null;
        Hero.TalkTarget = null;
        Hero.CacheTarget = null;
        Hero.CorpseTarget = null;
        Hero.TrapTarget = null;
        Hero.ShrineTarget = null;
        Hero.InteractTarget = null;
        Hero.Goal = null;
        Hero.Path.Clear();
    }

    /// <summary>Walk to a piece of loot and pick it up (a click on its label).</summary>
    public void PickUp(GroundItem item)
    {
        if (Hero.IsDead || !_ground.Contains(item) || HeldBySong())
            return;
        Hero.Order = null;
        Hero.TalkTarget = null;
        Hero.CacheTarget = null;
        Hero.CorpseTarget = null;
        Hero.TrapTarget = null;
        Hero.ShrineTarget = null;
        Hero.InteractTarget = null;
        Hero.PickupTarget = item;
        Hero.Goal = (item.X, item.Y);
        PlanPath(Hero, item.X, item.Y);
    }

    /// <summary>Drops an item from the bag at the hero's feet (never a key or a locked item).</summary>
    public bool DropFromBag(int index)
    {
        if (Hero.Equipment.Bag[index] is { Definition.Category: ItemCategory.Key } kept)
        {
            Say($"You keep the {kept.Name}.");
            return false;
        }
        if (Hero.Equipment.Bag[index] is { Mark: ItemMark.Locked } locked)
        {
            Say($"{locked.Name} is locked. Ctrl-click it to unlock it.");
            return false;
        }
        if (Hero.Equipment.TakeFromBag(index) is not { } item)
            return false;
        _ground.Add(new GroundItem(Hero.X + 0.4, Hero.Y + 0.4, new LootDrop(item), Time));
        return true;
    }

    // ----- Caches --------------------------------------------------------------------

    private readonly List<Cache> _caches = [];

    /// <summary>Walk to a chest (a click on it) and open it on arrival. An opened one just says so.</summary>
    public void OpenCache(Cache cache)
    {
        if (Hero.IsDead || !_caches.Contains(cache))
            return;
        Hero.Order = null;
        Hero.PickupTarget = null;
        Hero.TalkTarget = null;
        if (cache.Opened)
        {
            Say($"The {cache.Name.ToLowerInvariant()} is empty.");
            return;
        }
        if (cache.DistanceTo(Hero.X, Hero.Y) <= Cache.OpenRadius)
        {
            Loot(cache);
            return;
        }
        Hero.CacheTarget = cache;
        var (fx, fy) = InFrontOf(cache.X, cache.Y);
        Hero.Goal = (fx, fy);
        PlanPath(Hero, fx, fy);
    }

    /// <summary>Opens a chest: its loot, rolled at the hero's level (never below the zone's), spills around it.</summary>
    private void Loot(Cache cache)
    {
        Hero.CacheTarget = null;
        Hero.CorpseTarget = null;
        Hero.TrapTarget = null;
        Hero.ShrineTarget = null;
        Hero.InteractTarget = null;
        Hero.Goal = null;
        Hero.Path.Clear();
        Hero.Face(cache.X, cache.Y);
        cache.Opened = true;
        Sound(SoundCue.CacheOpen, cache.X, cache.Y);
        Say($"You open the {cache.Name.ToLowerInvariant()}.");
        if (_loot is null)
            return;
        var drops = _loot.RollCache(cache.Definition.Kind, Math.Max(Hero.Character.Level, Zone.Level), Random, Zone.Act);
        Spill(cache.X, cache.Y, drops);
        Sound(SoundCue.LootDrop, cache.X, cache.Y);
    }

    // ----- Trading -------------------------------------------------------------------

    /// <summary>Walk to a trader (a click on them) and open their wares on arrival.</summary>
    public void TalkTo(Vendor vendor)
    {
        if (Hero.IsDead || !_vendors.Contains(vendor))
            return;
        Hero.Order = null;
        Hero.PickupTarget = null;
        Hero.CacheTarget = null;
        Hero.CorpseTarget = null;
        Hero.TrapTarget = null;
        Hero.ShrineTarget = null;
        Hero.InteractTarget = null;
        if (vendor.DistanceTo(Hero.X, Hero.Y) <= Vendor.TalkRadius)
        {
            Open(vendor);
            return;
        }
        Hero.TalkTarget = vendor;
        var (fx, fy) = InFrontOf(vendor.X, vendor.Y);
        Hero.Goal = (fx, fy);
        PlanPath(Hero, fx, fy);
    }

    /// <summary>The walkable spot next to something standing on its own tile closest to the hero, so the walk ends facing it.</summary>
    private (double X, double Y) InFrontOf(double ox, double oy)
    {
        var best = (ox + 1, oy + 1);
        var bestDistance = double.MaxValue;
        for (var dy = -1; dy <= 1; dy++)
        for (var dx = -1; dx <= 1; dx++)
        {
            var (x, y) = (ox + dx, oy + dy);
            if ((dx == 0 && dy == 0) || !Map.IsWalkable(x, y))
                continue;
            var d = Hero.DistanceTo(x, y);
            if (d < bestDistance)
                (best, bestDistance) = ((x, y), d);
        }
        return best;
    }

    private void Open(Vendor vendor)
    {
        Hero.TalkTarget = null;
        Hero.CacheTarget = null;
        Hero.CorpseTarget = null;
        Hero.TrapTarget = null;
        Hero.ShrineTarget = null;
        Hero.InteractTarget = null;
        Hero.Goal = null;
        Hero.Path.Clear();
        if (OpenVendor == vendor)
            return;
        OpenVendor = vendor;
        vendor.ClearBuyback();
        Hero.Face(vendor.X, vendor.Y);
        Say($"{vendor.Definition.Name}: \"{TermsOf(vendor)?.Greeting ?? vendor.Definition.Greeting}\"");
    }

    /// <summary>The terms <paramref name="vendor"/> deals on as the world's flags stand: the first that holds, or none.</summary>
    public VendorTerms? TermsOf(Vendor vendor) => vendor.Definition.Terms?.FirstOrDefault(t => Holds(t.Requires));

    /// <summary>The open trader's price scale (1 at list price, or with no trader open).</summary>
    private double TradeScale => OpenVendor is { } vendor ? TermsOf(vendor)?.PriceScale ?? 1 : 1;

    /// <summary>What the open trader asks for <paramref name="item"/>, on its terms.</summary>
    public int BuyPrice(Item item) => Pricing.BuyPrice(item, Hero.Character, TradeScale);

    /// <summary>What one of a belt item costs at the open trader.</summary>
    public int SupplyPrice(ItemDefinition item) => Pricing.SupplyPrice(item, Hero.Character, TradeScale);

    /// <summary>What a potion charge costs at the open trader.</summary>
    public int PotionPrice => Pricing.PotionPrice(Hero.Character, TradeScale);

    /// <summary>Raised when the hero leaves a trader (Esc or walking away): an autosave moment.</summary>
    public event Action<Vendor>? TradeClosed;

    public void CloseVendor()
    {
        if (OpenVendor is not { } vendor)
            return;
        OpenVendor = null;
        TradeClosed?.Invoke(vendor);
    }

    /// <summary>Buys item <paramref name="index"/> of the open trader's stock into the bag.</summary>
    public bool Buy(int index)
    {
        if (OpenVendor is not { } vendor || index < 0 || index >= vendor.Stock.Count)
            return false;
        var item = vendor.Stock[index];
        var cost = BuyPrice(item);
        if (!Pay(cost) || !GiveOrRefund(item, cost))
            return false;
        vendor.Take(item);
        Say($"Bought {item.Name}.");
        return true;
    }

    /// <summary>Buys back something sold this visit, for what the trader paid.</summary>
    public bool BuyBack(int index)
    {
        if (OpenVendor is not { } vendor || index < 0 || index >= vendor.Buyback.Count)
            return false;
        var item = vendor.Buyback[index];
        if (!Pay(Pricing.SellPrice(item)) || !GiveOrRefund(item, Pricing.SellPrice(item)))
            return false;
        vendor.Unsold(item);
        Say($"Bought back {item.Name}.");
        return true;
    }

    /// <summary>Sells bag item <paramref name="index"/> to the open trader.</summary>
    public bool Sell(int index)
    {
        if (OpenVendor is null || Hero.Equipment.Bag[index] is not { } kept)
            return false;
        if (kept.Definition.Category == ItemCategory.Key)
        {
            Say($"You keep the {kept.Name}.");
            return false;
        }
        if (kept.Mark == ItemMark.Locked)
        {
            Say($"{kept.Name} is locked. Ctrl-click it to unlock it.");
            return false;
        }
        if (OpenVendor is not { } vendor || Hero.Equipment.TakeFromBag(index) is not { } item)
            return false;
        var price = Pricing.SellPrice(item);
        Hero.Equipment.AddGold(price);
        vendor.Sold(item);
        Sound(SoundCue.Gold, Hero);
        Say($"Sold {item.Name} for {price} gold.");
        return true;
    }

    /// <summary>Buys one healing draught from the open trader: into an empty flask, else the draught case.</summary>
    public bool BuyPotion()
    {
        if (OpenVendor is null)
            return false;
        if (Hero.Character.Potion.Full)
        {
            Say("Your flasks and your draught case are full.");
            return false;
        }
        if (!Pay(PotionPrice))
            return false;
        var toCase = Hero.Character.Potion.Charges >= HealingPotion.MaxCharges;
        Hero.Character.Potion.AddCharge();
        Sound(SoundCue.Potion, Hero);
        Say(toCase ? "Bought a healing draught for the case." : "Bought a healing draught.");
        return true;
    }

    /// <summary>The open trader's supplies (belt items always in stock), for the trade panel.</summary>
    public IReadOnlyList<ItemDefinition> Supplies =>
        OpenVendor?.Definition.Supplies is { } ids && _loot is not null
            ? ids.Where(_loot.Items.ContainsKey).Select(id => _loot.Items[id]).ToList()
            : [];

    /// <summary>Buys one of a belt item from the open trader's supplies.</summary>
    public bool BuySupply(ItemDefinition item)
    {
        if (OpenVendor is null || !Supplies.Contains(item))
            return false;
        var price = SupplyPrice(item);
        if (!Pay(price) || !GiveOrRefund(new Item(item, Rarity.Common, Hero.Character.Level), price))
            return false;
        Say($"Bought {item.Name.ToLowerInvariant()}.");
        return true;
    }

    /// <summary>Whether <paramref name="item"/> is the hero's: in the bag or worn.</summary>
    public bool Owns(Item item) => Hero.Equipment.Bag.Contains(item) || Hero.Equipment.Worn.Values.Contains(item);

    /// <summary>Has the open smith etch the next rank of <paramref name="kind"/> onto one of the hero's items.</summary>
    public bool Etch(Item item, RuneKind kind)
    {
        if (OpenVendor is not { Definition.Runework: true } || !Owns(item))
            return false;
        if (Runework.CannotEtch(item, kind) is { } reason)
        {
            Say(reason);
            return false;
        }
        if (!Pay(Runework.EtchPrice(item, kind, Hero.Character)))
            return false;
        Runework.Etch(item, kind);
        Hero.Equipment.Refresh();
        Sound(SoundCue.Pickup, Hero, "metal");
        Say($"Etched: {item.Name}.");
        return true;
    }

    /// <summary>
    /// Has the open smith move <paramref name="from"/>'s <paramref name="kind"/> rune onto <paramref name="to"/>
    /// (both the hero's); the weaker rune <paramref name="to"/> held goes back onto <paramref name="from"/>.
    /// </summary>
    public bool MoveRune(Item from, Item to, RuneKind kind)
    {
        if (OpenVendor is not { Definition.Runework: true } || !Owns(from) || !Owns(to))
            return false;
        if (Runework.CannotMove(from, to, kind) is { } reason)
        {
            Say(reason);
            return false;
        }
        if (!Pay(Runework.MovePrice(from, kind, Hero.Character)))
            return false;
        Runework.Move(from, to, kind);
        Hero.Equipment.Refresh();
        Sound(SoundCue.Pickup, Hero, "metal");
        Say($"Moved the {Runework.Name(kind)} rune: {to.Name}.");
        return true;
    }

    private bool Pay(int price)
    {
        if (!Hero.Equipment.TrySpend(price))
        {
            Say($"You need {price} gold.");
            return false;
        }
        Sound(SoundCue.Gold, Hero);
        return true;
    }

    private bool GiveOrRefund(Item item, int price)
    {
        if (Hero.Equipment.TryAdd(item))
            return true;
        Hero.Equipment.AddGold(price);
        Say("Your bag is full.");
        return false;
    }

    /// <summary>
    /// Rolls a trader's wares for the hero's level: two thirds common, the rest uncommon, at the zone's level or the
    /// hero's, whichever is higher. Its own dice (from the world seed, the trader and the level), so stocking a
    /// table never changes a fight's rolls.
    /// </summary>
    private void Restock(Vendor vendor)
    {
        var level = Hero.Character.Level;
        vendor.StockedForLevel = level;
        if (_loot is null)
        {
            vendor.SetStock([]);
            return;
        }
        var random = new SeededRandom(unchecked(_seed * 31 + StableHash(vendor.Definition.Id) * 17 + level));
        var itemLevel = Math.Max(Zone.Level, level);
        var count = vendor.Definition.StockSize;
        var uncommon = count / 3;
        // Terms with rares (the Brothers' fence for one of their own) put that many on the table in place of commons.
        var rares = Math.Min(TermsOf(vendor)?.Rares ?? 0, count - uncommon);
        var items = Enumerable.Range(0, count)
            .Select(i => _loot.Generate(itemLevel, i < rares ? Rarity.Rare : i < count - uncommon ? Rarity.Common : Rarity.Uncommon,
                random, vendor.Definition.Stocks, Zone.Act))
            .OrderBy(i => i.Definition.Slot)
            .ThenBy(i => i.Rarity)
            .ToList();
        vendor.SetStock(items);
    }

    private static int StableHash(string text)
    {
        unchecked
        {
            var hash = 17;
            foreach (var ch in text)
                hash = hash * 31 + ch;
            return hash;
        }
    }

    public void Stop()
    {
        Hero.Order = null;
        Hero.Goal = null;
        Hero.Path.Clear();
    }

    /// <summary>Seconds until <paramref name="ability"/> can be used again (cooldown or Flourish lockout).</summary>
    public double CooldownRemaining(AbilityDefinition ability)
    {
        var ready = Hero.ReadyAt.GetValueOrDefault(ability.Id);
        if (ability.IsFlourish)
            ready = Math.Max(ready, Hero.FlourishReadyAt);
        return Math.Max(0, ready - Time);
    }

    /// <summary>Drinks a potion charge if one is ready. The healing arrives over two seconds.</summary>
    public bool DrinkPotion()
    {
        var c = Hero.Character;
        if (Hero.IsDead || c.Potion.Drink(Time, c.Level, Random, c.Conditions) is not { } heal)
            return false;
        Hero.HealRemaining = heal;
        Hero.HealPerSecond = heal / HealingPotion.HealDurationSeconds;
        Sound(SoundCue.Potion, Hero);
        Say($"Potion: +{heal} HP over 2 s");
        return true;
    }

    /// <summary>The living monster under a map point, if any (generous radius for clicking).</summary>
    public MonsterActor? MonsterAt(double x, double y, double radius = 0.8) =>
        _monsters.Where(m => CanTarget(m) && m.DistanceTo(x, y) <= radius)
            .OrderBy(m => m.DistanceTo(x, y))
            .FirstOrDefault();

    // ----- Simulation --------------------------------------------------------------

    /// <summary>Advances real time; the world steps in fixed 1/60 s increments.</summary>
    public void Update(double seconds)
    {
        _accumulator += Math.Min(seconds, 0.25);
        while (_accumulator >= StepSeconds)
        {
            _accumulator -= StepSeconds;
            Step(StepSeconds);
        }
    }

    private void Step(double dt)
    {
        Time += dt;
        if (Hero.Equipment.BagVersion != _beltBagVersion)
            RefreshBelt();
        UpdateWard();
        UpdateAuras();
        var (wasX, wasY) = (Hero.X, Hero.Y);
        UpdateHero(dt);
        // Movement builds Momentum (not a jump to a shrine or another zone).
        var moved = Hero.DistanceTo(wasX, wasY);
        if (moved < 1)
            Hero.Resource.OnMoved(moved);
        UpdateHidden(dt);
        UpdateTraps();
        UpdateCold(dt);
        UpdateAllies(dt);
        Reveal();
        UpdateProjectiles(dt);
        UpdateMonsterShots(dt);
        UpdateHazards(dt);
        foreach (var monster in _monsters.ToList())
            UpdateMonster(monster, dt);
        Separate();
        _monsters.RemoveAll(m => m.IsDead && !m.IsRising && m.DeadFor > CorpseSeconds);
        _texts.RemoveAll(t => Time - t.BornAt > FloatingText.Lifetime);
        _impacts.RemoveAll(i => Time - i.At > Impact.Lifetime);
        UpdateBursts();
        Hero.Character.Taint.Update(dt);
    }

    private void UpdateHero(double dt)
    {
        var hero = Hero;
        var c = hero.Character;
        hero.Advance(dt);
        c.Conditions.Update(dt);
        hero.Resource.Update(dt);

        if (hero.IsDead)
        {
            Regenerating = false;
            hero.RespawnIn -= dt;
            if (hero.RespawnIn <= 0)
                Respawn();
            return;
        }

        if (hero.HealRemaining > 0)
        {
            var amount = Math.Min(hero.HealRemaining, hero.HealPerSecond * dt);
            hero.HealRemaining -= amount;
            _healCarry += amount;
            var whole = (int)_healCarry;
            if (whole > 0)
            {
                c.Heal(whole);
                _healCarry -= whole;
            }
        }

        Regenerate(dt);
        UpdateReload(dt);
        if (!TickHeroPersistent(dt))
            return;
        Bog(dt);

        foreach (var shrine in Zone.Shrines)
        {
            if (hero.DistanceTo(shrine.At[0] + 0.5, shrine.At[1] + 0.5) <= ShrineRadius)
                ActivateShrine(shrine);
        }
        ExitDefinition? inside = null;
        foreach (var exit in Zone.Exits)
        {
            if (hero.DistanceTo(exit.At[0] + 0.5, exit.At[1] + 0.5) <= exit.Radius)
                inside = exit;
        }
        // Walking into an exit completes the zone the first time and goes where it leads; an exit that leads nowhere
        // known yet shows its banner, every time it is walked into.
        if (inside is not null && inside != _insideExit && inside.Requires is { } gate && !Holds(gate))
        {
            // A Herald gate, still sealed: it neither completes the zone nor goes anywhere.
            Arrival = new Banner(inside.Label, inside.SealedText ?? "The way is barred.", Time);
            Say($"{inside.Label}: {inside.SealedText ?? "The way is barred."}");
        }
        else if (inside is not null && inside != _insideExit)
        {
            if (!ZoneComplete)
            {
                ZoneComplete = true;
                Sound(SoundCue.ZoneComplete, hero);
            }
            if (TravelThrough(inside))
                return;
            Arrival = new Banner(inside.Label, inside.OpenText ?? "The way on is not open yet.", Time);
            Say($"You reach {inside.Label}.");
        }
        _insideExit = inside;
        if (hero.ShrineTarget is { } wanted)
        {
            if (_activatedShrines.Contains(wanted.Id) && ShrineDistance(wanted) <= RiftRadius)
                OpenRift(wanted);
        }
        if (OpenShrine is { } rift && ShrineDistance(rift) > ShrineRadius + 1)
            OpenShrine = null;

        CollectLoot();
        UpdateInteraction();
        if (hero.TalkTarget is { } talk && talk.DistanceTo(hero.X, hero.Y) <= Vendor.TalkRadius)
            Open(talk);
        if (hero.CacheTarget is { } chest && chest.DistanceTo(hero.X, hero.Y) <= Cache.OpenRadius)
            Loot(chest);
        UpdateCorpseTarget();
        UpdateTrapTarget();
        UpdateZoneTraps();
        if (OpenVendor is { } open && open.DistanceTo(hero.X, hero.Y) > Vendor.TalkRadius + 2.5)
            CloseVendor();

        if (hero.DodgeRemaining > 0)
        {
            AdvanceDodge(dt);
            return;
        }
        if (hero.StaggerRemaining > 0)
        {
            hero.StaggerRemaining -= dt;
            return;
        }
        if (!c.Conditions.CanAct)
            return;
        if (UpdateEnthralled(dt))
            return;

        if (hero.Action is { } action)
        {
            if (AdvanceAction(action, dt))
                return;
            hero.Action = null;
        }

        if (hero.Order is { } order)
        {
            var target = order.Target;
            if (target.IsDead && !(target.IsRising && order.Ability.Kind == AbilityKind.Blast))
            {
                hero.Order = null;
                hero.Play("stance");
                return;
            }
            var ability = order.Ability;
            var reach = ReachFor(ability, target);
            var startDistance = ability.Kind == AbilityKind.ChargeStrike ? reach + ability.Range : reach;
            var losOk = !(ability.IsSpell || ability.Kind is AbilityKind.Bomb or AbilityKind.Blast || (ability.UsesWeapon && Shoots))
                || Map.HasLineOfSight(hero.X, hero.Y, target.X, target.Y);
            if (hero.DistanceTo(target) > startDistance || !losOk)
            {
                hero.RepathIn -= dt;
                if (hero.Path.Count == 0 || hero.RepathIn <= 0)
                {
                    PlanPath(hero, target.X, target.Y);
                    hero.RepathIn = 0.3;
                }
                if (FollowPath(hero, HeroSpeed(), dt, null))
                    hero.Play("run");
                return;
            }
            hero.Path.Clear();

            // A gun empty or being loaded: the hero waits on it, facing the target.
            if (HoldsFire(ability))
            {
                if (hero.ReloadRemaining <= 0)
                    BeginReload();
                hero.Face(target.X, target.Y);
                hero.Play("stance");
                return;
            }
            if (WhyNot(ability) is { } problem)
            {
                if (ability.Repeat)
                {
                    hero.Face(target.X, target.Y);
                    hero.Play("stance");
                    return;
                }
                Say(problem);
                hero.Order = new HeroOrder(BasicAbility, target);
                return;
            }
            StartAction(ability, target);
            if (!ability.Repeat)
                hero.Order = new HeroOrder(BasicAbility, target);
            return;
        }

        if (FollowPath(hero, HeroSpeed(), dt, hero.Goal))
            hero.Play("run");
        else
        {
            hero.Goal = null;
            // Standing with a shield raised, the hero holds it up.
            hero.Play(hero.Ward is { Ability.NeedsShield: true } ? "block" : "stance");
        }
    }

    private double _healCarry;
    private double _calmSince;
    private double _topUpAt;

    /// <summary>Out of combat, how often the draught case refills an empty flask.</summary>
    public const double TopUpSeconds = 2.0;

    /// <summary>True while the hero is recovering HP out of combat (the HUD shows it as a buff).</summary>
    public bool Regenerating { get; private set; }

    /// <summary>
    /// Out-of-combat recovery, Grim Dawn style: with no monster awake and no attack under way for
    /// <see cref="RegenDelaySeconds"/>, the hero gets back <see cref="RegenShare"/> of max HP a second. Without it a
    /// melee hero, who can't avoid chip damage the way a caster at range can, wore down pack by pack.
    /// </summary>
    private void Regenerate(double dt)
    {
        var c = Hero.Character;
        if (Hero.Action is not null || _projectiles.Count > 0 || _monsters.Any(m => !m.IsDead && m.State != MonsterState.Idle))
            _calmSince = Time;
        // Out of combat, the draught case refills an empty flask every couple of seconds.
        if (Time - _calmSince >= RegenDelaySeconds && Time >= _topUpAt && !Hero.IsDead && c.Potion.TopUp())
        {
            _topUpAt = Time + TopUpSeconds;
            Float(Hero, "flask refilled", FloatingTextKind.Heal, 0.4);
        }
        Regenerating = Time - _calmSince >= RegenDelaySeconds && c.HitPoints < c.MaxHitPoints;
        if (!Regenerating)
            return;
        _healCarry += c.MaxHitPoints * RegenShare * (1 + c.RecoveryBonus) * dt;
        var whole = (int)_healCarry;
        if (whole > 0)
        {
            c.Heal(whole);
            _healCarry -= whole;
        }
    }

    /// <summary>Gold within reach is always picked up; the item the player clicked is picked up on arrival.</summary>
    private void CollectLoot()
    {
        var hero = Hero;
        foreach (var g in _ground.ToList())
        {
            var near = hero.DistanceTo(g.X, g.Y);
            var wanted = hero.PickupTarget == g && near <= PickupRadius;
            // Gold, belt items (bombs, antidotes) and the gunsmith's materials are always wanted: walking over them picks them up.
            var magnet = (g.Drop.Gold > 0 || g.Drop.Item is { Definition.IsBeltItem: true } or { Definition.IsMaterial: true })
                && near <= GoldMagnetRadius && Time - g.DroppedAt > 0.6;
            if (!wanted && !magnet)
                continue;
            if (hero.PickupTarget == g)
            {
                hero.PickupTarget = null;
                hero.Goal = null;
                hero.Path.Clear();
            }
            if (g.Drop.Item is { } item)
            {
                if (!hero.Equipment.TryAdd(item))
                {
                    if (wanted)
                        Say("Your bag is full.");
                    continue;
                }
                Say($"Picked up {item.Name}.");
                Sound(SoundCue.Pickup, hero, PickupVariant(item));
            }
            else if (g.Drop.Draught)
            {
                var flasksFull = hero.Character.Potion.Charges >= HealingPotion.MaxCharges;
                if (!hero.Character.Potion.AddCharge())
                {
                    if (wanted)
                        Say("Your flasks and your draught case are full.");
                    continue;
                }
                Float(hero, flasksFull ? "+1 draught (case)" : "+1 potion", FloatingTextKind.Heal, 0.6);
                Sound(SoundCue.Potion, hero);
            }
            else
            {
                hero.Equipment.AddGold(g.Drop.Gold);
                Sound(SoundCue.Gold, hero);
                Float(hero, $"+{g.Drop.Gold} gold", FloatingTextKind.Info, 0.3);
            }
            _ground.Remove(g);
        }
    }

    /// <summary>What a picked-up item sounds like: jewellery, metal (weapons, chain) or cloth and leather.</summary>
    private static string PickupVariant(Item item) => item.Definition switch
    {
        { IsJewelry: true } => "jewel",
        { IsWeapon: true } or { ArmorCategory: ArmorCategory.Medium or ArmorCategory.Heavy } => "metal",
        { Loot: "steel_armor" } => "metal",
        _ => "cloth",
    };

    private void DropLoot(MonsterActor monster)
    {
        if (_loot is null)
            return;
        var drops = _loot.Roll(monster.Creature.Stats.Rank, monster.Creature.Stats.EffectiveLevel, Random, monster.Definition.IsSpawner, Zone.Act).ToList();
        if (monster.Definition.RareDrop)
            drops.Insert(0, new LootDrop(_loot.Generate(Math.Max(1, monster.Creature.Stats.EffectiveLevel), Rarity.Rare, Random)));
        Spill(monster.X, monster.Y, drops);
        if (drops.Count > 0)
            Sound(drops.Any(d => d.Gold > 0 && d.Item is null) && drops.All(d => d.Item is null) ? SoundCue.Gold : SoundCue.LootDrop, monster);
    }

    /// <summary>Scatters drops on walkable ground within a tile or so of a point (a corpse, a chest).</summary>
    private void Spill(double cx, double cy, IEnumerable<LootDrop> drops)
    {
        foreach (var drop in drops)
        {
            double x = cx, y = cy;
            for (var attempt = 0; attempt < 12; attempt++)
            {
                var angle = Random.Roll(0, 359) * Math.PI / 180;
                var r = 0.6 + Random.Roll(0, 80) / 100.0;
                double px = cx + Math.Cos(angle) * r, py = cy + Math.Sin(angle) * r;
                if (Map.IsWalkable(px, py))
                {
                    (x, y) = (px, py);
                    break;
                }
            }
            _ground.Add(new GroundItem(x, y, drop, Time));
            // A Unique or a Set piece rings out, and says what it is.
            if (drop.Item is { Rarity: Rarity.Unique or Rarity.Set } special)
            {
                Sound(SoundCue.RareDrop, x, y);
                Say($"{(special.Rarity == Rarity.Unique ? "A unique" : "A set piece")} falls: {special.Name}!");
            }
        }
    }

    /// <summary>Weapon abilities need melee reach; spells and thrown bombs their range; reach grows with the target's size.</summary>
    private double ReachFor(AbilityDefinition ability, MonsterActor target) =>
        ability.IsSpell ? ability.Range + Hero.Character.SpellRangeBonus
        : ability.Kind == AbilityKind.Bomb ? ability.Range
        : ability.Kind == AbilityKind.Blast ? ability.Range + Hero.Character.SpellRangeBonus
        : ability.Kind == AbilityKind.Burst ? ability.Radius * 0.8 + target.Radius
        : Shoots ? Hero.Character.Weapon.Definition.Range + Hero.Character.WeaponRangeBonus + target.Radius
        : MeleeReach + Hero.Character.ReachBonus + target.Radius;

    /// <summary>Whether the hero's weapon Strikes are arrows (a bow in hand).</summary>
    public bool Shoots => Hero.Character.Weapon.Definition.IsRanged;

    /// <summary>Why an ability can't start right now, or null if it can.</summary>
    private string? WhyNot(AbilityDefinition ability)
    {
        var cooldown = CooldownRemaining(ability);
        if (cooldown > 0)
            return ability.IsFlourish && Hero.FlourishReadyAt > Time && Hero.ReadyAt.GetValueOrDefault(ability.Id) <= Time
                ? $"{ability.Name}: another Flourish was used ({cooldown:0.0} s)."
                : $"{ability.Name} is not ready ({cooldown:0.0} s).";
        if (!HasCharges(ability))
            return $"{ability.Name} needs {(ability.Charges == 1 ? "a Grave Charge" : $"{ability.Charges} Grave Charges")}.";
        if (!CanAfford(ability))
            return $"Not enough {(ability.CostsTaint ? "Taint" : ResourceFor(ability).Name)} for {ability.Name} ({ability.Cost:0}).";
        if (ability.NeedsTwoHanded && !WieldsTwoHanded)
            return $"{ability.Name} needs a two-handed weapon.";
        if (ability.NeedsRanged && !Shoots)
            return $"{ability.Name} needs a bow.";
        if (ability.NeedsFirearm && !WieldsFirearm)
            return $"{ability.Name} needs a gun.";
        if (ability.NeedsShield && ability.Kind != AbilityKind.Ward && Hero.Equipment.In(EquipSlot.OffHand) is not { Definition.IsShield: true })
            return $"{ability.Name} needs a shield in your off hand.";
        if (ability.IsSweep && Shoots && !WieldsFirearm)
            return $"{ability.Name} needs a melee weapon.";
        return null;
    }

    private void StartAction(AbilityDefinition ability, MonsterActor target)
    {
        var hero = Hero;
        var c = hero.Character;
        var extraDice = 0;
        if (ability.IsConsumable && !UseUpBeltItem(ability))
        {
            hero.Order = null;
            return;
        }
        if (ability.SpendAll)
            extraDice = ability.DiceForSpent(hero.Resource.SpendAll());
        else
            Pay(ability);
        if (ability.Cooldown > 0)
            hero.ReadyAt[ability.Id] = Time + ability.Cooldown;
        if (ability.IsFlourish)
            hero.FlourishReadyAt = Time + FlourishLockoutSeconds;

        var shots = ability.UsesWeapon && WieldsFirearm ? FireRounds(ability) : 0;
        var slowed = c.Conditions.ActionTimeMultiplier;
        var swing = ActionTiming.AttackInterval(c.Weapon.Definition.AttacksPerSecond, c.Gear.AttackSpeed, slowed) / (TwinPistols ? TwinPistolSpeed : 1);
        var duration = ability.UsesWeapon
            ? Math.Max(swing, ability.Actions > 1 ? ActionTiming.CastTime(ability.Actions, c.Gear.AttackSpeed, slowed) : 0)
            : ActionTiming.CastTime(ability.Actions, ability.Kind == AbilityKind.Burst && !ability.IsSpellBurst ? c.Gear.AttackSpeed : c.Gear.CastSpeed, slowed);
        var dashing = ability.Kind == AbilityKind.ChargeStrike && hero.DistanceTo(target) > ReachFor(ability, target);
        // Any blow, spell or bomb brings the hero out of the shadows; its first blow still finds the target off-guard.
        var fromHiding = hero.IsHidden;
        if (fromHiding)
            Unhide(null);
        hero.Action = new HeroAction(ability, target, duration) { Dashing = dashing, ExtraDice = extraDice, FromHiding = fromHiding, Shots = shots };
        hero.Face(target.X, target.Y);
        if (dashing)
            hero.Play("run", 2.0, restart: true);
        else
            hero.Play(ability.IsSpell || ability.IsSpellBurst ? "cast" : ability.UsesWeapon && Shoots ? "shoot" : "swing", AnimationRate(ability, duration), restart: true);
        if (!ability.IsSpell && ability.Kind != AbilityKind.Burst && !dashing && !(ability.UsesWeapon && Shoots))
            Sound(SoundCue.Swing, hero);
    }

    private static double AnimationRate(AbilityDefinition ability, double duration) =>
        (ability.IsSpell ? 0.4 : 0.4) / Math.Max(0.1, duration);

    /// <summary>Runs the current action one step; false once it has finished.</summary>
    private bool AdvanceAction(HeroAction action, double dt)
    {
        var hero = Hero;
        var target = action.Target;
        if (action.Dashing)
        {
            if (target.IsDead)
                return false;
            var reach = ReachFor(action.Ability, target);
            var d = hero.DistanceTo(target);
            var step = Math.Min(DashSpeed * dt, Math.Max(0, d - reach * 0.9));
            var nx = hero.X + (target.X - hero.X) / d * step;
            var ny = hero.Y + (target.Y - hero.Y) / d * step;
            action.DashTime += dt;
            if (step <= 1e-6 || !Map.IsWalkable(nx, ny) || action.DashTime > 1.5 || d <= reach)
            {
                action.Dashing = false;
                hero.Face(target.X, target.Y);
                hero.Play("swing", AnimationRate(action.Ability, action.Duration), restart: true);
                Sound(SoundCue.Swing, hero);
                return true;
            }
            hero.X = nx;
            hero.Y = ny;
            hero.Face(target.X, target.Y);
            return true;
        }

        action.Elapsed += dt;
        var landsAt = action.Ability.IsSpell ? 0.6 : 0.5;
        if (!action.Landed && action.Elapsed >= action.Duration * landsAt)
        {
            action.Landed = true;
            ApplyAbility(action.Ability, target, action.ExtraDice, action.FromHiding, action.Shots);
        }
        return action.Elapsed < action.Duration;
    }

    private void ApplyAbility(AbilityDefinition ability, MonsterActor target, int spentDice = 0, bool fromHiding = false, int shots = 0)
    {
        var rank = AbilityDefinition.SpellRank(Hero.Character.Level);
        switch (ability.Kind)
        {
            case AbilityKind.WeaponStrike or AbilityKind.ChargeStrike:
                var dice = ability.ExtraDiceAt(Hero.Character.Level) + spentDice;
                if (Shoots && WieldsFirearm)
                {
                    if (!target.IsDead)
                        FireGun(ability, target, dice, Math.Max(1, shots), fromHiding);
                    break;
                }
                if (Shoots)
                {
                    // A bow looses its arrows (Twin Shot two), a beat apart; each is a Strike when it lands.
                    if (!target.IsDead)
                    {
                        for (var i = 0; i < ability.StrikeCount; i++)
                            _projectiles.Add(new Projectile(ability, target, rank, Hero.X, Hero.Y, Time, i)
                            {
                                Shot = new ArrowShot(dice, ability.CadenceAttacks, i == 0, fromHiding && i == 0, ability.Feint || (i > 0 && ability.Twin),
                                    ability.Pierce + Hero.Character.ArrowPierce),
                            });
                        Sound(SoundCue.Bow, Hero);
                    }
                    break;
                }
                if (!target.IsDead && Hero.DistanceTo(target) <= ReachFor(ability, target) + 0.3)
                {
                    HeroStrike(target, ability, dice, ability.CadenceAttacks, fromHiding: fromHiding, feint: ability.Feint);
                    // Twin Feint: a second Strike on the heels of the first, against a target now off-guard to it.
                    for (var i = 1; i < ability.StrikeCount && !target.IsDead; i++)
                        HeroStrike(target, ability, dice, ability.CadenceAttacks, feint: ability.Feint || ability.Twin);
                }
                // A sweep strikes more enemies within reach and its arc, nearest first; only the main target builds Fury.
                foreach (var other in _monsters.Where(m => !m.IsDead && m != target && Hero.DistanceTo(m) <= ReachFor(ability, m) + 0.3
                                 && WithinArc(ability.Arc, target, m))
                             .OrderBy(m => Hero.DistanceTo(m)).Take(ability.Targets - 1).ToList())
                    HeroStrike(other, ability, dice, ability.CadenceAttacks, buildsFury: false);
                break;
            case AbilityKind.Bomb:
                _projectiles.Add(new Projectile(ability, target, rank, Hero.X, Hero.Y, Time, 0));
                Sound(SoundCue.Swing, Hero);
                break;
            case AbilityKind.SpellBolt:
                _projectiles.Add(new Projectile(ability, target, rank, Hero.X, Hero.Y, Time, 0) { ChainLeft = ability.Chain });
                Sound(SoundCue.Ignition, Hero);
                break;
            case AbilityKind.Burst:
                Burst(ability, target, (Hero.X, Hero.Y));
                break;
            case AbilityKind.Blast:
                if (!target.IsDead || target.IsRising)
                {
                    var corpse = target.IsRising;
                    Burst(ability, null, (target.X, target.Y));
                    if (corpse)
                        DestroyCorpse(target, "blown apart");
                }
                break;
            case AbilityKind.Missiles:
                var count = ability.MissilesAt(rank);
                for (var i = 0; i < count; i++)
                    _projectiles.Add(new Projectile(ability, target, rank, Hero.X, Hero.Y, Time, i));
                Sound(SoundCue.ForceBolt, Hero);
                break;
        }
    }

    /// <summary>Whether <paramref name="other"/> lies within a sweep's arc, centred on the direction to its main target.</summary>
    private bool WithinArc(double arc, MonsterActor target, MonsterActor other)
    {
        if (arc >= 360)
            return true;
        var a = Math.Atan2(target.Y - Hero.Y, target.X - Hero.X);
        var b = Math.Atan2(other.Y - Hero.Y, other.X - Hero.X);
        var difference = Math.Abs(Math.IEEERemainder(a - b, 2 * Math.PI)) * 180 / Math.PI;
        return difference <= arc / 2;
    }

    private void UpdateProjectiles(double dt)
    {
        foreach (var p in _projectiles.ToList())
        {
            if (Time < p.LaunchAt)
                continue;
            var target = p.Target;
            if (target.IsDead)
            {
                _projectiles.Remove(p);
                continue;
            }
            var (tx, ty) = (target.X, target.Y);
            var d = Math.Sqrt((tx - p.X) * (tx - p.X) + (ty - p.Y) * (ty - p.Y));
            var step = ProjectileSpeed * (p.Shot is { Bullet: true } ? BulletSpeedScale : 1) * dt;
            if (d > step + 0.25)
            {
                // Missiles weave a little so a volley doesn't fly as one dot.
                var weave = p.Index == 0 ? 0 : Math.Sin((Time - p.LaunchAt) * 12 + p.Index) * 0.04 * (p.Index % 2 == 0 ? 1 : -1);
                p.X += (tx - p.X) / d * step - (ty - p.Y) / d * weave;
                p.Y += (ty - p.Y) / d * step + (tx - p.X) / d * weave;
                continue;
            }
            _projectiles.Remove(p);
            ProjectileHits(p);
        }
    }

    private void ProjectileHits(Projectile p)
    {
        var target = p.Target;
        var ability = p.Ability;
        if (ability.Kind == AbilityKind.Bomb)
        {
            BombLands(p);
            return;
        }
        if (p.Shot is { } shot)
        {
            HeroStrike(target, ability, shot.ExtraDice, shot.CadenceAttacks, buildsFury: shot.Builds, fromHiding: shot.FromHiding, feint: shot.Feint);
            PierceOn(p, shot);
            return;
        }
        var dice = ability.DamageAt(p.Rank);
        var critical = false;
        int damage;
        if (ability.Kind == AbilityKind.SpellBolt)
        {
            if (LostInTheDark(target))
            {
                Chain(p);
                return;
            }
            var cover = CoverAgainst(Hero.X, Hero.Y, target);
            var result = Strike.ResolveSpellAttack(Hero.Character, target.Creature, dice, ability.DamageType, Random, cover);
            critical = result.Degree == DegreeOfSuccess.CriticalSuccess;
            damage = result.Damage;
            if (critical)
                HeroCrits++;
            Chain(p);
            SpellSplash(ability, target, p.Rank);
            if (damage == 0)
            {
                Float(target, cover > 0 ? "cover" : result.Label, FloatingTextKind.Miss);
                Aggro(target);
                return;
            }
            if (critical && ability.CritPersistent is { } burn)
            {
                var persistent = Dice.Parse(burn);
                target.Creature.Persistent.Add(ability.DamageType, persistent with { Count = persistent.Count + (p.Rank - 1) });
            }
        }
        else
        {
            damage = DamageCalculator.ApplyDefenses(dice.Roll(Random), ability.DamageType, target.Creature.Defenses);
        }
        damage = DamageMonster(target, damage);
        Float(target, critical ? $"{damage}!" : damage.ToString(), critical ? FloatingTextKind.Critical : FloatingTextKind.DamageDealt);
        // Siphon Life: a share of what it dealt comes back to the hero.
        if (ability.Drain > 0 && (int)Math.Round(damage * ability.Drain) is > 0 and var drained && Hero.Character.HitPoints < Hero.Character.MaxHitPoints)
        {
            Hero.Character.Heal(drained);
            Float(Hero, $"+{drained}", FloatingTextKind.Heal, 0.3);
        }
        target.HitFlinch = 0.15;
        Aggro(target);
        AddImpact(target.X, target.Y, ability.DamageType);
        Sound(SoundCue.Impact, target);
        if (target.IsDead)
            Kill(target, critical);
        else
            OnHit(ability, target, critical);
    }

    /// <summary>A chaining spell leaps from where it struck to the nearest other enemy within 3 tiles.</summary>
    private void Chain(Projectile p)
    {
        if (p.ChainLeft <= 0)
            return;
        var next = _monsters.Where(m => !m.IsDead && m != p.Target && m.DistanceTo(p.Target) <= 3.0).MinBy(m => m.DistanceTo(p.Target));
        if (next is not null)
            _projectiles.Add(new Projectile(p.Ability, next, p.Rank, p.Target.X, p.Target.Y, Time, 0) { ChainLeft = p.ChainLeft - 1 });
    }

    /// <summary>
    /// What an ability does to a target it hits, besides damage: a condition (frightened one step more on a
    /// critical hit) and knockback away from the hero, stopping at anything that blocks.
    /// </summary>
    private void OnHit(AbilityDefinition ability, MonsterActor target, bool critical)
    {
        if (ability.UsesWeapon && ability.WeaponPersistentAt(Hero.Character.Level) is { } persistent)
        {
            target.Creature.Persistent.Add(ability.DamageType, persistent);
            Float(target, ability.DamageType.ToString().ToLowerInvariant(), FloatingTextKind.Info, 0.3);
        }
        if (ability.OnHit is { } name && Enum.TryParse<ConditionKind>(name, out var kind))
        {
            var value = ability.OnHitValue + (critical && kind == ConditionKind.Frightened ? 1 : 0);
            target.Creature.Conditions.Apply(kind, value, ability.OnHitSeconds > 0 ? ability.OnHitSeconds : null);
            Float(target, Regex.Replace(name, "(?<=[a-z])(?=[A-Z])", "-").ToLowerInvariant(), FloatingTextKind.Info, 0.3);
        }
        if (ability.Knockback > 0)
        {
            var (dx, dy) = (target.X - Hero.X, target.Y - Hero.Y);
            var length = Math.Sqrt(dx * dx + dy * dy);
            if (length > 1e-6)
            {
                (dx, dy) = (dx / length, dy / length);
                for (var moved = 0.0; moved < ability.Knockback; moved += 0.25)
                {
                    var (nx, ny) = (target.X + dx * 0.25, target.Y + dy * 0.25);
                    if (!Map.IsWalkable(nx, ny))
                        break;
                    (target.X, target.Y) = (nx, ny);
                }
                target.Path.Clear();
            }
        }
    }

    private void AddImpact(double x, double y, DamageType type) => _impacts.Add(new Impact(x, y, type, Time));

    /// <param name="fromHiding">Struck out of hiding: the target is off-guard to it (and with the Cutthroat's ambush a hit is a critical).</param>
    /// <param name="feint">The target is off-guard to this Strike (Sneak Strike, Twin Feint's second blow).</param>
    private void HeroStrike(MonsterActor target, AbilityDefinition? ability = null, int extraDice = 0, int cadenceAttacks = 1,
        bool buildsFury = true, bool fromHiding = false, bool feint = false)
    {
        if (LostInTheDark(target))
            return;
        var c = Hero.Character;
        var (offGuard, sneak) = SneakAttack(target, fromHiding || feint);
        var cover = Shoots ? CoverAgainst(Hero.X, Hero.Y, target) : 0;
        var result = Strike.Resolve(c, target.Creature, Hero.Cadence, Time, Random, graze: GrazeEffect.Near,
            attackSpeedBonus: c.Gear.AttackSpeed, extraDice: extraDice, cadenceAttacks: cadenceAttacks,
            targetAc: offGuard + cover, precisionDice: sneak, hitIsCritical: fromHiding && c.AmbushCrit,
            preyDice: target == Hero.Prey ? c.PreyDice : 0);
        var critical = result.Degree == DegreeOfSuccess.CriticalSuccess;
        if (critical && Shoots && c.CritResourceBonus > 0)
            Hero.Resource.Gain(c.CritResourceBonus);
        if (sneak > 0 && result.Damage > 0 && !result.Grazed)
            Float(target, "sneak attack", FloatingTextKind.Info, 0.6);
        Sound(result.Damage > 0 ? (critical ? SoundCue.Crit : SoundCue.Hit) : SoundCue.Miss, target);
        if (result.Grazed)
        {
            // A near miss: half damage, half the Fury, and none of the ability's other effects.
            var grazed = DamageMonster(target, result.Damage);
            Hero.Resource.OnCombat(graze: true, builds: buildsFury);
            Float(target, $"graze {grazed}", FloatingTextKind.Graze);
            Aggro(target);
            if (target.IsDead)
                Kill(target, false);
            return;
        }
        if (result.Damage > 0)
        {
            var damage = DamageMonster(target, result.Damage + (critical ? Hero.Character.CritDamageBonus : 0) + ChampionStrikeExtra(target, critical)
                + GunStrikeExtra(ability, target, critical));
            Hero.Resource.OnCombat(critical, builds: buildsFury);
            if (critical)
                HeroCrits++;
            Float(target, critical ? $"{damage}!" : damage.ToString(), critical ? FloatingTextKind.Critical : FloatingTextKind.DamageDealt);
            target.HitFlinch = 0.15;
            Aggro(target);
        }
        else
        {
            Float(target, cover > 0 ? "cover" : result.Label, FloatingTextKind.Miss);
        }
        if (result.AttackerStaggered)
            Hero.StaggerRemaining = ActionTiming.CritFailStaggerSeconds;
        if (result.Check.Natural == 1)
            Misfire();
        if (target.IsDead)
            Kill(target, critical);
        else if (result.Damage > 0 && ability is not null)
            OnHit(ability, target, critical);
    }


    private void Kill(MonsterActor monster, bool critical)
    {
        // An illusion struck down is gone, and worth nothing.
        if (monster.Definition.Illusion)
        {
            Dissolve(monster);
            return;
        }
        // The Choir: a reflection is gone, worth nothing; a sister falls with the bar she shares, and brings the boss down.
        if (monster.IsReflection)
        {
            ShatterReflection(monster);
            return;
        }
        if (monster.SharesWith is { } pool)
        {
            monster.State = MonsterState.Dead;
            monster.Path.Clear();
            monster.Play("die", restart: true);
            if (pool.Creature.IsDead && pool.State != MonsterState.Dead)
                Kill(pool, critical);
            return;
        }
        if (Hero.Prey == monster)
            Hero.Prey = null;
        if (monster.SpawnIndex >= 0)
            _slain.Add(monster.SpawnIndex);
        monster.State = MonsterState.Dead;
        monster.Path.Clear();
        Sound(SoundCue.MonsterDeath, monster, monster.Definition.Voice);
        monster.Play(critical && HasAnimation(monster, "critdie") ? "critdie" : "die", restart: true);
        // A risen corpse paid out the first time it fell.
        if ((monster.SummonedBy is null || monster.Creature.Stats.Rank != CreatureRank.Minion) && monster.Rises == 0)
        {
            var xp = (int)Math.Round(Encounter.CreatureXp(monster.Creature.Stats.Level, Hero.Character.Level)
                * RankXpWeight(monster.Creature.Stats.Rank) * Zone.XpScale);
            if (xp > 0)
                GainXp(xp, monster);
        }
        if (Hero.Order?.Target == monster)
            Hero.Order = null;
        if ((monster.SummonedBy is null || monster.Creature.Stats.Rank != CreatureRank.Minion) && monster.Rises == 0)
            DropLoot(monster);
        LayToRise(monster, critical);
        Avenge(monster);
        ShatterImages(monster);
        GatherGraveCharge(monster);
        if (monster.Has(MonsterAffix.Tainted))
            LeaveTaint(monster);
        if (monster.Definition.Boss is not null)
            BossDied(monster);
        // A side story's pack: once every one of its placed monsters has fallen, its flag is set.
        if (monster.SpawnIndex >= 0 && monster.PackId - 1 < Zone.Spawns.Count && Zone.Spawns[monster.PackId - 1] is { Flag: { } packFlag } spawn
            && !_monsters.Any(m => m.PackId == monster.PackId && m.SpawnIndex >= 0 && !m.IsDead))
        {
            SetFlag(packFlag, spawn.Value ?? "cleared");
            if (spawn.Says is { } line)
                Say(line);
        }
    }

    public const int VengeanceMax = 3;
    public const double VengeanceRadius = 12.0;

    /// <summary>A fallen monster's Vengeful pack-mates nearby take a stack of Vengeance each, and come for the hero.</summary>
    private void Avenge(MonsterActor fallen)
    {
        foreach (var m in _monsters.Where(m => m != fallen && !m.IsDead && m.PackId == fallen.PackId && m.Has(MonsterAffix.Vengeful)
                     && m.Vengeance < VengeanceMax && m.DistanceTo(fallen) <= VengeanceRadius))
        {
            m.Vengeance++;
            Float(m, m.Vengeance == VengeanceMax ? "VENGEANCE!" : "vengeance", FloatingTextKind.Critical, 0.8);
            if (m.State == MonsterState.Idle)
                Aggro(m);
        }
    }

    /// <summary>A monster's Vengeance: to-hit, damage share and speed share.</summary>
    /// <remarks>Under a standing standard (Hadric's banner) it also has +2 to hit and a fifth more damage; a boss's phase may quicken it.</remarks>
    private (int Attack, double Damage, double Speed) VengeanceBonus(MonsterActor m)
    {
        var rallied = Rallied(m);
        var phaseSpeed = m.Definition.Boss is { } b ? b.Phases[m.BossPhase].SpeedScale : 1;
        return (m.Vengeance + (rallied ? 2 : 0), (1 + 0.1 * m.Vengeance) * (rallied ? 1.2 : 1), (1 + 0.05 * m.Vengeance) * phaseSpeed);
    }

    /// <summary>
    /// Pathfinder's flanking (spec, section 9): a monster in melee reach of the hero, with another monster in melee
    /// reach on a side more than 120 degrees round from it, has the hero off-guard: −2 AC to its strikes.
    /// </summary>
    internal bool IsFlanking(MonsterActor m) => !m.Definition.Ranged && InMelee(m) && FlankPartner(m, requireFlank: true) is not null;

    private bool InMelee(MonsterActor m) =>
        !m.IsDead && !m.Definition.Ranged && !m.Definition.IsSpawner && m.DistanceTo(Hero) <= m.Definition.AttackRange + Hero.Radius + 0.3;

    /// <summary>Another monster in melee with the hero; with <paramref name="requireFlank"/>, only one on a far side.</summary>
    private MonsterActor? FlankPartner(MonsterActor m, bool requireFlank)
    {
        var (ax, ay) = (m.X - Hero.X, m.Y - Hero.Y);
        foreach (var other in _monsters)
        {
            if (other == m || !InMelee(other))
                continue;
            if (!requireFlank)
                return other;
            var (bx, by) = (other.X - Hero.X, other.Y - Hero.Y);
            var cos = (ax * bx + ay * by) / Math.Max(1e-6, Math.Sqrt(ax * ax + ay * ay) * Math.Sqrt(bx * bx + by * by));
            if (cos < Math.Cos(120 * Math.PI / 180))
                return other;
        }
        return null;
    }

    /// <summary>Where a flanker wants to stand: across the hero from an ally already in melee, on open ground.</summary>
    /// <summary>A kiting monster falls back when the hero comes within this many tiles.</summary>
    public const double KiteTiles = 3.5;

    /// <summary>How long it falls back before it stands its ground.</summary>
    public const double KiteSeconds = 2.5;

    /// <summary>How fast its will to fall back returns while the hero keeps away (seconds of kiting per second).</summary>
    public const double KiteRestRate = 0.5;

    /// <summary>
    /// Where a kiting monster falls back to: 3 tiles straight away from the hero, or turned up to 90 degrees either way
    /// when a wall is behind it; null when it is cornered.
    /// </summary>
    private (double X, double Y)? KiteSpot(MonsterActor m)
    {
        var (dx, dy) = (m.X - Hero.X, m.Y - Hero.Y);
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length < 1e-6)
            (dx, dy, length) = (1, 0, 1);
        var angle = Math.Atan2(dy, dx);
        foreach (var turn in new[] { 0.0, 0.5, -0.5, 1.0, -1.0, 1.5, -1.5 })
        {
            var (x, y) = (m.X + Math.Cos(angle + turn) * 3, m.Y + Math.Sin(angle + turn) * 3);
            if (Map.IsWalkable(x, y) && Map.HasLineOfSight(m.X, m.Y, x, y))
                return (x, y);
        }
        return null;
    }

    private (double X, double Y)? FlankSpot(MonsterActor m)
    {
        if (FlankPartner(m, requireFlank: false) is not { } ally)
            return null;
        var (dx, dy) = (Hero.X - ally.X, Hero.Y - ally.Y);
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length < 1e-6)
            return null;
        var reach = Math.Max(0.8, m.Definition.AttackRange * 0.8);
        foreach (var turn in new[] { 0.0, 0.5, -0.5 })
        {
            var (cos, sin) = (Math.Cos(turn), Math.Sin(turn));
            var (ux, uy) = ((dx * cos - dy * sin) / length, (dx * sin + dy * cos) / length);
            var (sx, sy) = (Hero.X + ux * reach, Hero.Y + uy * reach);
            if (Map.IsWalkable(sx, sy))
                return (sx, sy);
        }
        return null;
    }

    /// <summary>Set by the app so a death can pick "critdie" only for sprites that have it.</summary>
    public Func<MonsterActor, string, bool> HasAnimation { get; set; } = (_, _) => false;

    private static double RankXpWeight(CreatureRank rank) => rank switch
    {
        CreatureRank.Herald or CreatureRank.Prophet => 10.0,
        _ => Encounter.RankWeight(rank),
    };

    /// <summary>
    /// Raises the hero to <paramref name="level"/> at once, the levels' picks left waiting for the level-up screen: the
    /// battle demo's hero at a zone's level ("--battle iron_march" fights at 6).
    /// </summary>
    public void TrainTo(int level)
    {
        while (Hero.Character.Level < Math.Min(level, 20))
            GainXp(Experience.XpPerLevel, Hero);
    }

    internal void GainXp(int xp, Actor at)
    {
        Float(at, $"+{xp} XP", FloatingTextKind.Xp, 0.6);
        var gained = Experience.Add(xp);
        for (var i = 0; i < gained; i++)
        {
            // Hit points rise at once; the level's feats, skill increase and boosts wait for the level-up screen.
            Hero.Character.LevelUp();
            RefreshBar();
            Say($"Level {Hero.Character.Level}! Press L to choose what it brings.");
            Sound(SoundCue.LevelUp, Hero);
            Float(Hero, "LEVEL UP", FloatingTextKind.Info, 1.2);
        }
        foreach (var vendor in _vendors.Where(v => v.StockedForLevel < Hero.Character.Level))
            Restock(vendor);
    }

    private void UpdateMonster(MonsterActor m, double dt)
    {
        m.Advance(dt);
        // Before the corpse check: the killing blow's flinch would otherwise tint the corpse for good.
        if (m.HitFlinch > 0)
            m.HitFlinch -= dt;
        if (m.IsDead)
        {
            m.DeadFor += dt;
            if (m.RisesIn > 0)
                UpdateRising(m, dt);
            return;
        }
        m.Creature.Conditions.Update(dt);
        UpdateVeil(m, dt);
        if (m.IsDead)
            return;
        foreach (var (type, amount) in m.Creature.Persistent.Update(dt, Random))
        {
            var dealt = DamageMonster(m, DamageCalculator.ApplyDefenses(amount, type, m.Creature.Defenses));
            Float(m, dealt.ToString(), FloatingTextKind.DamageDealt);
            if (m.IsDead)
            {
                Kill(m, false);
                return;
            }
        }

        var hero = Hero;
        var def = m.Definition;
        var distance = m.DistanceTo(hero);

        if (m.State == MonsterState.Idle)
        {
            // An ally close by is noticed too, and the pack turns on it.
            if (!hero.IsDead && _allies.FirstOrDefault(a => !a.IsDead && m.DistanceTo(a) <= def.AggroRadius * 0.6
                    && Map.HasLineOfSight(m.X, m.Y, a.X, a.Y)) is { } seen)
            {
                Aggro(m);
                m.Victim = seen;
                m.VictimIn = 1.0;
            }
            // A hidden hero is noticed only close by (and is then hidden no longer).
            if (!hero.IsDead && distance <= (hero.IsHidden ? HiddenNoticeTiles : def.AggroRadius) && Map.HasLineOfSight(m.X, m.Y, hero.X, hero.Y))
            {
                if (hero.IsHidden)
                    Unhide($"The {m.Name} spots you.");
                Aggro(m);
                Sound(SoundCue.MonsterAggro, m, def.Voice);
            }
            else
            {
                m.Play("stance");
                return;
            }
        }

        // A boss never gives up the chase; the fight starts again only when the hero falls.
        if (hero.IsDead || (distance > LeashDistance && def.Boss is null))
        {
            m.State = MonsterState.Idle;
            m.Path.Clear();
            m.AttackElapsed = -1;
            m.Play("stance");
            return;
        }
        if (def.Boss is not null && UpdateBoss(m, dt))
            return;
        UpdateAffixes(m, dt);
        if (m.IsDead || hero.IsDead)
            return;
        if (!m.Creature.Conditions.CanAct)
            return;

        if (def.IsSpawner)
        {
            UpdateSpawner(m, dt);
            return;
        }
        // A standard only stands there, rallying its legion, until it is broken.
        if (def.Rallies)
        {
            m.Play("stance");
            return;
        }

        if (m.AttackCooldown > 0)
            m.AttackCooldown -= dt;

        // Whom it fights: the hero, or an ally nearer to it.
        var victim = _allies.Count == 0 ? hero : VictimOf(m, dt);
        var victimDistance = m.DistanceTo(victim);
        if (m.AttackElapsed >= 0)
        {
            var interval = 1.0 / (m.Creature.AttacksPerSecond * BossAttackRate(m));
            var windup = Math.Min(0.6, interval * 0.6);
            m.AttackElapsed += dt;
            if (!m.AttackLanded && m.AttackElapsed >= windup * 0.6)
            {
                m.AttackLanded = true;
                if (victimDistance <= def.AttackRange + victim.Radius + 0.4)
                {
                    // An archer's arrow or a caster's bolt flies; its blow lands when it arrives.
                    if (def.Ranged && victimDistance > MeleeReach + victim.Radius)
                        Loose(m, victim);
                    else if (victim is AllyActor { IsDead: false } ally)
                        MonsterBitesAlly(m, ally);
                    else if (victim == hero)
                        MonsterStrike(m);
                }
            }
            if (m.AttackElapsed >= windup)
            {
                m.AttackElapsed = -1;
                m.AttackCooldown = interval - windup;
            }
            return;
        }

        // A kiting archer falls back when the hero closes in, for a while; then it stands and fights until rested.
        if (def.Kites && victim == hero && !def.Stationary && !m.Fused && m.Creature.Conditions.CanMove)
        {
            if (victimDistance < KiteTiles && m.KiteTime < KiteSeconds && KiteSpot(m) is { } away)
            {
                m.KiteTime += dt;
                m.State = MonsterState.Chase;
                m.RepathIn -= dt;
                if (m.Path.Count == 0 || m.RepathIn <= 0)
                {
                    PlanPath(m, away.X, away.Y);
                    m.RepathIn = 0.4;
                }
                if (FollowPath(m, MonsterSpeed(m), dt, null))
                {
                    m.Play("run");
                    return;
                }
            }
            else if (victimDistance >= KiteTiles + 1)
            {
                m.KiteTime = Math.Max(0, m.KiteTime - dt * KiteRestRate);
            }
        }

        var inRange = victimDistance <= def.AttackRange + victim.Radius
            && (!def.Ranged || Map.HasLineOfSight(m.X, m.Y, victim.X, victim.Y));
        // A flanker in reach but not yet flanking circles round for up to 3 s while an ally holds the hero's attention.
        if (inRange && victim == hero && def.Flanks && m.AttackCooldown <= 0 && !IsFlanking(m) && m.FlankTime < 3 && !def.Stationary
            && m.Creature.Conditions.CanMove && FlankSpot(m) is { } spot)
        {
            m.FlankTime += dt;
            m.State = MonsterState.Chase;
            m.RepathIn -= dt;
            if (m.Path.Count == 0 || m.RepathIn <= 0)
            {
                PlanPath(m, spot.X, spot.Y);
                m.RepathIn = 0.4;
            }
            if (FollowPath(m, MonsterSpeed(m), dt, null))
                m.Play("run");
            return;
        }
        if (inRange)
        {
            m.FlankTime = 0;
            m.Path.Clear();
            m.Face(victim.X, victim.Y);
            if (m.AttackCooldown <= 0)
            {
                m.State = MonsterState.Attack;
                m.AttackElapsed = 0;
                m.AttackLanded = false;
                if (def.Unseen)
                    RevealMonster(m);                                    // it shows itself as it strikes
                var anim = def.Ranged ? (def.Caster || def.Sprite.Contains("mage") ? "cast" : "shoot") : "swing";
                if (def.Ranged)
                    Sound(anim == "cast" ? SoundCue.WitchCast : SoundCue.Bow, m);
                var windup = Math.Min(0.6, 0.6 / (m.Creature.AttacksPerSecond * BossAttackRate(m)));
                m.Play(anim, 0.5 / windup, restart: true);
            }
            else if (m.Animation is not ("swing" or "shoot" or "cast") || m.AnimationTime > 1)
            {
                m.Play("stance");
            }
            return;
        }

        if (def.Stationary || m.Fused)
            return;
        m.State = MonsterState.Chase;
        m.RepathIn -= dt;
        if (m.Path.Count == 0 || m.RepathIn <= 0)
        {
            PlanPath(m, victim.X, victim.Y);
            m.RepathIn = 0.5 + Random.Roll(0, 20) / 100.0;
        }
        if (!m.Creature.Conditions.CanMove)
        {
            m.Play("stance");
            return;
        }
        if (FollowPath(m, MonsterSpeed(m), dt, null))
            m.Play("run");
        else
            m.Play("stance");
    }

    /// <summary>A monster's walking speed: slowed by the Slowed condition, quickened by Vengeance and Haste.</summary>
    private double MonsterSpeed(MonsterActor m)
    {
        var slowed = m.Creature.Conditions.ActionTimeMultiplier;
        return m.Definition.MoveSpeed * (slowed > 1 ? 1 / slowed : 1) * VengeanceBonus(m).Speed * (m.Has(MonsterAffix.Hasted) ? HasteSpeed : 1);
    }

    private void UpdateSpawner(MonsterActor m, double dt)
    {
        var def = m.Definition;
        m.SummonIn -= dt;
        if (m.SummonIn > 0 || m.Summoned >= def.SummonCap || def.Summons is null)
        {
            if (m.Animation != "spawn" || m.AnimationTime > 1.2)
                m.Play("stance");
            return;
        }
        m.SummonIn = def.SummonEverySeconds;
        var alive = _monsters.Count(x => x.SummonedBy == m && !x.IsDead);
        if (alive >= 3)
            return;
        m.Summoned++;
        var child = SpawnNear(def.Summons, CreatureRank.Minion, m.X, m.Y, 1.5, m.PackId, m.Creature.Stats.Level);
        child.SummonedBy = m;
        child.Play("spawn", restart: true);
        Sound(SoundCue.GraveRise, m, "grave");
        Aggro(child);
        m.Play("spawn", restart: true);
    }

    /// <summary>Standard cover (Pathfinder): +2 AC against a ranged attack across a low wall, cart or barricade.</summary>
    public const int CoverAcBonus = 2;

    /// <summary>The AC a ranged attack from (x, y) at <paramref name="target"/> must also beat for cover (0 in the open).</summary>
    internal int CoverAgainst(double x, double y, Actor target) => Map.InCover(x, y, target.X, target.Y) ? CoverAcBonus : 0;

    /// <param name="shot">The blow is a shot arriving (an arrow or bolt): cover between the shooter and the hero counts.</param>
    private void MonsterStrike(MonsterActor m, bool shot = false)
    {
        if (Time < Hero.InvulnerableUntil)
        {
            Float(Hero, "dodged", FloatingTextKind.Miss, 0.3);
            return;
        }
        // Flanked: off-guard to this strike, unless the hero is off-guard already (prone, say).
        var flanked = IsFlanking(m) && !Hero.Character.Conditions.IsOffGuard;
        if (flanked && Time - _flankedShownAt > 3)
        {
            _flankedShownAt = Time;
            Float(Hero, "flanked", FloatingTextKind.Info, 0.5);
        }
        var (attack, damage, _) = VengeanceBonus(m);
        var cover = shot ? CoverAgainst(m.X, m.Y, Hero) : 0;
        var result = Strike.Resolve(m.Creature, Hero.Character, m.Cadence, Time, Random,
            targetAc: (flanked ? -ConditionSet.OffGuardAcPenalty : 0) + cover, attackBonus: attack, damageScale: damage);
        if (result.Damage > 0)
        {
            if (result.Degree == DegreeOfSuccess.CriticalSuccess)
                HeroCritsTaken++;
            // Devotion builds on every blow taken, blocked or not.
            var dealt = AuraSoak(ShieldBlock(result.Damage, m), Hero);
            if (dealt <= 0)
            {
                Hero.Resource.OnCombat(taken: true);
                return;
            }
            Sound(SoundCue.HeroHurt, Hero);
            if (!HurtHero(dealt))
                return;
            Inflict(m);
            Retaliate(m);
            if (m.Definition.PoisonOnHit is { } poison && Random.Roll(1, 100) <= m.Definition.PoisonChance * 100)
                Poison(Dice.Parse(poison));
        }
        else
        {
            Float(Hero, cover > 0 ? "cover" : "miss", FloatingTextKind.Miss);
        }
    }

    private double _flankedShownAt = double.MinValue;

    public const int ConcealedFlatCheckDc = 5;

    /// <summary>
    /// How far the hero's light reaches: the zone's own (6.5 tiles outdoors, less in a dungeon) plus the best light
    /// carried (the pilgrim's lantern adds 2.5).
    /// </summary>
    public double HeroLightRadius =>
        Zone.HeroLight + Hero.Character.LightBonus + Hero.Equipment.Bag.OfType<Item>().Concat(Hero.Equipment.Worn.Values).Select(i => i.Definition.LightRadius).DefaultIfEmpty(0).Max();

    /// <summary>
    /// Whether a point can be seen: within the hero's light or a fixed light of the zone (torches, braziers, shrines are
    /// legend lights), or anywhere in a zone that isn't truly dark. Lights reach about 80% of their drawn radius at full
    /// strength.
    /// </summary>
    public bool IsLit(double x, double y)
    {
        if (!Zone.IsDark)
            return true;
        bool Within(double lx, double ly, double r) => (x - lx) * (x - lx) + (y - ly) * (y - ly) <= r * r * 0.64;
        return Within(Hero.X, Hero.Y, HeroLightRadius) || Map.Lights.Any(l => Within(l.X, l.Y, l.Radius));
    }

    /// <summary>
    /// Striking at something in the dark: a monster outside every light is concealed, so the blow or spell must pass a
    /// DC 5 flat check; a failure is lost in the dark. True when it was.
    /// </summary>
    internal bool LostInTheDark(MonsterActor target)
    {
        // Act 3's unseen: a blow at one not revealed must pass Pathfinder's hidden check, DC 11.
        if (IsUnseen(target) && !Random.FlatCheck(UnseenFlatCheckDc))
        {
            Sound(SoundCue.Miss, target);
            Float(target, "missed the unseen", FloatingTextKind.Miss);
            return true;
        }
        if (IsLit(target.X, target.Y) || Random.FlatCheck(ConcealedFlatCheckDc))
            return false;
        Sound(SoundCue.Miss, target);
        Float(target, "lost in the dark", FloatingTextKind.Miss);
        return true;
    }

    /// <summary>Damage the hero takes: the number, the Hero Point or death. False when the hero fell for good.</summary>
    private bool HurtHero(int dealt)
    {
        if (Hero.IsHidden)
            Unhide("A blow drives you out of the shadows.");
        var outcome = Hero.Character.TakeDamage(dealt);
        Hero.Resource.OnCombat(taken: true);
        Float(Hero, dealt.ToString(), FloatingTextKind.DamageTaken);
        if (outcome == DeathOutcome.RevivedByHeroPoint)
        {
            Say("A Hero Point pulls you back from death!");
            Float(Hero, "HERO POINT", FloatingTextKind.Info, 1.0);
        }
        else if (outcome == DeathOutcome.Died)
        {
            HeroDied();
            return false;
        }
        else if (!Hero.IsSwinging && Hero.Path.Count == 0)
        {
            Hero.Play("hit", restart: true);
        }
        return true;
    }

    /// <summary>Persistent poison on the hero (a stronger dose replaces a weaker one).</summary>
    private void Poison(Dice dice)
    {
        if (Hero.Character.Defenses.Immunities.Contains(DamageType.Poison))
            return;
        Hero.Character.Persistent.Add(DamageType.Poison, dice);
        Float(Hero, "poisoned", FloatingTextKind.Info, 0.5);
    }

    public const double BogSaveSeconds = 2.0;
    public const double BogSpeed = 0.7;

    /// <summary>The Fortitude DC of the bog's rot: 13 + the zone's level (15 in the Fens).</summary>
    public int BogDc => 13 + Zone.Level;

    private double _bogTimer;

    /// <summary>
    /// Standing in poison bog: every <see cref="BogSaveSeconds"/> a Fortitude save against <see cref="BogDc"/>. A failure
    /// brings persistent poison (1d4, 2d4 on a critical failure) that runs on after the hero climbs out.
    /// </summary>
    private void Bog(double dt)
    {
        if (!Map.IsBog(Hero.X, Hero.Y) || Hero.DodgeRemaining > 0)
        {
            _bogTimer = 0;
            return;
        }
        _bogTimer += dt;
        if (_bogTimer < BogSaveSeconds)
            return;
        _bogTimer -= BogSaveSeconds;
        var save = Check.Roll(Random, Hero.Character.Save(StatisticKind.Fortitude).Total + PoisonSaveBonus(DamageType.Poison), BogDc);
        if (save.Degree >= DegreeOfSuccess.Success)
            return;
        if (Hero.Character.Persistent.DiceFor(DamageType.Poison) is null)
            Say("The bog's rot seeps into you. Get out of the water.");
        Poison(save.Degree == DegreeOfSuccess.CriticalFailure ? new Dice(2, 4) : new Dice(1, 4));
    }

    /// <summary>Persistent damage on the hero ticks (bog rot, poisoned blades). False when it killed the hero.</summary>
    private bool TickHeroPersistent(double dt)
    {
        foreach (var (type, amount) in Hero.Character.Persistent.Update(dt, Random))
        {
            var dealt = DamageCalculator.ApplyDefenses(amount, type, Hero.Character.Defenses);
            if (dealt > 0 && !HurtHero(dealt))
                return false;
        }
        return true;
    }

    /// <summary>The hero's walking speed here: slower in the bog.</summary>
    private double HeroSpeed() =>
        Hero.Character.MoveSpeedTilesPerSecond() * MapSpeedScale * (Map.IsBog(Hero.X, Hero.Y) ? BogSpeed : 1.0)
        * (Hero.IsHidden && !Hero.Character.SwiftSneak ? HiddenSpeed : 1.0);

    private void HeroDied()
    {
        ClearRogueState();
        EndAuras();
        ClearNecromancerState();
        Hero.Play("die", restart: true);
        Hero.RespawnIn = RespawnSeconds;
        Hero.EnthralledUntil = 0;
        Sound(SoundCue.Death, Hero);
        Hero.Order = null;
        Hero.Action = null;
        Hero.PickupTarget = null;
        Hero.TalkTarget = null;
        Hero.CacheTarget = null;
        Hero.CorpseTarget = null;
        Hero.TrapTarget = null;
        Hero.ShrineTarget = null;
        Hero.InteractTarget = null;
        OpenVendor = null;
        OpenShrine = null;
        EndDialogue();
        CloseStash();
        Hero.Goal = null;
        Hero.Path.Clear();
        _projectiles.Clear();
        _monsterShots.Clear();
        if (NearestShrine(Hero.X, Hero.Y) is { } shrine)
        {
            RespawnPoint = ShrineRespawnPoint(shrine);
            Say($"You have fallen. The Ember Heart drags you back to {shrine.Name}...");
        }
        else
        {
            Say("You have fallen. The Ember Heart drags you back to the road...");
        }
    }

    /// <summary>The shrine the hero would walk to soonest from (x, y), awake or not; null when the zone has none.</summary>
    internal ShrineDefinition? NearestShrine(double x, double y)
    {
        ShrineDefinition? best = null;
        var bestDistance = double.PositiveInfinity;
        foreach (var shrine in Zone.Shrines)
        {
            var (sx, sy) = ShrineRespawnPoint(shrine);
            var path = Pathfinder.Find(Map, ((int)x, (int)y), ((int)sx, (int)sy), maxNodes: Map.Width * Map.Height);
            // A shrine that can't be walked to only wins when none can: then the closest as the crow flies.
            var distance = path is null ? 1e6 + Math.Sqrt((sx - x) * (sx - x) + (sy - y) * (sy - y)) : PathLength(x, y, path);
            if (distance < bestDistance)
            {
                best = shrine;
                bestDistance = distance;
            }
        }
        return best;
    }

    private static double PathLength(double x, double y, List<(double X, double Y)> path)
    {
        var total = 0.0;
        foreach (var (px, py) in path)
        {
            total += Math.Sqrt((px - x) * (px - x) + (py - y) * (py - y));
            (x, y) = (px, py);
        }
        return total;
    }

    private static (double X, double Y) ShrineRespawnPoint(ShrineDefinition shrine) => (shrine.At[0] + 0.5, shrine.At[1] + 1.5);

    private void Respawn()
    {
        Experience.ApplyDeathPenalty();
        ResetBosses();
        Hero.Character.RestAtShrine();
        // HeroDied picks it too (for the message and a save made while dead); deaths outside a monster's strike only arrive here.
        if (NearestShrine(Hero.X, Hero.Y) is { } shrine)
            RespawnPoint = ShrineRespawnPoint(shrine);
        Hero.X = RespawnPoint.X;
        Hero.Y = RespawnPoint.Y;
        Hero.HealRemaining = 0;
        Hero.Resource.Reset();
        Hero.ReadyAt.Clear();
        Hero.FlourishReadyAt = 0;
        EndWard();
        Hero.AntidoteUntil = 0;
        Hero.Play("stance", restart: true);
        RestAllies();
        PlaceAllies();
        foreach (var m in _monsters.Where(m => !m.IsDead))
        {
            m.State = MonsterState.Idle;
            m.Path.Clear();
            m.AttackElapsed = -1;
        }
    }

    private void ActivateShrine(ShrineDefinition shrine)
    {
        var at = ShrineRespawnPoint(shrine);
        var first = _activatedShrines.Add(shrine.Id);
        if (!first && RespawnPoint == at)
            return;
        RespawnPoint = at;
        ShrineActivated?.Invoke(shrine);
        var c = Hero.Character;
        c.Potion.Refill();
        c.Taint.Cleanse();
        Hero.Cold = 0;
        if (first)
        {
            c.Heal(c.MaxHitPoints);
            RestAllies();
            Say($"{shrine.Name} awakens. Potions refilled.");
            Sound(SoundCue.Shrine, Hero);
            Float(Hero, "SHRINE", FloatingTextKind.Info, 1.0);
        }
    }

    private void Aggro(MonsterActor m)
    {
        if (m.IsDead || m.State != MonsterState.Idle)
            return;
        m.State = MonsterState.Chase;
        foreach (var other in _monsters.Where(o => o.PackId == m.PackId && o != m && !o.IsDead && o.State == MonsterState.Idle))
        {
            if (other.DistanceTo(m) <= PackAggroRadius)
                Aggro(other);
        }
    }

    /// <summary>Kills a monster outright through the normal death path (XP, loot, the slain list). Tests and the debug menu.</summary>
    internal void Slay(MonsterActor monster)
    {
        if (monster.IsDead)
            return;
        monster.Creature.TakeDamage(monster.Creature.HitPoints);
        Kill(monster, false);
    }

    // ----- Restoring a save -------------------------------------------------------------

    /// <summary>Puts back a trader's saved wares (unknown traders, from an older zone file, are skipped).</summary>
    internal void RestoreVendor(string id, int stockedForLevel, IEnumerable<Item> stock)
    {
        if (_vendors.FirstOrDefault(v => v.Definition.Id == id) is not { } vendor)
            return;
        vendor.SetStock(stock);
        vendor.StockedForLevel = stockedForLevel;
    }

    /// <summary>
    /// Puts a saved world back: the hero's place, the respawn point and shrines, the monsters already killed, the
    /// loot left lying around and whether the zone was finished. Called right after construction.
    /// </summary>
    internal void Restore(double heroX, double heroY, (double X, double Y) respawn, IEnumerable<string> shrines,
        IEnumerable<int> slain, IEnumerable<GroundItem> ground, bool complete, int level, int xp, string? explored = null,
        IEnumerable<string>? openedCaches = null)
    {
        foreach (var id in openedCaches ?? [])
        {
            if (_caches.FirstOrDefault(c => c.Definition.Id == id) is { } cache)
                cache.Opened = true;
        }
        RestoreExplored(explored);
        Hero.X = heroX;
        Hero.Y = heroY;
        // A save made standing in an exit doesn't send the hero through it the moment it loads.
        _insideExit = Zone.Exits.FirstOrDefault(e => Hero.DistanceTo(e.At[0] + 0.5, e.At[1] + 0.5) <= e.Radius);
        RespawnPoint = respawn;
        foreach (var id in shrines)
            _activatedShrines.Add(id);
        foreach (var index in slain)
            _slain.Add(index);
        _monsters.RemoveAll(m => m.SpawnIndex >= 0 && _slain.Contains(m.SpawnIndex));
        _ground.AddRange(ground);
        ZoneComplete = complete;
        Experience.Restore(level, xp);
        RefreshBar();
        PlaceAllies();
    }

    // ----- Movement ------------------------------------------------------------------

    private void PlanPath(Actor actor, double x, double y)
    {
        actor.Path.Clear();
        if (WalkableLine(actor.X, actor.Y, x, y) && BogCrossed(actor.X, actor.Y, x, y) == 0)
        {
            actor.Path.Add((x, y));
            return;
        }
        var path = Pathfinder.Find(Map, ((int)actor.X, (int)actor.Y), ((int)Math.Floor(x), (int)Math.Floor(y)));
        if (path is null)
            return;
        if (path.Count > 0 && Map.IsWalkable(x, y))
            path[^1] = (x, y);
        // Drop waypoints we can already see past, so movement isn't stuck to the grid; but never cut across more bog
        // than the path itself crosses, or the shortcut would wade through the pool the path went round.
        var smoothed = new List<(double X, double Y)>();
        var from = (actor.X, actor.Y);
        var bog = 0;
        for (var i = 0; i < path.Count; i++)
        {
            bog += Map.IsBog(path[i].X, path[i].Y) ? 1 : 0;
            if (i + 1 < path.Count && WalkableLine(from.Item1, from.Item2, path[i + 1].X, path[i + 1].Y)
                && BogCrossed(from.Item1, from.Item2, path[i + 1].X, path[i + 1].Y) <= bog + (Map.IsBog(path[i + 1].X, path[i + 1].Y) ? 1 : 0))
                continue;
            smoothed.Add(path[i]);
            from = path[i];
            bog = 0;
        }
        actor.Path.AddRange(smoothed);
    }

    /// <summary>How many bog cells a straight line passes through, leaving its start cell out.</summary>
    private int BogCrossed(double x0, double y0, double x1, double y1)
    {
        var distance = Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
        var steps = Math.Max(1, (int)(distance * 4));
        var start = ((int)Math.Floor(x0), (int)Math.Floor(y0));
        var last = start;
        var count = 0;
        for (var i = 1; i <= steps; i++)
        {
            var t = (double)i / steps;
            var cell = ((int)Math.Floor(x0 + (x1 - x0) * t), (int)Math.Floor(y0 + (y1 - y0) * t));
            if (cell == last)
                continue;
            last = cell;
            if (Map.IsBog(cell.Item1, cell.Item2))
                count++;
        }
        return count;
    }

    private bool WalkableLine(double x0, double y0, double x1, double y1)
    {
        var distance = Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
        var steps = Math.Max(1, (int)(distance * 5));
        for (var i = 1; i <= steps; i++)
        {
            var t = (double)i / steps;
            double px = x0 + (x1 - x0) * t, py = y0 + (y1 - y0) * t;
            if (!Map.IsWalkable(px, py) || !Map.IsWalkable(px + 0.25, py) || !Map.IsWalkable(px - 0.25, py)
                || !Map.IsWalkable(px, py + 0.25) || !Map.IsWalkable(px, py - 0.25))
                return false;
        }
        return true;
    }

    /// <summary>Moves along the path; returns false once there is nowhere left to go.</summary>
    private static bool FollowPath(Actor actor, double speed, double dt, (double X, double Y)? _)
    {
        var budget = speed * dt;
        var moved = false;
        while (budget > 0 && actor.Path.Count > 0)
        {
            var (tx, ty) = actor.Path[0];
            var d = actor.DistanceTo(tx, ty);
            if (d < 1e-4)
            {
                actor.Path.RemoveAt(0);
                continue;
            }
            actor.Face(tx, ty);
            var step = Math.Min(budget, d);
            actor.X += (tx - actor.X) / d * step;
            actor.Y += (ty - actor.Y) / d * step;
            budget -= step;
            moved = true;
            if (step >= d)
                actor.Path.RemoveAt(0);
        }
        return moved || actor.Path.Count > 0;
    }

    /// <summary>The extra gap (tiles) allies keep from one another beyond their radii.</summary>
    private const double AllyRoom = 0.3;

    /// <summary>Soft separation so monsters crowd around the hero instead of stacking on one spot, and allies spread out.</summary>
    private void Separate()
    {
        var living = _monsters.Where(m => !m.IsDead).ToList();
        for (var i = 0; i < living.Count; i++)
        {
            var a = living[i];
            Push(a, Hero, onlyFirst: true);
            foreach (var ally in _allies)
            {
                if (!ally.IsDead)
                    Push(a, ally, onlyFirst: true);
            }
            for (var j = i + 1; j < living.Count; j++)
                Push(a, living[j], onlyFirst: false);
        }
        // Allies keep a little room between them too, so a pack of skeletons reads as a pack, not one stacked figure.
        var allies = _allies.Where(a => !a.IsDead).ToList();
        for (var i = 0; i < allies.Count; i++)
        for (var j = i + 1; j < allies.Count; j++)
            Push(allies[i], allies[j], onlyFirst: false, room: AllyRoom);

        void Push(Actor a, Actor b, bool onlyFirst, double room = 0)
        {
            double dx = a.X - b.X, dy = a.Y - b.Y;
            var d = Math.Sqrt(dx * dx + dy * dy);
            var min = a.Radius + b.Radius + room;
            if (d >= min || d < 1e-6 && room == 0)
                return;
            if (d < 1e-6)
            {
                // Allies exactly on top of each other: part them along a fixed slant.
                (dx, dy, d) = (0.6, 0.8, 1e-3);
                dx *= d;
                dy *= d;
            }
            var push = (min - d) * (onlyFirst ? 1.0 : 0.5);
            double nx = dx / d * push, ny = dy / d * push;
            TryShift(a, nx, ny);
            if (!onlyFirst)
                TryShift(b, -nx, -ny);
        }

        void TryShift(Actor actor, double dx, double dy)
        {
            if (actor is MonsterActor { Definition.Stationary: true })
                return;
            if (Map.IsWalkable(actor.X + dx, actor.Y + dy))
            {
                actor.X += dx;
                actor.Y += dy;
            }
        }
    }

    // ----- Helpers -------------------------------------------------------------------

    private MonsterActor SpawnNear(string creatureId, CreatureRank rank, double x, double y, double radius, int packId,
        int? level = null, IReadOnlyList<string>? affixNames = null)
    {
        var def = _creatures.TryGetValue(creatureId, out var d) ? d : throw new InvalidDataException($"Unknown creature '{creatureId}'.");
        var stats = CreatureStats.For(level ?? Zone.Level, rank);
        if (def.HitPointsScale != 1.0)
            stats = stats with { HitPoints = Math.Max(1, (int)Math.Round(stats.HitPoints * def.HitPointsScale)) };
        var dice = def.Damage is { } text ? Dice.Parse(text) : stats.DefaultStrikeDamage;
        var affixes = (affixNames ?? []).Select(a => Enum.TryParse<MonsterAffix>(a, ignoreCase: true, out var affix)
            ? affix : throw new InvalidDataException($"Unknown monster affix '{a}'.")).ToList();
        var prefix = string.Concat(affixes.Select(a => $"{a} "));
        var title = def.Named ? def.Name : rank switch
        {
            CreatureRank.Champion => $"{prefix}{def.Name} Champion",
            CreatureRank.Elite => $"{prefix}{def.Name}, the Elite",
            _ => prefix + def.Name,
        };
        var creature = new Creature(title, stats, dice, def.DamageType, def.BuildDefenses(),
            def.AttacksPerSecond * (affixes.Contains(MonsterAffix.Hasted) ? HasteAttackRate : 1));
        double px = x, py = y;
        for (var attempt = 0; attempt < 12; attempt++)
        {
            var angle = Random.Roll(0, 359) * Math.PI / 180;
            var r = radius * Random.Roll(0, 100) / 100.0;
            px = x + Math.Cos(angle) * r;
            py = y + Math.Sin(angle) * r;
            if (Map.IsWalkable(px, py))
                break;
            (px, py) = (x, y);
        }
        var monster = new MonsterActor(def, creature, packId)
        {
            X = px,
            Y = py,
            Direction = Random.Roll(0, 7),
            Radius = def.Stationary ? 0.6 : 0.35 * def.Scale,
            Affixes = affixes,
            HomeX = x,
            HomeY = y,
        };
        if (def.Boss is { } boss)
        {
            (monster.X, monster.Y) = (x, y);
            monster.PoolIn = boss.Phases[0].PoolEverySeconds;
            monster.FloodRadius = boss.ArenaRadius;
        }
        if (def.IsSpawner)
            monster.SummonIn = 1.0;
        _monsters.Add(monster);
        return monster;
    }

    private static double SwingRate(double swingSeconds) => 0.4 / Math.Max(0.1, swingSeconds);

    private void Float(Actor at, string text, FloatingTextKind kind, double lift = 0) =>
        _texts.Add(new FloatingText(at.X - lift * 0.3, at.Y - lift * 0.3, text, kind, Time));

    /// <summary>Shows a message at the top of the screen (saving, loading).</summary>
    public void Announce(string message) => Say(message);

    private void Say(string message)
    {
        LastMessage = message;
        LastMessageAt = Time;
    }
}
