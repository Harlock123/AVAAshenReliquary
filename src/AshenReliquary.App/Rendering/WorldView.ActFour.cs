using AshenReliquary.Core.World;
using SkiaSharp;

namespace AshenReliquary.App.Rendering;

/// <summary>
/// Act 4's world on screen (M4 step 8): fire in the braziers and on the Order's beacon, the wards across the Reliquary's
/// passages, the Wyrm's Egg in its embers, and the ash storm closing in round the hero.
/// </summary>
public sealed partial class WorldView
{
    /// <summary>The brazier tile in Act 4's set (the ruins' iron basket): drawn with a fire in it.</summary>
    private const int BrazierTile = 1295;

    private (TileMap Map, List<(int X, int Y)> Found)? _braziers;

    private List<(int X, int Y)> Braziers(TileMap map)
    {
        if (_braziers is { } known && known.Map == map)
            return known.Found;
        var found = new List<(int X, int Y)>();
        for (var y = 0; y < map.Height; y++)
        for (var x = 0; x < map.Width; x++)
            if (map.Objects(x, y).Contains(BrazierTile))
                found.Add((x, y));
        _braziers = (map, found);
        return found;
    }

    /// <summary>After the dark: the fires and wards, which light themselves.</summary>
    private void DrawActFour(SKCanvas canvas, GameWorld world, double time)
    {
        if (!world.Zone.Ashen)
            return;
        VisibleCells(new SKSize(_viewport.Width, _viewport.Height), world, out var x0, out var y0, out var x1, out var y1);
        foreach (var (x, y) in Braziers(world.Map))
        {
            if (x >= x0 - 1 && x <= x1 + 1 && y >= y0 - 1 && y <= y1 + 1)
                DrawFire(canvas, ToScreen(x + 0.5, y + 0.5), new SKColor(255, 150, 60), 1f, time + x * 0.7);
        }
        foreach (var who in world.Interactives)
        {
            if (who.Thing?.Action == "beacon" && world.Flag("act4.beacon") == "lit")
                DrawFire(canvas, ToScreen(who.X, who.Y), new SKColor(235, 240, 255), 1.8f, time);
            if (who.Thing?.Action == "egg")
                DrawEgg(canvas, ToScreen(who.X, who.Y), world.Flag("act4.egg"), time);
        }
        foreach (var gate in world.Zone.Gates ?? [])
        {
            if (world.GateOpen(gate))
                continue;
            foreach (var cell in gate.Cells)
                DrawWard(canvas, ToScreen(cell[0] + 0.5, cell[1] + 0.5), time + cell[0] * 0.37 + cell[1] * 0.21);
        }
    }

    /// <summary>A fire in a brazier's basket: a glow and a few tongues of flame that flicker.</summary>
    private void DrawFire(SKCanvas canvas, SKPoint foot, SKColor colour, float size, double time)
    {
        var z = (float)Zoom * size;
        var at = new SKPoint(foot.X, foot.Y - 38 * (float)Zoom);
        var flicker = (float)(0.85 + 0.1 * Math.Sin(time * 9.1) + 0.05 * Math.Sin(time * 23.7));
        using var halo = new SKPaint { IsAntialias = true, Shader = SKShader.CreateRadialGradient(at, 30 * z * flicker,
            [colour.WithAlpha(120), colour.WithAlpha(0)], SKShaderTileMode.Clamp) };
        canvas.DrawCircle(at, 30 * z * flicker, halo);
        using var flame = new SKPaint { IsAntialias = true };
        for (var i = 0; i < 3; i++)
        {
            var sway = (float)Math.Sin(time * (6 + i * 2.3) + i) * 2.5f * z;
            var h = (16 + 6 * (float)Math.Sin(time * (11 + i * 3.1) + i * 2)) * z;
            var w = (7 - i * 1.5f) * z;
            var dx = (i - 1) * 4 * z;
            flame.Color = i == 1 ? SKColors.White.WithAlpha(220) : colour.WithAlpha(230);
            using var path = new SKPath();
            path.MoveTo(at.X + dx - w, at.Y);
            path.QuadTo(at.X + dx - w * 0.6f + sway, at.Y - h * 0.6f, at.X + dx + sway, at.Y - h);
            path.QuadTo(at.X + dx + w * 0.6f + sway, at.Y - h * 0.6f, at.X + dx + w, at.Y);
            path.Close();
            canvas.DrawPath(path, flame);
        }
    }

    /// <summary>A ward across a passage: a curtain of pale fire standing on the floor, its tongues licking upward.</summary>
    private void DrawWard(SKCanvas canvas, SKPoint foot, double time)
    {
        var z = (float)Zoom;
        var pulse = (float)(0.8 + 0.2 * Math.Sin(time * 3.3));
        using var glow = new SKPaint { IsAntialias = true, Shader = SKShader.CreateRadialGradient(new SKPoint(foot.X, foot.Y - 30 * z), 70 * z,
            [new SKColor(255, 160, 70, (byte)(110 * pulse)), new SKColor(255, 160, 70, 0)], SKShaderTileMode.Clamp) };
        canvas.DrawCircle(foot.X, foot.Y - 30 * z, 70 * z, glow);
        using var flame = new SKPaint { IsAntialias = true };
        for (var i = 0; i < 5; i++)
        {
            var dx = (i - 2) * 16 * z;
            var dy = (i - 2) * 8 * z * 0.5f;
            var h = (60 + 30 * (float)Math.Sin(time * (5 + i * 1.7) + i * 2.1)) * z;
            var sway = (float)Math.Sin(time * (4 + i) + i) * 5 * z;
            var w = 11 * z;
            var at = new SKPoint(foot.X + dx, foot.Y + dy);
            flame.Shader = SKShader.CreateLinearGradient(at, new SKPoint(at.X, at.Y - h),
                [new SKColor(255, 150, 60, 230), new SKColor(255, 220, 150, 160), new SKColor(255, 240, 200, 0)], [0, 0.55f, 1], SKShaderTileMode.Clamp);
            using var path = new SKPath();
            path.MoveTo(at.X - w, at.Y);
            path.QuadTo(at.X - w * 0.7f + sway, at.Y - h * 0.55f, at.X + sway * 1.5f, at.Y - h);
            path.QuadTo(at.X + w * 0.7f + sway, at.Y - h * 0.55f, at.X + w, at.Y);
            path.Close();
            canvas.DrawPath(path, flame);
            flame.Shader?.Dispose();
            flame.Shader = null;
        }
    }

    /// <summary>The Wyrm's Egg in its bed of embers: black and glossy, glowing through its cracks; shards once smashed.</summary>
    private void DrawEgg(SKCanvas canvas, SKPoint foot, string? fate, double time)
    {
        if (fate is "taken" or "hatched")
            return;
        var z = (float)Zoom;
        using var shell = new SKPaint { IsAntialias = true, Color = new SKColor(26, 20, 22) };
        using var crack = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2 * z, StrokeCap = SKStrokeCap.Round };
        if (fate == "smashed")
        {
            foreach (var (dx, dy, r) in new[] { (-12f, -4f, 7f), (10f, -2f, 9f), (2f, 6f, 6f) })
                canvas.DrawOval(SKRect.Create(foot.X + dx * z - r * z, foot.Y + dy * z - r * 0.6f * z, 2 * r * z, 1.2f * r * z), shell);
            return;
        }
        var beat = (float)(0.6 + 0.4 * Math.Pow(Math.Max(0, Math.Sin(time * 2.2)), 6));
        var at = new SKPoint(foot.X, foot.Y - 22 * z);
        using var halo = new SKPaint { IsAntialias = true, Shader = SKShader.CreateRadialGradient(at, 42 * z,
            [new SKColor(255, 110, 30, (byte)(110 * beat)), new SKColor(255, 110, 30, 0)], SKShaderTileMode.Clamp) };
        canvas.DrawCircle(at, 42 * z, halo);
        canvas.DrawOval(SKRect.Create(at.X - 15 * z, at.Y - 22 * z, 30 * z, 42 * z), shell);
        using var gloss = new SKPaint { IsAntialias = true, Color = new SKColor(120, 100, 110, 120) };
        canvas.DrawOval(SKRect.Create(at.X - 9 * z, at.Y - 17 * z, 7 * z, 12 * z), gloss);
        crack.Color = new SKColor(255, (byte)(120 + 80 * beat), 40, (byte)(200 * beat + 40));
        using var path = new SKPath();
        path.MoveTo(at.X - 4 * z, at.Y - 18 * z);
        path.LineTo(at.X + 2 * z, at.Y - 8 * z);
        path.LineTo(at.X - 3 * z, at.Y + 2 * z);
        path.LineTo(at.X + 5 * z, at.Y + 12 * z);
        path.MoveTo(at.X + 2 * z, at.Y - 8 * z);
        path.LineTo(at.X + 10 * z, at.Y - 4 * z);
        canvas.DrawPath(path, crack);
    }

    /// <summary>
    /// An ash storm (the Ash Wastes): the ash closes in round the hero, so that past a few paces nothing shows but grey,
    /// and drives across the screen.
    /// </summary>
    private void DrawAshStorm(SKCanvas canvas, SKSize size, GameWorld world, double time)
    {
        var hero = ToScreen(world.Hero.X, world.Hero.Y);
        var z = (float)Zoom;
        // A circle of the hero's sight on the map is an ellipse on screen, half as tall as it is wide.
        var r = (float)(GameWorld.AshStormSightTiles * HalfTileWidth * Math.Sqrt(2)) * z;
        canvas.Save();
        canvas.Translate(hero.X, hero.Y);
        canvas.Scale(1, 0.5f);
        using (var haze = new SKPaint
        {
            Shader = SKShader.CreateRadialGradient(SKPoint.Empty, r * 1.1f,
                [new SKColor(118, 110, 104, 0), new SKColor(118, 110, 104, 120), new SKColor(118, 110, 104, 238)],
                [0, 0.72f, 1], SKShaderTileMode.Clamp),
        })
            canvas.DrawRect(SKRect.Create(-size.Width * 2, -size.Height * 4, size.Width * 4, size.Height * 8), haze);
        canvas.Restore();
        using var paint = new SKPaint { IsAntialias = true, StrokeCap = SKStrokeCap.Round };
        for (var i = 0; i < 220; i++)
        {
            var speed = 90 + 150 * Hash01(i * 13 + 41);
            var x = (float)((Hash01(i * 5 + 43) * size.Width + time * speed) % size.Width);
            var y = (float)((Hash01(i * 9 + 47) * size.Height + time * speed * 0.2) % size.Height);
            paint.Color = new SKColor(170, 162, 150, (byte)(70 + 90 * Hash01(i * 19 + 53)));
            paint.StrokeWidth = (float)(1.5 + 2 * Hash01(i * 29 + 59));
            canvas.DrawLine(x, y, x - 7, y - 1.5f, paint);
        }
    }

    /// <summary>
    /// Vorthrax's breath while he draws it: the cone it will burn, glowing brighter as the fire comes; then a flash of
    /// flame down it.
    /// </summary>
    private void DrawBreaths(SKCanvas canvas, GameWorld world)
    {
        foreach (var m in world.Monsters)
        {
            if (m.Breath is not { } b || m.IsDead)
                continue;
            var burning = world.Time >= b.BurnAt;
            var gather = (float)Math.Clamp(1 - (b.BurnAt - world.Time) / 1.2, 0, 1);
            using var path = new SKPath();
            var start = b.Direction - b.Arc * Math.PI / 360;
            var end = b.Direction + b.Arc * Math.PI / 360;
            path.MoveTo(ToScreen(b.X, b.Y));
            for (var i = 0; i <= 16; i++)
            {
                var a = start + (end - start) * i / 16;
                path.LineTo(ToScreen(b.X + Math.Cos(a) * b.Range, b.Y + Math.Sin(a) * b.Range));
            }
            path.Close();
            using var fill = new SKPaint
            {
                IsAntialias = true,
                Color = burning ? new SKColor(255, 170, 60, 200) : new SKColor(255, 90, 30, (byte)(50 + 110 * gather)),
            };
            canvas.DrawPath(path, fill);
            using var edge = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2.5f, Color = new SKColor(255, 140, 50, 220) };
            canvas.DrawPath(path, edge);
        }
    }

    /// <summary>A platform cracking (Vorthrax's arena): a red edge pulsing round each cell, faster as it goes.</summary>
    private void DrawCrumbling(SKCanvas canvas, GameWorld world)
    {
        foreach (var c in world.Crumbling)
        {
            var left = (float)Math.Clamp(c.SinksAt - world.Time, 0, 3);
            var pulse = (float)(0.5 + 0.5 * Math.Sin(world.Time * (8 + 10 / Math.Max(0.3, left))));
            using var edge = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f, Color = new SKColor(255, 80, 30, (byte)(120 + 120 * pulse)) };
            foreach (var (x, y) in c.Cells)
            {
                using var path = new SKPath();
                path.MoveTo(ToScreen(x, y));
                path.LineTo(ToScreen(x + 1, y));
                path.LineTo(ToScreen(x + 1, y + 1));
                path.LineTo(ToScreen(x, y + 1));
                path.Close();
                canvas.DrawPath(path, edge);
            }
        }
    }

    /// <summary>The ashen world beyond the map's edge: rock spires here and there on the ash.</summary>
    private static int? OutsideSpire(int x, int y) =>
        Hash01(x * 7919 + y * 104729) < 0.35 ? new[] { 144, 145, 146, 147, 148, 149, 153 }[(int)(Hash01(x * 31 + y * 17) * 7) % 7] : null;
}
