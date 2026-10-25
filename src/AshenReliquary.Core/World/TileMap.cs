namespace AshenReliquary.Core.World;

/// <summary>
/// The zone's grid: a ground tile and up to a few object tiles per cell, plus collision. Cell (x, y) covers
/// map coordinates [x, x+1) x [y, y+1); its centre is (x + 0.5, y + 0.5).
/// </summary>
public sealed class TileMap
{
    public static readonly IReadOnlyDictionary<string, int[]> GroundSets = new Dictionary<string, int[]>
    {
        ["grass"] = Enumerable.Range(16, 16).ToArray(),
        ["path"] = Enumerable.Range(32, 12).ToArray(),
        ["path_edge"] = Enumerable.Range(44, 4).ToArray(),
        ["water"] = Enumerable.Range(176, 16).ToArray(),
        // The Fens' bog: grass the renderer darkens to wet, rotten olive (Flare's water sits a level down, so it can't
        // lie flat among the ground tiles).
        ["bog"] = Enumerable.Range(16, 16).ToArray(),
        // Act 4's (tileset_cinder): ash is the cave set's earth recoloured; lava and embers are drawn by ShapeLava and by
        // the cell's parity, so these ids are only a fallback.
        ["ash"] = Enumerable.Range(16, 16).ToArray(),
        ["lava"] = [LavaTile + 4 * 15],
        ["embers"] = [EmberTile],
        ["none"] = [],
    };

    private readonly int[,] _ground;
    private readonly int[]?[,] _objects;
    private readonly bool[,] _blocked;
    private readonly bool[,] _blocksSight;
    private readonly bool[,] _water;
    private readonly bool[,] _bog;
    private readonly bool[,] _cover;
    private readonly bool[,] _avoid;
    private readonly bool[,] _lava;
    private readonly bool[,] _gate;
    private readonly Dictionary<string, List<(int X, int Y)>> _platforms = [];
    private readonly Dictionary<(int X, int Y), (int Ground, bool Blocked, bool Lava, bool Embers)> _sunk = [];
    private readonly bool[,] _embers;

    /// <summary>Act 4's lava tiles: <see cref="LavaTile"/> + 4 x the corners that are lava + the cell's parity.</summary>
    public const int LavaTile = 3000;

    /// <summary>Act 4's embers: a walkable crust that burns, by the cell's parity.</summary>
    public const int EmberTile = 3100;

    /// <summary>What a step onto embers costs the pathfinder: walkers keep to cool ground where they can.</summary>
    public const double EmberStepCost = 4.0;

    /// <summary>What a step onto a bog cell costs the pathfinder: walkers go round a pool when the way round is short.</summary>
    public const double BogStepCost = 3.0;

    public TileMap(ZoneDefinition zone)
    {
        Width = zone.Width;
        Height = zone.Height;
        _ground = new int[Width, Height];
        _objects = new int[]?[Width, Height];
        _blocked = new bool[Width, Height];
        _blocksSight = new bool[Width, Height];
        _water = new bool[Width, Height];
        _bog = new bool[Width, Height];
        _cover = new bool[Width, Height];
        _avoid = new bool[Width, Height];
        _lava = new bool[Width, Height];
        _gate = new bool[Width, Height];
        _embers = new bool[Width, Height];

        for (var y = 0; y < Height; y++)
        {
            var row = zone.Rows[y];
            for (var x = 0; x < Width; x++)
            {
                var key = x < row.Length ? row[x].ToString() : " ";
                if (!zone.Legend.TryGetValue(key, out var entry))
                    throw new InvalidDataException($"Zone '{zone.Id}' uses '{key}' at ({x}, {y}) but its legend doesn't define it.");
                var set = entry.Tiles is { Count: > 0 } tiles ? [.. tiles]
                    : GroundSets.TryGetValue(entry.Ground, out var ids) ? ids : throw new InvalidDataException($"Unknown ground set '{entry.Ground}'.");
                _ground[x, y] = set.Length == 0 ? -1 : set[Hash(x, y, 0) % set.Length];
                if (entry.Objects is { Count: > 0 } objects)
                    _objects[x, y] = entry.Stack ? [.. objects] : [objects[Hash(x, y, 1) % objects.Count]];
                _water[x, y] = entry.Ground == "water";
                _bog[x, y] = entry.Ground == "bog";
                _lava[x, y] = entry.Ground == "lava";
                _embers[x, y] = entry.Ground == "embers";
                if (_embers[x, y] && entry.Tiles is not { Count: > 0 })
                    _ground[x, y] = EmberTile + (x & 1) + 2 * (y & 1);
                // Lava can't be walked, but a bridge over it can.
                _blocked[x, y] = entry.Blocks || _water[x, y] || _lava[x, y] && !entry.Bridge;
                _blocksSight[x, y] = entry.BlocksSight;
                _cover[x, y] = entry.Cover;
                if (entry.Crumbles is { } group)
                {
                    if (!_platforms.TryGetValue(group, out var cells))
                        _platforms[group] = cells = [];
                    cells.Add((x, y));
                }
                if (entry.Light > 0)
                    _lights.Add(new MapLight(x + 0.5, y + 0.5, entry.Light, entry.LightColor));
            }
        }
        ShapeShores();
        ShapeLava();
        // Traders and chests stand on their tile: walk round them, not through.
        foreach (var at in (zone.Vendors ?? []).Select(v => v.At).Concat((zone.Caches ?? []).Select(c => c.At))
                     .Concat((zone.Npcs ?? []).Select(n => n.At)).Concat((zone.Interactables ?? []).Where(i => i.Blocks).Select(i => i.At)))
        {
            if (InBounds(at[0], at[1]))
                _blocked[at[0], at[1]] = true;
        }
    }

    public int Width { get; }
    public int Height { get; }

    private readonly List<MapLight> _lights = [];

    /// <summary>The zone's fixed lights (torches, braziers), from its legend.</summary>
    public IReadOnlyList<MapLight> Lights => _lights;

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    /// <summary>The ground tile id, or -1 for no ground (void).</summary>
    public int Ground(int x, int y) => InBounds(x, y) ? _ground[x, y] : -1;

    public IReadOnlyList<int> Objects(int x, int y) => InBounds(x, y) ? _objects[x, y] ?? [] : [];

    public bool IsBlocked(int x, int y) => !InBounds(x, y) || _blocked[x, y] || _gate[x, y];

    /// <summary>True for a cell nothing can be seen through (a wall, solid rock, a tree trunk, a ward still burning).</summary>
    public bool BlocksSight(int x, int y) => !InBounds(x, y) || _blocksSight[x, y] || _gate[x, y];

    /// <summary>A boss arena's platforms that can sink (legend <c>crumbles</c>), by group, in map order.</summary>
    public IReadOnlyDictionary<string, List<(int X, int Y)>> Platforms => _platforms;

    /// <summary>Bumped whenever the ground of a cell changes in play (a platform cracking or sinking), for the renderer's cache.</summary>
    public int GroundVersion { get; private set; }

    /// <summary>A platform cracks: its cells show embers (and burn) until it sinks. The cell's ground is kept to restore.</summary>
    internal void Crack(int x, int y)
    {
        if (!InBounds(x, y) || _sunk.ContainsKey((x, y)))
            return;
        _sunk[(x, y)] = (_ground[x, y], _blocked[x, y], _lava[x, y], _embers[x, y]);
        _embers[x, y] = true;
        _ground[x, y] = EmberTile + (x & 1) + 2 * (y & 1);
        GroundVersion++;
    }

    /// <summary>A cracked cell goes into the lava: blocked, and the banks round it reshaped.</summary>
    internal void Sink(int x, int y)
    {
        if (!InBounds(x, y))
            return;
        if (!_sunk.ContainsKey((x, y)))
            _sunk[(x, y)] = (_ground[x, y], _blocked[x, y], _lava[x, y], _embers[x, y]);
        _embers[x, y] = false;
        _lava[x, y] = true;
        _blocked[x, y] = true;
        ReshapeLava(x, y);
        GroundVersion++;
    }

    /// <summary>Every cracked or sunk cell back as it was (the fight starts again).</summary>
    internal void RaisePlatforms()
    {
        if (_sunk.Count == 0)
            return;
        foreach (var ((x, y), was) in _sunk)
        {
            (_ground[x, y], _blocked[x, y], _lava[x, y], _embers[x, y]) = was;
        }
        foreach (var (x, y) in _sunk.Keys.ToList())
            ReshapeLava(x, y);
        _sunk.Clear();
        GroundVersion++;
    }

    /// <summary>A cell sunk or cracked in play (cracked: still embers; sunk: lava).</summary>
    public bool IsCrumbling(int x, int y) => _sunk.ContainsKey((x, y)) && !_lava[x, y];

    /// <summary>The lava tiles round a changed cell chosen again by their corners.</summary>
    private void ReshapeLava(int cx, int cy)
    {
        bool LavaPoint(int px, int py) => IsLava(px - 1, py - 1) && IsLava(px, py - 1) && IsLava(px - 1, py) && IsLava(px, py);
        for (var y = cy - 1; y <= cy + 1; y++)
        for (var x = cx - 1; x <= cx + 1; x++)
        {
            if (!InBounds(x, y) || !_lava[x, y])
                continue;
            var corners = (LavaPoint(x, y) ? 1 : 0) | (LavaPoint(x + 1, y) ? 2 : 0) | (LavaPoint(x + 1, y + 1) ? 4 : 0) | (LavaPoint(x, y + 1) ? 8 : 0);
            var parity = (x & 1) + 2 * (y & 1);
            _ground[x, y] = corners == 0 ? EmberTile + parity : LavaTile + 4 * corners + parity;
        }
    }

    /// <summary>True for a cell a ward still closes (the Reliquary's).</summary>
    public bool IsGate(int x, int y) => InBounds(x, y) && _gate[x, y];

    /// <summary>Closes a cell with a ward, or opens it.</summary>
    internal void SetGate(int x, int y, bool closed)
    {
        if (InBounds(x, y))
            _gate[x, y] = closed;
    }

    /// <summary>True for a cell the zone file marks as water (its edge cells show a shore).</summary>
    public bool IsWater(int x, int y) => InBounds(x, y) && _water[x, y];

    /// <summary>True for poison bog: walkable, but slow and sickening to stand in.</summary>
    public bool IsBog(int x, int y) => InBounds(x, y) && _bog[x, y];

    public bool IsBog(double x, double y) => IsBog((int)Math.Floor(x), (int)Math.Floor(y));

    /// <summary>Floods a cell with poison bog, or drains it (a boss's rising water).</summary>
    internal void SetBog(int x, int y, bool bog)
    {
        if (InBounds(x, y))
            _bog[x, y] = bog;
    }

    /// <summary>What a step onto a spotted trap costs the pathfinder: walkers go well round one.</summary>
    public const double AvoidStepCost = 25.0;

    /// <summary>Marks a cell to walk round (a spotted trap), or clears it.</summary>
    internal void SetAvoid(int x, int y, bool avoid)
    {
        if (InBounds(x, y))
            _avoid[x, y] = avoid;
    }

    /// <summary>The pathfinder's cost multiplier for stepping onto a cell.</summary>
    public double StepCost(int x, int y) => InBounds(x, y) && _avoid[x, y] ? AvoidStepCost : IsBog(x, y) ? BogStepCost
        : IsEmbers(x, y) ? EmberStepCost : 1.0;

    /// <summary>True for a lava cell (a bridge over lava too).</summary>
    public bool IsLava(int x, int y) => InBounds(x, y) && _lava[x, y];

    /// <summary>True for embers: walkable, and it burns.</summary>
    public bool IsEmbers(int x, int y) => InBounds(x, y) && _embers[x, y];

    public bool IsEmbers(double x, double y) => IsEmbers((int)Math.Floor(x), (int)Math.Floor(y));

    /// <summary>
    /// Lava's banks, as <see cref="ShapeShores"/> does a pond's, but with Act 4's own tiles, which lie level with the
    /// ground: a grid point is lava when the four cells round it are, and each lava cell shows the tile for which of its
    /// corners are (1 top, 2 right, 4 bottom, 8 left), in the variant its parity picks, so the lava runs on unbroken
    /// from tile to tile. A lava cell with no lava corner (a lone one) shows its bank's lip as a pool of embers.
    /// </summary>
    private void ShapeLava()
    {
        bool LavaPoint(int px, int py) => IsLava(px - 1, py - 1) && IsLava(px, py - 1) && IsLava(px - 1, py) && IsLava(px, py);
        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
        {
            if (!_lava[x, y])
                continue;
            var corners = (LavaPoint(x, y) ? 1 : 0) | (LavaPoint(x + 1, y) ? 2 : 0) | (LavaPoint(x + 1, y + 1) ? 4 : 0) | (LavaPoint(x, y + 1) ? 8 : 0);
            var parity = (x & 1) + 2 * (y & 1);
            _ground[x, y] = corners == 0 ? EmberTile + parity : LavaTile + 4 * corners + parity;
        }
    }

    /// <summary>
    /// Flare's water lies a level below the ground, so a pond needs its banks: cliff tiles whose grass top covers the
    /// land corners of a cell and whose face drops to the water at the others. Corners are grid points: a point is
    /// water when all four cells around it are water, so the banks take up the pond's outer ring of cells. On screen
    /// cell (x, y) has its top corner at point (x, y), right at (x + 1, y), bottom at (x + 1, y + 1) and left at
    /// (x, y + 1). Each shape has two variants; a cell whose corners are all land (a lone water cell) becomes grass.
    /// </summary>
    private void ShapeShores()
    {
        bool WaterPoint(int px, int py) =>
            IsWater(px - 1, py - 1) && IsWater(px, py - 1) && IsWater(px - 1, py) && IsWater(px, py);
        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
        {
            if (!_water[x, y])
                continue;
            bool top = !WaterPoint(x, y), right = !WaterPoint(x + 1, y), bottom = !WaterPoint(x + 1, y + 1), left = !WaterPoint(x, y + 1);
            int? shape = (top, right, bottom, left) switch
            {
                (false, false, false, false) => null,
                (true, true, true, true) => -1,
                (true, false, false, false) => 160,
                (false, true, false, false) => 161,
                (false, false, true, false) => 162,
                (false, false, false, true) => 163,
                (true, false, false, true) => 144,
                (true, true, false, false) => 145,
                (false, true, true, false) => 146,
                (false, false, true, true) => 147,
                (true, true, false, true) => 152,
                (true, true, true, false) => 153,
                (false, true, true, true) => 154,
                (true, false, true, true) => 155,
                // Opposite corners only (a pinched pond): show the bank on the side that has more land around it.
                (true, false, true, false) => 153,
                (false, true, false, true) => 154,
            };
            if (shape == -1)
            {
                _ground[x, y] = GroundSets["grass"][Hash(x, y, 0) % GroundSets["grass"].Length];
                _blocked[x, y] = _objects[x, y] is not null;
            }
            else if (shape is { } id)
            {
                _ground[x, y] = id + 4 * (Hash(x, y, 2) % 2); // each shape's second variant is 4 ids on
            }
        }
    }

    public bool IsWalkable(double x, double y) => !IsBlocked((int)Math.Floor(x), (int)Math.Floor(y));

    /// <summary>True when nothing that blocks sight lies on the straight line between the two points.</summary>
    public bool HasLineOfSight(double x0, double y0, double x1, double y1)
    {
        var distance = Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
        var steps = Math.Max(1, (int)(distance * 4));
        for (var i = 1; i < steps; i++)
        {
            var t = (double)i / steps;
            int cx = (int)Math.Floor(x0 + (x1 - x0) * t), cy = (int)Math.Floor(y0 + (y1 - y0) * t);
            if (!InBounds(cx, cy) || _blocksSight[cx, cy] || _gate[cx, cy])
                return false;
        }
        return true;
    }

    /// <summary>True for a low wall, cart or barricade: something to shelter behind from arrows and bolts.</summary>
    public bool IsCover(int x, int y) => InBounds(x, y) && _cover[x, y];

    /// <summary>How close behind cover a target must stand for it to shelter them.</summary>
    public const double CoverReach = 2.0;

    /// <summary>
    /// Whether a ranged attack from (x0, y0) at (x1, y1) is against cover: its line crosses a cover cell within
    /// <see cref="CoverReach"/> tiles of the target (not the target's own cell). An attacker that close itself shoots
    /// past the cover, as it would lean over a wall.
    /// </summary>
    public bool InCover(double x0, double y0, double x1, double y1)
    {
        var distance = Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
        if (distance <= CoverReach + 0.5)
            return false;
        int tx = (int)Math.Floor(x1), ty = (int)Math.Floor(y1);
        var steps = Math.Max(1, (int)(distance * 4));
        for (var i = 1; i < steps; i++)
        {
            var t = (double)i / steps;
            if (distance * (1 - t) > CoverReach)
                continue;
            int cx = (int)Math.Floor(x0 + (x1 - x0) * t), cy = (int)Math.Floor(y0 + (y1 - y0) * t);
            if ((cx, cy) != (tx, ty) && IsCover(cx, cy))
                return true;
        }
        return false;
    }

    /// <summary>A stable pseudo-random pick per cell, so the same zone always looks the same.</summary>
    internal static int Hash(int x, int y, int salt)
    {
        unchecked
        {
            var h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(salt * 83492791);
            h ^= h >> 13;
            h *= 0x5bd1e995;
            h ^= h >> 15;
            return (int)(h & 0x7fffffff);
        }
    }
}

/// <summary>A fixed light on the map: its centre, radius in tiles and colour ("#rrggbb", or null for firelight).</summary>
public sealed record MapLight(double X, double Y, double Radius, string? Color);
