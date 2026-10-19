using AshenReliquary.Core.Abilities;
using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Content;
using AshenReliquary.Core.Rules;
using AshenReliquary.Core.World;
using SkiaSharp;

namespace AshenReliquary.App.Rendering;

/// <summary>
/// The character sheet (C), on the left: attributes, then every derived number. Hovering a number shows its formula,
/// part by part, with any bonus the stacking rule dropped shown greyed out.
/// </summary>
internal sealed class CharacterSheet
{
    public const float Width = 330;

    public SKRect Bounds { get; private set; }

    public void Draw(SKCanvas canvas, SKSize size, GameWorld world, Action<SKRect, Func<IReadOnlyList<TipLine>>, SKColor> region)
    {
        var c = world.Hero.Character;
        // Height from the rows the sheet will draw, so the panel fits its contents.
        var caster = world.Hero.Resource.Kind == ResourceKind.Mana;
        var rows = 5 + (c.Defenses.Resistances.Any(r => r.Value > 0) ? 1 : 0) + 3 + (caster ? 2 : 0) + 2
            + (c.Rank(ProficiencyTrack.Spell) == ProficiencyRank.Untrained ? 2 : 3);
        Bounds = SKRect.Create(16, 70, Width, Math.Min(size.Height - 70 - 150, 65 + 48 + 3 * 24 + rows * 17 + 10));
        using var back = new SKPaint { Color = Palette.Panel };
        using var frame = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2, Color = Palette.Frame };
        using var title = new SKFont(WorldOverlay.Serif, 18);
        using var head = new SKFont(WorldOverlay.Serif, 13);
        using var font = new SKFont(WorldOverlay.Serif, 12.5f);
        using var ink = new SKPaint { IsAntialias = true, Color = Palette.Ink };
        using var gold = new SKPaint { IsAntialias = true, Color = new SKColor(240, 200, 90) };
        using var dim = new SKPaint { IsAntialias = true, Color = Palette.Dim };
        canvas.DrawRect(Bounds, back);
        canvas.DrawRect(Bounds, frame);
        var x = Bounds.Left + 16;
        var y = Bounds.Top + 26;
        canvas.DrawText(c.Name, x, y, SKTextAlign.Left, title, ink);
        y += 17;
        canvas.DrawText($"Level {c.Level} {(c.Heritage is { } h ? h.Name + " " : "")}{c.Ancestry.Name} {c.Class.Name}", x, y, SKTextAlign.Left, font, dim);
        y += 22;

        // Attributes, two columns.
        var kinds = Enum.GetValues<AttributeKind>();
        for (var i = 0; i < kinds.Length; i++)
        {
            var k = kinds[i];
            var col = x + i % 3 * 100;
            var row = y + i / 3 * 20;
            canvas.DrawText(k.Abbreviation(), col, row, SKTextAlign.Left, head, dim);
            canvas.DrawText($"{c.Attributes[k]:+0;-0;+0}", col + 38, row, SKTextAlign.Left, head, gold);
            var kind = k;
            region(SKRect.Create(col - 4, row - 14, 92, 19), () => AttributeTip(c, kind), new SKColor(240, 200, 90));
        }
        y += 48;

        void Section(string name)
        {
            canvas.DrawText(name, x, y, SKTextAlign.Left, head, gold);
            y += 18;
        }

        void Line(string name, string value, Func<IReadOnlyList<TipLine>> tip)
        {
            canvas.DrawText(name, x + 6, y, SKTextAlign.Left, font, dim);
            canvas.DrawText(value, Bounds.Right - 16, y, SKTextAlign.Right, font, ink);
            region(SKRect.Create(x, y - 13, Width - 32, 17), tip, Palette.Frame);
            y += 17;
        }

        void Total(string name, ModifierTotal total, string format = "+0;-0;+0") =>
            Line(name, total.Total.ToString(format), () => Breakdown(name, total, format));

        Section("Defence");
        Line("Hit points", $"{c.HitPoints} / {c.MaxHitPoints}", () =>
        [
            new("Hit points", Palette.Ink, $"{c.MaxHitPoints}", true),
            new($"Ancestry {c.Ancestry.HitPoints}", Palette.Dim),
            new($"+ (class {c.Class.HitPointsPerLevel} + CON {c.Attributes[AttributeKind.Constitution]:+0;-0;+0}) × level {c.Level}", Palette.Dim),
            .. (c.Heritage is { HitPointsPerLevel: > 0 } hh ? [new TipLine($"+ {hh.Name} {hh.HitPointsPerLevel} × level", Palette.Dim)] : Array.Empty<TipLine>()),
            .. (c.Gear.MaxHitPoints != 0 ? [new TipLine($"+ gear {c.Gear.MaxHitPoints}", Palette.Dim)] : Array.Empty<TipLine>()),
        ]);
        Total("Armour class", c.ArmorClass(), "0");
        Total("Fortitude", c.Save(StatisticKind.Fortitude));
        Total("Reflex", c.Save(StatisticKind.Reflex));
        Total("Will", c.Save(StatisticKind.Will));
        var resist = c.Defenses.Resistances.Where(r => r.Value > 0).ToList();
        if (resist.Count > 0)
            Line("Resistance", string.Join(", ", resist.Select(r => $"{r.Key.ToString().ToLowerInvariant()} {r.Value}")), () =>
                [new("Resistances", Palette.Ink, null, true), new("Damage of that type is reduced by the number.", Palette.Dim),
                 new("From gear, heritage and ancestry; they add up.", Palette.Dim)]);
        y += 6;

        Section("Offence");
        Total($"Attack ({c.Weapon.Definition.Name})", c.AttackModifier());
        Total("Damage bonus", c.DamageBonus());
        Total("Class DC", c.ClassDc(), "0");
        if (world.Hero.Resource.Kind == ResourceKind.Mana)
        {
            Total("Spell attack", c.SpellAttack());
            Total("Spell DC", c.SpellDc(), "0");
        }
        y += 6;

        Section("Other");
        Total("Perception", c.Perception());
        Line("Speed", $"{c.SpeedFeet} ft", () =>
        [
            new("Speed", Palette.Ink, $"{c.SpeedFeet} ft", true),
            new($"{c.Ancestry.Name} {c.Ancestry.SpeedFeet} ft" + (c.Heritage is { SpeedFeet: > 0 } hs ? $" + {hs.Name} {hs.SpeedFeet} ft" : ""), Palette.Dim),
            .. (c.Armor.SpeedPenaltyFeet(c.Attributes[AttributeKind.Strength]) is > 0 and var pen ? [new TipLine($"− armour {pen} ft", Palette.Dim)] : Array.Empty<TipLine>()),
            .. (c.Gear.MoveSpeed > 0 ? [new TipLine($"+ gear {c.Gear.MoveSpeed * 100:0}% (cap 40%)", Palette.Dim)] : Array.Empty<TipLine>()),
        ]);
        if (c.SneakAttackDice > 0)
        {
            Line("Sneak attack", $"{c.SneakAttackDice}d6", () =>
            [
                new("Sneak attack", Palette.Ink, $"{c.SneakAttackDice}d6", true),
                new("Precision damage on a Strike with an agile or finesse", Palette.Dim),
                new("weapon against a target off-guard to you: unaware of", Palette.Dim),
                new("you, struck from hiding, feinted, prone or off-guard.", Palette.Dim),
                new($"Class {c.Class.SneakAttackDiceAt(c.Level)}d6 (more at 5, 11, 17){(c.SneakDiceBonus > 0 ? $" + feats {c.SneakDiceBonus}d6" : "")}.", Palette.Dim),
            ]);
        }
        foreach (var track in new[] { ProficiencyTrack.Attack, ProficiencyTrack.Armor, ProficiencyTrack.Spell })
        {
            var rank = c.Rank(track);
            if (rank == ProficiencyRank.Untrained && track == ProficiencyTrack.Spell)
                continue;
            Line($"{track} proficiency", rank.ToString(), () =>
            [
                new($"{track} proficiency", Palette.Ink, rank.ToString(), true),
                new($"Bonus {Proficiency.Bonus(rank, c.Level):+0} (2 per rank + level).", Palette.Dim),
                new($"Your class raises it at set levels.", Palette.Dim),
            ]);
        }
    }

    private static IReadOnlyList<TipLine> Breakdown(string name, ModifierTotal total, string format)
    {
        var lines = new List<TipLine> { new(name, Palette.Ink, total.Total.ToString(format), true) };
        lines.AddRange(total.Applied.Select(m => new TipLine($"  {m}", Palette.Dim)));
        if (total.Suppressed.Count > 0)
        {
            lines.Add(new("Not counted (only the best of each type applies):", new SKColor(120, 112, 98)));
            lines.AddRange(total.Suppressed.Select(m => new TipLine($"  {m}", new SKColor(110, 102, 90))));
        }
        return lines;
    }

    private static IReadOnlyList<TipLine> AttributeTip(Core.Characters.Character c, AttributeKind k) =>
    [
        new(k.ToString(), new SKColor(240, 200, 90), $"{c.Attributes[k]:+0;-0;+0}", true),
        new(k switch
        {
            AttributeKind.Strength => "Melee attack and damage, Athletics.",
            AttributeKind.Dexterity => "Ranged and finesse attacks, AC up to the armour's cap, Reflex.",
            AttributeKind.Constitution => "Hit points per level, Fortitude, slower Taint.",
            AttributeKind.Intelligence => "Arcane spell attack and DC, Mana, Arcana.",
            AttributeKind.Wisdom => "Perception, Will, Medicine, Survival.",
            _ => "Divine abilities, Diplomacy, Intimidation, prices.",
        }, Palette.Dim),
        .. (c.Attributes.HasPartialBoost(k) ? [new TipLine("Holds a partial boost: the next boost here adds +1.", Palette.Dim)] : Array.Empty<TipLine>()),
        .. (k == c.KeyAttribute ? [new TipLine("Your class's key attribute.", new SKColor(240, 200, 90))] : Array.Empty<TipLine>()),
    ];
}
