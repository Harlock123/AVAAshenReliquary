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
        canvas.DrawText($"Act 1  ·  Level {world.Zone.Level} area", 22, 54, SKTextAlign.Left, small, dim);

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

        // Potion charges and Hero Points.
        using var flask = new SKPaint { IsAntialias = true };
        var flaskLeft = hp.X + orbRadius + 22;
        for (var i = 0; i < HealingPotion.MaxCharges; i++)
        {
            flask.Color = i < c.Potion.Charges ? new SKColor(200, 40, 40) : new SKColor(60, 50, 50);
            canvas.DrawRoundRect(SKRect.Create(flaskLeft + i * 22, size.Height - 64, 14, 26), 5, 5, flask);
        }
        canvas.DrawText("Q", flaskLeft + 33, size.Height - 26, SKTextAlign.Center, small, dim);
        Region(SKRect.Create(flaskLeft - 4, size.Height - 68, 70, 46), () =>
        [
            new("Healing potion", Ink, $"{c.Potion.Charges} / {HealingPotion.MaxCharges}", true),
            new($"Q: heals 1d8 + {2 * c.Level} over 2 s; 8 s cooldown.", Dim),
            new("Charges refill at shrines; healing draughts add one.", Dim),
            .. (c.Conditions.PotionsBlocked ? [new TipLine("Sickened: you can't drink!", Bad)] : Array.Empty<TipLine>()),
        ], new SKColor(200, 60, 50));
        for (var i = 0; i < c.HeroPoints.Count; i++)
            icons.Draw(canvas, "hero-point", SKRect.Create(flaskLeft + i * 24 - 2, size.Height - 100, 22, 22), Gold);
        if (c.HeroPoints.Count > 0)
        {
            Region(SKRect.Create(flaskLeft - 2, size.Height - 100, 24 * c.HeroPoints.Count, 22), () =>
            [
                new("Hero Points", Gold, $"{c.HeroPoints.Count} / {HeroPoints.Max}", true),
                new("One per zone, up to three. At 0 HP one is spent", Dim),
                new("automatically and you get up at half health.", Dim),
            ], Gold);
        }

        // The Step dodge (Shift), beside the potions: dimmed with its seconds left while it recovers.
        var step = SKRect.Create(flaskLeft + HealingPotion.MaxCharges * 22 + 8, size.Height - 66, 28, 28);
        var cooldown = world.DodgeCooldownRemaining;
        icons.Draw(canvas, "step", step, cooldown > 0 ? Dim.WithAlpha(110) : Ink);
        canvas.DrawText(cooldown > 0 ? $"{cooldown:0.0}" : "Shift", step.MidX, size.Height - 26, SKTextAlign.Center, small, dim);
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

        DrawBuffs(canvas, world, new SKPoint(24, hp.Y - orbRadius - 44));
        DrawCompanion(canvas, world, new SKPoint(hp.X + orbRadius + 22, size.Height - 136), small);

        // Resource orb and the Taint bar.
        var res = new SKPoint(size.Width - 28 - orbRadius, size.Height - 28 - orbRadius);
        var resource = hero.Resource;
        var (deep, bright) = resource.Kind switch
        {
            ResourceKind.Fury => (new SKColor(120, 40, 10), new SKColor(255, 140, 50)),
            ResourceKind.Momentum => (new SKColor(14, 80, 64), new SKColor(90, 230, 170)),
            ResourceKind.Focus => (new SKColor(60, 80, 20), new SKColor(190, 225, 90)),
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
        Region(SKRect.Create(taintBar.Left, taintBar.Top - 18, taintBar.Width, 26), () =>
        [
            new("Taint", new SKColor(160, 220, 90), $"{c.Taint.Value:0}%", true),
            new("The dead god's ash, building in you.", Dim),
            new("At 50%: −1 to all saves. At 100%: the whispers turn you", Dim),
            new("around for 2 s. Shrines cleanse it.", Dim),
            new($"Your rate: ×{c.Taint.RateMultiplier:0.00} (ancestry and CON).", Dim),
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
        var below = boss is not null ? 66f : 0f;
        if (target is { IsDead: false } t && t != boss)
            DrawTarget(canvas, size, t, below);

        // Latest message, fading after 5 s.
        if (world.LastMessage is { } message && world.Time - world.LastMessageAt < 5)
        {
            var alpha = (byte)(255 * Math.Clamp(5 - (world.Time - world.LastMessageAt), 0, 1));
            using var paint = new SKPaint { IsAntialias = true, Color = new SKColor(240, 215, 150, alpha) };
            canvas.DrawText(message, size.Width / 2, below + (target is { IsDead: false } && target != boss ? 108 : 90), SKTextAlign.Center, font, paint);
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
            items.Add((ward.Ability.Id, Good, $"+{ward.AcBonus}", $"{wardLeft:0.0}s", () =>
            [
                new(ward.Ability.NeedsShield ? "Shield raised" : "Shield", Good, $"{wardLeft:0.0}s", true),
                new($"+{ward.AcBonus} circumstance bonus to AC.", Dim),
                new(blockIn > 0 ? $"Shield Block recovers in {blockIn:0.0} s." : $"Shield Block ready: the next blow loses {ward.Hardness}.", blockIn > 0 ? Dim : Good),
                .. (ward.Ability.BlockLockout > 0 ? [new TipLine($"Once it blocks, the ward breaks for {ward.Ability.BlockLockout:0} s.", Dim)] : Array.Empty<TipLine>()),
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
        using var shade = new SKPaint { Color = new SKColor(0, 0, 0, 160) };
        using var ink = new SKPaint { IsAntialias = true, Color = Ink };
        using var key = new SKPaint { IsAntialias = true, Color = new SKColor(200, 180, 130) };
        using var costInk = new SKPaint { IsAntialias = true, Color = new SKColor(255, 170, 90) };
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
                face.Shader = SKShader.CreateLinearGradient(new SKPoint(rect.Left, rect.Top), new SKPoint(rect.Right, rect.Bottom),
                    AbilityColors(ability), SKShaderTileMode.Clamp);
                canvas.DrawRect(SKRect.Inflate(rect, -3, -3), face);
                icons.Draw(canvas, ability.Id, SKRect.Inflate(rect, -8, -8), AbilityTint(ability));

                var remaining = world.CooldownRemaining(ability);
                var total = Math.Max(ability.Cooldown, ability.IsFlourish ? GameWorld.FlourishLockoutSeconds : 0);
                if (remaining > 0 && total > 0)
                {
                    var fraction = (float)Math.Clamp(remaining / total, 0, 1);
                    canvas.DrawRect(SKRect.Create(rect.Left, rect.Top + rect.Height * (1 - fraction), rect.Width, rect.Height * fraction), shade);
                    canvas.DrawText($"{remaining:0.0}", rect.MidX, rect.MidY + 6, SKTextAlign.Center, big, stroke);
                    canvas.DrawText($"{remaining:0.0}", rect.MidX, rect.MidY + 6, SKTextAlign.Center, big, ink);
                }
                else if (!world.Hero.Resource.CanSpend(ability.Cost))
                {
                    canvas.DrawRect(rect, shade);
                }
                if (ability.Cost > 0)
                {
                    canvas.DrawText($"{ability.Cost:0}", rect.Right - 4, rect.Bottom - 4, SKTextAlign.Right, small, stroke);
                    canvas.DrawText($"{ability.Cost:0}", rect.Right - 4, rect.Bottom - 4, SKTextAlign.Right, small, costInk);
                }
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
                if (world.CooldownRemaining(ability) > 0 || count == 0)
                    canvas.DrawRect(rect, shade);
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
        if (a.Cost > 0) facts.Add($"{a.Cost:0} {world.Hero.Resource.Name}");
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
                lines.Add(new($"Up to {a.Range:0.#} tiles, untouchable for {GameWorld.TumbleSeconds:0.##} s{(c.TumbleFeint ? "; enemies passed are left off-guard" : "")}.", Good));
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
                if (a.UsesWeapon && a.WeaponPersistentAt(c.Level) is { } wound)
                    lines.Add(new($"On a hit: {wound} persistent {a.DamageType.ToString().ToLowerInvariant()}.", Dim));
                break;
        }
        return lines;
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
        var width = Math.Min(620, size.Width - 360);
        var bar = SKRect.Create(size.Width / 2 - width / 2, 30, width, 16);
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

    private void DrawTarget(SKCanvas canvas, SKSize size, MonsterActor t, float below = 0)
    {
        using var small = new SKFont(WorldOverlay.Serif, 13);
        using var ink = new SKPaint { IsAntialias = true, Color = Ink };
        using var dim = new SKPaint { IsAntialias = true, Color = Dim };
        using var back = new SKPaint { Color = new SKColor(20, 18, 16, 220) };
        var frame = SKRect.Create(size.Width / 2 - 170, 18 + below, 340, 18);
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
