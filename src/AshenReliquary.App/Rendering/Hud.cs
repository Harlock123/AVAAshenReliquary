using AshenReliquary.Core.Abilities;
using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Creatures;
using AshenReliquary.Core.World;
using SkiaSharp;

namespace AshenReliquary.App.Rendering;

/// <summary>
/// The HUD, Grim Dawn layout: health orb bottom left with potion charges, Hero Points and the buff and condition
/// row; the resource orb bottom right under the Taint bar; the 8-slot ability bar and XP bar between them, the belt
/// (bombs and elixirs, keys 7 to 0) above the bar's right end; the zone name top left; the target frame (with the
/// target's conditions and affixes) top centre. Everything it draws is registered as a hover region, and hovering one
/// shows its tooltip: every number is one hover away. <paramref name="drawItem"/> draws an item's icon (the belt's).
/// </summary>
internal sealed class Hud(IconStore icons, Action<SKCanvas, Core.Items.ItemDefinition, SKRect>? drawItem = null)
{
    /// <summary>The "Level up!" prompt's rect (empty when no level is waiting); clicking it opens the level-up screen.</summary>
    public SKRect LevelUpBadge { get; private set; }

    private static readonly IReadOnlyList<string> Slots = GameWorld.BarSlots;
    private readonly Dictionary<string, SKRect> _slotRects = [];
    /// <summary>The top of the potions-and-Step row (above the health orb in a narrow window).</summary>
    public float RowTop { get; private set; }
    // Each timer's seconds left at the last frame, and when it last came ready (for the ready flash).
    private readonly Dictionary<string, double> _lastRemaining = [];
    private readonly Dictionary<string, double> _readyAt = [];
    public const double FlashSeconds = 0.25;

    /// <summary>
    /// The ability being placed from the Abilities panel (K), or null: while one is held the bar's slots light up to
    /// take it.
    /// </summary>
    public string? Placing { get; set; }

    /// <summary>The bar slot under a point, as last drawn, or null.</summary>
    public string? SlotAt(SKPoint p) => _slotRects.FirstOrDefault(kv => kv.Value.Contains(p)).Key;

    /// <summary>Where a bar slot is on screen, as last drawn (tests click it).</summary>
    public SKRect SlotRect(string slot) => _slotRects.GetValueOrDefault(slot);
    private readonly List<(SKRect Rect, Func<IReadOnlyList<TipLine>> Tip, SKColor Edge)> _regions = [];

    private static readonly SKColor Ink = Palette.Ink, Dim = Palette.Dim, Gold = new(240, 200, 90), Good = new(130, 210, 120), Bad = new(232, 105, 94);

    /// <summary>True when the pointer is over a HUD element (world clicks there are ignored).</summary>
    public bool Contains(SKPoint p) => _regions.Any(r => r.Rect.Contains(p)) || _blocked.Any(r => r.Contains(p));

    private readonly List<SKRect> _blocked = [];

    public void Draw(SKCanvas canvas, SKSize size, GameWorld world, MonsterActor? target, SKPoint? pointer)
    {
        _regions.Clear();
        _blocked.Clear();
        var hero = world.Hero;
        var c = hero.Character;
        using var font = new SKFont(WorldOverlay.Serif, 15);
        using var small = new SKFont(WorldOverlay.Serif, 13);
        using var tiny = new SKFont(WorldOverlay.Serif, 11);
        using var big = new SKFont(WorldOverlay.Serif, 22);
        using var ink = new SKPaint { IsAntialias = true, Color = Ink };
        using var dim = new SKPaint { IsAntialias = true, Color = Dim };

        // Zone name and level, top left.
        canvas.DrawText(world.Zone.Name, 22, 34, SKTextAlign.Left, big, ink);
        _titleRight = 22 + big.MeasureText(world.Zone.Name);
        canvas.DrawText($"Act {world.Zone.Act}  ·  Level {world.Zone.Level} area", 22, 54, SKTextAlign.Left, small, dim);
        if (world.Zone.Cold)
            DrawCold(canvas, world, small);

        // Health orb.
        const float orbRadius = 58f;
        var hp = new SKPoint(28 + orbRadius, size.Height - 28 - orbRadius);
        Orb(canvas, hp, orbRadius, c.HitPoints / (float)c.MaxHitPoints, new SKColor(150, 18, 20), new SKColor(255, 80, 60));
        canvas.DrawText($"{c.HitPoints} / {c.MaxHitPoints}", hp.X, hp.Y + 5, SKTextAlign.Center, font, ink);
        Region(SKRect.Create(hp.X - orbRadius, hp.Y - orbRadius, orbRadius * 2, orbRadius * 2), () =>
        [
            new("Health", Ink, $"{c.HitPoints} / {c.MaxHitPoints}", true),
            new($"Ancestry {c.Ancestry.HitPoints} + (class {c.Class.HitPointsPerLevel} + CON {c.Attributes[Core.Rules.AttributeKind.Constitution]:+0;-0;+0}) × level {c.Level}", Dim),
            .. (c.Gear.MaxHitPoints != 0 ? [new TipLine($"Gear +{c.Gear.MaxHitPoints}", Dim)] : Array.Empty<TipLine>()),
            new("At 0 HP a Hero Point revives you at half health;", Dim),
            new("without one you wake at your last shrine (−100 XP).", Dim),
        ], new SKColor(200, 60, 50));

        // Potion charges, the Step and Hero Points, beside the health orb; in a window too narrow for them to clear
        // the ability bar's left end, they sit in a row above the orb instead (the bar had covered them).
        using var flask = new SKPaint { IsAntialias = true };
        var abilityBarLeft = size.Width / 2 - (Slots.Count * 54 + (Slots.Count - 1) * 6) / 2f;
        var narrow = hp.X + orbRadius + 22 + HealingPotion.MaxCharges * 22 + 8 + 28 + 6 > abilityBarLeft;
        var flaskLeft = narrow ? 30 : hp.X + orbRadius + 22;
        // The row's foot: the window's bottom, or just above the orb.
        var rowBase = narrow ? hp.Y - orbRadius - 4 : size.Height;
        RowTop = rowBase - 68;
        for (var i = 0; i < HealingPotion.MaxCharges; i++)
        {
            flask.Color = i < c.Potion.Charges ? new SKColor(200, 40, 40) : new SKColor(60, 50, 50);
            canvas.DrawRoundRect(SKRect.Create(flaskLeft + i * 22, rowBase - 64, 14, 26), 5, 5, flask);
        }
        var flasks = SKRect.Create(flaskLeft - 3, rowBase - 67, HealingPotion.MaxCharges * 22 - 2, 32);
        var potionWait = c.Potion.CooldownRemaining(world.Time);
        if (potionWait > 0 && c.Potion.Charges > 0)
        {
            canvas.Save();
            using (var shapes = new SKPath())
            {
                for (var i = 0; i < c.Potion.Charges; i++)
                    shapes.AddRoundRect(SKRect.Create(flaskLeft + i * 22, rowBase - 64, 14, 26), 5, 5);
                canvas.ClipPath(shapes, antialias: true);
            }
            DrawSweep(canvas, flasks, potionWait, HealingPotion.CooldownSeconds, null, edge: false);
            canvas.Restore();
        }
        ReadyFlash(canvas, "potion", flasks, c.Potion.Charges > 0 ? potionWait : 0, world.Time);
        canvas.DrawText(potionWait > 0 && c.Potion.Charges > 0 ? SweepText(potionWait) : "Q", flaskLeft + 33, rowBase - 26, SKTextAlign.Center, small, dim);
        Region(SKRect.Create(flaskLeft - 4, rowBase - 68, 70, 46), () =>
        [
            new("Healing potion", Ink, $"{c.Potion.Charges} / {HealingPotion.MaxCharges}", true),
            new($"Q: heals 1d8 + {2 * c.Level} over 2 s; 8 s cooldown.", Dim),
            new("Charges refill at shrines; healing draughts add one.", Dim),
            .. (c.Conditions.PotionsBlocked ? [new TipLine("Sickened: you can't drink!", Bad)] : Array.Empty<TipLine>()),
        ], new SKColor(200, 60, 50));
        // Hero Points above the potions, or after the Step in the narrow row.
        var pointsAt = narrow ? new SKPoint(flaskLeft + HealingPotion.MaxCharges * 22 + 8 + 28 + 12, rowBase - 61) : new SKPoint(flaskLeft - 2, rowBase - 100);
        for (var i = 0; i < c.HeroPoints.Count; i++)
            icons.Draw(canvas, "hero-point", SKRect.Create(pointsAt.X + i * 24, pointsAt.Y, 22, 22), Gold);
        if (c.HeroPoints.Count > 0)
        {
            Region(SKRect.Create(pointsAt.X, pointsAt.Y, 24 * c.HeroPoints.Count, 22), () =>
            [
                new("Hero Points", Gold, $"{c.HeroPoints.Count} / {HeroPoints.Max}", true),
                new("One per zone, up to three. At 0 HP one is spent", Dim),
                new("automatically and you get up at half health.", Dim),
            ], Gold);
        }

        // The Step dodge (Shift), beside the potions: dimmed with its seconds left while it recovers.
        var step = SKRect.Create(flaskLeft + HealingPotion.MaxCharges * 22 + 8, rowBase - 66, 28, 28);
        var cooldown = world.DodgeCooldownRemaining;
        icons.Draw(canvas, "step", step, Ink);
        if (cooldown > 0)
            DrawSweep(canvas, SKRect.Inflate(step, 2, 2), cooldown, GameWorld.DodgeCooldown, null, c => icons.Draw(c, "step", step, Dim.WithAlpha(110)));
        ReadyFlash(canvas, "step", SKRect.Inflate(step, 2, 2), cooldown, world.Time);
        canvas.DrawText(cooldown > 0 ? SweepText(cooldown) : "Shift", step.MidX, rowBase - 26, SKTextAlign.Center, small, dim);
        Region(SKRect.Create(step.Left - 4, step.Top - 2, step.Width + 8, 46), () =>
        [
            new("Step", Ink, cooldown > 0 ? $"{cooldown:0.0}s" : "ready", true),
            new($"Shift: dodge {GameWorld.DodgeTiles:0} tiles toward the pointer.", Dim),
            new($"No blow lands for {GameWorld.DodgeSeconds:0.##} s; {GameWorld.DodgeCooldown:0} s cooldown.", Dim),
        ], Ink);

        // Levels waiting for their picks (or the subclass a save from before them still lacks).
        if (c.PendingLevels.Count > 0 || c.NeedsSubclass)
        {
            using var badgeFont = new SKFont(WorldOverlay.Serif, 16);
            var pulse = (byte)(170 + 85 * Math.Abs(Math.Sin(world.Time * 2.5)));
            using var badgeInk = new SKPaint { IsAntialias = true, Color = Gold.WithAlpha(pulse) };
            using var badgeBack = new SKPaint { Color = new SKColor(0, 0, 0, 170) };
            var text = c.NeedsSubclass ? $"New: press L to choose your {c.Class.Name.ToLowerInvariant()} subclass"
                : $"Level up! Press L to choose what level {c.PendingLevels[0]} brings";
            var width = badgeFont.MeasureText(text) + 24;
            LevelUpBadge = SKRect.Create((size.Width - width) / 2, size.Height - 168, width, 28);
            canvas.DrawRect(LevelUpBadge, badgeBack);
            canvas.DrawText(text, size.Width / 2, LevelUpBadge.Top + 19, SKTextAlign.Center, badgeFont, badgeInk);
            _blocked.Add(LevelUpBadge);
        }
        else
        {
            LevelUpBadge = SKRect.Empty;
        }

        DrawBuffs(canvas, world, new SKPoint(24, Math.Min(hp.Y - orbRadius, RowTop) - 44));
        DrawCompanion(canvas, world, new SKPoint(hp.X + orbRadius + 22, size.Height - 136), small);

        // Resource orb and the Taint bar.
        var res = new SKPoint(size.Width - 28 - orbRadius, size.Height - 28 - orbRadius);
        var resource = hero.Resource;
        var (deep, bright) = resource.Kind switch
        {
            ResourceKind.Fury => (new SKColor(120, 40, 10), new SKColor(255, 140, 50)),
            ResourceKind.Momentum => (new SKColor(14, 80, 64), new SKColor(90, 230, 170)),
            ResourceKind.Focus => (new SKColor(60, 80, 20), new SKColor(190, 225, 90)),
            ResourceKind.Devotion => (new SKColor(84, 60, 12), new SKColor(205, 160, 52)),
            _ => (new SKColor(20, 40, 130), new SKColor(90, 150, 255)),
        };
        Orb(canvas, res, orbRadius, (float)(resource.Current / resource.Max), deep, bright);
        canvas.DrawText(resource.Name, res.X, res.Y - 4, SKTextAlign.Center, small, ink);
        canvas.DrawText($"{resource.Current:0} / {resource.Max:0}", res.X, res.Y + 14, SKTextAlign.Center, small, ink);
        var perHit = c.Gear.FuryPerHit + c.FuryPerHitBonus;
        Region(SKRect.Create(res.X - orbRadius, res.Y - orbRadius, orbRadius * 2, orbRadius * 2), () => resource.Kind switch
        {
            ResourceKind.Fury =>
            [
                new("Fury", new SKColor(255, 150, 70), $"{resource.Current:0} / {resource.Max:0}", true),
                new($"+{ClassResource.FuryPerHit + perHit:0} per hit, +{ClassResource.FuryPerCrit + perHit:0} per critical, +{ClassResource.FuryWhenHit:0} when you are hit.", Dim),
                new($"Drains {ClassResource.FuryDecayPerSecond:0} a second after {ClassResource.FuryDecayDelay:0} s out of combat.", Dim),
            ],
            ResourceKind.Momentum =>
            [
                new("Momentum", bright, $"{resource.Current:0} / {resource.Max:0}", true),
                new($"+{ClassResource.MomentumPerTile:0} a tile you move, +{ClassResource.MomentumPerHit + perHit:0} per hit, +{ClassResource.MomentumPerCrit + perHit:0} per critical.", Dim),
                new($"Out of a fight, moving builds it only to {ClassResource.MomentumRunningStart:0}: a running start.", Dim),
                new($"Drains {ClassResource.MomentumDecayPerSecond:0} a second once you stand still out of a fight.", Dim),
            ],
            ResourceKind.Devotion =>
            [
                new("Devotion", bright, $"{resource.Current:0} / {resource.Max:0}", true),
                new($"+{ClassResource.DevotionWhenHit:0} for every blow you take (blocked or not), +{ClassResource.DevotionPerHit + perHit:0} per hit, +{ClassResource.DevotionPerCrit + perHit:0} per critical.", Dim),
                new("Spent on Lay on Hands, Smite and your auras.", Dim),
                new($"Drains {ClassResource.DevotionDecayPerSecond:0} a second after {ClassResource.DevotionDecayDelay:0} s out of a fight.", Dim),
            ],
            _ =>
            [
                new(resource.Name, bright, $"{resource.Current:0} / {resource.Max:0}", true),
                new($"Regenerates {resource.RegenPerSecond:0.0} a second.", Dim),
                new("Max 20 + 5 × level + 4 × key attribute.", Dim),
            ],
        }, bright);

        var taintBar = SKRect.Create(res.X - orbRadius, res.Y - orbRadius - 18, orbRadius * 2, 6);
        using (var tb = new SKPaint { Color = new SKColor(20, 18, 16, 220) })
            canvas.DrawRect(SKRect.Inflate(taintBar, 1, 1), tb);
        using (var tf = new SKPaint { Color = c.Taint.Value >= TaintMeter.PenaltyThreshold ? new SKColor(180, 230, 80) : new SKColor(140, 200, 70) })
            canvas.DrawRect(SKRect.Create(taintBar.Left, taintBar.Top, taintBar.Width * (float)(c.Taint.Value / TaintMeter.Max), taintBar.Height), tf);
        canvas.DrawText($"Taint {c.Taint.Value:0}%", res.X, taintBar.Top - 4, SKTextAlign.Center, small, dim);
        if (hero.Dedication is { } pool)
            DrawDedicationPool(canvas, pool, SKRect.Create(taintBar.Left, taintBar.Top - 24, taintBar.Width, 5), small);
        if (world.MaxGraveCharges > 0)
            DrawGraveCharges(canvas, world, new SKPoint(res.X, taintBar.Top - (hero.Dedication is null ? 30 : 54)));
        Region(SKRect.Create(taintBar.Left, taintBar.Top - 18, taintBar.Width, 26), () =>
        [
            new("Taint", new SKColor(160, 220, 90), $"{c.Taint.Value:0}%", true),
            new("The dead god's ash, building in you.", Dim),
            new("At 50%: −1 to all saves. At 100%: the whispers turn you", Dim),
            new("around for 2 s. Shrines cleanse it.", Dim),
            new($"Your rate: ×{c.Taint.RateMultiplier:0.00} (ancestry and CON).", Dim),
            .. (c.TaintFuel
                ? new TipLine[]
                {
                    new($"Oathbreaker: each hit adds {GameWorld.OathTaintPerHit:0}; your Strikes drain {(int)(c.Taint.Value / GameWorld.TaintPerDrain)} (1 per {GameWorld.TaintPerDrain:0}).", new SKColor(180, 230, 80)),
                    new("Aura of Dread is paid for out of it.", Dim),
                }
                : []),
        ], new SKColor(140, 200, 70));

        DrawAbilityBar(canvas, size, world, tiny);
        DrawBelt(canvas, size, world, tiny);

        // XP bar and the level line.
        var barLeft = hp.X + orbRadius + 110;
        var barRight = res.X - orbRadius - 30;
        var bar = SKRect.Create(barLeft, size.Height - 22, barRight - barLeft, 8);
        using var back = new SKPaint { Color = new SKColor(20, 18, 16, 220) };
        using var xp = new SKPaint { Color = new SKColor(150, 120, 220) };
        canvas.DrawRect(SKRect.Inflate(bar, 1, 1), back);
        canvas.DrawRect(SKRect.Create(bar.Left, bar.Top, bar.Width * world.Experience.Xp / 1000f, bar.Height), xp);
        var attack = resource.Kind == ResourceKind.Mana ? $"Spell attack {c.SpellAttack().Total:+0}" : $"Attack {c.AttackModifier().Total:+0}";
        canvas.DrawText($"Level {c.Level} {c.Class.Name}  ·  {world.Experience.Xp} / 1000 XP  ·  AC {c.ArmorClass().Total}  ·  {attack}",
            bar.MidX, bar.Top - 8, SKTextAlign.Center, small, dim);
        Region(SKRect.Inflate(bar, 0, 6), () =>
        [
            new($"Level {c.Level}", new SKColor(190, 160, 255), $"{world.Experience.Xp} / 1000 XP", true),
            new("Monsters give Pathfinder XP for their level against yours,", Dim),
            new("weighted by rank (Normal ½, Champion 1, Elite 2).", Dim),
            new("C: character sheet with every number explained.", Dim),
        ], new SKColor(150, 120, 220));

        // A Herald fight puts the boss's bar across the top; any other target's frame goes beneath it.
        var boss = world.Boss;
        if (boss is not null)
            DrawBossBar(canvas, size, boss, world);
        var below = boss is not null ? 66f + _bossBarTop - 30 : 0f;
        if (target is { IsDead: false } t && t != boss)
            DrawTarget(canvas, size, t, below);

        // Latest message, fading after 5 s.
        if (world.LastMessage is { } message && world.Time - world.LastMessageAt < 5)
        {
            var alpha = (byte)(255 * Math.Clamp(5 - (world.Time - world.LastMessageAt), 0, 1));
            using var paint = new SKPaint { IsAntialias = true, Color = new SKColor(240, 215, 150, alpha) };
            // Under the target's frame and its affix line (lower when a narrow window pushed the frame down).
            var y = target is { IsDead: false } && target != boss ? Math.Max(below + 108, _targetTop + 70) : below + 90;
            canvas.DrawText(message, size.Width / 2, y, SKTextAlign.Center, font, paint);
        }

        if (world.Arrival is { } arrival && TownBanner.Alpha(world.Time - arrival.At) is var fade and > 0)
        {
            using var shade = new SKPaint { Color = new SKColor(0, 0, 0, (byte)(150 * fade)) };
            canvas.DrawRect(SKRect.Create(0, size.Height / 2 - 60, size.Width, 120), shade);
            using var title = new SKFont(WorldOverlay.Serif, 34);
            using var titleInk = new SKPaint { IsAntialias = true, Color = Palette.Ink.WithAlpha((byte)(255 * fade)) };
            using var subtitleInk = new SKPaint { IsAntialias = true, Color = Palette.Dim.WithAlpha((byte)(255 * fade)) };
            canvas.DrawText(arrival.Title, size.Width / 2, size.Height / 2, SKTextAlign.Center, title, titleInk);
            if (arrival.Subtitle is { } subtitle)
                canvas.DrawText(subtitle, size.Width / 2, size.Height / 2 + 30, SKTextAlign.Center, font, subtitleInk);
        }

        // The block behind the bottom bar (orbs to orbs) swallows world clicks too.
        _blocked.Add(SKRect.Create(0, size.Height - 130, size.Width, 130));
    }

    /// <summary>Draws the tooltip of the HUD element under the pointer, if any. Call last, over everything.</summary>
    public void DrawTooltip(SKCanvas canvas, SKSize size, SKPoint? pointer)
    {
        if (pointer is not { } p)
            return;
        for (var i = _regions.Count - 1; i >= 0; i--)
        {
            if (_regions[i].Rect.Contains(p))
            {
                TooltipBox.Draw(canvas, size, p, _regions[i].Tip(), _regions[i].Edge);
                return;
            }
        }
    }

    private void Region(SKRect rect, Func<IReadOnlyList<TipLine>> tip, SKColor edge) => _regions.Add((rect, tip, edge));

    /// <summary>Lets the minimap and the character sheet add their own hover regions to the same tooltip system.</summary>
    public void AddRegion(SKRect rect, Func<IReadOnlyList<TipLine>> tip, SKColor edge) => _regions.Add((rect, tip, edge));

    /// <summary>Areas that swallow clicks without a tooltip of their own (panels).</summary>
    public void Block(SKRect rect) => _blocked.Add(rect);

    /// <summary>A cold zone's exposure meter under the zone's name: pale blue, white when freezing, a blizzard named beside it.</summary>
    private void DrawCold(SKCanvas canvas, GameWorld world, SKFont small)
    {
        var cold = world.Hero.Cold;
        var bar = SKRect.Create(22, 64, 140, 6);
        using (var back = new SKPaint { Color = new SKColor(20, 18, 16, 220) })
            canvas.DrawRect(SKRect.Inflate(bar, 1, 1), back);
        using (var fill = new SKPaint { Color = cold >= 100 ? new SKColor(235, 245, 255) : cold >= 50 ? new SKColor(150, 200, 255) : new SKColor(100, 150, 210) })
            canvas.DrawRect(SKRect.Create(bar.Left, bar.Top, bar.Width * (float)(cold / 100), bar.Height), fill);
        using var ink = new SKPaint { IsAntialias = true, Color = world.Blizzard ? new SKColor(235, 245, 255) : Dim };
        canvas.DrawText(world.Blizzard ? $"Cold {cold:0}%  ·  blizzard" : $"Cold {cold:0}%", bar.Right + 8, bar.Bottom + 3, SKTextAlign.Left, small, ink);
        Region(SKRect.Create(bar.Left, bar.Top - 6, 240, 18), () =>
        [
            new("Cold", new SKColor(150, 200, 255), $"{cold:0}%", true),
            new("It rises out in the pass, three times as fast in a blizzard.", Dim),
            new("At 50%: clumsy 1. At 100%: freezing damage every 3 s.", Dim),
            new("Fires and braziers warm you; a shrine thaws you at once.", Dim),
            new("Cold resistance slows it.", Dim),
        ], new SKColor(150, 200, 255));
    }

    // ----- Buffs and conditions -------------------------------------------------------------

    private static readonly Dictionary<ConditionKind, string> ConditionText = new()
    {
        [ConditionKind.Frightened] = "−{0} to all checks and DCs; drops by 1 every 2 s.",
        [ConditionKind.Sickened] = "−{0} to all checks and DCs; you can't drink potions.",
        [ConditionKind.Clumsy] = "−{0} to Dexterity-based checks and DCs, including AC and Reflex.",
        [ConditionKind.Enfeebled] = "−{0} to Strength-based checks, DCs and damage.",
        [ConditionKind.Stupefied] = "−{0} to Intelligence, Wisdom and Charisma checks and DCs.",
        [ConditionKind.Drained] = "−{0} to Fortitude and −{0} × level maximum HP; rest at a shrine.",
        [ConditionKind.Slowed] = "Every action takes {1}% longer.",
        [ConditionKind.Stunned] = "You can't act.",
        [ConditionKind.OffGuard] = "−2 circumstance penalty to AC.",
        [ConditionKind.Prone] = "Can't move; off-guard (−2 AC).",
        [ConditionKind.Grabbed] = "Can't move; off-guard (−2 AC).",
        [ConditionKind.Immobilized] = "Can't move.",
        [ConditionKind.Blinded] = "Light radius 1 tile; attacks need a DC 11 flat check.",
        [ConditionKind.Dazzled] = "Attacks need a DC 11 flat check.",
    };

    private void DrawBuffs(SKCanvas canvas, GameWorld world, SKPoint at)
    {
        var hero = world.Hero;
        var c = hero.Character;
        var items = new List<(string Icon, SKColor Tint, string? Badge, string? Timer, Func<IReadOnlyList<TipLine>> Tip, SKColor Edge)>();
        foreach (var cond in c.Conditions.Active)
        {
            var text = string.Format(ConditionText.GetValueOrDefault(cond.Kind, ""), cond.Value, cond.Value * 25);
            var value = cond.Value > 1 || cond.Kind is ConditionKind.Frightened or ConditionKind.Sickened or ConditionKind.Drained ? cond.Value.ToString() : null;
            var left = cond.Remaining is { } r ? $"{r:0.0}s" : null;
            items.Add((cond.Kind.ToString(), Bad, value, left, () =>
            [
                new(Words(cond.Kind.ToString()) + (value is null ? "" : $" {value}"), Bad, left, true),
                .. Wrap(text, Dim),
            ], Bad));
        }
        foreach (var type in c.Persistent.Types)
        {
            var name = type == DamageType.Fire ? "Burning" : type == DamageType.Poison ? "Poisoned" : $"Persistent {type.ToString().ToLowerInvariant()}";
            var dice = c.Persistent.DiceFor(type);
            items.Add((icons.Get($"persistent-{type}") is null ? "persistent" : $"persistent-{type}", Bad, null, null, () =>
            [
                new(name, Bad, dice?.ToString(), true),
                .. Wrap($"{dice} {type.ToString().ToLowerInvariant()} damage a second; each second there's a 30% chance it ends. A shrine cleanses it.", Dim),
            ], Bad));
        }
        if (hero.HealRemaining > 0)
            items.Add(("healing", Good, null, null, () => [new("Healing", Good, $"+{hero.HealRemaining:0} to come", true), new("A potion's healing arrives over 2 s.", Dim)], Good));
        else if (world.Regenerating)
            items.Add(("healing", Good, null, null, () =>
            [
                new("Recovering", Good, $"+{c.MaxHitPoints * GameWorld.RegenShare:0.#} HP/s", true),
                new($"Out of combat for {GameWorld.RegenDelaySeconds:0} s, you get back {GameWorld.RegenShare:0%} of your max HP a second.", Dim),
            ], Good));
        if (hero.Ward is { } ward)
        {
            var wardLeft = ward.EndsAt - world.Time;
            var blockIn = world.ShieldBlockReadyIn;
            // A shield raised by Shield of Faith shows as a raised shield, beside the aura's own badge.
            var raised = ward.Ability.NeedsShield || ward.Ability.RaisesShield;
            items.Add((ward.Ability.RaisesShield ? "raise_shield" : ward.Ability.Id, Good, $"+{ward.AcBonus}", $"{wardLeft:0.0}s", () =>
            [
                new(raised ? "Shield raised" : "Shield", Good, $"{wardLeft:0.0}s", true),
                new($"+{ward.AcBonus} circumstance bonus to AC.", Dim),
                new(blockIn > 0 ? $"Shield Block recovers in {blockIn:0.0} s." : $"Shield Block ready: the next blow loses {ward.Hardness}.", blockIn > 0 ? Dim : Good),
                .. (ward.Ability.BlockLockout > 0 ? [new TipLine($"Once it blocks, the ward breaks for {ward.Ability.BlockLockout:0} s.", Dim)] : Array.Empty<TipLine>()),
            ], Good));
        }
        foreach (var aura in world.Auras)
        {
            var (ability, auraLeft) = (aura.Ability, aura.EndsAt - world.Time);
            var rank = AbilityDefinition.SpellRank(c.Level);
            items.Add((ability.Id, Good, null, $"{auraLeft:0}s", () =>
            [
                new(ability.Name, Good, $"{auraLeft:0} s", true),
                .. AuraLines(ability, rank, c),
            ], Good));
        }
        if (hero.IsHidden)
        {
            var hidden = hero.HiddenRemaining;
            items.Add(("hidden", Good, null, $"{hidden:0.0}s", () =>
            [
                new("Hidden", Good, $"{hidden:0.0}s", true),
                .. Wrap($"Monsters that haven't seen you notice you only once you're next to them. Your first blow finds its target off-guard{(c.AmbushCrit ? ", and a hit is a critical hit" : "")}. Attacking or being hit brings you out.", Dim),
            ], Good));
        }
        if (hero.AntidoteUntil > world.Time)
        {
            var left = hero.AntidoteUntil - world.Time;
            items.Add(("antidote", Good, null, $"{left:0}s", () =>
            [
                new("Antidote", Good, $"{left:0} s", true),
                new("+2 to saves against poison: the bog, the rot.", Dim),
            ], Good));
        }
        if (c.Taint.Value >= TaintMeter.PenaltyThreshold)
            items.Add(("tainted", new SKColor(180, 230, 80), null, null, () => [new("Tainted", new SKColor(180, 230, 80), null, true), new("−1 status penalty to all saves until cleansed at a shrine.", Dim)], new SKColor(140, 200, 70)));
        // A Flourish ability with no cooldown of its own (Power Attack) shows exactly the lockout time left.
        var lockout = world.Bar.Values.FirstOrDefault(a => a.IsFlourish && a.Cooldown <= 0) is { } flourish ? world.CooldownRemaining(flourish) : 0;
        if (lockout > 0)
            items.Add(("flourish", Dim, null, $"{lockout:0.0}s", () => [new("Flourish", Ink, null, true), new("You used a Flourish: no other Flourish for 3 s.", Dim)], Dim));

        using var badge = new SKFont(WorldOverlay.Serif, 12);
        using var timer = new SKFont(WorldOverlay.Serif, 10);
        using var back = new SKPaint { Color = new SKColor(14, 11, 10, 220) };
        using var ink = new SKPaint { IsAntialias = true, Color = Ink };
        using var stroke = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3, Color = SKColors.Black };
        for (var i = 0; i < items.Count; i++)
        {
            var (icon, tint, value, left, tip, edge) = items[i];
            var rect = SKRect.Create(at.X + i * 36, at.Y, 32, 32);
            canvas.DrawRect(rect, back);
            using var frame = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, Color = edge.WithAlpha(200) };
            canvas.DrawRect(rect, frame);
            icons.Draw(canvas, icon, SKRect.Inflate(rect, -3, -3), tint);
            if (value is not null)
            {
                canvas.DrawText(value, rect.Right - 3, rect.Bottom - 3, SKTextAlign.Right, badge, stroke);
                canvas.DrawText(value, rect.Right - 3, rect.Bottom - 3, SKTextAlign.Right, badge, ink);
            }
            if (left is not null)
                canvas.DrawText(left, rect.MidX, rect.Bottom + 11, SKTextAlign.Center, timer, ink);
            Region(rect, tip, edge);
        }
    }

    /// <summary>The companion's health beside the potions: its icon and a bar, or the seconds until it is back.</summary>
    private void DrawCompanion(SKCanvas canvas, GameWorld world, SKPoint at, SKFont small)
    {
        if (world.Companion is null && world.Hero.Character.Class.GraveCharges)
        {
            DrawMinions(canvas, world, at, small);
            return;
        }
        if (world.Companion is not { } ally)
            return;
        var icon = SKRect.Create(at.X, at.Y, 26, 26);
        var c = ally.Creature;
        icons.Draw(canvas, "companion", icon, ally.IsDead ? Dim.WithAlpha(110) : Good);
        var bar = SKRect.Create(icon.Right + 6, icon.MidY - 4, 74, 8);
        using var back = new SKPaint { Color = new SKColor(0, 0, 0, 180) };
        using var fill = new SKPaint { Color = new SKColor(90, 190, 80) };
        using var ink = new SKPaint { IsAntialias = true, Color = Dim };
        canvas.DrawRect(SKRect.Inflate(bar, 1, 1), back);
        if (ally.IsDead)
            canvas.DrawText($"back in {ally.ReturnIn:0} s", bar.Left, bar.Bottom + 1, SKTextAlign.Left, small, ink);
        else
            canvas.DrawRect(SKRect.Create(bar.Left, bar.Top, bar.Width * c.HitPoints / Math.Max(1, c.Stats.HitPoints), bar.Height), fill);
        Region(SKRect.Create(icon.Left, icon.Top, bar.Right - icon.Left, icon.Height), () =>
        [
            new($"Your {ally.Name.ToLowerInvariant()}", Good, ally.IsDead ? $"back in {ally.ReturnIn:0} s" : $"{c.HitPoints} / {c.Stats.HitPoints}", true),
            new($"AC {c.ArmorClass().Total}, bite {c.AttackModifier().Total:+0} for {c.StrikeDamage} {c.StrikeDamageType.ToString().ToLowerInvariant()}.", Dim),
            new("It follows you and falls on what you fight. It recovers", Dim),
            new($"when you do; if it falls it finds you {GameWorld.AllyReturnSeconds:0} s later.", Dim),
        ], Good);
    }

    /// <summary>A multiclass dedication's small pool (a Fighter's Mana): a thin bar in its resource's colour, named above.</summary>
    private void DrawDedicationPool(SKCanvas canvas, ClassResource pool, SKRect bar, SKFont small)
    {
        var colour = pool.Kind switch
        {
            ResourceKind.Fury => new SKColor(255, 140, 50),
            ResourceKind.Momentum => new SKColor(90, 230, 170),
            ResourceKind.Focus => new SKColor(190, 225, 90),
            ResourceKind.Devotion => new SKColor(205, 160, 52),
            _ => new SKColor(90, 150, 255),
        };
        using var back = new SKPaint { Color = new SKColor(20, 18, 16, 220) };
        using var fill = new SKPaint { Color = colour };
        using var ink = new SKPaint { IsAntialias = true, Color = Dim };
        canvas.DrawRect(SKRect.Inflate(bar, 1, 1), back);
        canvas.DrawRect(SKRect.Create(bar.Left, bar.Top, bar.Width * (float)(pool.Current / pool.Max), bar.Height), fill);
        canvas.DrawText($"{pool.Name} {pool.Current:0} / {pool.Max:0}", bar.MidX, bar.Top - 3, SKTextAlign.Center, small, ink);
        Region(SKRect.Create(bar.Left, bar.Top - 16, bar.Width, 22), () =>
        [
            new(pool.Name, colour, $"{pool.Current:0} / {pool.Max:0}", true),
            .. Wrap($"Your {pool.Source.Name} Dedication's {pool.Name.ToLowerInvariant()}, for the {pool.Source.Name.ToLowerInvariant()}'s ability it grants. "
                + (pool.StartsEmpty ? "It builds as a " + pool.Source.Name.ToLowerInvariant() + "'s does." : $"Half a {pool.Source.Name.ToLowerInvariant()}'s pool; regenerates {pool.RegenPerSecond:0.0} a second."), Dim),
        ], colour);
    }

    /// <summary>The Necromancer's Grave Charges: a row of small bone-white diamonds, lit for each held.</summary>
    private void DrawGraveCharges(SKCanvas canvas, GameWorld world, SKPoint centre)
    {
        const float size = 9, gap = 5;
        var count = world.MaxGraveCharges;
        var left = centre.X - (count * size * 2 + (count - 1) * gap) / 2;
        using var lit = new SKPaint { IsAntialias = true, Color = new SKColor(196, 220, 255) };
        using var unlit = new SKPaint { IsAntialias = true, Color = new SKColor(60, 64, 80, 200) };
        using var edge = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.2f, Color = new SKColor(20, 20, 28) };
        for (var i = 0; i < count; i++)
        {
            var x = left + i * (size * 2 + gap) + size;
            using var path = new SKPath();
            path.MoveTo(x, centre.Y - size);
            path.LineTo(x + size * 0.7f, centre.Y);
            path.LineTo(x, centre.Y + size);
            path.LineTo(x - size * 0.7f, centre.Y);
            path.Close();
            canvas.DrawPath(path, i < world.GraveCharges ? lit : unlit);
            canvas.DrawPath(path, edge);
        }
        Region(SKRect.Create(left, centre.Y - size, count * (size * 2 + gap), size * 2), () =>
        [
            new("Grave Charges", new SKColor(196, 220, 255), $"{world.GraveCharges} / {world.MaxGraveCharges}", true),
            .. Wrap($"Every monster that dies within {GameWorld.GraveReach:0} tiles of you leaves one{(world.Hero.Character.ChargesPerCorpseBonus > 0 ? $" ({1 + world.Hero.Character.ChargesPerCorpseBonus} for you)" : "")}. Raise Bones takes one in place of its Mana; Bone Rush, Bone Armor and Corpse Explosion need them. Lost if you fall.", Dim),
        ], new SKColor(150, 170, 220));
    }

    /// <summary>The Necromancer's skeletons: an icon and how many stand of how many it can hold.</summary>
    private void DrawMinions(SKCanvas canvas, GameWorld world, SKPoint at, SKFont small)
    {
        var standing = world.Minions.Where(m => !m.IsDead).ToList();
        var icon = SKRect.Create(at.X, at.Y, 26, 26);
        icons.Draw(canvas, "raise_bones", icon, standing.Count > 0 ? Good : Dim.WithAlpha(110));
        using var ink = new SKPaint { IsAntialias = true, Color = standing.Count > 0 ? Ink : Dim };
        var text = $"{standing.Count} / {world.MinionCap}";
        canvas.DrawText(text, icon.Right + 6, icon.MidY + 5, SKTextAlign.Left, small, ink);
        Region(SKRect.Create(icon.Left, icon.Top, 80, icon.Height), () =>
        [
            new("Your skeletons", Good, text, true),
            .. standing.Select(m => new TipLine($"{m.Name}: {m.Creature.HitPoints} / {m.Creature.Stats.HitPoints} HP", Dim)),
            .. Wrap($"Raise Bones calls one; you hold {world.MinionCap} at once (the oldest crumbles to make room). One that falls is gone, and all crumble if you fall.", Dim),
        ], Good);
    }

    // ----- Ability bar -------------------------------------------------------------------------

    private void DrawAbilityBar(SKCanvas canvas, SKSize size, GameWorld world, SKFont small)
    {
        const float slot = 54, gap = 6;
        var width = Slots.Count * slot + (Slots.Count - 1) * gap;
        var left = size.Width / 2 - width / 2;
        var top = size.Height - 98;
        using var frame = new SKPaint { IsAntialias = true, Color = Palette.Frame, Style = SKPaintStyle.Stroke, StrokeWidth = 2 };
        using var back = new SKPaint { Color = new SKColor(16, 13, 12, 230) };
        using var face = new SKPaint { IsAntialias = true };
        using var ink = new SKPaint { IsAntialias = true, Color = Ink };
        using var key = new SKPaint { IsAntialias = true, Color = new SKColor(200, 180, 130) };
        using var costInk = new SKPaint { IsAntialias = true, Color = new SKColor(255, 170, 90) };
        using var poorInk = new SKPaint { IsAntialias = true, Color = new SKColor(255, 80, 70) };
        using var greyFace = new SKPaint { Color = new SKColor(44, 40, 38) };
        using var poorShade = new SKPaint { Color = new SKColor(0, 0, 0, 95) };
        using var stroke = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3, Color = SKColors.Black };
        using var big = new SKFont(WorldOverlay.Serif, 17);
        var active = world.Hero.CurrentAbility?.Id;

        // Abilities the hero has that are on no slot: a reminder above the bar's left end.
        var waiting = world.AbilitiesOffBar.Count();
        if (waiting > 0 && Placing is null)
        {
            var hint = $"K: {waiting} {(waiting == 1 ? "ability" : "abilities")} off the bar";
            canvas.DrawText(hint, left, top - 8, SKTextAlign.Left, small, stroke);
            canvas.DrawText(hint, left, top - 8, SKTextAlign.Left, small, key);
            Region(SKRect.Create(left, top - 22, small.MeasureText(hint), 16), () =>
            [
                new("Abilities", Gold, "K", true),
                new($"{waiting} of your abilities {(waiting == 1 ? "is" : "are")} on no slot.", Dim),
                new("Press K to choose what goes on the bar.", Dim),
            ], Gold);
        }

        for (var i = 0; i < Slots.Count; i++)
        {
            var rect = SKRect.Create(left + i * (slot + gap), top, slot, slot);
            _slotRects[Slots[i]] = rect;
            canvas.DrawRect(rect, back);
            var label = Slots[i] switch { "lmb" => "LMB", "rmb" => "RMB", var n => n };
            if (world.Bar.TryGetValue(Slots[i], out var ability))
            {
                var remaining = world.CooldownRemaining(ability);
                var total = Math.Max(ability.Cooldown, ability.IsFlourish ? GameWorld.FlourishLockoutSeconds : 0);
                var cooling = remaining > 0 && total > 0;
                // Short of Fury, Mana and the like (but not cooling): the icon dims a little and its cost turns red,
                // so "wait" and "you need more" look different.
                var poor = !cooling && !world.CanAfford(ability);
                face.Shader = SKShader.CreateLinearGradient(new SKPoint(rect.Left, rect.Top), new SKPoint(rect.Right, rect.Bottom),
                    AbilityColors(ability), SKShaderTileMode.Clamp);
                canvas.DrawRect(SKRect.Inflate(rect, -3, -3), face);
                face.Shader = null;
                icons.Draw(canvas, ability.Id, SKRect.Inflate(rect, -8, -8), AbilityTint(ability));
                if (poor)
                    canvas.DrawRect(SKRect.Inflate(rect, -3, -3), poorShade);

                if (cooling)
                {
                    var id = ability.Id;
                    DrawSweep(canvas, rect, remaining, total, big, c =>
                    {
                        c.DrawRect(SKRect.Inflate(rect, -3, -3), greyFace);
                        icons.Draw(c, id, SKRect.Inflate(rect, -8, -8), new SKColor(128, 122, 116));
                    });
                }
                if (ability.Cost > 0)
                {
                    canvas.DrawText($"{ability.Cost:0}", rect.Right - 4, rect.Bottom - 4, SKTextAlign.Right, small, stroke);
                    canvas.DrawText($"{ability.Cost:0}", rect.Right - 4, rect.Bottom - 4, SKTextAlign.Right, small, poor ? poorInk : costInk);
                }
                ReadyFlash(canvas, Slots[i], rect, remaining, world.Time);
                if (ability.Id == active)
                    frame.Color = new SKColor(255, 210, 120);
                var a = ability;
                Region(rect, () => AbilityTip(world, a, label), AbilityTint(a));
            }
            if (Placing is not null)
                frame.Color = Gold;
            canvas.DrawRect(rect, frame);
            frame.Color = Palette.Frame;
            canvas.DrawText(label, rect.Left + 4, rect.Top + 12, SKTextAlign.Left, small, stroke);
            canvas.DrawText(label, rect.Left + 4, rect.Top + 12, SKTextAlign.Left, small, key);
        }
        face.Shader = null;
    }

    /// <summary>
    /// A cooldown's sweep over <paramref name="rect"/>: a dark pie, the part still to wait, that shrinks clockwise from
    /// 12 o'clock with a bright line on its moving edge, and (given a font) the seconds left in the middle.
    /// <paramref name="greyed"/> draws what lies under the dark part in grey, so the icon stays readable.
    /// </summary>
    internal static void DrawSweep(SKCanvas canvas, SKRect rect, double remaining, double total, SKFont? font,
        Action<SKCanvas>? greyed = null, bool edge = true)
    {
        var fraction = (float)Math.Clamp(remaining / total, 0, 1);
        if (fraction <= 0)
            return;
        var centre = new SKPoint(rect.MidX, rect.MidY);
        var radius = MathF.Sqrt(rect.Width * rect.Width + rect.Height * rect.Height) / 2 + 1;
        var start = -90 + 360 * (1 - fraction);
        using var pie = new SKPath();
        if (fraction >= 0.999f)
            pie.AddRect(rect);
        else
        {
            pie.MoveTo(centre);
            pie.ArcTo(new SKRect(centre.X - radius, centre.Y - radius, centre.X + radius, centre.Y + radius), start, 360 * fraction, false);
            pie.Close();
        }
        canvas.Save();
        canvas.ClipRect(rect);
        canvas.ClipPath(pie, antialias: true);
        greyed?.Invoke(canvas);
        using var dark = new SKPaint { Color = new SKColor(0, 0, 0, greyed is null ? (byte)165 : (byte)110) };
        canvas.DrawRect(rect, dark);
        canvas.Restore();

        if (edge && fraction < 0.999f)
        {
            var angle = start * MathF.PI / 180;
            canvas.Save();
            canvas.ClipRect(rect);
            using var line = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2, Color = new SKColor(255, 222, 160, 220) };
            canvas.DrawLine(centre, new SKPoint(centre.X + MathF.Cos(angle) * radius, centre.Y + MathF.Sin(angle) * radius), line);
            canvas.Restore();
        }
        if (font is not null)
        {
            var text = SweepText(remaining);
            using var stroke = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3, Color = SKColors.Black };
            using var ink = new SKPaint { IsAntialias = true, Color = Ink };
            canvas.DrawText(text, rect.MidX, rect.MidY + 6, SKTextAlign.Center, font, stroke);
            canvas.DrawText(text, rect.MidX, rect.MidY + 6, SKTextAlign.Center, font, ink);
        }
    }

    /// <summary>Seconds left on a cooldown: whole seconds, rounded up, and tenths under 3 s.</summary>
    internal static string SweepText(double remaining) =>
        remaining < 3 ? $"{remaining:0.0}" : $"{Math.Ceiling(remaining):0}";

    /// <summary>
    /// Notes when a timer comes ready (seconds left going from above 0 to 0) and, for <see cref="FlashSeconds"/> after,
    /// flares the slot's frame.
    /// </summary>
    private void ReadyFlash(SKCanvas canvas, string key, SKRect rect, double remaining, double now)
    {
        if (_lastRemaining.TryGetValue(key, out var before) && before > 0 && remaining <= 0)
            _readyAt[key] = now;
        _lastRemaining[key] = remaining;
        var strength = FlashStrength(key, now);
        if (strength <= 0)
            return;
        using var glow = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3,
            Color = new SKColor(255, 230, 170, (byte)(255 * strength)),
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Outer, 4),
        };
        canvas.DrawRect(rect, glow);
        using var fill = new SKPaint { Color = new SKColor(255, 230, 170, (byte)(70 * strength)) };
        canvas.DrawRect(rect, fill);
    }

    /// <summary>How bright a slot's ready flash is now, from 1 as it comes ready down to 0 after <see cref="FlashSeconds"/>.</summary>
    internal float FlashStrength(string key, double now) =>
        _readyAt.TryGetValue(key, out var at) && now - at < FlashSeconds && now >= at ? (float)(1 - (now - at) / FlashSeconds) : 0;

    /// <summary>The belt: four small slots above the bar's right end, each an item's icon with how many are left.</summary>
    private void DrawBelt(SKCanvas canvas, SKSize size, GameWorld world, SKFont small)
    {
        const float slot = 38, gap = 4, barWidth = 8 * 54 + 7 * 6;
        var left = size.Width / 2 + barWidth / 2 - (GameWorld.BeltSize * slot + (GameWorld.BeltSize - 1) * gap);
        var top = size.Height - 98 - slot - 4;
        using var frame = new SKPaint { IsAntialias = true, Color = Palette.Frame, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
        using var back = new SKPaint { Color = new SKColor(16, 13, 12, 220) };
        using var shade = new SKPaint { Color = new SKColor(0, 0, 0, 150) };
        using var key = new SKPaint { IsAntialias = true, Color = new SKColor(200, 180, 130) };
        using var ink = new SKPaint { IsAntialias = true, Color = Ink };
        using var stroke = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3, Color = SKColors.Black };
        for (var i = 0; i < GameWorld.BeltSize; i++)
        {
            var rect = SKRect.Create(left + i * (slot + gap), top, slot, slot);
            canvas.DrawRect(rect, back);
            if (world.Belt[i] is { } item && world.BeltAbility(i) is { } ability)
            {
                drawItem?.Invoke(canvas, item, SKRect.Inflate(rect, -3, -3));
                var count = world.BeltCount(i);
                var remaining = world.CooldownRemaining(ability);
                if (count == 0)
                    canvas.DrawRect(rect, shade);
                else if (remaining > 0 && ability.Cooldown > 0)
                    DrawSweep(canvas, rect, remaining, ability.Cooldown, null);
                ReadyFlash(canvas, "belt" + i, rect, count == 0 ? 0 : remaining, world.Time);
                canvas.DrawText(count.ToString(), rect.Right - 3, rect.Bottom - 3, SKTextAlign.Right, small, stroke);
                canvas.DrawText(count.ToString(), rect.Right - 3, rect.Bottom - 3, SKTextAlign.Right, small, ink);
                var (index, ab, label) = (i, ability, GameWorld.BeltKeys[i]);
                Region(rect, () => [.. AbilityTip(world, ab, label).Take(1), new($"{world.BeltCount(index)} in your bag", Dim),
                    .. AbilityTip(world, ab, label).Skip(1)], Gold);
            }
            canvas.DrawRect(rect, frame);
            canvas.DrawText(GameWorld.BeltKeys[i], rect.Left + 3, rect.Top + 11, SKTextAlign.Left, small, stroke);
            canvas.DrawText(GameWorld.BeltKeys[i], rect.Left + 3, rect.Top + 11, SKTextAlign.Left, small, key);
        }
    }

    internal static IReadOnlyList<TipLine> AbilityTip(GameWorld world, AbilityDefinition a, string key)
    {
        var c = world.Hero.Character;
        var rank = AbilityDefinition.SpellRank(c.Level);
        var lines = new List<TipLine> { new(a.Name, Gold, key, true) };
        var facts = new List<string> { $"{a.Actions} action{(a.Actions == 1 ? "" : "s")}" };
        if (a.Charges > 0) facts.Add(a.Charges == 1 ? "a Grave Charge" : $"{a.Charges} Grave Charges");
        if (a.Cost > 0) facts.Add($"{(a.ChargeInstead ? "a Grave Charge or " : "")}{a.Cost:0} {(a.CostsTaint ? "Taint" : world.ResourceFor(a).Name)}");
        if (a.Cooldown > 0) facts.Add($"{a.Cooldown:0} s cooldown");
        if (a.Range > 0) facts.Add($"range {a.Range:0}");
        lines.Add(new(string.Join(" · ", facts), Ink));
        if (a.Traits is { Count: > 0 } traits)
            lines.Add(new(string.Join(", ", traits), new SKColor(170, 150, 110)));
        lines.AddRange(Wrap(a.Description, Dim));
        switch (a.Kind)
        {
            case AbilityKind.Ward when a.NeedsShield:
                lines.Add(world.Hero.Equipment.In(Core.Items.EquipSlot.OffHand) is { Definition.IsShield: true } shield
                    ? new($"Now: {shield.Definition.Name}, +{shield.Definition.AcBonus} AC, hardness {shield.Definition.Hardness}", Good)
                    : new("You have no shield in your off hand.", Bad));
                break;
            case AbilityKind.Ward:
                lines.Add(new($"Now (rank {rank}): +{a.WardAc} AC, hardness {a.HardnessAt(rank)}", Good));
                break;
            case AbilityKind.Bomb:
                lines.Add(new($"Now (rank {rank}): {a.DamageAt(rank)} {a.DamageType.ToString().ToLowerInvariant()}, splash {a.Splash * rank}, attack {c.ThrowAttack().Total:+0}", Good));
                break;
            case AbilityKind.Elixir:
                break;
            case AbilityKind.Tumble:
                lines.Add(new($"Up to {a.Range:0.#} tiles, untouchable for {GameWorld.TumbleSeconds:0.##} s{(c.TumbleFeint ? "; enemies passed are left off-guard" : "")}"
                    + (a.HideSeconds > 0 ? $"; hidden for {a.HideSeconds:0} s after." : "."), Good));
                break;
            case AbilityKind.Mark:
                lines.Add(new(world.Hero.Prey is { IsDead: false } prey ? $"Now hunting: {prey.Name}; +{c.PreyDice}d8 against it." : $"+{c.PreyDice}d8 against your prey.", Good));
                break;
            case AbilityKind.Command:
                lines.Add(world.Companion is { } wolf
                    ? new($"Your {wolf.Name.ToLowerInvariant()}: {(wolf.IsDead ? $"down, back in {wolf.ReturnIn:0} s" : $"{wolf.Creature.HitPoints} / {wolf.Creature.Stats.HitPoints} HP")}, bite {wolf.Creature.StrikeDamage}+{a.ExtraDice}d{wolf.Creature.StrikeDamage.Sides}", wolf.IsDead ? Bad : Good)
                    : new("You have no companion.", Bad));
                break;
            case AbilityKind.Hide:
                lines.Add(new($"Now: Stealth {c.Skill("Stealth").Total:+0;-0;+0} against each hunter's Perception.", Good));
                break;
            case AbilityKind.Trap:
                lines.Add(new($"Now (rank {rank}): {a.DamageAt(rank)} {a.DamageType.ToString().ToLowerInvariant()}, radius {a.Radius:0.#}, class DC {c.ClassDc().Total}", Good));
                break;
            case AbilityKind.Summon when a.BuffSeconds > 0:
                lines.Add(new($"Now: {a.Count} for {a.BuffSeconds:0} s, beside the allies you hold.", Good));
                break;
            case AbilityKind.Summon:
                lines.Add(new($"Now: {world.Minions.Count(m => !m.IsDead && !m.IsTimed)} of {world.MinionCap} skeletons standing, {(6 + 5 * c.Level) * (1 + 0.5 * c.MinionGrowth):0} HP each.", Good));
                break;
            case AbilityKind.Blast or AbilityKind.Burst:
                // Bursts and blasts: the weapon's dice (a bow's arrows, a fan of knives) or their own, and the DC to save against.
                var harm = a.WeaponDamage ? $"{c.Weapon.Definition.Name} {c.Weapon.DiceCount + a.ExtraDiceAt(c.Level)}d{c.Weapon.Definition.DamageDie}{c.DamageBonus().Total:+0;-0;+0}"
                    : a.Dice is not null ? $"{a.DamageAt(rank)} {a.DamageType.ToString().ToLowerInvariant()}" : "no damage";
                lines.Add(new($"Now (rank {rank}): {harm}, radius {a.Radius:0.#}, {(a.IsSpellBurst ? $"spell DC {c.SpellDc().Total}" : $"class DC {c.ClassDc().Total}")}", Good));
                if (a.Drain > 0)
                    lines.Add(new($"{a.Drain:0%} of all it deals heals you.", Dim));
                if (a.HideSeconds > 0)
                    lines.Add(new($"You are hidden for {a.HideSeconds:0} s after.", Dim));
                break;
            case AbilityKind.Heal:
                lines.Add(new($"Now (rank {rank}): heals {(int)Math.Round(a.HealAt(rank) * (1 + c.HealShare))} HP{(world.Allies.Count > 0 ? $", allies within {a.Radius:0.#} tiles too" : "")}", Good));
                if (a.CleansesTaint > 0)
                    lines.Add(new($"Takes {a.CleansesTaint} off your Taint meter.", Dim));
                break;
            case AbilityKind.Aura:
                lines.Add(new($"Now (rank {rank}), {a.BuffSeconds:0} s, radius {a.Radius:0.#}:", Good));
                lines.AddRange(AuraLines(a, rank, c));
                break;
            case AbilityKind.SpellBolt:
                lines.Add(new($"Now (rank {rank}): {a.DamageAt(rank)} {a.DamageType.ToString().ToLowerInvariant()}, spell attack {c.SpellAttack().Total:+0}", Good));
                break;
            case AbilityKind.Missiles:
                lines.Add(new($"Now (rank {rank}): {a.MissilesAt(rank)} × {a.DamageAt(rank)} {a.DamageType.ToString().ToLowerInvariant()}", Good));
                break;
            default:
                var w = c.Weapon;
                var spent = a.SpendAll ? a.DiceForSpent(world.Hero.Resource.Current) : 0;
                var dice = w.DiceCount + a.ExtraDiceAt(c.Level) + spent;
                lines.Add(new($"Now: {w.Definition.Name} {dice}d{w.Definition.DamageDie}{c.DamageBonus().Total:+0;-0;+0}, attack {c.AttackModifier().Total:+0}"
                    + (w.Definition.IsRanged ? $", range {w.Definition.Range + c.WeaponRangeBonus:0}" : ""), Good));
                if (a.UsesWeapon && c.PreyDice > 0)
                    lines.Add(new($"Hunt Prey +{c.PreyDice}d8 against your prey.", Dim));
                if (a.UsesWeapon && w.Definition.IsRanged && a.Pierce + c.ArrowPierce > 0)
                    lines.Add(new($"Each arrow flies on through {a.Pierce + c.ArrowPierce} more {(a.Pierce + c.ArrowPierce == 1 ? "enemy" : "enemies")}.", Dim));
                if (a.SpendAll)
                    lines.Add(new($"Your {world.Hero.Resource.Current:0} {world.Hero.Resource.Name} buys {spent} extra {(spent == 1 ? "die" : "dice")}.", Dim));
                if (a.UsesWeapon && c.SneakAttackDice > 0)
                    lines.Add(new($"Sneak attack +{c.SneakAttackDice}d6 against an off-guard target.", Dim));
                if (a.UsesWeapon && c.UndeadBane > 0)
                    lines.Add(new($"Undead bane: +{c.UndeadBane} vitality against the undead.", Dim));
                if (a.UsesWeapon && c.TaintFuel)
                    lines.Add(new($"Taint drain: +{(int)(c.Taint.Value / GameWorld.TaintPerDrain)} Taint, healing you as much.", Dim));
                if (a.UsesWeapon && a.WeaponPersistentAt(c.Level) is { } wound)
                    lines.Add(new($"On a hit: {wound} persistent {a.DamageType.ToString().ToLowerInvariant()}.", Dim));
                break;
        }
        return lines;
    }

    /// <summary>What an aura does now, line by line (its tooltip and the buff's).</summary>
    private static IEnumerable<TipLine> AuraLines(AbilityDefinition a, int rank, Core.Characters.Character c)
    {
        if (a.AuraAc > 0)
            yield return new($"+{a.AuraAc} status bonus to AC and saves.", Dim);
        if (a.AuraSoakAt(rank) is > 0 and var soak)
            yield return new($"Every blow at you or an ally within loses {soak}.", Dim);
        if (a.RaisesShield)
            yield return new("Your shield is raised for as long.", Dim);
        if (a.Retaliate)
            yield return new("A monster that strikes you in reach takes a Strike back (once a second).", Dim);
        if (a.Dice is not null)
            yield return new($"Every {GameWorld.AuraTickSeconds:0} s: {a.AuraDiceAt(rank)} {(a.Holy ? "vitality to the undead, fire to the living" : a.DamageType.ToString().ToLowerInvariant())}, basic {a.Save} save (class DC {c.ClassDc().Total}).", Dim);
        if (a.Drain > 0)
            yield return new($"{a.Drain:0%} of what it deals heals you.", Dim);
        if (a.AuraHealAt(rank) is > 0 and var heal)
            yield return new($"Every {GameWorld.AuraTickSeconds:0} s: you and your allies within heal {heal}.", Dim);
    }

    private static SKColor AbilityTint(AbilityDefinition a) => a.DamageType switch
    {
        DamageType.Fire when a.IsSpell => new SKColor(255, 170, 90),
        DamageType.Force when a.IsSpell => new SKColor(200, 170, 255),
        _ => new SKColor(240, 228, 205),
    };

    private static SKColor[] AbilityColors(AbilityDefinition ability) => ability.DamageType switch
    {
        DamageType.Fire when ability.IsSpell => [new SKColor(110, 38, 10), new SKColor(40, 14, 6)],
        DamageType.Force when ability.IsSpell => [new SKColor(64, 40, 120), new SKColor(24, 14, 46)],
        _ when ability.Cost > 0 || ability.Cooldown > 0 => [new SKColor(100, 34, 26), new SKColor(38, 14, 12)],
        _ => [new SKColor(58, 52, 44), new SKColor(26, 22, 19)],
    };

    // ----- Target frame -------------------------------------------------------------------------

    /// <summary>
    /// The Herald's bar: its name and rank, health with a notch at each phase's threshold, the phase now, and in a
    /// last stand with a deluge timer, the seconds left.
    /// </summary>
    private void DrawBossBar(SKCanvas canvas, SKSize size, MonsterActor boss, GameWorld world)
    {
        using var title = new SKFont(WorldOverlay.Serif, 16);
        using var small = new SKFont(WorldOverlay.Serif, 12);
        using var ink = new SKPaint { IsAntialias = true, Color = new SKColor(235, 225, 200) };
        using var dim = new SKPaint { IsAntialias = true, Color = Dim };
        using var back = new SKPaint { Color = new SKColor(16, 14, 12, 230) };
        using var edge = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, Color = new SKColor(150, 120, 70) };
        var (barLeft, barTop, barWidth) = TopFrame(size, Math.Min(620, size.Width - 360), 30, 96);
        var bar = SKRect.Create(barLeft, barTop, barWidth, 16);
        _bossBarTop = barTop;
        canvas.DrawRect(SKRect.Inflate(bar, 3, 3), back);
        var share = (float)boss.Creature.HitPoints / boss.Creature.Stats.HitPoints;
        using (var fill = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(new SKPoint(bar.Left, bar.Top), new SKPoint(bar.Left, bar.Bottom),
                [new SKColor(140, 190, 70), new SKColor(60, 100, 30)], SKShaderTileMode.Clamp),
        })
            canvas.DrawRect(SKRect.Create(bar.Left, bar.Top, bar.Width * share, bar.Height), fill);
        canvas.DrawRect(SKRect.Inflate(bar, 3, 3), edge);
        using var notch = new SKPaint { Color = new SKColor(240, 220, 160, 200), StrokeWidth = 2 };
        foreach (var phase in boss.Definition.Boss!.Phases.Skip(1))
        {
            var x = bar.Left + bar.Width * (float)phase.Below;
            canvas.DrawLine(x, bar.Top - 3, x, bar.Bottom + 3, notch);
        }
        canvas.DrawText($"{boss.Definition.Name.ToUpperInvariant()}  ·  HERALD", bar.MidX, bar.Top - 8, SKTextAlign.Center, title, ink);
        var phaseName = boss.Definition.Boss.Phases[boss.BossPhase].Name;
        var line = boss.EnrageLeft is { } left
            ? left > 0 ? $"{phaseName}  ·  the cauldron boils over in {left:0} s" : $"{phaseName}  ·  the deluge!"
            : boss.BossPhase == 0 ? $"{boss.Creature.HitPoints} / {boss.Creature.Stats.HitPoints}" : phaseName;
        using var urgent = new SKPaint { IsAntialias = true, Color = boss.EnrageLeft is { } l && l < 10 ? new SKColor(255, 120, 90) : Dim };
        canvas.DrawText(line, bar.MidX, bar.Bottom + 17, SKTextAlign.Center, small, urgent);
        _blocked.Add(SKRect.Inflate(bar, 6, 22));
    }

    /// <summary>The zone name's right edge, and the tops of the Herald's bar and the target's frame, as last drawn.</summary>
    private float _titleRight, _bossBarTop = 30, _targetTop;

    /// <summary>
    /// Where a frame across the top goes: centred, or, in a window too narrow for that to clear the zone name and the
    /// minimap, between the two; with no room there either, under the zone name at <paramref name="lowTop"/>.
    /// </summary>
    private (float Left, float Top, float Width) TopFrame(SKSize size, float width, float top, float lowTop)
    {
        var (from, to) = (_titleRight + 14, size.Width - Minimap.Width - 28);
        var left = size.Width / 2 - width / 2;
        if (left >= from && left + width <= to)
            return (left, top, width);
        if (to - from >= 220)
        {
            var fit = Math.Min(width, to - from);
            return ((from + to) / 2 - fit / 2, top, fit);
        }
        return (22, lowTop, Math.Min(width, to - 22));
    }

    private void DrawTarget(SKCanvas canvas, SKSize size, MonsterActor t, float below = 0)
    {
        using var small = new SKFont(WorldOverlay.Serif, 13);
        using var ink = new SKPaint { IsAntialias = true, Color = Ink };
        using var dim = new SKPaint { IsAntialias = true, Color = Dim };
        using var back = new SKPaint { Color = new SKColor(20, 18, 16, 220) };
        var (left, top, width) = TopFrame(size, 340, 18 + below, below > 0 ? 18 + below : 84);
        var frame = SKRect.Create(left, top, width, 18);
        _targetTop = top;
        using var red = new SKPaint { Color = t.Creature.Stats.Rank == CreatureRank.Champion ? new SKColor(200, 110, 40) : new SKColor(150, 25, 22) };
        canvas.DrawRect(SKRect.Inflate(frame, 2, 2), back);
        canvas.DrawRect(SKRect.Create(frame.Left, frame.Top, frame.Width * t.Creature.HitPoints / t.Creature.Stats.HitPoints, frame.Height), red);
        canvas.DrawText($"{t.Name}  ·  AC {t.Creature.ArmorClass().Total}", frame.MidX, frame.Top + 14, SKTextAlign.Center, small, ink);
        var affixes = t.Affixes.Count > 0 ? "  ·  " + string.Join(", ", t.Affixes) : "";
        var warded = t.Ward > 0 ? $"  ·  ward {t.Ward}" : "";
        canvas.DrawText($"{t.Creature.Stats.Rank}, level {t.Creature.Stats.EffectiveLevel}{affixes}{warded}", frame.MidX, frame.Bottom + 16, SKTextAlign.Center, small, dim);
        Region(SKRect.Inflate(frame, 2, 18), () => TargetTip(t), new SKColor(200, 60, 50));

        // The target's conditions and persistent damage, in a row under the frame.
        var marks = t.Creature.Conditions.Active.Select(cond => (Icon: cond.Kind.ToString(), Name: Words(cond.Kind.ToString()), Value: cond.Value, Left: cond.Remaining))
            .Concat(t.Creature.Persistent.Types.Select(type => (Icon: icons.Get($"persistent-{type}") is null ? "persistent" : $"persistent-{type}",
                Name: type == DamageType.Fire ? "Burning" : $"Persistent {type.ToString().ToLowerInvariant()}", Value: 0, Left: (double?)null)))
            .ToList();
        var x = frame.MidX - marks.Count * 13;
        foreach (var m in marks)
        {
            var rect = SKRect.Create(x, frame.Bottom + 22, 24, 24);
            icons.Draw(canvas, m.Icon, rect, m.Name.StartsWith("Burning") ? new SKColor(255, 160, 70) : Bad);
            var mark = m;
            Region(rect, () => [new(mark.Name + (mark.Value > 1 ? $" {mark.Value}" : ""), Bad, mark.Left is { } l ? $"{l:0.0}s" : null, true),
                .. (ConditionText.TryGetValue(Enum.TryParse<ConditionKind>(mark.Icon, out var k) ? k : ConditionKind.Stunned, out var text) && mark.Icon == k.ToString()
                    ? Wrap(string.Format(text, mark.Value, mark.Value * 25), Dim)
                    : [new TipLine("Takes damage every second; a DC 15 flat check (30%) ends it.", Dim)])], Bad);
            x += 26;
        }
    }

    private static IReadOnlyList<TipLine> TargetTip(MonsterActor t)
    {
        var cr = t.Creature;
        var lines = new List<TipLine>
        {
            new(t.Name, Ink, $"{cr.HitPoints} / {cr.Stats.HitPoints} HP", true),
            new($"{cr.Stats.Rank}, level {cr.Stats.EffectiveLevel} · AC {cr.ArmorClass().Total} · attack {cr.AttackModifier().Total:+0}", Dim),
            new($"Strikes {cr.StrikeDamage} {cr.StrikeDamageType.ToString().ToLowerInvariant()} × {cr.Stats.DamageMultiplier:0.##}", Dim),
        };
        if (cr.Defenses.Weaknesses.Count > 0)
            lines.Add(new("Weak: " + string.Join(", ", cr.Defenses.Weaknesses.Select(w => $"{w.Key.ToString().ToLowerInvariant()} {w.Value}")), Good));
        if (cr.Defenses.Resistances.Count > 0)
            lines.Add(new("Resists: " + string.Join(", ", cr.Defenses.Resistances.Select(r => $"{r.Key.ToString().ToLowerInvariant()} {r.Value}")), Bad));
        if (cr.Defenses.Immunities.Count > 0)
            lines.Add(new("Immune: " + string.Join(", ", cr.Defenses.Immunities.Select(i => i.ToString().ToLowerInvariant())), Dim));
        foreach (var affix in t.Affixes)
            lines.AddRange(Wrap($"{affix}: {AffixText[affix]}", Gold));
        if (t.Ward > 0)
            lines.Add(new($"Warded: the next {t.Ward} damage is soaked.", Bad));
        return lines;
    }

    private static readonly Dictionary<MonsterAffix, string> AffixText = new()
    {
        [MonsterAffix.Vengeful] = "each ally that falls near it makes it stronger and faster, up to three times.",
        [MonsterAffix.Hasted] = "a quarter more blows a second and 15% faster on its feet.",
        [MonsterAffix.Shielded] = "every 10 s it wards itself and its pack, each ward soaking damage for 8 s.",
        [MonsterAffix.Tainted] = "it dies in a pool of Taint; stay out of it.",
    };

    // ----- Helpers -------------------------------------------------------------------------------

    private static string Words(string pascal) =>
        string.Concat(pascal.Select((ch, i) => i > 0 && char.IsUpper(ch) ? " " + char.ToLowerInvariant(ch) : ch.ToString()));

    /// <summary>Splits long text into tooltip lines of about 58 characters.</summary>
    public static IEnumerable<TipLine> Wrap(string text, SKColor color, int width = 58)
    {
        var line = "";
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length + word.Length + 1 > width && line.Length > 0)
            {
                yield return new TipLine(line, color);
                line = "";
            }
            line = line.Length == 0 ? word : line + " " + word;
        }
        if (line.Length > 0)
            yield return new TipLine(line, color);
    }

    private static void Orb(SKCanvas canvas, SKPoint centre, float radius, float fill, SKColor deep, SKColor bright)
    {
        using var rim = new SKPaint { IsAntialias = true, Color = new SKColor(70, 60, 48), Style = SKPaintStyle.Stroke, StrokeWidth = 5 };
        using var empty = new SKPaint { IsAntialias = true, Color = new SKColor(18, 14, 14, 235) };
        canvas.DrawCircle(centre, radius, empty);
        canvas.Save();
        using (var clip = new SKPath())
        {
            clip.AddCircle(centre.X, centre.Y, radius);
            canvas.ClipPath(clip, antialias: true);
        }
        var top = centre.Y + radius - 2 * radius * Math.Clamp(fill, 0, 1);
        using var liquid = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateRadialGradient(new SKPoint(centre.X - radius * 0.3f, centre.Y - radius * 0.3f), radius * 1.4f,
                [bright, deep, deep.WithAlpha(255)], [0f, 0.6f, 1f], SKShaderTileMode.Clamp),
        };
        canvas.DrawRect(new SKRect(centre.X - radius, top, centre.X + radius, centre.Y + radius), liquid);
        canvas.Restore();
        canvas.DrawCircle(centre, radius, rim);
    }
}

/// <summary>The banner shown on walking into a town.</summary>
public static class TownBanner
{
    public const double FadeIn = 1.0, Hold = 4.0, FadeOut = 1.5;

    /// <summary>The banner's opacity <paramref name="age"/> seconds after arriving: fade in, hold, fade out, gone.</summary>
    public static double Alpha(double age) =>
        age < 0 ? 0
        : age < FadeIn ? age / FadeIn
        : age < FadeIn + Hold ? 1
        : Math.Clamp(1 - (age - FadeIn - Hold) / FadeOut, 0, 1);
}
