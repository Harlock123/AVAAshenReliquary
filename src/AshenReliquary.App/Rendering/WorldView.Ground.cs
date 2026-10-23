using AshenReliquary.Core.Content.Flare;
using AshenReliquary.Core.World;
using SkiaSharp;

namespace AshenReliquary.App.Rendering;

/// <summary>
/// The ground, cached. Drawing every ground tile one by one was the biggest single cost of a frame: in a fullscreen
/// window on this machine over a thousand small draws, about 7 ms of CPU a frame, which held fights to the mid-40s.
/// The static ground is drawn once into chunks of <see cref="ChunkCells"/> × <see cref="ChunkCells"/> cells (GPU
/// textures on the GPU renderer), and a frame draws a few dozen chunk images. Bog cells (their shade swells and fades)
/// and animated tiles stay live, drawn tile by tile over their chunk. The chunks are kept per zone, zoom and pixel
/// density, built as they come into view and dropped two seconds after they leave it; while the zoom glides, the ground is drawn
/// tile by tile as before.
/// <para>
/// Every ground tile, cached or live, sits at its own whole-pixel position relative to the map plus the camera's whole-
/// pixel offset, so chunks meet each other and the live tiles without a seam.
/// </para>
/// </summary>
public sealed partial class WorldView
{
    /// <summary>A chunk's side in cells.</summary>
    public const int ChunkCells = 4;

    /// <summary>A chunk out of view this many frames (two seconds) is dropped; walking back builds it again.</summary>
    private const int KeepFrames = 120;

    private enum GroundKind
    {
        Plain,
        Bog,
        Rim,
        Fringe,
    }

    private sealed class GroundChunk
    {
        public SKImage? Image;
        public int Left, Top;
        public readonly List<(int X, int Y)> Live = [];
        public long Used;
    }

    private readonly Dictionary<(int X, int Y), GroundChunk> _chunks = [];
    private (TileMap Map, double Zoom, float Scale, bool Gpu)? _chunkKey;
    private long _chunkFrame;

    /// <summary>False draws the ground tile by tile, as while zooming (tests compare the two).</summary>
    internal bool CacheGround { get; set; } = true;

    /// <summary>How many ground chunks are held (tests).</summary>
    internal int GroundChunks => _chunks.Count;

    /// <summary>The ground tile of a cell, and how it is shaded: beyond the map's edge outdoors the forest floor carries on.</summary>
    private static FlareTile? GroundAt(GameWorld world, FlareTileset tileset, int x, int y, out GroundKind kind)
    {
        kind = GroundKind.Plain;
        var map = world.Map;
        var id = map.InBounds(x, y) ? map.Ground(x, y) : world.Zone.Outdoors ? OutsideGround(x, y) : -1;
        if (id < 0 || !tileset.Tiles.TryGetValue(id, out var tile))
            return null;
        var wet = (map.IsBog(x + 1, y) ? 1 : 0) + (map.IsBog(x - 1, y) ? 1 : 0) + (map.IsBog(x, y + 1) ? 1 : 0) + (map.IsBog(x, y - 1) ? 1 : 0);
        if (map.IsBog(x, y))
            kind = wet < 4 ? GroundKind.Rim : GroundKind.Bog;
        else if (map.InBounds(x, y) && !map.IsWater(x, y) && wet > 0)
            kind = GroundKind.Fringe;
        return tile;
    }

    /// <summary>A ground tile's top-left in device pixels relative to the map's origin, camera aside.</summary>
    private static (int X, int Y) TileDevice(FlareTile tile, int x, int y, float zs) =>
        ((int)MathF.Round(((x - y) * HalfTileWidth - tile.OffsetX) * zs), (int)MathF.Round(((x + y + 1) * HalfTileHeight - tile.OffsetY) * zs));

    private void DrawGroundChunks(SKCanvas canvas, GameWorld world, double time, int x0, int y0, int x1, int y1,
        Func<GroundKind, SKPaint?> paintFor)
    {
        var tileset = _assets.Tileset(world.Zone.Tileset);
        var ds = _deviceScale;
        var z = (float)Zoom;
        var zs = z * ds;
        var gpu = _worldOnGpu && _gpuContext is not null;
        var key = (world.Map, Zoom, ds, gpu);
        if (_chunkKey != key)
        {
            ClearGroundChunks();
            _chunkKey = key;
        }
        _chunkFrame++;
        var (cx, cy) = Camera;
        var camX = (int)MathF.Round((float)((-(cx - cy) * HalfTileWidth * z + Bounds.Width / 2) * ds));
        var camY = (int)MathF.Round((float)((-(cx + cy) * HalfTileHeight * z + Bounds.Height / 2) * ds));
        var view = SKRect.Create(_viewport.Left * ds, _viewport.Top * ds, _viewport.Width * ds, _viewport.Height * ds);

        canvas.Save();
        canvas.Scale(1 / ds);
        int First(int v) => (int)Math.Floor(v / (double)ChunkCells);
        for (var ky = First(y0); ky <= First(y1); ky++)
        for (var kx = First(x0); kx <= First(x1); kx++)
        {
            // Roughly where the chunk lies, to skip those out of view before building them.
            int gx0 = kx * ChunkCells, gy0 = ky * ChunkCells;
            var reach = SKRect.Create(((gx0 - gy0 - ChunkCells) * HalfTileWidth - 2 * HalfTileWidth) * zs + camX,
                ((gx0 + gy0) * HalfTileHeight - 4 * HalfTileWidth) * zs + camY,
                (2 * ChunkCells + 4) * HalfTileWidth * zs, (2 * ChunkCells * HalfTileHeight + 6 * HalfTileWidth) * zs);
            if (!reach.IntersectsWith(view))
                continue;
            if (!_chunks.TryGetValue((kx, ky), out var chunk))
                _chunks[(kx, ky)] = chunk = BuildChunk(world, tileset, kx, ky, zs, gpu);
            chunk.Used = _chunkFrame;
            if (chunk.Image is { } image)
            {
                var at = SKRect.Create(chunk.Left + camX, chunk.Top + camY, image.Width, image.Height);
                if (at.IntersectsWith(view))
                    canvas.DrawImage(image, at.Left, at.Top);
            }
            foreach (var (x, y) in chunk.Live)
            {
                if (GroundAt(world, tileset, x, y, out var kind) is not { } tile)
                    continue;
                var (sx, sy) = AnimationFrame(tile, time);
                var frame = _sprites.Get(tile.Image, sx, sy, tile.Width, tile.Height, zs);
                var (tx, ty) = TileDevice(tile, x, y, zs);
                if (SKRect.Create(tx + camX, ty + camY, frame.Width, frame.Height).IntersectsWith(view))
                    canvas.DrawImage(frame, tx + camX, ty + camY, paintFor(kind));
            }
        }
        canvas.Restore();

        if (_chunkFrame % 30 == 0)
        {
            foreach (var stale in _chunks.Where(kv => kv.Value.Used < _chunkFrame - KeepFrames).Select(kv => kv.Key).ToList())
            {
                _chunks[stale].Image?.Dispose();
                _chunks.Remove(stale);
            }
        }
    }

    /// <summary>Draws a chunk's static tiles into one image; its bog and animated cells are listed to be drawn live.</summary>
    private GroundChunk BuildChunk(GameWorld world, FlareTileset tileset, int kx, int ky, float zs, bool gpu)
    {
        var chunk = new GroundChunk();
        var tiles = new List<(SKImage Image, int X, int Y)>();
        for (var y = ky * ChunkCells; y < (ky + 1) * ChunkCells; y++)
        for (var x = kx * ChunkCells; x < (kx + 1) * ChunkCells; x++)
        {
            if (GroundAt(world, tileset, x, y, out var kind) is not { } tile)
                continue;
            if (kind != GroundKind.Plain || tile.Animation.Count > 0)
            {
                chunk.Live.Add((x, y));
                continue;
            }
            var (tx, ty) = TileDevice(tile, x, y, zs);
            tiles.Add((_sprites.Get(tile.Image, tile.X, tile.Y, tile.Width, tile.Height, zs), tx, ty));
        }
        if (tiles.Count == 0)
            return chunk;
        int left = tiles.Min(t => t.X), top = tiles.Min(t => t.Y);
        int width = tiles.Max(t => t.X + t.Image.Width) - left, height = tiles.Max(t => t.Y + t.Image.Height) - top;
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = (gpu ? SKSurface.Create(_gpuContext!, true, info) : null) ?? SKSurface.Create(info);
        surface.Canvas.Clear(SKColors.Transparent);
        foreach (var (image, x, y) in tiles)
            surface.Canvas.DrawImage(image, x - left, y - top);
        surface.Canvas.Flush();
        chunk.Image = surface.Snapshot();
        (chunk.Left, chunk.Top) = (left, top);
        return chunk;
    }

    /// <summary>The source cell of a tile's current animation frame (its own cell when it has none).</summary>
    private static (int X, int Y) AnimationFrame(FlareTile tile, double time)
    {
        if (tile.Animation.Count == 0)
            return (tile.X, tile.Y);
        var total = tile.Animation.Sum(f => f.DurationMs);
        var t = (int)(time * 1000) % Math.Max(1, total);
        foreach (var f in tile.Animation)
        {
            if (t < f.DurationMs)
                return (f.X, f.Y);
            t -= f.DurationMs;
        }
        return (tile.X, tile.Y);
    }

    private void ClearGroundChunks()
    {
        foreach (var chunk in _chunks.Values)
            chunk.Image?.Dispose();
        _chunks.Clear();
    }
}
