using AshenReliquary.Core.Items;
using AshenReliquary.Core.World;
using SkiaSharp;

namespace AshenReliquary.App.Rendering;

/// <summary>
/// The minimap, top right, in the same isometric orientation as the world and centred on the hero: explored terrain
/// (the whole map cached at a fixed scale, redrawn only when more is explored), shrines, the exit, nearby living
/// monsters, notable loot and the hero.
/// </summary>
internal sealed class Minimap
{
    public const float Width = 230, Height = 140;

    /// <summary>Pixels per tile on the minimap.</summary>
    public const float Scale = 3.5f;
    private SKImage? _terrain;
    private int _version = -1;
    private string? _zone;
    private float _scale;
    private SKPoint _origin;

    public SKRect Bounds { get; private set; }

    public void Draw(SKCanvas canvas, SKSize size, GameWorld world, Action<SKRect, Func<IReadOnlyList<TipLine>>, SKColor> region)
    {
        Bounds = SKRect.Create(size.Width - Width - 14, 14, Width, Height);
        var map = world.Map;
        _scale = Scale;
        if (_terrain is null || _version != world.ExploredVersion || _zone != world.Zone.Id)
            Rebuild(world);
        // Centre on the hero: the cached image puts map cell (0,0)'s corner at _imageOrigin.
        var heroOnImage = new SKPoint(_imageOrigin.X + (float)(world.Hero.X - world.Hero.Y) * _scale,
            _imageOrigin.Y + (float)(world.Hero.X + world.Hero.Y) * _scale / 2);
        var imageAt = new SKPoint(Bounds.MidX - heroOnImage.X, Bounds.MidY - heroOnImage.Y);
        _origin = new SKPoint(imageAt.X + _imageOrigin.X, imageAt.Y + _imageOrigin.Y);

        using var back = new SKPaint { Color = new SKColor(10, 8, 8, 215) };
        using var frame = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, Color = Palette.Frame };
        canvas.DrawRect(Bounds, back);
        canvas.Save();
        canvas.ClipRect(Bounds);
        canvas.DrawImage(_terrain!, imageAt.X, imageAt.Y);

        using var paint = new SKPaint { IsAntialias = true };
        foreach (var shrine in world.Zone.Shrines)
        {
            var at = At(shrine.At[0] + 0.5, shrine.At[1] + 0.5);
            if (!world.IsExplored(shrine.At[0], shrine.At[1]))
                continue;
            var lit = world.ActivatedShrines.Contains(shrine.Id);
            paint.Color = lit ? new SKColor(120, 190, 255) : new SKColor(90, 120, 150);
            Diamond(canvas, at, 4.5f, paint);
            var s = shrine;
            region(SKRect.Create(at.X - 6, at.Y - 6, 12, 12), () =>
                [new(s.Name, new SKColor(120, 190, 255), lit ? "awake" : "asleep", true), new(lit ? "Click it to open its Rift: travel to any woken shrine." : "Walk near it to wake it and refill your potions.", Palette.Dim), new("You rise at the nearest shrine when you fall.", Palette.Dim)],
                new SKColor(120, 190, 255));
        }
        foreach (var vendor in world.Vendors)
        {
            var (vx, vy) = ((int)vendor.X, (int)vendor.Y);
            if (!world.IsExplored(vx, vy))
                continue;
            var at = At(vendor.X, vendor.Y);
            paint.Color = Palette.Gold;
            canvas.DrawCircle(at, 3.5f, paint);
            var v = vendor;
            region(SKRect.Create(at.X - 6, at.Y - 6, 12, 12), () => [new(v.Definition.Name, Palette.Gold, v.Definition.Title, true), new("Click them to trade.", Palette.Dim)],
                Palette.Gold);
        }
        foreach (var cache in world.Caches)
        {
            if (!world.IsExplored((int)cache.X, (int)cache.Y))
                continue;
            var at = At(cache.X, cache.Y);
            var colour = cache.Opened ? Palette.Dim : cache.Definition.Kind == CacheKind.Reliquary ? new SKColor(230, 170, 255) : Palette.Gold;
            paint.Color = colour;
            canvas.DrawRect(SKRect.Create(at.X - 2.5f, at.Y - 2.5f, 5, 5), paint);
            var c = cache;
            region(SKRect.Create(at.X - 6, at.Y - 6, 12, 12), () =>
                [new(c.Name, colour, c.Opened ? "opened" : "shut", true), new(c.Opened ? "Already looted." : "Click it to open it: loot for your level.", Palette.Dim)],
                colour);
        }
        foreach (var exit in world.Zone.Exits)
        {
            var at = At(exit.At[0] + 0.5, exit.At[1] + 0.5);
            paint.Color = new SKColor(240, 200, 90);
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = 1.5f;
            canvas.DrawCircle(at, 5, paint);
            paint.Style = SKPaintStyle.Fill;
            var e = exit;
            region(SKRect.Create(at.X - 7, at.Y - 7, 14, 14), () => [new(e.Label, new SKColor(240, 200, 90), null, true), new("Where the road leads.", Palette.Dim)],
                new SKColor(240, 200, 90));
        }
        foreach (var g in world.GroundItems)
        {
            if (g.Drop.Item is not { Rarity: >= Rarity.Uncommon } item)
                continue;
            paint.Color = Palette.Rarity(item.Rarity);
            canvas.DrawCircle(At(g.X, g.Y), 1.8f, paint);
        }
        foreach (var m in world.Monsters)
        {
            if (m.IsDead || m.DistanceTo(world.Hero) > 14 || !world.IsLit(m.X, m.Y))
                continue;
            paint.Color = m.Creature.Stats.Rank >= Core.Creatures.CreatureRank.Champion ? new SKColor(255, 150, 60) : new SKColor(220, 50, 40);
            canvas.DrawCircle(At(m.X, m.Y), m.Creature.Stats.Rank >= Core.Creatures.CreatureRank.Champion ? 2.6f : 1.8f, paint);
        }

        // The hero: a gold dot with a tick showing which way they face.
        var hero = At(world.Hero.X, world.Hero.Y);
        paint.Color = new SKColor(255, 225, 120);
        canvas.DrawCircle(hero, 3.2f, paint);
        var angle = (180 + 45 * world.Hero.Direction) * Math.PI / 180;
        using var tick = new SKPaint { IsAntialias = true, Color = paint.Color, StrokeWidth = 1.5f };
        canvas.DrawLine(hero, new SKPoint(hero.X + (float)Math.Cos(angle) * 7, hero.Y + (float)Math.Sin(angle) * 7), tick);
        canvas.Restore();
        canvas.DrawRect(Bounds, frame);
    }

    private SKPoint _imageOrigin;

    private SKPoint At(double x, double y) =>
        new(_origin.X + (float)(x - y) * _scale, _origin.Y + (float)(x + y) * _scale / 2);

    private static void Diamond(SKCanvas canvas, SKPoint at, float r, SKPaint paint)
    {
        using var path = new SKPath();
        path.MoveTo(at.X, at.Y - r);
        path.LineTo(at.X + r, at.Y);
        path.LineTo(at.X, at.Y + r);
        path.LineTo(at.X - r, at.Y);
        path.Close();
        canvas.DrawPath(path, paint);
    }

    /// <summary>Draws every explored cell as a small diamond in its terrain colour, once per change.</summary>
    private void Rebuild(GameWorld world)
    {
        _version = world.ExploredVersion;
        _zone = world.Zone.Id;
        _terrain?.Dispose();
        var map0 = world.Map;
        var w = (int)Math.Ceiling((map0.Width + map0.Height) * _scale) + 4;
        var h = (int)Math.Ceiling((map0.Width + map0.Height) * _scale / 2) + 4;
        // In the image, cell (0,0)'s corner sits Height tiles in from the left (the map's west tip).
        _imageOrigin = new SKPoint(map0.Height * _scale + 2, 2);
        _origin = _imageOrigin;
        using var surface = SKSurface.Create(new SKImageInfo(w, h));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        using var paint = new SKPaint { IsAntialias = false };
        using var path = new SKPath();
        var map = world.Map;
        for (var y = 0; y < map.Height; y++)
        for (var x = 0; x < map.Width; x++)
        {
            if (!world.IsExplored(x, y))
                continue;
            paint.Color = CellColour(map, x, y);
            var c = At(x + 0.5, y + 0.5);
            path.Reset();
            path.MoveTo(c.X, c.Y - _scale / 2 - 0.3f);
            path.LineTo(c.X + _scale + 0.3f, c.Y);
            path.LineTo(c.X, c.Y + _scale / 2 + 0.3f);
            path.LineTo(c.X - _scale - 0.3f, c.Y);
            path.Close();
            canvas.DrawPath(path, paint);
        }
        _terrain = surface.Snapshot();
    }

    private static SKColor CellColour(TileMap map, int x, int y)
    {
        var ground = map.Ground(x, y);
        if (map.IsWater(x, y))
            return new SKColor(40, 62, 70);
        if (map.IsBog(x, y))
            return new SKColor(62, 74, 34);
        if (map.IsBlocked(x, y))
            return map.Objects(x, y).Any(id => id >= 240) ? new SKColor(28, 40, 26) : new SKColor(70, 64, 56);
        if (ground is >= 32 and <= 47)
            return new SKColor(120, 104, 80);
        return new SKColor(56, 64, 40);
    }
}
