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
        ["none"] = [],
    };

    private readonly int[,] _ground;
    private readonly int[]?[,] _objects;
    private readonly bool[,] _blocked;
    private readonly bool[,] _blocksSight;
    private readonly bool[,] _water;

    public TileMap(ZoneDefinition zone)
    {
        Width = zone.Width;
        Height = zone.Height;
        _ground = new int[Width, Height];
        _objects = new int[]?[Width, Height];
        _blocked = new bool[Width, Height];
        _blocksSight = new bool[Width, Height];
        _water = new bool[Width, Height];

        for (var y = 0; y < Height; y++)
        {
            var row = zone.Rows[y];
            for (var x = 0; x < Width; x++)
            {
                var key = x < row.Length ? row[x].ToString() : " ";
                if (!zone.Legend.TryGetValue(key, out var entry))
                    throw new InvalidDataException($"Zone '{zone.Id}' uses '{key}' at ({x}, {y}) but its legend doesn't define it.");
                var set = GroundSets.TryGetValue(entry.Ground, out var ids) ? ids : throw new InvalidDataException($"Unknown ground set '{entry.Ground}'.");
                _ground[x, y] = set.Length == 0 ? -1 : set[Hash(x, y, 0) % set.Length];
                if (entry.Objects is { Count: > 0 } objects)
                    _objects[x, y] = entry.Stack ? [.. objects] : [objects[Hash(x, y, 1) % objects.Count]];
                _water[x, y] = entry.Ground == "water";
                _blocked[x, y] = entry.Blocks || _water[x, y];
                _blocksSight[x, y] = entry.BlocksSight;
            }
        }
        ShapeShores();
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

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    /// <summary>The ground tile id, or -1 for no ground (void).</summary>
    public int Ground(int x, int y) => InBounds(x, y) ? _ground[x, y] : -1;

    public IReadOnlyList<int> Objects(int x, int y) => InBounds(x, y) ? _objects[x, y] ?? [] : [];

    public bool IsBlocked(int x, int y) => !InBounds(x, y) || _blocked[x, y];

    /// <summary>True for a cell the zone file marks as water (its edge cells show a shore).</summary>
    public bool IsWater(int x, int y) => InBounds(x, y) && _water[x, y];

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
            if (!InBounds(cx, cy) || _blocksSight[cx, cy])
                return false;
        }
        return true;
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
