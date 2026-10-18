namespace AshenReliquary.Core.World;

/// <summary>
/// A plain <see cref="Cache"/> (a wooden chest) or a <see cref="Reliquary"/> (a spiked skull chest, rarer and richer,
/// usually guarded).
/// </summary>
public enum CacheKind
{
    Cache,
    Reliquary,
}

/// <summary>A chest placed in a zone. <see cref="Facing"/> picks one of the art's two orientations (0 or 1).</summary>
public sealed record CacheDefinition(string Id, int[] At, CacheKind Kind = CacheKind.Cache, int Facing = 0);

/// <summary>
/// A chest in play. Clicking it walks the hero over and opens it; its loot is rolled then, at the hero's level, and
/// spills onto the ground around it. It opens once (saves remember which).
/// </summary>
public sealed class Cache
{
    public const double OpenRadius = 1.6;

    internal Cache(CacheDefinition definition)
    {
        Definition = definition;
        X = definition.At[0] + 0.5;
        Y = definition.At[1] + 0.5;
    }

    public CacheDefinition Definition { get; }
    public double X { get; }
    public double Y { get; }
    public bool Opened { get; internal set; }

    public string Name => Definition.Kind == CacheKind.Reliquary ? "Reliquary chest" : "Cache";

    public double DistanceTo(double x, double y) => Math.Sqrt((X - x) * (X - x) + (Y - y) * (Y - y));
}
