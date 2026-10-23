using AshenReliquary.Core.World;
using SkiaSharp;

namespace AshenReliquary.App.Rendering;

/// <summary>
/// The bar's right-click menu: right-click a slot (LMB, RMB, 1 to 6) and every ability the hero has is listed above it,
/// with the slots each is already on. Choosing one binds it to that slot as well (the same ability may sit on several
/// slots, sharing its cooldown); "Empty this slot" clears it. A click anywhere else, or Escape, closes it.
/// </summary>
internal sealed class BindMenu(IconStore icons)
{
    public const float Width = 300;
    private const float Pad = 8, Header = 30, MaxRow = 34, MinRow = 22;

    /// <summary>The id of the "Empty this slot" row.</summary>
    public const string EmptyRow = "";

    private readonly List<(SKRect Rect, string Id)> _rows = [];

    /// <summary>The slot the menu is open for, or null when it is closed.</summary>
    public string? Slot { get; set; }

    public SKRect Bounds { get; private set; }

    /// <summary>The row under a point, as last drawn: an ability id, <see cref="EmptyRow"/>, or null.</summary>
    public string? RowAt(SKPoint p) => _rows.Where(r => r.Rect.Contains(p)).Select(r => r.Id).FirstOrDefault();

    /// <summary>Where a row is on screen, as last drawn (tests click it).</summary>
    public SKRect RowRect(string id) => _rows.FirstOrDefault(r => r.Id == id).Rect;

    /// <summary>Draws the menu above <paramref name="slotRect"/>, the slot it is for, kept on screen.</summary>
    public void Draw(SKCanvas canvas, SKSize size, GameWorld world, SKRect slotRect, SKPoint? pointer,
        Action<SKRect, Func<IReadOnlyList<TipLine>>, SKColor> addRegion)
    {
        _rows.Clear();
        if (Slot is not { } slot)
            return;
        var abilities = world.KnownAbilities;
        var count = abilities.Count + (world.Bar.ContainsKey(slot) ? 1 : 0);
        var room = Math.Max(120, slotRect.Top - 16 - Header - Pad);
        var row = Math.Clamp(room / Math.Max(1, count), MinRow, MaxRow);
        var height = Header + count * row + Pad;
        var left = Math.Clamp(slotRect.MidX - Width / 2, 8, Math.Max(8, size.Width - Width - 8));
        var top = Math.Max(8, slotRect.Top - 8 - height);
        Bounds = SKRect.Create(left, top, Width, height);

        using var back = new SKPaint { Color = Palette.Panel };
        using var frame = new SKPaint { Color = Palette.Frame, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
        using var titleFont = new SKFont(WorldOverlay.Serif, 14);
        using var nameFont = new SKFont(WorldOverlay.Serif, row >= 28 ? 14 : 12);
        using var textFont = new SKFont(WorldOverlay.Serif, 11);
        using var gold = new SKPaint { IsAntialias = true, Color = Palette.Gold };
        using var ink = new SKPaint { IsAntialias = true, Color = Palette.Ink };
        using var dim = new SKPaint { IsAntialias = true, Color = Palette.Dim };
        using var hover = new SKPaint { Color = new SKColor(255, 255, 255, 22) };
        using var current = new SKPaint { Color = Palette.Gold.WithAlpha(36) };
        using var iconBack = new SKPaint { Color = new SKColor(40, 32, 26) };
        canvas.DrawRect(Bounds, back);
        canvas.DrawRect(Bounds, frame);
        canvas.DrawText($"{AbilitiesPanel.SlotLabel(slot)}: choose what it does", Bounds.Left + 10, Bounds.Top + 20, SKTextAlign.Left, titleFont, gold);

        var bound = world.Bar.GetValueOrDefault(slot)?.Id;
        var y = Bounds.Top + Header;
        foreach (var a in abilities)
        {
            var rect = SKRect.Create(Bounds.Left + 4, y, Width - 8, row - 2);
            _rows.Add((rect, a.Id));
            if (a.Id == bound)
                canvas.DrawRect(rect, current);
            else if (pointer is { } p && rect.Contains(p))
                canvas.DrawRect(rect, hover);
            var icon = SKRect.Create(rect.Left + 3, rect.Top + 1, rect.Height - 2, rect.Height - 2);
            canvas.DrawRect(icon, iconBack);
            icons.Draw(canvas, a.Id, SKRect.Inflate(icon, -2, -2), Palette.Ink);
            canvas.DrawText(a.Name, icon.Right + 8, rect.MidY + nameFont.Size / 3, SKTextAlign.Left, nameFont, a.Id == bound ? gold : ink);
            var on = AbilitiesPanel.SlotsLabel(world, a.Id);
            if (on is not null)
                canvas.DrawText(on, rect.Right - 6, rect.MidY + 4, SKTextAlign.Right, textFont, a.Id == bound ? gold : dim);
            var ability = a;
            addRegion(rect, () => Hud.AbilityTip(world, ability, on ?? ""), Palette.Gold);
            y += row;
        }
        if (bound is not null)
        {
            var rect = SKRect.Create(Bounds.Left + 4, y, Width - 8, row - 2);
            _rows.Add((rect, EmptyRow));
            if (pointer is { } p && rect.Contains(p))
                canvas.DrawRect(rect, hover);
            canvas.DrawText("Empty this slot", rect.Left + rect.Height + 8, rect.MidY + nameFont.Size / 3, SKTextAlign.Left, nameFont, dim);
        }
    }

    /// <summary>A click on a row: binds that ability (or empties the slot). True when a row was chosen.</summary>
    public bool Choose(SKPoint at, GameWorld world)
    {
        if (Slot is not { } slot || RowAt(at) is not { } id)
            return false;
        if (id == EmptyRow)
            world.ClearSlot(slot);
        else
            world.BindAbility(id, slot);
        return true;
    }
}
