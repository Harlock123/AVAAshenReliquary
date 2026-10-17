using AshenReliquary.Core.Abilities;
using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Creatures;
using AshenReliquary.Core.World;
using SkiaSharp;

namespace AshenReliquary.App.Rendering;

/// <summary>
/// The first-pass HUD, Grim Dawn layout: health orb bottom left, resource orb bottom right, potion charges
/// and Hero Points beside them, the XP bar along the bottom, the zone name, messages and the target frame.
/// </summary>
internal static class Hud
{
    public static void Draw(SKCanvas canvas, SKSize size, GameWorld world, MonsterActor? target)
    {
        var hero = world.Hero;
        var c = hero.Character;
        using var font = new SKFont(WorldOverlay.Serif, 15);
        using var small = new SKFont(WorldOverlay.Serif, 13);
        using var big = new SKFont(WorldOverlay.Serif, 22);
        using var ink = new SKPaint { IsAntialias = true, Color = new SKColor(232, 220, 196) };
        using var dim = new SKPaint { IsAntialias = true, Color = new SKColor(170, 160, 140) };

        // Zone name and level, top left.
        canvas.DrawText(world.Zone.Name, 22, 34, SKTextAlign.Left, big, ink);
        canvas.DrawText($"Act 1  ·  Level {world.Zone.Level} area", 22, 54, SKTextAlign.Left, small, dim);

        // Orbs.
        var orbRadius = 58f;
        var hp = new SKPoint(28 + orbRadius, size.Height - 28 - orbRadius);
        Orb(canvas, hp, orbRadius, c.HitPoints / (float)c.MaxHitPoints, new SKColor(150, 18, 20), new SKColor(255, 80, 60));
        canvas.DrawText($"{c.HitPoints} / {c.MaxHitPoints}", hp.X, hp.Y + 5, SKTextAlign.Center, font, ink);
        var res = new SKPoint(size.Width - 28 - orbRadius, size.Height - 28 - orbRadius);
        var resource = hero.Resource;
        var (deep, bright) = resource.Kind == ResourceKind.Fury
            ? (new SKColor(120, 40, 10), new SKColor(255, 140, 50))
            : (new SKColor(20, 40, 130), new SKColor(90, 150, 255));
        Orb(canvas, res, orbRadius, (float)(resource.Current / resource.Max), deep, bright);
        canvas.DrawText(resource.Name, res.X, res.Y - 4, SKTextAlign.Center, small, ink);
        canvas.DrawText($"{resource.Current:0} / {resource.Max:0}", res.X, res.Y + 14, SKTextAlign.Center, small, ink);

        // Taint: a thin bar above the resource orb.
        var taintBar = SKRect.Create(res.X - orbRadius, res.Y - orbRadius - 18, orbRadius * 2, 6);
        using (var tb = new SKPaint { Color = new SKColor(20, 18, 16, 220) })
            canvas.DrawRect(SKRect.Inflate(taintBar, 1, 1), tb);
        using (var tf = new SKPaint { Color = new SKColor(140, 200, 70) })
            canvas.DrawRect(SKRect.Create(taintBar.Left, taintBar.Top, taintBar.Width * (float)(c.Taint.Value / TaintMeter.Max), taintBar.Height), tf);
        canvas.DrawText($"Taint {c.Taint.Value:0}%", res.X, taintBar.Top - 4, SKTextAlign.Center, small, dim);

        DrawAbilityBar(canvas, size, world, small);

        // Potion charges and Hero Points.
        using var flask = new SKPaint { IsAntialias = true };
        for (var i = 0; i < HealingPotion.MaxCharges; i++)
        {
            flask.Color = i < c.Potion.Charges ? new SKColor(200, 40, 40) : new SKColor(60, 50, 50);
            var x = hp.X + orbRadius + 22 + i * 22;
            canvas.DrawRoundRect(SKRect.Create(x, size.Height - 64, 14, 26), 5, 5, flask);
        }
        canvas.DrawText("Q", hp.X + orbRadius + 55, size.Height - 26, SKTextAlign.Center, small, dim);
        using var star = new SKPaint { IsAntialias = true, Color = new SKColor(240, 200, 90) };
        for (var i = 0; i < c.HeroPoints.Count; i++)
            canvas.DrawCircle(hp.X + orbRadius + 32 + i * 18, size.Height - 92, 6, star);

        // XP bar and level.
        var barLeft = hp.X + orbRadius + 110;
        var barRight = res.X - orbRadius - 30;
        var bar = SKRect.Create(barLeft, size.Height - 22, barRight - barLeft, 8);
        using var back = new SKPaint { Color = new SKColor(20, 18, 16, 220) };
        using var xp = new SKPaint { Color = new SKColor(150, 120, 220) };
        canvas.DrawRect(SKRect.Inflate(bar, 1, 1), back);
        canvas.DrawRect(SKRect.Create(bar.Left, bar.Top, bar.Width * world.Experience.Xp / 1000f, bar.Height), xp);
        var attack = hero.Resource.Kind == ResourceKind.Mana ? $"Spell attack {c.SpellAttack().Total:+0}" : $"Attack {c.AttackModifier().Total:+0}";
        canvas.DrawText($"Level {c.Level} {c.Class.Name}  ·  {world.Experience.Xp} / 1000 XP  ·  AC {c.ArmorClass().Total}  ·  {attack}",
            bar.MidX, bar.Top - 8, SKTextAlign.Center, small, dim);

        // Target frame, top centre.
        if (target is { IsDead: false } t)
        {
            var frame = SKRect.Create(size.Width / 2 - 170, 18, 340, 18);
            using var red = new SKPaint { Color = t.Creature.Stats.Rank == CreatureRank.Champion ? new SKColor(200, 110, 40) : new SKColor(150, 25, 22) };
            canvas.DrawRect(SKRect.Inflate(frame, 2, 2), back);
            canvas.DrawRect(SKRect.Create(frame.Left, frame.Top, frame.Width * t.Creature.HitPoints / t.Creature.Stats.HitPoints, frame.Height), red);
            canvas.DrawText($"{t.Name}  ·  AC {t.Creature.Stats.ArmorClass}", frame.MidX, frame.Top + 14, SKTextAlign.Center, small, ink);
            canvas.DrawText($"{t.Creature.Stats.Rank}, level {t.Creature.Stats.EffectiveLevel}", frame.MidX, frame.Bottom + 16, SKTextAlign.Center, small, dim);
        }

        // Latest message, fading after 5 s.
        if (world.LastMessage is { } message && world.Time - world.LastMessageAt < 5)
        {
            var alpha = (byte)(255 * Math.Clamp(5 - (world.Time - world.LastMessageAt), 0, 1));
            using var paint = new SKPaint { IsAntialias = true, Color = new SKColor(240, 215, 150, alpha) };
            canvas.DrawText(message, size.Width / 2, 90, SKTextAlign.Center, font, paint);
        }

        if (world.ZoneComplete)
        {
            using var shade = new SKPaint { Color = new SKColor(0, 0, 0, 150) };
            canvas.DrawRect(SKRect.Create(0, size.Height / 2 - 60, size.Width, 120), shade);
            using var title = new SKFont(WorldOverlay.Serif, 34);
            canvas.DrawText("Fort Gallowmere", size.Width / 2, size.Height / 2, SKTextAlign.Center, title, ink);
            canvas.DrawText("The refugees open the gate. End of the vertical slice.", size.Width / 2, size.Height / 2 + 30, SKTextAlign.Center, font, dim);
        }
    }

    private static readonly string[] Slots = ["lmb", "rmb", "1", "2", "3", "4", "5", "6"];

    /// <summary>The 8-slot ability bar above the XP bar: mouse buttons then keys 1 to 6, with cooldown sweeps.</summary>
    private static void DrawAbilityBar(SKCanvas canvas, SKSize size, GameWorld world, SKFont small)
    {
        const float slot = 54, gap = 6;
        var width = Slots.Length * slot + (Slots.Length - 1) * gap;
        var left = size.Width / 2 - width / 2;
        var top = size.Height - 98;
        using var frame = new SKPaint { IsAntialias = true, Color = new SKColor(90, 76, 58), Style = SKPaintStyle.Stroke, StrokeWidth = 2 };
        using var back = new SKPaint { Color = new SKColor(16, 13, 12, 230) };
        using var face = new SKPaint { IsAntialias = true };
        using var shade = new SKPaint { Color = new SKColor(0, 0, 0, 160) };
        using var ink = new SKPaint { IsAntialias = true, Color = new SKColor(236, 224, 200) };
        using var key = new SKPaint { IsAntialias = true, Color = new SKColor(200, 180, 130) };
        using var cost = new SKPaint { IsAntialias = true, Color = new SKColor(255, 170, 90) };
        using var nameFont = new SKFont(WorldOverlay.Serif, 11);
        var active = world.Hero.CurrentAbility?.Id;

        for (var i = 0; i < Slots.Length; i++)
        {
            var rect = SKRect.Create(left + i * (slot + gap), top, slot, slot);
            canvas.DrawRect(rect, back);
            if (world.Bar.TryGetValue(Slots[i], out var ability))
            {
                face.Shader = SKShader.CreateLinearGradient(new SKPoint(rect.Left, rect.Top), new SKPoint(rect.Right, rect.Bottom),
                    AbilityColors(ability), SKShaderTileMode.Clamp);
                canvas.DrawRect(SKRect.Inflate(rect, -3, -3), face);
                var remaining = world.CooldownRemaining(ability);
                var total = Math.Max(ability.Cooldown, ability.IsFlourish ? GameWorld.FlourishLockoutSeconds : 0);
                var cooling = remaining > 0 && total > 0;
                if (!cooling)
                {
                    // Name in the middle (below the key label), cost in the bottom-right corner.
                    var words = ability.Name.Split(' ');
                    for (var w = 0; w < words.Length && w < 2; w++)
                        canvas.DrawText(words[w], rect.MidX, rect.MidY + 6 + w * 12 - (words.Length - 1) * 6, SKTextAlign.Center, nameFont, ink);
                    if (ability.Cost > 0)
                        canvas.DrawText($"{ability.Cost:0}", rect.Right - 5, rect.Bottom - 4, SKTextAlign.Right, nameFont, cost);
                }
                else
                {
                    var fraction = (float)Math.Clamp(remaining / total, 0, 1);
                    canvas.DrawRect(SKRect.Create(rect.Left, rect.Top + rect.Height * (1 - fraction), rect.Width, rect.Height * fraction), shade);
                    using var big = new SKFont(WorldOverlay.Serif, 17);
                    canvas.DrawText($"{remaining:0.0}", rect.MidX, rect.MidY + 9, SKTextAlign.Center, big, ink);
                }
                if (!cooling && !world.Hero.Resource.CanSpend(ability.Cost))
                    canvas.DrawRect(rect, shade);
                if (ability.Id == active)
                    frame.Color = new SKColor(255, 210, 120);
            }
            canvas.DrawRect(rect, frame);
            frame.Color = new SKColor(90, 76, 58);
            canvas.DrawText(Slots[i] switch { "lmb" => "LMB", "rmb" => "RMB", var n => n }, rect.Left + 4, rect.Top + 12, SKTextAlign.Left, nameFont, key);
        }
        face.Shader = null;
    }

    private static SKColor[] AbilityColors(AbilityDefinition ability) => ability.DamageType switch
    {
        DamageType.Fire when ability.IsSpell => [new SKColor(150, 50, 10), new SKColor(60, 18, 8)],
        DamageType.Force when ability.IsSpell => [new SKColor(80, 50, 150), new SKColor(30, 18, 60)],
        _ when ability.Cost > 0 || ability.Cooldown > 0 => [new SKColor(120, 40, 30), new SKColor(45, 16, 14)],
        _ => [new SKColor(70, 62, 52), new SKColor(30, 26, 22)],
    };

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
