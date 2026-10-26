using AshenReliquary.Core.Characters;
using AshenReliquary.Core.Progression;
using AshenReliquary.Core.Rules;
using AshenReliquary.Core.World;
using SkiaSharp;

namespace AshenReliquary.App.Rendering;

/// <summary>
/// The level-up screen (L): the lowest level still waiting for its picks. Each feat the level grants is a list of
/// the feats this character may take; a skill increase lists the skills that can rise a rank; level 5 adds four
/// attribute boosts. A level without a skill increase says when the next comes and lists the skills' ranks. Hover an option for its description; Confirm once everything is picked.
/// </summary>
internal sealed class LevelUpPanel
{
    /// <summary>The panel's width in a window wide enough; a narrower window gets a narrower panel, never one off its edges.</summary>
    public const float Width = 900;
    private const float Pad = 22, Row = 23, Columns = 3;

    /// <summary>One column's width: three fit across the panel (two feat columns and the skills at most).</summary>
    private float ColumnWidth => (Bounds.Width - 2 * Pad) / Columns;

    public SKRect Bounds { get; private set; }

    private int _level;
    private readonly Dictionary<FeatType, string> _feats = [];
    private string? _skill;
    private readonly List<AttributeKind> _boosts = [];
    private readonly List<(SKRect Rect, Action Pick, string Title, string Text)> _hits = [];
    /// <summary>The skill increase's rows, for the hover that shows what each would do.</summary>
    private readonly List<(SKRect Rect, string Skill)> _skillHits = [];
    private SKRect _confirm;

    private static readonly (LevelGain Gain, FeatType Type, string Title)[] FeatGains =
    [
        (LevelGain.ClassFeat, FeatType.Class, "Class feat"),
        (LevelGain.AncestryFeat, FeatType.Ancestry, "Ancestry feat"),
        (LevelGain.GeneralFeat, FeatType.General, "General feat"),
        (LevelGain.SkillFeat, FeatType.Skill, "Skill feat"),
    ];

    /// <summary>The subclass picked on the subclass page (a save from before subclasses), not yet confirmed.</summary>
    private string? _subclass;

    /// <summary>Whether the panel last drew the subclass page, which comes before any waiting level.</summary>
    public bool ShowsSubclass { get; private set; }

    public void Draw(SKCanvas canvas, SKSize size, GameWorld world, SKPoint? pointer)
    {
        var c = world.Hero.Character;
        ShowsSubclass = c.NeedsSubclass;
        if (ShowsSubclass)
        {
            DrawSubclass(canvas, size, c, pointer);
            return;
        }
        if (c.PendingLevels.FirstOrDefault() is not (> 0 and var level))
            return;
        if (level != _level)
        {
            _level = level;
            _feats.Clear();
            _skill = null;
            _boosts.Clear();
        }
        var gains = LevelProgression.GainsAt(level);
        var width = Math.Min(Width, size.Width - 16);
        Bounds = SKRect.Create((size.Width - width) / 2, 16, width, Math.Max(420, size.Height - 32 - 150));
        _hits.Clear();
        _skillHits.Clear();

        using var back = new SKPaint { Color = Palette.Panel.WithAlpha(255) };   // a full-screen sheet: nothing should show through
        using var frame = new SKPaint { Color = Palette.Frame, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
        using var titleFont = new SKFont(WorldOverlay.Serif, 24);
        using var head = new SKFont(WorldOverlay.Serif, 13);
        using var font = new SKFont(WorldOverlay.Serif, 14);
        using var small = new SKFont(WorldOverlay.Serif, 12);
        using var gold = new SKPaint { IsAntialias = true, Color = Palette.Gold };
        using var ink = new SKPaint { IsAntialias = true, Color = Palette.Ink };
        using var dim = new SKPaint { IsAntialias = true, Color = Palette.Dim };
        using var picked = new SKPaint { Color = new SKColor(240, 200, 90, 60) };
        using var hover = new SKPaint { Color = new SKColor(240, 200, 90, 24) };
        canvas.DrawRect(Bounds, back);
        canvas.DrawRect(Bounds, frame);
        canvas.DrawText($"Level {level}", Bounds.Left + Pad, Bounds.Top + 38, SKTextAlign.Left, titleFont, gold);
        var more = c.PendingLevels.Count > 1 ? $"  ·  {c.PendingLevels.Count - 1} more level(s) after this" : "";
        canvas.DrawText($"Choose what level {level} brings{more}. L or Esc to close.", Bounds.Left + Pad + 110, Bounds.Top + 37, SKTextAlign.Left, small, dim);

        // Each option: its name, and under it what it does (feats), wrapped to the column.
        void Option(float x, ref float y, string label, bool selected, Action pick, string title, string text, bool describe = true)
        {
            var lines = describe ? Wrap(text, small, ColumnWidth - 28) : [];
            var rect = SKRect.Create(x, y, ColumnWidth - 12, Row - 2 + lines.Count * 15);
            if (selected)
                canvas.DrawRect(rect, picked);
            else if (pointer is { } p && rect.Contains(p))
                canvas.DrawRect(rect, hover);
            canvas.DrawText(label, x + 8, y + 16, SKTextAlign.Left, font, selected ? gold : ink);
            for (var i = 0; i < lines.Count; i++)
                canvas.DrawText(lines[i], x + 14, y + 31 + i * 15, SKTextAlign.Left, small, dim);
            _hits.Add((rect, pick, title, text));
            y += rect.Height + 4;
        }

        // Feats, one column each.
        var column = 0;
        var top = Bounds.Top + 70;
        foreach (var (gain, type, title) in FeatGains)
        {
            if ((gains & gain) == 0)
                continue;
            var x = Bounds.Left + Pad + column++ * ColumnWidth;
            var y = top;
            canvas.DrawText(title.ToUpperInvariant(), x, y, SKTextAlign.Left, head, dim);
            y += 10;
            var options = c.FeatOptions(type, level);
            foreach (var feat in options.Where(f => f.Dedication is null))
            {
                var f = feat;
                Option(x, ref y, feat.Name, _feats.GetValueOrDefault(type) == feat.Id, () => _feats[type] = f.Id, feat.Name, feat.Description);
            }
            // Multiclass dedications, by name only (hover for what each grants) unless picked: six descriptions would
            // run off a short window.
            if (options.Any(f => f.Dedication is not null))
            {
                y += 6;
                canvas.DrawText("OR A DEDICATION (ONE ONLY)", x, y + 4, SKTextAlign.Left, head, dim);
                y += 12;
                foreach (var feat in options.Where(f => f.Dedication is not null))
                {
                    var f = feat;
                    var chosen = _feats.GetValueOrDefault(type) == feat.Id;
                    Option(x, ref y, feat.Name, chosen, () => _feats[type] = f.Id, feat.Name, feat.Description, describe: chosen);
                }
            }
        }

        // The skill increase, then the boosts, in the next column.
        var sx = Bounds.Left + Pad + column * ColumnWidth;
        var sy = top;
        if ((gains & LevelGain.SkillIncrease) != 0)
        {
            canvas.DrawText("SKILL INCREASE", sx, sy, SKTextAlign.Left, head, dim);
            sy += 10;
            foreach (var skill in Skills.Attribute.Keys.Order().Where(s => c.CanIncrease(s, level)))
            {
                var s = skill;
                var rank = c.SkillRank(skill);
                var rowTop = sy;
                Option(sx, ref sy, $"{skill}  {rank} → {rank + 1}", _skill == skill, () => _skill = s,
                    skill, $"Raise {skill} from {rank.ToString().ToLowerInvariant()} to {(rank + 1).ToString().ToLowerInvariant()}: "
                    + $"{c.Skill(skill).Total:+0;-0;+0} → {c.SkillAt(skill, rank + 1).Total:+0;-0;+0} to its checks.", describe: false);
                _skillHits.Add((SKRect.Create(sx, rowTop, ColumnWidth - 12, sy - rowTop - 4), s));
            }
            sy += 14;
        }
        else
        {
            // No increase this level: say so, and when the next one comes, and show where the skills stand.
            canvas.DrawText("SKILLS", sx, sy, SKTextAlign.Left, head, dim);
            sy += 24;
            foreach (var line in Wrap(SkillNote(level), small, Bounds.Right - Pad - sx - 8))
            {
                canvas.DrawText(line, sx + 8, sy, SKTextAlign.Left, small, dim);
                sy += 16;
            }
            sy += 6;
            foreach (var skill in Skills.Attribute.Keys.Order())
            {
                var rank = c.SkillRank(skill);
                canvas.DrawText(skill, sx + 8, sy, SKTextAlign.Left, font, rank > ProficiencyRank.Untrained ? ink : dim);
                canvas.DrawText(rank.ToString(), sx + 130, sy, SKTextAlign.Left, font, rank > ProficiencyRank.Untrained ? gold : dim);
                sy += 21;
            }
            sy += 14;
        }
        var needsBoosts = (gains & LevelGain.AttributeBoosts) != 0 && !c.LevelUpBoosts.ContainsKey(level);
        if (needsBoosts)
        {
            canvas.DrawText($"ATTRIBUTE BOOSTS ({_boosts.Count} / {AttributeSet.BoostsPerLevelUp})", sx, sy, SKTextAlign.Left, head, dim);
            sy += 10;
            foreach (var kind in Enum.GetValues<AttributeKind>())
            {
                var k = kind;
                var value = c.Attributes[kind];
                var partial = value >= AttributeSet.PartialBoostThreshold;
                Option(sx, ref sy, $"{kind}  {value:+0;-0;+0}{(partial ? " (partial)" : "")}", _boosts.Contains(kind),
                    () => { if (!_boosts.Remove(k) && _boosts.Count < AttributeSet.BoostsPerLevelUp) _boosts.Add(k); },
                    kind.ToString(), partial ? "At +4 or more a boost is partial: two make +1." : "+1 to this attribute.", describe: false);
            }
        }

        // Confirm.
        var bottom = Bounds.Bottom - Pad;
        var ready = Choice(c, level) is not null;
        _confirm = SKRect.Create(Bounds.Right - Pad - 140, bottom - 36, 140, 34);
        using (var button = new SKPaint { Color = ready ? new SKColor(80, 60, 30, 240) : new SKColor(34, 26, 22, 240) })
            canvas.DrawRect(_confirm, button);
        canvas.DrawRect(_confirm, frame);
        canvas.DrawText("Confirm", _confirm.MidX, _confirm.MidY + 5, SKTextAlign.Center, font, ready ? gold : dim);
        if (!ready)
        {
            // What still stands between the player and Confirm, so a pick missed is never a mystery.
            var lines = Wrap($"Still to choose: {string.Join(", ", Missing(c, level))}.", small, _confirm.Left - Bounds.Left - 2 * Pad);
            for (var i = 0; i < lines.Count; i++)
                canvas.DrawText(lines[i], Bounds.Left + Pad, _confirm.MidY + 5 - (lines.Count - 1 - i) * 15, SKTextAlign.Left, small, gold);
        }

        // Hovering a skill shows what the increase would do: its checks before and after, and where the game rolls it.
        // Beside the row rather than over the skills under it.
        var row = pointer is { } at ? _skillHits.FirstOrDefault(h => h.Rect.Contains(at)) : default;
        Tip = row.Skill is { } hovered ? SkillTip(c, hovered, small) : null;
        if (Tip is not null)
            TooltipBox.Draw(canvas, size, new SKPoint(row.Rect.Right - 12, row.Rect.Bottom), Tip, Palette.Gold);
    }

    /// <summary>The skill tooltip as last drawn, or null (tests read it).</summary>
    public IReadOnlyList<TipLine>? Tip { get; private set; }

    /// <summary>The hover over a skill increase: <see cref="SkillIncrease.Preview"/>, wrapped to the tooltip.</summary>
    private static List<TipLine> SkillTip(Character c, string skill, SKFont font)
    {
        var content = Core.Content.ContentDatabase.Embedded;
        var rank = c.SkillRank(skill);
        var tip = new List<TipLine> { new($"{skill}: {rank} → {rank + 1}", Palette.Gold, null, true) };
        foreach (var line in SkillIncrease.Preview(c, skill, content.Dialogues.Values, content.Feats.Values))
        {
            var color = line.Better ? InventoryPanel.Better : Palette.Dim;
            if (line.Right is not null)
                tip.Add(new(line.Text, color, line.Right));
            else
                tip.AddRange(Wrap(line.Text, font, 390).Select((w, i) => new TipLine(i == 0 ? w : "  " + w, color)));
        }
        return tip;
    }

    /// <summary>
    /// The subclass page: a character from before subclasses chooses one, its feature and ability described side by
    /// side, before any waiting level.
    /// </summary>
    private void DrawSubclass(SKCanvas canvas, SKSize size, Character c, SKPoint? pointer)
    {
        var width = Math.Min(Width, size.Width - 16);
        Bounds = SKRect.Create((size.Width - width) / 2, 16, width, Math.Max(420, size.Height - 32 - 150));
        _hits.Clear();
        using var back = new SKPaint { Color = Palette.Panel.WithAlpha(255) };
        using var frame = new SKPaint { Color = Palette.Frame, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
        using var titleFont = new SKFont(WorldOverlay.Serif, 24);
        using var nameFont = new SKFont(WorldOverlay.Serif, 18);
        using var font = new SKFont(WorldOverlay.Serif, 14);
        using var small = new SKFont(WorldOverlay.Serif, 12);
        using var gold = new SKPaint { IsAntialias = true, Color = Palette.Gold };
        using var ink = new SKPaint { IsAntialias = true, Color = Palette.Ink };
        using var dim = new SKPaint { IsAntialias = true, Color = Palette.Dim };
        using var picked = new SKPaint { Color = new SKColor(240, 200, 90, 60) };
        using var hover = new SKPaint { Color = new SKColor(240, 200, 90, 24) };
        canvas.DrawRect(Bounds, back);
        canvas.DrawRect(Bounds, frame);
        canvas.DrawText($"{c.Class.Name} subclass", Bounds.Left + Pad, Bounds.Top + 38, SKTextAlign.Left, titleFont, gold);
        var paths = c.SubclassOptions.Count switch { 2 => "two", 3 => "three", var n => n.ToString() };
        var intro = Wrap($"Every {c.Class.Name.ToLowerInvariant()} now walks one of {paths} paths, chosen once (a respec keeps it). "
            + "Its ability joins your bar. L or Esc to close.", small, Bounds.Width - 2 * Pad);
        for (var i = 0; i < intro.Count; i++)
            canvas.DrawText(intro[i], Bounds.Left + Pad, Bounds.Top + 62 + i * 15, SKTextAlign.Left, small, dim);

        var options = c.SubclassOptions;
        var gap = 16f;
        var columnWidth = (Bounds.Width - 2 * Pad - gap * (options.Count - 1)) / Math.Max(1, options.Count);
        var top = Bounds.Top + 80 + intro.Count * 15;
        for (var i = 0; i < options.Count; i++)
        {
            var subclass = options[i];
            var x = Bounds.Left + Pad + i * (columnWidth + gap);
            var lines = Wrap(subclass.Description, font, columnWidth - 24);
            var rect = SKRect.Create(x, top, columnWidth, 48 + lines.Count * 19);
            if (_subclass == subclass.Id)
                canvas.DrawRect(rect, picked);
            else if (pointer is { } p && rect.Contains(p))
                canvas.DrawRect(rect, hover);
            canvas.DrawRect(rect, frame);
            canvas.DrawText(subclass.Name, x + 12, top + 28, SKTextAlign.Left, nameFont, _subclass == subclass.Id ? gold : ink);
            for (var l = 0; l < lines.Count; l++)
                canvas.DrawText(lines[l], x + 12, top + 52 + l * 19, SKTextAlign.Left, font, dim);
            var id = subclass.Id;
            _hits.Add((rect, () => _subclass = id, subclass.Name, subclass.Description));
        }

        var bottom = Bounds.Bottom - Pad;
        var ready = _subclass is not null;
        _confirm = SKRect.Create(Bounds.Right - Pad - 140, bottom - 36, 140, 34);
        using (var button = new SKPaint { Color = ready ? new SKColor(80, 60, 30, 240) : new SKColor(34, 26, 22, 240) })
            canvas.DrawRect(_confirm, button);
        canvas.DrawRect(_confirm, frame);
        canvas.DrawText("Confirm", _confirm.MidX, _confirm.MidY + 5, SKTextAlign.Center, font, ready ? gold : dim);
        if (!ready)
            canvas.DrawText("Still to choose: a subclass.", Bounds.Left + Pad, _confirm.MidY + 5, SKTextAlign.Left, small, gold);
    }

    /// <summary>The line at a level with no skill increase: none now, and the level that brings the next.</summary>
    public static string SkillNote(int level) =>
        LevelProgression.NextSkillIncrease(level) is { } next
            ? $"No skill increase at level {level}; the next comes at level {next}."
            : $"No skill increase at level {level}.";

    private static List<string> Wrap(string text, SKFont font, float width)
    {
        var lines = new List<string>();
        var line = "";
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = line.Length == 0 ? word : $"{line} {word}";
            if (font.MeasureText(candidate) > width && line.Length > 0)
            {
                lines.Add(line);
                line = word;
            }
            else
            {
                line = candidate;
            }
        }
        if (line.Length > 0)
            lines.Add(line);
        return lines;
    }

    /// <summary>Where each option was last drawn, by its title (tests click them).</summary>
    public IEnumerable<(SKRect Rect, string Title)> Options => _hits.Select(h => (h.Rect, h.Title));

    public SKRect ConfirmButton => _confirm;

    /// <summary>What the level still needs picked before Confirm works ("ancestry feat", "2 more attribute boosts").</summary>
    public List<string> Missing(Character c, int level)
    {
        var missing = new List<string>();
        var gains = LevelProgression.GainsAt(level);
        foreach (var (gain, type, title) in FeatGains)
        {
            if ((gains & gain) != 0 && !_feats.ContainsKey(type))
                missing.Add(title.ToLowerInvariant());
        }
        if ((gains & LevelGain.SkillIncrease) != 0 && _skill is null)
            missing.Add("a skill increase");
        var boostsLeft = (gains & LevelGain.AttributeBoosts) != 0 && !c.LevelUpBoosts.ContainsKey(level) ? AttributeSet.BoostsPerLevelUp - _boosts.Count : 0;
        if (boostsLeft > 0)
            missing.Add($"{boostsLeft} {(boostsLeft == AttributeSet.BoostsPerLevelUp ? "" : "more ")}attribute boost{(boostsLeft == 1 ? "" : "s")}");
        return missing;
    }

    /// <summary>The level's picks, once everything it grants is chosen; null until then.</summary>
    private LevelChoice? Choice(Character c, int level)
    {
        var gains = LevelProgression.GainsAt(level);
        string? Feat(LevelGain gain, FeatType type) => (gains & gain) != 0 ? _feats.GetValueOrDefault(type) : null;
        bool Missing(LevelGain gain, FeatType type) => (gains & gain) != 0 && !_feats.ContainsKey(type);
        if (FeatGains.Any(g => Missing(g.Gain, g.Type)))
            return null;
        if ((gains & LevelGain.SkillIncrease) != 0 && _skill is null)
            return null;
        var needsBoosts = (gains & LevelGain.AttributeBoosts) != 0 && !c.LevelUpBoosts.ContainsKey(level);
        if (needsBoosts && _boosts.Count != AttributeSet.BoostsPerLevelUp)
            return null;
        return new LevelChoice(
            Boosts: needsBoosts ? [.. _boosts] : null,
            ClassFeat: Feat(LevelGain.ClassFeat, FeatType.Class),
            SkillFeat: Feat(LevelGain.SkillFeat, FeatType.Skill),
            GeneralFeat: Feat(LevelGain.GeneralFeat, FeatType.General),
            AncestryFeat: Feat(LevelGain.AncestryFeat, FeatType.Ancestry),
            SkillIncrease: (gains & LevelGain.SkillIncrease) != 0 ? _skill : null);
    }

    /// <summary>Picks an option or confirms; true when the click landed on the panel.</summary>
    public bool Click(SKPoint p, GameWorld world)
    {
        if (!Bounds.Contains(p))
            return false;
        foreach (var hit in _hits)
        {
            if (hit.Rect.Contains(p))
            {
                hit.Pick();
                return true;
            }
        }
        if (ShowsSubclass)
        {
            if (_confirm.Contains(p) && _subclass is { } id && world.Hero.Character.NeedsSubclass)
            {
                world.ChooseSubclass(id);
                _subclass = null;
            }
            return true;
        }
        if (_confirm.Contains(p) && Choice(world.Hero.Character, _level) is { } choice)
        {
            world.ChooseLevel(_level, choice);
            _level = 0;
        }
        return true;
    }
}
