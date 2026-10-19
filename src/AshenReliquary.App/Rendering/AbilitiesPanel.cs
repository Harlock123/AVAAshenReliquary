using AshenReliquary.Core.Abilities;
using AshenReliquary.Core.World;
using SkiaSharp;

namespace AshenReliquary.App.Rendering;

/// <summary>
/// The Abilities panel (K): every ability the hero has, with the slot it sits in or "not on the bar". Click one (or
/// drag it) and click a slot on the bar to put it there; whatever was there changes places with it, or goes to the
/// list. With the panel open a slot's ability can be picked up and moved, a right click on a slot empties it, and
/// hovering an ability and pressing 1 to 6 puts it on that key.
/// </summary>
internal sealed class AbilitiesPanel(IconStore icons)
{
    public const float Width = 440;
    private const float Pad = 18, RowHeight = 44, Top = 76;

    private readonly List<(SKRect Rect, string Id)> _rows = [];

    public SKRect Bounds { get; private set; }

    /// <summary>The ability picked up to be placed, or null.</summary>
    public string? Held { get; set; }

    /// <summary>The ability under a point, as last drawn.</summary>
    public string? RowAt(SKPoint p) => _rows.FirstOrDefault(r => r.Rect.Contains(p)).Id;

    /// <summary>Where an ability's row is on screen, as last drawn (tests click it).</summary>
    public SKRect RowRect(string id) => _rows.FirstOrDefault(r => r.Id == id).Rect;

    public void Draw(SKCanvas canvas, SKSize size, GameWorld world, SKPoint? pointer, Action<SKRect, Func<IReadOnlyList<TipLine>>, SKColor> addRegion)
    {
        _rows.Clear();
        var abilities = world.KnownAbilities;
        var height = Math.Min(Top + abilities.Count * RowHeight + Pad, Math.Max(240, size.Height - 32 - 150));
        Bounds = SKRect.Create((size.Width - Width) / 2, 16, Width, height);
        using var back = new SKPaint { Color = Palette.Panel };
        using var frame = new SKPaint { Color = Palette.Frame, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
        canvas.DrawRect(Bounds, back);
        canvas.DrawRect(Bounds, frame);
        using var titleFont = new SKFont(WorldOverlay.Serif, 22);
        using var nameFont = new SKFont(WorldOverlay.Serif, 15);
        using var textFont = new SKFont(WorldOverlay.Serif, 12);
        using var gold = new SKPaint { IsAntialias = true, Color = Palette.Gold };
        using var ink = new SKPaint { IsAntialias = true, Color = Palette.Ink };
        using var dim = new SKPaint { IsAntialias = true, Color = Palette.Dim };
        using var hover = new SKPaint { Color = new SKColor(255, 255, 255, 18) };
        using var held = new SKPaint { Color = Palette.Gold.WithAlpha(40) };
        using var heldEdge = new SKPaint { IsAntialias = true, Color = Palette.Gold, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
        using var iconBack = new SKPaint { Color = new SKColor(40, 32, 26) };

        var x = Bounds.Left + Pad;
        canvas.DrawText("Abilities", x, Bounds.Top + 34, SKTextAlign.Left, titleFont, gold);
        canvas.DrawText("K to close", Bounds.Right - Pad, Bounds.Top + 34, SKTextAlign.Right, textFont, dim);
        canvas.DrawText(Held is null ? "Click an ability, then a slot on the bar. Right-click a slot to empty it."
            : "Now click a slot on the bar (or press 1-6). Right-click to put it down.", x, Bounds.Top + 56, SKTextAlign.Left, textFont, dim);

        canvas.Save();
        canvas.ClipRect(Bounds);
        var y = Bounds.Top + Top;
        foreach (var a in abilities)
        {
            var row = SKRect.Create(Bounds.Left + 8, y, Width - 16, RowHeight - 4);
            _rows.Add((row, a.Id));
            if (a.Id == Held)
            {
                canvas.DrawRect(row, held);
                canvas.DrawRect(row, heldEdge);
            }
            else if (pointer is { } p && row.Contains(p))
            {
                canvas.DrawRect(row, hover);
            }
            var icon = SKRect.Create(row.Left + 4, row.Top + 2, RowHeight - 8, RowHeight - 8);
            canvas.DrawRect(icon, iconBack);
            icons.Draw(canvas, a.Id, SKRect.Inflate(icon, -3, -3), Palette.Ink);
            canvas.DrawText(a.Name, icon.Right + 10, row.Top + 17, SKTextAlign.Left, nameFont, ink);
            canvas.DrawText(Facts(world, a), icon.Right + 10, row.Top + 33, SKTextAlign.Left, textFont, dim);
            var slot = world.SlotOf(a.Id);
            canvas.DrawText(slot is null ? "not on the bar" : SlotLabel(slot), row.Right - 8, row.Top + 24, SKTextAlign.Right, textFont,
                slot is null ? dim : gold);
            var ability = a;
            addRegion(row, () => Hud.AbilityTip(world, ability, slot is null ? "" : SlotLabel(slot)), Palette.Gold);
            y += RowHeight;
        }
        canvas.Restore();
    }

    /// <summary>"LMB", "RMB" or "Key 3".</summary>
    public static string SlotLabel(string slot) => slot switch { "lmb" => "LMB", "rmb" => "RMB", var n => $"Key {n}" };

    private static string Facts(GameWorld world, AbilityDefinition a)
    {
        var facts = new List<string>();
        if (a.Charges > 0)
            facts.Add(a.Charges == 1 ? "a Grave Charge" : $"{a.Charges} Grave Charges");
        if (a.Cost > 0)
            facts.Add($"{(a.ChargeInstead ? "a Grave Charge or " : "")}{a.Cost:0} {(a.CostsTaint ? "Taint" : world.Hero.Resource.Name)}{(a.SpendAll ? " or more" : "")}");
        if (a.Cooldown > 0)
            facts.Add($"{a.Cooldown:0} s cooldown");
        if (a.Repeat)
            facts.Add("repeats");
        if (facts.Count == 0)
            facts.Add("free");
        return string.Join(" · ", facts);
    }

    /// <summary>The held ability's icon under the pointer.</summary>
    public void DrawHeld(SKCanvas canvas, SKPoint? pointer)
    {
        if (Held is not { } id || pointer is not { } p)
            return;
        var rect = SKRect.Create(p.X - 20, p.Y - 20, 40, 40);
        using var shadow = new SKPaint { Color = new SKColor(0, 0, 0, 160) };
        canvas.DrawRect(SKRect.Inflate(rect, 2, 2), shadow);
        icons.Draw(canvas, id, rect, Palette.Gold);
    }
}
