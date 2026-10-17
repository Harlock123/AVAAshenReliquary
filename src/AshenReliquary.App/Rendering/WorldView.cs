using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using AshenReliquary.Core.Content.Flare;
using AshenReliquary.Core.Creatures;
using AshenReliquary.Core.Items;
using AshenReliquary.Core.World;
using SkiaSharp;

namespace AshenReliquary.App.Rendering;

/// <summary>
/// Draws a <see cref="GameWorld"/> in Grim Dawn-style isometric: ground, then objects and actors sorted by
/// depth, then the act's colour grade, darkness with light pools, drifting ash, a vignette, floating combat
/// text and the HUD. Pure view: it reads the world and turns clicks into world commands.
/// </summary>
public sealed class WorldView : Control
{
    public const float HalfTileWidth = 96f;
    public const float HalfTileHeight = 48f;
    public static readonly double[] ZoomSteps = [0.42, 0.55, 0.72];

    private readonly FlareAssets _assets = FlareAssets.Shared;
    private readonly SpriteCache _sprites = new(FlareAssets.Shared, ActGrade());
    private readonly SpriteCache _uiSprites = new(FlareAssets.Shared);
    private readonly List<(SKRect Rect, GroundItem Item)> _lootLabels = [];
    private InventoryPanel? _inventory;
    private float _uiScale = 1f;

    private (string Id, SKImage Image)? _portrait;

    /// <summary>The session's portrait, decoded once for Skia.</summary>
    private SKImage? PortraitImage()
    {
        if (Services.Portraits.Find(Session?.PortraitId) is not { } p)
            return null;
        if (_portrait is not { } cached || cached.Id != p.Id)
            _portrait = (p.Id, SKImage.FromEncodedData(Services.Portraits.Png(p)).ToRasterImage(ensurePixelData: true));
        return _portrait.Value.Image;
    }

    /// <summary>The inventory and character panel (I).</summary>
    public bool InventoryOpen { get; set; }

    private InventoryPanel Inventory => _inventory ??= new InventoryPanel(DrawIcon);

    /// <summary>Draws icon <paramref name="icon"/> from Flare's icon sheet into <paramref name="rect"/> (UI canvas).</summary>
    private void DrawIcon(SKCanvas canvas, int icon, SKRect rect)
    {
        var x = icon % FlareAssets.IconsPerRow * FlareAssets.IconSize;
        var y = icon / FlareAssets.IconsPerRow * FlareAssets.IconSize;
        var image = _uiSprites.Get(FlareAssets.IconSheet, x, y, FlareAssets.IconSize, FlareAssets.IconSize, rect.Width / FlareAssets.IconSize * _uiScale);
        canvas.DrawImage(image, rect, new SKSamplingOptions(SKFilterMode.Linear));
    }
    private SKImage? _vignette;
    private SKSurface? _darknessSurface;
    private float _deviceScale = 1f;
    private int _zoomIndex = 1;
    private SKRect _viewport;
    private Point? _pointer;
    private bool _holding;
    private double _lastHoldMove;

    static WorldView()
    {
        ClipToBoundsProperty.OverrideDefaultValue<WorldView>(true);
        FocusableProperty.OverrideDefaultValue<WorldView>(true);
    }

    public GameSession? Session { get; set; }

    /// <summary>When set, the view shows this many seconds of real time for the ash drift (tests pin it).</summary>
    public double? FixedTime { get; set; }

    public double Zoom => ZoomSteps[_zoomIndex];

    private GameWorld? World => Session?.World;

    // ----- Projection ------------------------------------------------------------------

    private (double X, double Y) Camera => World is { } w ? (w.Hero.X, w.Hero.Y) : (0, 0);

    public SKPoint ToScreen(double x, double y)
    {
        var (cx, cy) = Camera;
        var z = (float)Zoom;
        return new SKPoint(
            (float)((x - y - (cx - cy)) * HalfTileWidth * z + Bounds.Width / 2),
            (float)((x + y - (cx + cy)) * HalfTileHeight * z + Bounds.Height / 2));
    }

    public (double X, double Y) ToMap(Point screen)
    {
        var (cx, cy) = Camera;
        var a = (screen.X - Bounds.Width / 2) / (HalfTileWidth * Zoom) + (cx - cy);
        var b = (screen.Y - Bounds.Height / 2) / (HalfTileHeight * Zoom) + (cx + cy);
        return ((a + b) / 2, (b - a) / 2);
    }

    // ----- Input ---------------------------------------------------------------------------

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        if (World is not { } world)
            return;
        var p = e.GetPosition(this);
        var props = e.GetCurrentPoint(this).Properties;
        var sp = new SKPoint((float)p.X, (float)p.Y);
        if (InventoryOpen && Inventory.Click(sp, props.IsRightButtonPressed, world))
        {
            e.Handled = true;
            InvalidateVisual();
            return;
        }
        if (props.IsLeftButtonPressed && _lootLabels.LastOrDefault(l => l.Rect.Contains(sp)).Item is { } loot)
        {
            world.PickUp(loot);
            _holding = false;
            e.Handled = true;
            return;
        }
        if (props.IsRightButtonPressed)
        {
            UseSlot("rmb", p);
            e.Handled = true;
            return;
        }
        if (!props.IsLeftButtonPressed)
            return;
        if (MonsterUnder(p) is { } monster)
        {
            world.Attack(monster);
            _holding = false;
        }
        else
        {
            var (x, y) = ToMap(p);
            world.MoveTo(x, y);
            _holding = true;
            _lastHoldMove = world.Time;
        }
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        _pointer = e.GetPosition(this);
        if (_holding && World is { } world && world.Time - _lastHoldMove > 0.12)
        {
            var (x, y) = ToMap(_pointer.Value);
            world.MoveTo(x, y);
            _lastHoldMove = world.Time;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _holding = false;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        _zoomIndex = Math.Clamp(_zoomIndex + (e.Delta.Y > 0 ? 1 : -1), 0, ZoomSteps.Length - 1);
        InvalidateVisual();
        e.Handled = true;
    }

    /// <summary>Uses a bar slot on the monster under <paramref name="at"/> (or under the mouse pointer).</summary>
    public void UseSlot(string slot, Point? at = null)
    {
        if (World is not { } world)
            return;
        var point = at ?? _pointer;
        var target = point is { } p ? MonsterUnder(p) ?? NearestTo(p) : null;
        world.UseSlot(slot, target);
    }

    /// <summary>Forgiving targeting for keys: the closest living monster within 2 tiles of the pointer.</summary>
    private MonsterActor? NearestTo(Point p)
    {
        var (x, y) = ToMap(p);
        return World?.MonsterAt(x, y, 2.0);
    }

    /// <summary>The living monster whose sprite is under a screen point.</summary>
    public MonsterActor? MonsterUnder(Point p)
    {
        if (World is not { } world)
            return null;
        var z = (float)Zoom;
        MonsterActor? best = null;
        var bestDistance = double.MaxValue;
        foreach (var m in world.Monsters)
        {
            if (m.IsDead)
                continue;
            var foot = ToScreen(m.X, m.Y);
            var height = (m.Definition.Stationary ? 110 : 150) * z * (float)m.Definition.Scale;
            var halfWidth = 38 * z;
            if (p.X < foot.X - halfWidth || p.X > foot.X + halfWidth || p.Y < foot.Y - height || p.Y > foot.Y + 12 * z)
                continue;
            var d = Math.Abs(p.X - foot.X) + Math.Abs(p.Y - (foot.Y - height / 2));
            if (d < bestDistance)
            {
                bestDistance = d;
                best = m;
            }
        }
        return best;
    }

    // ----- Rendering -------------------------------------------------------------------------

    public override void Render(DrawingContext context) => context.Custom(new DrawOp(this, new Rect(Bounds.Size)));

    private sealed class DrawOp(WorldView owner, Rect bounds) : ICustomDrawOperation
    {
        public Rect Bounds => bounds;
        public bool HitTest(Point p) => bounds.Contains(p);
        public bool Equals(ICustomDrawOperation? other) => false;
        public void Dispose() { }

        public void Render(ImmediateDrawingContext context)
        {
            var lease = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (lease is null)
                return;
            using var api = lease.Lease();
            var canvas = api.SkCanvas;
            var save = canvas.Save();
            canvas.ClipRect(new SKRect(0, 0, (float)bounds.Width, (float)bounds.Height));
            try
            {
                owner.Draw(canvas, new SKSize((float)bounds.Width, (float)bounds.Height), gpu: api.GrContext is not null);
            }
            finally
            {
                canvas.RestoreToCount(save);
            }
        }
    }

    /// <summary>
    /// Pixels per logical unit for the world layer. Null = automatic: full device resolution on a GPU, but only
    /// 1x on a software (CPU) renderer, where a HiDPI screen would otherwise mean 4x the pixels to fill.
    /// </summary>
    public float? WorldRenderScale { get; set; }

    /// <summary>The scale the last frame's world layer was actually drawn at.</summary>
    public float LastWorldScale { get; private set; } = 1f;

    private SKSurface? _worldSurface;

    private void Draw(SKCanvas canvas, SKSize size, bool gpu)
    {
        canvas.Clear(new SKColor(8, 7, 10));
        if (World is not { } world)
            return;
        Stats.Begin();
        var deviceScale = Math.Max(1f, canvas.TotalMatrix.ScaleX);
        var scale = WorldRenderScale ?? (gpu ? deviceScale : 1f);
        LastWorldScale = scale;

        if (Math.Abs(scale - deviceScale) < 0.01f)
        {
            DrawWorld(canvas, size, world);
        }
        else
        {
            // Draw the world into an offscreen surface at the chosen scale, then stretch it over the window once.
            int w = Math.Max(1, (int)Math.Ceiling(size.Width * scale)), h = Math.Max(1, (int)Math.Ceiling(size.Height * scale));
            if (_worldSurface is null || _worldSurface.Canvas.DeviceClipBounds.Width != w || _worldSurface.Canvas.DeviceClipBounds.Height != h)
            {
                _worldSurface?.Dispose();
                // Opaque: the stretch below is then a plain copy with no blending.
                _worldSurface = SKSurface.Create(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Opaque));
            }
            var offscreen = _worldSurface.Canvas;
            offscreen.ResetMatrix();
            offscreen.Clear(new SKColor(8, 7, 10));
            offscreen.Scale(scale);
            DrawWorld(offscreen, size, world);
            using var snapshot = _worldSurface.Snapshot();
            // A whole-number stretch (1x world on a 2x screen) needs no smoothing: nearest is a fast pixel double
            // and looks exactly like a 1x display. Other ratios get bilinear filtering.
            var ratio = deviceScale / scale;
            var sampling = Math.Abs(ratio - MathF.Round(ratio)) < 0.01f ? new SKSamplingOptions(SKFilterMode.Nearest) : new SKSamplingOptions(SKFilterMode.Linear);
            using var copy = new SKPaint { BlendMode = SKBlendMode.Src };
            canvas.DrawImage(snapshot, SKRect.Create(size), sampling, copy);
            Stats.Mark("upscale");
        }

        // Text and HUD always at full resolution, so they stay crisp.
        _uiScale = deviceScale;
        _viewport = SKRect.Create(size);
        DrawLootLabels(canvas, world);
        WorldOverlay.DrawFloatingText(this, canvas, world);
        Hud.Draw(canvas, size, world, MonsterUnderPointer());
        if (InventoryOpen)
            Inventory.Draw(canvas, size, world, _pointer is { } pt ? new SKPoint((float)pt.X, (float)pt.Y) : null, PortraitImage());
        Stats.Mark("text+hud");

        var now = System.Diagnostics.Stopwatch.GetTimestamp();
        if (_lastPresent != 0)
            _frameIntervals.Enqueue(System.Diagnostics.Stopwatch.GetElapsedTime(_lastPresent, now).TotalMilliseconds);
        _lastPresent = now;
        while (_frameIntervals.Count > 60)
            _frameIntervals.Dequeue();
        if (Stats.Frames >= 60)
        {
            _statsLine = $"{(gpu ? "GPU" : "CPU")} renderer, world at {scale:0.##}x of {deviceScale:0.##}x  ·  draw {Stats}";
            Stats.Reset();
        }
        if (ShowStats)
        {
            var fps = _frameIntervals.Count > 0 ? 1000 / _frameIntervals.Average() : 0;
            using var font = new SKFont(WorldOverlay.Serif, 13);
            using var ink = new SKPaint { IsAntialias = true, Color = new SKColor(255, 230, 140) };
            using var back = new SKPaint { Color = new SKColor(0, 0, 0, 170) };
            canvas.DrawRect(SKRect.Create(size.Width - 760, 8, 752, 44), back);
            canvas.DrawText($"{fps:0} fps", size.Width - 14, 26, SKTextAlign.Right, font, ink);
            canvas.DrawText(_statsLine, size.Width - 14, 44, SKTextAlign.Right, font, ink);
        }
    }

    /// <summary>F3: frame rate and where the frame time goes.</summary>
    public bool ShowStats { get; set; }

    private readonly Queue<double> _frameIntervals = new();
    private long _lastPresent;
    private string _statsLine = "measuring...";

    private void DrawWorld(SKCanvas canvas, SKSize size, GameWorld world)
    {
        var time = FixedTime ?? world.Time;
        _viewport = SKRect.Create(size);
        // Frames are cached at the target's pixel density so drawing them is a straight copy.
        _deviceScale = Math.Max(0.25f, canvas.TotalMatrix.ScaleX);
        // The act's colour grade is baked into every cached sprite (SpriteCache), so no full-screen filter pass.
        DrawGround(canvas, size, world, time);
        Stats.Mark("ground");
        DrawDepthSorted(canvas, size, world);
        DrawSpells(canvas, world);
        DrawLootBeams(canvas, world);
        Stats.Mark("objects+actors");
        DrawDarkness(canvas, size, world, time);
        Stats.Mark("darkness");
        DrawAsh(canvas, size, time);
        DrawVignette(canvas, size);
        Stats.Mark("ash+vignette");
    }

    /// <summary>How long each stage of the last frames took (CPU side), for the F3 overlay and performance tests.</summary>
    public RenderStats Stats { get; } = new();

    private MonsterActor? MonsterUnderPointer() =>
        World?.Hero.CurrentTarget is { IsDead: false } target ? target : _pointer is { } p ? MonsterUnder(p) : null;

    /// <summary>Act 1's grade: 30% desaturated, warmed toward bone and rust, a touch darker.</summary>
    private static SKColorFilter ActGrade()
    {
        const float s = 0.7f;
        float rw = 0.2126f, gw = 0.7152f, bw = 0.0722f;
        float[] sat =
        [
            rw * (1 - s) + s, gw * (1 - s), bw * (1 - s), 0, 0,
            rw * (1 - s), gw * (1 - s) + s, bw * (1 - s), 0, 0,
            rw * (1 - s), gw * (1 - s), bw * (1 - s) + s, 0, 0,
            0, 0, 0, 1, 0,
        ];
        float[] tint = [1.02f, 0.96f, 0.86f];
        for (var row = 0; row < 3; row++)
        for (var col = 0; col < 3; col++)
            sat[row * 5 + col] *= tint[row] * 0.92f;
        return SKColorFilter.CreateColorMatrix(sat);
    }

    private void VisibleCells(SKSize size, GameWorld world, out int x0, out int y0, out int x1, out int y1, bool clampToMap = true)
    {
        var margin = 520 * Zoom;
        var corners = new[]
        {
            ToMap(new Point(-margin, -margin)), ToMap(new Point(size.Width + margin, -margin)),
            ToMap(new Point(-margin, size.Height + margin * 1.6)), ToMap(new Point(size.Width + margin, size.Height + margin * 1.6)),
        };
        x0 = (int)Math.Floor(corners.Min(c => c.X));
        y0 = (int)Math.Floor(corners.Min(c => c.Y));
        x1 = (int)Math.Ceiling(corners.Max(c => c.X));
        y1 = (int)Math.Ceiling(corners.Max(c => c.Y));
        if (clampToMap)
        {
            x0 = Math.Max(0, x0);
            y0 = Math.Max(0, y0);
            x1 = Math.Min(world.Map.Width - 1, x1);
            y1 = Math.Min(world.Map.Height - 1, y1);
        }
    }

    private void DrawGround(SKCanvas canvas, SKSize size, GameWorld world, double time)
    {
        var tileset = _assets.Tileset(world.Zone.Tileset);
        VisibleCells(size, world, out var x0, out var y0, out var x1, out var y1, clampToMap: false);
        for (var y = y0; y <= y1; y++)
        for (var x = x0; x <= x1; x++)
        {
            // Beyond the map's edge the forest simply carries on, so the view never shows a void.
            var id = world.Map.InBounds(x, y) ? world.Map.Ground(x, y) : OutsideGround(x, y);
            if (id >= 0 && tileset.Tiles.TryGetValue(id, out var tile))
                DrawTile(canvas, tile, ToScreen(x + 0.5, y + 0.5), time, null);
        }
    }

    private static int OutsideGround(int x, int y) => 16 + (int)(Hash01(x * 92821 + y * 68917) * 16);

    private static int? OutsideTree(int x, int y) =>
        Hash01(x * 7919 + y * 104729) < 0.75 ? 248 + (int)(Hash01(x * 31 + y * 17) * 4) : null;

    private void DrawTile(SKCanvas canvas, FlareTile tile, SKPoint at, double time, SKPaint? paint)
    {
        int sx = tile.X, sy = tile.Y;
        if (tile.Animation.Count > 0)
        {
            var total = tile.Animation.Sum(f => f.DurationMs);
            var t = (int)(time * 1000) % Math.Max(1, total);
            foreach (var f in tile.Animation)
            {
                if (t < f.DurationMs)
                {
                    (sx, sy) = (f.X, f.Y);
                    break;
                }
                t -= f.DurationMs;
            }
        }
        Blit(canvas, tile.Image, sx, sy, tile.Width, tile.Height, tile.OffsetX, tile.OffsetY, at, (float)Zoom, paint);
    }

    /// <summary>
    /// Draws one frame or tile anchored at <paramref name="anchor"/>, from the pre-scaled cache, at a whole-pixel
    /// position so Skia can copy it without filtering.
    /// </summary>
    private void Blit(SKCanvas canvas, string sheet, int x, int y, int width, int height, int offsetX, int offsetY,
        SKPoint anchor, float scale, SKPaint? paint)
    {
        var left = anchor.X - offsetX * scale;
        var top = anchor.Y - offsetY * scale;
        if (!SKRect.Create(left, top, width * scale, height * scale).IntersectsWith(_viewport))
            return;
        var image = _sprites.Get(sheet, x, y, width, height, scale * _deviceScale);
        canvas.Save();
        canvas.Scale(1 / _deviceScale);
        canvas.DrawImage(image, MathF.Round(left * _deviceScale), MathF.Round(top * _deviceScale), paint);
        canvas.Restore();
    }

    private void DrawDepthSorted(SKCanvas canvas, SKSize size, GameWorld world)
    {
        var tileset = _assets.Tileset(world.Zone.Tileset);
        VisibleCells(size, world, out var x0, out var y0, out var x1, out var y1, clampToMap: false);
        var items = new List<(double Key, double X, Action Draw)>();
        var heroKey = world.Hero.X + world.Hero.Y;
        var heroFoot = ToScreen(world.Hero.X, world.Hero.Y);
        var heroTorso = SKRect.Create(heroFoot.X - 22 * (float)Zoom, heroFoot.Y - 130 * (float)Zoom, 44 * (float)Zoom, 130 * (float)Zoom);
        for (var y = y0; y <= y1; y++)
        for (var x = x0; x <= x1; x++)
        {
            IReadOnlyList<int> objects = world.Map.InBounds(x, y) ? world.Map.Objects(x, y)
                : OutsideTree(x, y) is { } tree ? [tree] : [];
            foreach (var id in objects)
            {
                if (!tileset.Tiles.TryGetValue(id, out var tile))
                    continue;
                int cx = x, cy = y;
                // Flat things on the ground (the shrine circle) sort under everything standing on them.
                var flat = tile.Height <= 110;
                var key = cx + cy + (flat ? 0.0 : 1.0);
                var paint = !flat && key > heroKey && Occludes(tile, ToScreen(cx + 0.5, cy + 0.5), heroTorso) ? _seeThrough : null;
                items.Add((key, cx, () => DrawTile(canvas, tile, ToScreen(cx + 0.5, cy + 0.5), world.Time, paint)));
            }
        }
        foreach (var m in world.Monsters)
        {
            var monster = m;
            items.Add((m.IsDead ? m.X + m.Y - 0.6 : m.X + m.Y, m.X, () => DrawMonster(canvas, monster, world)));
        }
        items.Add((world.Hero.X + world.Hero.Y, world.Hero.X, () => DrawHero(canvas, world)));
        foreach (var g in world.GroundItems)
        {
            var ground = g;
            items.Add((g.X + g.Y - 0.8, g.X, () => DrawGroundItem(canvas, ground, world)));
        }

        foreach (var item in items.OrderBy(i => i.Key).ThenBy(i => i.X))
            item.Draw();
    }

    /// <summary>Trees and buildings in front of the hero fade so the hero is never lost behind them.</summary>
    private readonly SKPaint _seeThrough = new() { Color = SKColors.White.WithAlpha(90) };

    private bool Occludes(FlareTile tile, SKPoint anchor, SKRect heroTorso)
    {
        var z = (float)Zoom;
        var bounds = SKRect.Create(anchor.X - tile.OffsetX * z, anchor.Y - tile.OffsetY * z, tile.Width * z, tile.Height * z);
        return bounds.IntersectsWith(heroTorso);
    }

    private void DrawFrame(SKCanvas canvas, string sheet, FlareFrame frame, SKPoint foot, float scale, SKPaint? paint)
    {
        if (frame.Width == 0)
            return;
        Blit(canvas, sheet, frame.X, frame.Y, frame.Width, frame.Height, frame.OffsetX, frame.OffsetY, foot, (float)Zoom * scale, paint);
    }

    private static FlareAnimation? Pick(FlareAnimationSet set, string name) =>
        set[name] ?? set[name == "critdie" ? "die" : "stance"];

    private void DrawMonster(SKCanvas canvas, MonsterActor m, GameWorld world)
    {
        var set = _assets.Enemy(m.Definition.Sprite);
        if (Pick(set, m.Animation) is not { } anim)
            return;
        var frame = anim.Frame(anim.FrameAt(m.AnimationTime), m.Direction);
        var foot = ToScreen(m.X, m.Y);
        using var paint = new SKPaint();
        var hovered = MonsterUnderPointer() == m;
        paint.ColorFilter = m.Creature.Stats.Rank switch
        {
            CreatureRank.Champion => SKColorFilter.CreateLighting(new SKColor(255, 190, 170), new SKColor(hovered ? (byte)70 : (byte)40, 0, 0)),
            CreatureRank.Minion => SKColorFilter.CreateLighting(new SKColor(200, 220, 190), new SKColor(0, hovered ? (byte)30 : (byte)8, 0)),
            _ when m.HitFlinch > 0 => SKColorFilter.CreateLighting(SKColors.White, new SKColor(90, 30, 30)),
            _ when hovered => SKColorFilter.CreateLighting(SKColors.White, new SKColor(35, 35, 35)),
            _ => null,
        };
        if (m.IsDead)
            paint.Color = SKColors.White.WithAlpha((byte)(255 * Math.Clamp(1 - (m.DeadFor - (GameWorld.CorpseSeconds - 3)) / 3, 0, 1)));
        DrawFrame(canvas, set.Image, frame, foot, (float)m.Definition.Scale, paint);
        if (!m.IsDead && (m.State != MonsterState.Idle || hovered) && m.Creature.HitPoints < m.Creature.Stats.HitPoints)
            WorldOverlay.DrawHealthBar(canvas, foot, (float)Zoom, m.Creature.HitPoints / (float)m.Creature.Stats.HitPoints,
                m.Creature.Stats.Rank == CreatureRank.Champion);
    }

    private void DrawHero(SKCanvas canvas, GameWorld world)
    {
        var hero = world.Hero;
        var foot = ToScreen(hero.X, hero.Y);
        var layers = Session!.AppearanceLayers;
        var animation = hero.Animation;
        foreach (var slot in _assets.LayerOrder(hero.Direction))
        {
            if (!layers.TryGetValue(slot, out var layer))
                continue;
            var set = _assets.AvatarLayer(layer);
            if (Pick(set, animation) is not { } anim)
                continue;
            DrawFrame(canvas, set.Image, anim.Frame(anim.FrameAt(hero.AnimationTime), hero.Direction), foot, 1f, null);
        }
    }

    private void DrawGroundItem(SKCanvas canvas, GroundItem g, GameWorld world)
    {
        var loot = g.Drop.Item?.Definition.Loot ?? (g.Drop.Draught ? "hp_potion" : g.Drop.Gold >= 100 ? "coins100" : g.Drop.Gold >= 25 ? "coins25" : "coins5");
        var set = _assets.Loot(loot);
        if (set.Animations.Values.FirstOrDefault() is not { } anim)
            return;
        var frame = anim.Frame(anim.FrameAt(world.Time - g.DroppedAt), 0);
        DrawFrame(canvas, set.Image, frame, ToScreen(g.X, g.Y), 1f, null);
    }

    /// <summary>Uncommon and better loot gets a beam of its rarity colour, so it can be spotted across the screen.</summary>
    private void DrawLootBeams(SKCanvas canvas, GameWorld world)
    {
        var z = (float)Zoom;
        foreach (var g in world.GroundItems)
        {
            if (g.Rarity == Rarity.Common || g.Drop.Item is null)
                continue;
            var at = ToScreen(g.X, g.Y);
            var color = Palette.Rarity(g.Rarity);
            var height = (g.Rarity >= Rarity.Rare ? 260 : 170) * z;
            using var beam = new SKPaint
            {
                BlendMode = SKBlendMode.Plus,
                Shader = SKShader.CreateLinearGradient(new SKPoint(at.X, at.Y), new SKPoint(at.X, at.Y - height),
                    [color.WithAlpha(150), color.WithAlpha(0)], SKShaderTileMode.Clamp),
            };
            canvas.DrawRect(SKRect.Create(at.X - 5 * z, at.Y - height, 10 * z, height), beam);
        }
    }

    /// <summary>Name labels over loot, nudged upwards so they never overlap; clicking one picks the item up.</summary>
    private void DrawLootLabels(SKCanvas canvas, GameWorld world)
    {
        _lootLabels.Clear();
        using var font = new SKFont(WorldOverlay.Serif, 13);
        using var back = new SKPaint { Color = new SKColor(0, 0, 0, 175) };
        using var ink = new SKPaint { IsAntialias = true };
        var placed = new List<SKRect>();
        foreach (var g in world.GroundItems.OrderByDescending(g => g.X + g.Y))
        {
            var at = ToScreen(g.X, g.Y);
            if (!_viewport.Contains(at))
                continue;
            var width = font.MeasureText(g.Label) + 12;
            var rect = SKRect.Create(at.X - width / 2, at.Y - 34 * (float)Zoom - 18, width, 18);
            while (placed.Any(r => r.IntersectsWith(rect)))
                rect.Offset(0, -20);
            placed.Add(rect);
            _lootLabels.Add((rect, g));
            var hovered = _pointer is { } p && rect.Contains((float)p.X, (float)p.Y);
            back.Color = hovered ? new SKColor(60, 50, 30, 220) : new SKColor(0, 0, 0, 175);
            canvas.DrawRect(rect, back);
            ink.Color = g.Drop.Item is { } item ? Palette.Rarity(item.Rarity) : g.Drop.Draught ? new SKColor(230, 110, 100) : Palette.Gold;
            canvas.DrawText(g.Label, rect.MidX, rect.Bottom - 5, SKTextAlign.Center, font, ink);
        }
        // Hovering a label shows the item, compared with what is worn in its slot.
        if (_pointer is { } pointer && _lootLabels.LastOrDefault(l => l.Rect.Contains((float)pointer.X, (float)pointer.Y)).Item?.Drop.Item is { } hoveredItem)
        {
            var gear = world.Hero.Equipment;
            var worn = gear.SlotFor(hoveredItem) is { } slot ? gear.In(slot) : null;
            InventoryPanel.Tooltip(canvas, _viewport.Size, new SKPoint((float)pointer.X, (float)pointer.Y), hoveredItem, worn,
                "On the ground", gear.PreviewEquip(hoveredItem));
        }
    }

    /// <summary>Spell colours: fire is orange, force violet, everything else a pale gold.</summary>
    private static SKColor SpellColor(AshenReliquary.Core.Combat.DamageType type) => type switch
    {
        AshenReliquary.Core.Combat.DamageType.Fire => new SKColor(255, 140, 40),
        AshenReliquary.Core.Combat.DamageType.Force => new SKColor(170, 120, 255),
        AshenReliquary.Core.Combat.DamageType.Void => new SKColor(120, 220, 110),
        _ => new SKColor(255, 230, 160),
    };

    /// <summary>Projectiles fly at chest height with a short glowing trail; impacts are a quick expanding burst.</summary>
    private void DrawSpells(SKCanvas canvas, GameWorld world)
    {
        var z = (float)Zoom;
        var lift = 70 * z;
        using var glow = new SKPaint { IsAntialias = true, BlendMode = SKBlendMode.Plus };
        foreach (var p in world.Projectiles)
        {
            if (world.Time < p.LaunchAt)
                continue;
            var at = ToScreen(p.X, p.Y);
            at.Y -= lift;
            var color = SpellColor(p.Ability.DamageType);
            var target = ToScreen(p.Target.X, p.Target.Y);
            target.Y -= lift;
            var dx = at.X - target.X;
            var dy = at.Y - target.Y;
            var len = MathF.Max(1, MathF.Sqrt(dx * dx + dy * dy));
            for (var i = 6; i >= 0; i--)
            {
                var t = i / 6f;
                var r = (10 - 6 * t) * z * 1.6f;
                glow.Shader = SKShader.CreateRadialGradient(new SKPoint(0, 0), r, [color.WithAlpha((byte)(220 * (1 - t))), color.WithAlpha(0)], SKShaderTileMode.Clamp);
                canvas.Save();
                canvas.Translate(at.X + dx / len * t * 46 * z, at.Y + dy / len * t * 46 * z);
                canvas.DrawCircle(0, 0, r, glow);
                canvas.Restore();
            }
            using var core = new SKPaint { IsAntialias = true, Color = SKColors.White.WithAlpha(230) };
            canvas.DrawCircle(at, 3.5f * z * 1.6f, core);
        }
        foreach (var impact in world.Impacts)
        {
            var age = (float)((world.Time - impact.At) / Impact.Lifetime);
            var at = ToScreen(impact.X, impact.Y);
            at.Y -= lift;
            var color = SpellColor(impact.Type);
            var r = (14 + 40 * age) * z * 1.4f;
            glow.Shader = SKShader.CreateRadialGradient(new SKPoint(0, 0), r,
                [color.WithAlpha((byte)(200 * (1 - age))), color.WithAlpha((byte)(80 * (1 - age))), color.WithAlpha(0)], [0, 0.6f, 1], SKShaderTileMode.Clamp);
            canvas.Save();
            canvas.Translate(at.X, at.Y);
            canvas.DrawCircle(0, 0, r, glow);
            canvas.Restore();
        }
        glow.Shader = null;
    }

    private void DrawDarkness(SKCanvas canvas, SKSize size, GameWorld world, double time)
    {
        var z = (float)Zoom;

        var lights = new List<(SKPoint At, float Radius, SKColor Glow)>
        {
            (ToScreen(world.Hero.X, world.Hero.Y), 6.5f, new SKColor(255, 190, 120, 28)),
        };
        VisibleCells(size, world, out var x0, out var y0, out var x1, out var y1);
        for (var y = y0; y <= y1; y++)
        for (var x = x0; x <= x1; x++)
        {
            foreach (var id in world.Map.Objects(x, y))
            {
                var flicker = (float)(Math.Sin(time * 9.1 + x * 3.7) * 0.25 + Math.Sin(time * 13.3 + y) * 0.15);
                if (id == 102)
                    lights.Add((ToScreen(x + 0.5, y + 0.5), 5.5f + flicker, new SKColor(255, 140, 50, 70)));
                else if (id == 265)
                    lights.Add((ToScreen(x + 0.5, y + 0.5), 3.5f + flicker * 0.3f, new SKColor(110, 170, 255, 60)));
            }
        }
        foreach (var m in world.Monsters)
        {
            if (!m.IsDead && m.Definition.IsSpawner)
                lights.Add((ToScreen(m.X, m.Y), 2.6f, new SKColor(140, 255, 120, 70)));
        }
        foreach (var p in world.Projectiles)
        {
            if (world.Time >= p.LaunchAt)
                lights.Add((ToScreen(p.X, p.Y), 2.2f, SpellColor(p.Ability.DamageType).WithAlpha(60)));
        }
        foreach (var impact in world.Impacts)
            lights.Add((ToScreen(impact.X, impact.Y), 3.0f, SpellColor(impact.Type).WithAlpha(70)));

        // The darkness mask is all soft gradients, so it is drawn at a quarter of the resolution and stretched.
        const float Down = 4f;
        int mw = Math.Max(1, (int)Math.Ceiling(size.Width / Down)), mh = Math.Max(1, (int)Math.Ceiling(size.Height / Down));
        if (_darknessSurface is null || _darknessSurface.Canvas.DeviceClipBounds.Width != mw || _darknessSurface.Canvas.DeviceClipBounds.Height != mh)
        {
            _darknessSurface?.Dispose();
            _darknessSurface = SKSurface.Create(new SKImageInfo(mw, mh, SKColorType.Rgba8888, SKAlphaType.Premul));
        }
        var mask = _darknessSurface.Canvas;
        mask.Clear(new SKColor(6, 5, 12, 168));
        mask.Save();
        mask.Scale(1 / Down);
        foreach (var (at, radius, _) in lights)
            Ellipse(mask, at, radius * HalfTileWidth * z, SKBlendMode.DstOut, SKColors.Black);
        // Coloured glow goes into the same overlay, so the whole lighting pass is one stretched draw.
        foreach (var (at, radius, glow) in lights)
            Ellipse(mask, at, radius * HalfTileWidth * z * 0.8f, SKBlendMode.SrcOver, glow);
        mask.Restore();
        using (var snapshot = _darknessSurface.Snapshot())
            canvas.DrawImage(snapshot, SKRect.Create(size), new SKSamplingOptions(SKFilterMode.Linear));

    }

    private static void Ellipse(SKCanvas canvas, SKPoint at, float radius, SKBlendMode blend, SKColor color)
    {
        using var paint = new SKPaint
        {
            BlendMode = blend,
            IsAntialias = true,
            Shader = SKShader.CreateRadialGradient(new SKPoint(0, 0), radius,
                [color, color.WithAlpha((byte)(color.Alpha * 0.55)), color.WithAlpha(0)], [0f, 0.55f, 1f], SKShaderTileMode.Clamp),
        };
        canvas.Save();
        canvas.Translate(at.X, at.Y - radius * 0.12f);
        canvas.Scale(1, 0.62f);
        canvas.DrawCircle(0, 0, radius, paint);
        canvas.Restore();
    }

    private static void DrawAsh(SKCanvas canvas, SKSize size, double time)
    {
        using var paint = new SKPaint { IsAntialias = true };
        for (var i = 0; i < 140; i++)
        {
            var hx = Hash01(i * 3 + 1);
            var hy = Hash01(i * 7 + 2);
            var speed = 8 + 18 * Hash01(i * 11 + 3);
            var x = (float)((hx * size.Width + time * speed * 0.6 + Math.Sin(time * 0.7 + i) * 14) % size.Width);
            var y = (float)((hy * size.Height + time * speed) % size.Height);
            paint.Color = new SKColor(190, 185, 175, (byte)(40 + 60 * Hash01(i * 13 + 4)));
            canvas.DrawCircle(x, y, (float)(0.8 + 1.6 * Hash01(i * 17 + 5)), paint);
        }
    }

    /// <summary>The vignette only changes with the window size, so it is drawn once into an image and reused.</summary>
    private void DrawVignette(SKCanvas canvas, SKSize size)
    {
        int vw = Math.Max(1, (int)(size.Width * _deviceScale)), vh = Math.Max(1, (int)(size.Height * _deviceScale));
        if (_vignette is null || _vignette.Width != vw || _vignette.Height != vh)
        {
            _vignette?.Dispose();
            using var surface = SKSurface.Create(new SKImageInfo(vw, vh));
            surface.Canvas.Scale(_deviceScale);
            var centre = new SKPoint(size.Width / 2, size.Height / 2);
            var radius = (float)Math.Sqrt(size.Width * size.Width + size.Height * size.Height) / 2;
            using var paint = new SKPaint
            {
                Shader = SKShader.CreateRadialGradient(centre, radius,
                    [SKColors.Transparent, SKColors.Transparent, new SKColor(0, 0, 0, 215)], [0f, 0.5f, 1f], SKShaderTileMode.Clamp),
            };
            surface.Canvas.Clear(SKColors.Transparent);
            surface.Canvas.DrawRect(SKRect.Create(size), paint);
            _vignette = surface.Snapshot();
        }
        canvas.DrawImage(_vignette, SKRect.Create(size));
    }

    private static double Hash01(int n)
    {
        unchecked
        {
            var h = (uint)n * 2654435761u;
            h ^= h >> 15;
            h *= 2246822519u;
            h ^= h >> 13;
            return (h & 0xffffff) / (double)0x1000000;
        }
    }
}
