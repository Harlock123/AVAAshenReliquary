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
    /// <summary>The mouse wheel's zoom steps, furthest out first; <see cref="DefaultZoomLevel"/> is where a game starts.</summary>
    public static readonly double[] ZoomSteps = [0.36, 0.45, 0.55, 0.67, 0.82];
    public const int DefaultZoomLevel = 2;

    /// <summary>How long the view takes to glide from one zoom step to the next.</summary>
    public const double ZoomSeconds = 0.18;

    private readonly FlareAssets _assets = FlareAssets.Shared;
    private readonly SpriteCache _sprites = new(FlareAssets.Shared, ActGrade());
    private readonly SpriteCache _uiSprites = new(FlareAssets.Shared);
    private readonly List<(SKRect Rect, GroundItem Item)> _lootLabels = [];
    private InventoryPanel? _inventory;
    private readonly IconStore _icons = new();
    private readonly Minimap _minimap = new();
    private readonly CharacterSheet _sheet = new();
    private Hud? _hud;

    private Hud HudLayer => _hud ??= new Hud(_icons, DrawItemIcon);

    /// <summary>The character sheet (C).</summary>
    public bool CharacterSheetOpen { get; set; }

    /// <summary>The journal (J).</summary>
    public bool JournalOpen { get; set; }

    /// <summary>The level-up screen (L).</summary>
    public bool LevelUpOpen { get; set; }

    /// <summary>The Abilities panel (K): what goes on the bar.</summary>
    public bool AbilitiesOpen
    {
        get => _abilitiesOpen;
        set
        {
            _abilitiesOpen = value;
            AbilitiesView.Held = null;
        }
    }

    private bool _abilitiesOpen;
    private AbilitiesPanel? _abilitiesPanel;
    private AbilitiesPanel AbilitiesView => _abilitiesPanel ??= new AbilitiesPanel(_icons);

    /// <summary>The Abilities panel, as last drawn (tests read its rows).</summary>
    internal AbilitiesPanel Abilities => AbilitiesView;

    /// <summary>Where a bar slot is on screen, as last drawn (tests click it).</summary>
    public Point BarSlot(string slot) => HudLayer.SlotRect(slot) is var r ? new Point(r.MidX, r.MidY) : default;

    /// <summary>Where an ability's row in the Abilities panel is, as last drawn (tests click it).</summary>
    public Point AbilityRow(string id) => AbilitiesView.RowRect(id) is var r ? new Point(r.MidX, r.MidY) : default;

    /// <summary>
    /// With the Abilities panel open, a number key puts the held ability (or the one under the pointer) on that slot.
    /// False when there is nothing to place, so the key uses the slot as usual.
    /// </summary>
    public bool PlaceOnKey(string slot)
    {
        if (!AbilitiesOpen || World is not { } world)
            return false;
        var id = AbilitiesView.Held ?? (_pointer is { } p ? AbilitiesView.RowAt(new SKPoint((float)p.X, (float)p.Y)) : null);
        if (id is null)
            return false;
        world.PlaceAbility(id, slot);
        AbilitiesView.Held = null;
        return true;
    }

    /// <summary>
    /// A click with the Abilities panel open: on an ability, pick it up; on a bar slot, put the held one there (or pick
    /// that slot's up to move it); a right click on a slot empties it, anywhere else puts the held one down. True when
    /// the click was the panel's.
    /// </summary>
    private bool AbilitiesClick(SKPoint at, bool right, GameWorld world)
    {
        var panel = AbilitiesView;
        var slot = HudLayer.SlotAt(at);
        if (right)
        {
            if (slot is not null)
                world.ClearSlot(slot);
            var had = panel.Held is not null;
            panel.Held = null;
            return slot is not null || had || panel.Bounds.Contains(at);
        }
        if (panel.RowAt(at) is { } id)
        {
            panel.Held = panel.Held == id ? null : id;
            _abilityPressedAt = at;
            return true;
        }
        if (slot is not null)
        {
            if (panel.Held is { } held)
            {
                world.PlaceAbility(held, slot);
                panel.Held = null;
            }
            else if (world.Bar.TryGetValue(slot, out var there))
            {
                panel.Held = there.Id;
                _abilityPressedAt = at;
            }
            return true;
        }
        if (panel.Held is not null)
        {
            panel.Held = null;
            return true;
        }
        return panel.Bounds.Contains(at);
    }

    private SKPoint? _abilityPressedAt;

    /// <summary>While the smith's Runework tab is open, right-clicked gear goes on his bench.</summary>
    private Func<Item, bool>? BenchFor(GameWorld world) =>
        world.OpenVendor is { Definition.Runework: true } && Trade.Runework ? item => Trade.PutOnBench(item, world) : null;

    /// <summary>Opens the level-up screen when a level (or the subclass) is waiting, closing any conversation that would cover it.</summary>
    public void OpenLevelUp()
    {
        if (World is not { } world || (world.Hero.Character.PendingLevels.Count == 0 && !world.Hero.Character.NeedsSubclass))
            return;
        world.EndDialogue();
        LevelUpOpen = true;
    }

    /// <summary>Picks a dialogue answer; one that hands back levels (the Lantern altar's respec) opens the level-up screen.</summary>
    public void Answer(int index)
    {
        if (World is not { } world)
            return;
        var pending = world.Hero.Character.PendingLevels.Count;
        world.Choose(index);
        if (world.Hero.Character.PendingLevels.Count > pending)
            OpenLevelUp();
    }

    private readonly LevelUpPanel _levelUp = new();

    /// <summary>The level-up screen (L), as last drawn.</summary>
    internal LevelUpPanel LevelUp => _levelUp;

    /// <summary>The Step dodge (Shift): toward the pointer, or straight ahead without one.</summary>
    public void DodgeTowardPointer()
    {
        if (World is not { } world)
            return;
        var (x, y) = _pointer is { } p ? ToMap(p) : (world.Hero.X + 1, world.Hero.Y + 1);
        world.Dodge(x, y);
    }

    private readonly DialoguePanel _dialogue = new();
    private StashPanel? _stashPanel;
    private StashPanel StashView => _stashPanel ??= new StashPanel(DrawItemIcon);
    private readonly JournalPanel _journal = new();
    private readonly Dictionary<string, SKImage> _portraits = [];

    /// <summary>A portrait from Assets/Portraits as a Skia image, decoded once.</summary>
    private SKImage? PortraitImage(string? id)
    {
        if (id is null || Services.Portraits.Find(id) is not { } p)
            return null;
        if (!_portraits.TryGetValue(id, out var image))
            _portraits[id] = image = SKImage.FromEncodedData(Services.Portraits.Png(p)).ToRasterImage(ensurePixelData: true);
        return image;
    }
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

    private InventoryPanel Inventory => _inventory ??= new InventoryPanel(DrawItemIcon);

    /// <summary>Where the inventory's buttons and worn slots and the stash's cells are on screen (tests click them).</summary>
    public Point SortButtonAt => Inventory.SortCentre is var c ? new Point(c.X, c.Y) : default;
    public Point JunkButtonAt => Inventory.JunkCentre is var c ? new Point(c.X, c.Y) : default;
    public Point WornSlot(Core.Items.EquipSlot slot) => Inventory.SlotCentre(slot) is var c ? new Point(c.X, c.Y) : default;
    public Point StashCell(int index) => StashView.CellCentre(index) is var c ? new Point(c.X, c.Y) : default;

    /// <summary>Where bag cell <paramref name="index"/> is on screen while the inventory is open (tests click it).</summary>
    public Point BagCell(int index) => Inventory.BagCellCentre(index) is var c ? new Point(c.X, c.Y) : default;

    private readonly RiftPanel _rift = new();
    private VendorPanel? _vendorPanel;
    private VendorPanel Trade => _vendorPanel ??= new VendorPanel(DrawItemIcon);

    /// <summary>Where a trader's supply <paramref name="index"/> is on screen while the table is open (tests click it).</summary>
    public Point SupplyCell(int index) => Trade.SupplyCentre(index) is var c ? new Point(c.X, c.Y) : default;

    /// <summary>The trader whose table was open last frame: opening one opens the bag, leaving closes it again.</summary>
    private Vendor? _trading;
    private bool _tradeOpenedInventory;
    private bool _stashing;

    /// <summary>Opens the bag beside a trader's table when trading starts, and closes it again when it ends.</summary>
    private void FollowTrade(GameWorld world)
    {
        // Closing the bag (I) mid-trade leaves the trader (or the stash) too.
        if (world.OpenVendor is not null && world.OpenVendor == _trading && !InventoryOpen)
        {
            _tradeOpenedInventory = false;
            world.CloseVendor();
        }
        if (world.StashOpen && _stashing && !InventoryOpen)
        {
            _tradeOpenedInventory = false;
            world.CloseStash();
        }
        if (world.StashOpen != _stashing)
        {
            _stashing = world.StashOpen;
            if (_stashing && !InventoryOpen)
            {
                _tradeOpenedInventory = true;
                InventoryOpen = true;
            }
            else if (!_stashing && _tradeOpenedInventory && world.OpenVendor is null)
            {
                InventoryOpen = false;
                _tradeOpenedInventory = false;
            }
        }
        if (world.OpenVendor == _trading)
            return;
        if (world.OpenVendor is not null && _trading is null)
        {
            _tradeOpenedInventory = !InventoryOpen;
            InventoryOpen = true;
        }
        else if (world.OpenVendor is null && _tradeOpenedInventory)
        {
            InventoryOpen = false;
            _tradeOpenedInventory = false;
        }
        _trading = world.OpenVendor;
    }

    /// <summary>
    /// An item's icon: Flare's painted icon, or for a thing the sheet lacks (the pilgrim's lantern) a game-icons glyph,
    /// warm on a dark tile.
    /// </summary>
    private void DrawItemIcon(SKCanvas canvas, Core.Items.ItemDefinition item, SKRect rect)
    {
        if (item.Glyph is not { } glyph)
        {
            DrawIcon(canvas, item.Icon, rect);
            return;
        }
        using var back = new SKPaint { Shader = SKShader.CreateRadialGradient(new SKPoint(rect.MidX, rect.MidY), rect.Width * 0.7f,
            [new SKColor(70, 50, 28), new SKColor(16, 12, 10)], SKShaderTileMode.Clamp) };
        canvas.DrawRect(rect, back);
        _icons.Draw(canvas, glyph, SKRect.Inflate(rect, -rect.Width * 0.12f, -rect.Height * 0.12f), new SKColor(255, 200, 110));
    }

    /// <summary>Draws icon <paramref name="icon"/> from Flare's icon sheet into <paramref name="rect"/> (UI canvas).</summary>
    private void DrawIcon(SKCanvas canvas, int icon, SKRect rect)
    {
        var x = icon % FlareAssets.IconsPerRow * FlareAssets.IconSize;
        var y = icon / FlareAssets.IconsPerRow * FlareAssets.IconSize;
        var image = _uiSprites.Get(FlareAssets.IconSheet, x, y, FlareAssets.IconSize, FlareAssets.IconSize, rect.Width / FlareAssets.IconSize * _uiScale);
        canvas.DrawImage(image, rect, new SKSamplingOptions(SKFilterMode.Linear));
    }
    private SKImage? _vignette;
    private bool _worldOnGpu;
    private SKSurface? _darknessSurface;
    private float _deviceScale = 1f;
    private int _zoomIndex = DefaultZoomLevel;
    private double _zoom = ZoomSteps[DefaultZoomLevel], _zoomFrom = ZoomSteps[DefaultZoomLevel], _zoomStarted = double.NegativeInfinity;
    private double _wheel;
    private readonly System.Diagnostics.Stopwatch _zoomClock = System.Diagnostics.Stopwatch.StartNew();
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

    /// <summary>The zoom this frame is drawn at: on a step, or gliding toward <see cref="ZoomTarget"/>.</summary>
    public double Zoom => _zoom;

    /// <summary>The zoom step being moved to (or rested on).</summary>
    public double ZoomTarget => ZoomSteps[_zoomIndex];

    /// <summary>Which of <see cref="ZoomSteps"/> the view is on; setting it glides there (the settings restore it at once).</summary>
    public int ZoomLevel
    {
        get => _zoomIndex;
        set => SetZoomLevel(value, glide: false);
    }

    /// <summary>Raised when the wheel changes the zoom step (the window saves it in the settings).</summary>
    public event Action? ZoomChanged;

    private void SetZoomLevel(int level, bool glide)
    {
        level = Math.Clamp(level, 0, ZoomSteps.Length - 1);
        if (level == _zoomIndex)
            return;
        _zoomFrom = _zoom;
        _zoomStarted = glide && FixedTime is null ? _zoomClock.Elapsed.TotalSeconds : double.NegativeInfinity;
        _zoomIndex = level;
        UpdateZoom();
        InvalidateVisual();
    }

    /// <summary>Moves the glide on: eased from where it started toward the step, a frame at a time.</summary>
    private void UpdateZoom()
    {
        var t = Math.Clamp((_zoomClock.Elapsed.TotalSeconds - _zoomStarted) / ZoomSeconds, 0, 1);
        var eased = 1 - (1 - t) * (1 - t);
        _zoom = t >= 1 ? ZoomTarget : _zoomFrom + (ZoomTarget - _zoomFrom) * eased;
    }

    /// <summary>Whether the view is between two zoom steps.</summary>
    public bool Zooming => _zoom != ZoomTarget;

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

    /// <summary>
    /// What the render thread and the UI thread both hold while they touch the world: Avalonia draws this view on its
    /// own thread, while the game ticks, takes input and saves on the UI thread. Without it a frame could read the world
    /// mid-change, and the bag's verdicts, which try items on to compare them, could clash with a click on the same gear.
    /// </summary>
    public object Gate { get; } = new();

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        lock (Gate)
            PointerPressed(e);
    }

    private void PointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        if (World is not { } world)
            return;
        var p = e.GetPosition(this);
        var props = e.GetCurrentPoint(this).Properties;
        var sp = new SKPoint((float)p.X, (float)p.Y);
        if (LevelUpOpen && props.IsLeftButtonPressed && _levelUp.Click(sp, world))
        {
            e.Handled = true;
            InvalidateVisual();
            return;
        }
        if (AbilitiesOpen && (props.IsLeftButtonPressed || props.IsRightButtonPressed) && AbilitiesClick(sp, props.IsRightButtonPressed, world))
        {
            e.Handled = true;
            InvalidateVisual();
            return;
        }
        if (props.IsLeftButtonPressed && HudLayer.LevelUpBadge.Contains(sp))
        {
            OpenLevelUp();
            e.Handled = true;
            return;
        }
        if (world.Dialogue is not null && props.IsLeftButtonPressed && _dialogue.HitTest(sp) is { } answer)
        {
            if (answer >= 0)
                Answer(answer);
            e.Handled = true;
            InvalidateVisual();
            return;
        }
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        Inventory.Bench = BenchFor(world);
        if (props.IsLeftButtonPressed && !ctrl && !shift && DragSource(sp, world) is { } drag)
        {
            // Pressed on an item: a click (on release, where it stays) or a drag (once it moves).
            _drag = drag;
            e.Handled = true;
            return;
        }
        if (world.StashOpen && props.IsLeftButtonPressed && StashView.Click(sp, world))
        {
            e.Handled = true;
            InvalidateVisual();
            return;
        }
        if (world.OpenShrine is not null && props.IsLeftButtonPressed && _rift.Click(sp, world))
        {
            e.Handled = true;
            InvalidateVisual();
            return;
        }
        if (world.OpenVendor is not null && props.IsLeftButtonPressed && Trade.Click(sp, world))
        {
            e.Handled = true;
            InvalidateVisual();
            return;
        }
        if (InventoryOpen && Inventory.Click(sp, props.IsRightButtonPressed, world, ctrl, shift))
        {
            e.Handled = true;
            InvalidateVisual();
            return;
        }
        // Clicks on the HUD, the minimap or the character sheet never walk the hero there.
        if (HudLayer.Contains(sp))
        {
            e.Handled = true;
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
        else if (VendorUnder(p) is { } vendor)
        {
            world.TalkTo(vendor);
            _holding = false;
        }
        else if (InteractiveUnder(p) is { } who)
        {
            world.Interact(who);
            _holding = false;
        }
        else if (CacheUnder(p) is { } cache)
        {
            world.OpenCache(cache);
            _holding = false;
        }
        else if (ShrineUnder(p) is { } shrine)
        {
            world.UseShrine(shrine);
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
        lock (Gate)
            PointerMoved(e);
    }

    private void PointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        _pointer = e.GetPosition(this);
        if (_drag is { Moving: false } pending && Math.Abs(_pointer.Value.X - pending.Start.X) + Math.Abs(_pointer.Value.Y - pending.Start.Y) > DragThreshold)
            pending.Moving = true;
        if (_holding && World is { } world && world.Time - _lastHoldMove > 0.12)
        {
            var (x, y) = ToMap(_pointer.Value);
            world.MoveTo(x, y);
            _lastHoldMove = world.Time;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        lock (Gate)
            PointerReleased(e);
    }

    private void PointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _holding = false;
        // An ability dragged from the panel (or from a slot) and let go over a slot goes there.
        if (AbilitiesOpen && _abilityPressedAt is { } pressed && AbilitiesView.Held is { } dragged && World is { } w)
        {
            var up = e.GetPosition(this);
            var release = new SKPoint((float)up.X, (float)up.Y);
            _abilityPressedAt = null;
            if (Math.Abs(release.X - pressed.X) + Math.Abs(release.Y - pressed.Y) > DragThreshold && HudLayer.SlotAt(release) is { } slot)
            {
                w.PlaceAbility(dragged, slot);
                AbilitiesView.Held = null;
            }
        }
        if (_drag is not { } drag || World is not { } world)
            return;
        _drag = null;
        var p = e.GetPosition(this);
        var at = new SKPoint((float)p.X, (float)p.Y);
        if (!drag.Moving)
        {
            // It stayed put: the click it always was.
            var start = new SKPoint((float)drag.Start.X, (float)drag.Start.Y);
            if (drag.From == DragFrom.Stash)
                StashView.Click(start, world);
            else
                Inventory.Click(start, false, world);
        }
        else
        {
            DropDragged(drag, at, world);
        }
        e.Handled = true;
        InvalidateVisual();
    }

    // ----- Dragging items ---------------------------------------------------------------------

    /// <summary>How far the pointer moves (logical pixels) before a press on an item becomes a drag.</summary>
    public const double DragThreshold = 6;

    private enum DragFrom { Bag, Worn, Stash }

    private sealed class DragState(DragFrom from, int index, Core.Items.EquipSlot slot, Core.Items.Item item, Point start)
    {
        public DragFrom From { get; } = from;
        public int Index { get; } = index;
        public Core.Items.EquipSlot Slot { get; } = slot;
        public Core.Items.Item Item { get; } = item;
        public Point Start { get; } = start;
        public bool Moving { get; set; }
    }

    private DragState? _drag;

    /// <summary>True while an item is being dragged (tests and the tooltip).</summary>
    public bool Dragging => _drag is { Moving: true };

    /// <summary>The item under a press that can be picked up: in the open stash, the bag, or a worn slot.</summary>
    private DragState? DragSource(SKPoint p, GameWorld world)
    {
        var start = new Point(p.X, p.Y);
        if (world.StashOpen && StashView.Bounds.Contains(p) && StashView.CellAt(p) is { } cell && world.Stash.Slots[cell] is { } stashed)
            return new DragState(DragFrom.Stash, cell, default, stashed, start);
        if (!InventoryOpen || !Inventory.Bounds.Contains(p))
            return null;
        var gear = world.Hero.Equipment;
        return Inventory.HitTest(p) switch
        {
            (null, { } bag) when gear.Bag[bag] is { } item => new DragState(DragFrom.Bag, bag, default, item, start),
            ({ } slot, null) when gear.In(slot) is { } worn => new DragState(DragFrom.Worn, -1, slot, worn, start),
            _ => null,
        };
    }

    /// <summary>
    /// Where a dragged item lands: a bag cell (moved, stacked or swapped; taken off; taken from the stash), a worn slot it
    /// fits (worn), a stash slot (stashed), the trader's table (sold), or the world outside every panel (dropped).
    /// </summary>
    private void DropDragged(DragState drag, SKPoint at, GameWorld world)
    {
        var gear = world.Hero.Equipment;
        if (InventoryOpen && Inventory.Bounds.Contains(at))
        {
            switch (Inventory.HitTest(at), drag.From)
            {
                case ((null, { } bag), DragFrom.Bag):
                    gear.Move(drag.Index, bag);
                    break;
                case ((null, { } bag), DragFrom.Worn):
                    if (!gear.UnequipTo(drag.Slot, bag))
                        gear.Unequip(drag.Slot);
                    break;
                case ((null, { } bag), DragFrom.Stash):
                    if (!world.RetrieveAt(drag.Index, bag))
                        world.Retrieve(drag.Index);
                    break;
                case (({ } slot, null), DragFrom.Bag):
                    if (!gear.EquipFromBag(drag.Index, slot))
                        world.Announce($"{drag.Item.Name} doesn't go there.");
                    break;
            }
            return;
        }
        if (world.StashOpen && StashView.Bounds.Contains(at))
        {
            if (StashView.CellAt(at) is { } cell)
            {
                if (drag.From == DragFrom.Bag && !world.StoreAt(drag.Index, cell))
                    world.Store(drag.Index);
                else if (drag.From == DragFrom.Stash)
                    world.Stash.Move(drag.Index, cell);
            }
            else if (drag.From == DragFrom.Bag)
            {
                world.Store(drag.Index);
            }
            return;
        }
        if (world.OpenVendor is not null && Trade.Bounds.Contains(at))
        {
            if (drag.From == DragFrom.Bag)
                world.Sell(drag.Index);
            return;
        }
        if (HudLayer.Contains(at) || (LevelUpOpen && _levelUp.Bounds.Contains(at)) || (JournalOpen && _journal.Bounds.Contains(at)))
            return;
        // Out of every panel, onto the ground.
        if (drag.From == DragFrom.Bag)
            world.DropFromBag(drag.Index);
        else if (drag.From == DragFrom.Worn)
            world.DropWorn(drag.Slot);
    }

    /// <summary>The dragged item, under the pointer.</summary>
    private void DrawDragged(SKCanvas canvas)
    {
        if (_drag is not { Moving: true } drag || _pointer is not { } p)
            return;
        var rect = SKRect.Create((float)p.X - 22, (float)p.Y - 22, 44, 44);
        using var shadow = new SKPaint { Color = new SKColor(0, 0, 0, 140) };
        canvas.DrawRect(SKRect.Inflate(rect, 2, 2), shadow);
        DrawItemIcon(canvas, drag.Item.Definition, rect);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        e.Handled = true;
        // A mouse wheel moves a step a notch; a touchpad sends small amounts, which add up to a step.
        if (Math.Sign(e.Delta.Y) != Math.Sign(_wheel))
            _wheel = 0;
        _wheel += e.Delta.Y;
        if (Math.Abs(_wheel) < 1)
            return;
        var steps = (int)_wheel;
        _wheel -= steps;
        var before = _zoomIndex;
        lock (Gate)
            SetZoomLevel(_zoomIndex + steps, glide: true);
        if (_zoomIndex != before)
            ZoomChanged?.Invoke();
    }

    /// <summary>Uses a bar slot on the monster under <paramref name="at"/> (or under the mouse pointer).</summary>
    public void UseSlot(string slot, Point? at = null)
    {
        if (World is not { } world)
            return;
        var point = at ?? _pointer;
        var target = point is { } p ? MonsterUnder(p) ?? NearestTo(p) : null;
        world.UseSlot(slot, target, point is { } aim ? ToMap(aim) : null);
    }

    /// <summary>Throws or drinks belt slot <paramref name="slot"/> (keys 7 to 0), a bomb at the monster under the pointer.</summary>
    public void UseBelt(int slot)
    {
        if (World is not { } world)
            return;
        var target = _pointer is { } p ? MonsterUnder(p) ?? NearestTo(p) : null;
        world.UseBelt(slot, target);
    }

    /// <summary>Forgiving targeting for keys: the closest living monster within 2 tiles of the pointer.</summary>
    private MonsterActor? NearestTo(Point p)
    {
        var (x, y) = ToMap(p);
        return World?.MonsterAt(x, y, 2.0) is { } m && World.IsLit(m.X, m.Y) ? m : null;
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
            if (m.IsDead || !world.IsLit(m.X, m.Y))
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

    /// <summary>The trader whose sprite is under a screen point.</summary>
    public Vendor? VendorUnder(Point p)
    {
        if (World is not { } world)
            return null;
        var z = (float)Zoom;
        foreach (var v in world.Vendors)
        {
            var foot = ToScreen(v.X, v.Y);
            if (p.X >= foot.X - 38 * z && p.X <= foot.X + 38 * z && p.Y >= foot.Y - 150 * z && p.Y <= foot.Y + 12 * z)
                return v;
        }
        return null;
    }

    /// <summary>The person or thing under a screen point.</summary>
    public Interactive? InteractiveUnder(Point p)
    {
        if (World is not { } world)
            return null;
        var z = (float)Zoom;
        foreach (var who in world.Interactives)
        {
            var foot = ToScreen(who.X, who.Y);
            var height = who.Npc is null ? 100 : 150;
            if (p.X >= foot.X - 40 * z && p.X <= foot.X + 40 * z && p.Y >= foot.Y - height * z && p.Y <= foot.Y + 20 * z)
                return who;
        }
        return null;
    }

    /// <summary>The shrine under a screen point: anywhere on its flat stone circle as drawn.</summary>
    public ShrineDefinition? ShrineUnder(Point p)
    {
        if (World is not { } world)
            return null;
        foreach (var s in world.Zone.Shrines)
        {
            var area = ShrineArea(world, s);
            var (dx, dy) = ((p.X - area.MidX) / (area.Width / 2), (p.Y - area.MidY) / (area.Height / 2));
            if (dx * dx + dy * dy <= 1)
                return s;
        }
        return null;
    }

    /// <summary>
    /// Where a shrine's stone circle is on screen: the rectangle its tile is drawn in. Flare's shrine art is not centred
    /// on its cell (the grassland circle's frame is drawn well east of its cell's middle), so a hit test round the cell missed most of the
    /// glowing ring. A shrine with no tile of its own falls back to a circle round its cell.
    /// </summary>
    public SKRect ShrineArea(GameWorld world, ShrineDefinition shrine)
    {
        var z = (float)Zoom;
        var (x, y) = (shrine.At[0], shrine.At[1]);
        var anchor = ToScreen(x + 0.5, y + 0.5);
        var tiles = _assets.Tileset(world.Zone.Tileset).Tiles;
        if (world.Map.InBounds(x, y))
        {
            foreach (var id in world.Map.Objects(x, y))
            {
                // The tile's frame has a transparent margin round the stone: keep to the inner 80%.
                if (tiles.TryGetValue(id, out var tile))
                    return SKRect.Inflate(SKRect.Create(anchor.X - tile.OffsetX * z, anchor.Y - tile.OffsetY * z, tile.Width * z, tile.Height * z),
                        -tile.Width * z * 0.1f, -tile.Height * z * 0.1f);
            }
        }
        return SKRect.Create(anchor.X - 80 * z, anchor.Y - 40 * z, 160 * z, 80 * z);
    }

    /// <summary>The chest under a screen point.</summary>
    public Cache? CacheUnder(Point p)
    {
        if (World is not { } world)
            return null;
        var z = (float)Zoom;
        foreach (var c in world.Caches)
        {
            var foot = ToScreen(c.X, c.Y);
            if (p.X >= foot.X - 44 * z && p.X <= foot.X + 44 * z && p.Y >= foot.Y - 80 * z && p.Y <= foot.Y + 30 * z)
                return c;
        }
        return null;
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
                owner._gpuContext = api.GrContext;
                // Avalonia draws this on its render thread while the game runs on the UI thread: hold the world still.
                lock (owner.Gate)
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
    private GRContext? _gpuContext;

    /// <summary>The GPU's texture cache: what it holds against its budget (F3), or null on the CPU renderer.</summary>
    private string? GpuCacheLine()
    {
        if (_gpuContext is not { } gr)
            return null;
        gr.GetResourceCacheUsage(out var count, out var bytes);
        return $"textures {count}, {bytes / 1048576.0:0} of {gr.GetResourceCacheLimit() / 1048576.0:0} MB";
    }

    private void Draw(SKCanvas canvas, SKSize size, bool gpu)
    {
        canvas.Clear(new SKColor(8, 7, 10));
        if (World is not { } world)
            return;
        Stats.Begin();
        if (Zooming)
            UpdateZoom(); // the window's tick asks for a frame every tick, so the glide carries on by itself
        var deviceScale = Math.Max(1f, canvas.TotalMatrix.ScaleX);
        var scale = WorldRenderScale ?? (gpu ? deviceScale : 1f);
        LastWorldScale = scale;

        if (Math.Abs(scale - deviceScale) < 0.01f)
        {
            _worldOnGpu = gpu;
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
            _worldOnGpu = false;
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
        DrawVendorLabels(canvas, world);
        DrawCacheLabels(canvas, world);
        DrawInteractiveLabels(canvas, world);
        WorldOverlay.DrawFloatingText(this, canvas, world);
        var pointer = _pointer is { } pt ? new SKPoint((float)pt.X, (float)pt.Y) : (SKPoint?)null;
        var hud = HudLayer;
        hud.Draw(canvas, size, world, MonsterUnderPointer(), pointer);
        if (!InventoryOpen)
        {
            _minimap.Draw(canvas, size, world, hud.AddRegion);
            hud.Block(_minimap.Bounds);
        }
        if (CharacterSheetOpen)
        {
            _sheet.Draw(canvas, size, world, hud.AddRegion);
            hud.Block(_sheet.Bounds);
        }
        FollowTrade(world);
        Inventory.Bench = BenchFor(world);
        Inventory.Benched = world.OpenVendor is not null ? Trade.Benched : null;
        if (InventoryOpen)
        {
            Inventory.Draw(canvas, size, world, pointer, PortraitImage());
            hud.Block(Inventory.Bounds);
        }
        if (world.OpenVendor is not null)
        {
            Trade.Draw(canvas, size, world, pointer);
            hud.Block(Trade.Bounds);
        }
        if (world.StashOpen)
        {
            StashView.Draw(canvas, size, world, pointer);
            hud.Block(StashView.Bounds);
        }
        if (world.OpenShrine is not null)
        {
            _rift.Draw(canvas, size, world, pointer);
            hud.Block(_rift.Bounds);
        }
        if (JournalOpen)
        {
            _journal.Draw(canvas, size, world);
            hud.Block(_journal.Bounds);
        }
        hud.Placing = AbilitiesOpen ? AbilitiesView.Held : null;
        if (AbilitiesOpen)
        {
            AbilitiesView.Draw(canvas, size, world, pointer, hud.AddRegion);
            hud.Block(AbilitiesView.Bounds);
        }
        if (LevelUpOpen && world.Hero.Character.PendingLevels.Count == 0 && !world.Hero.Character.NeedsSubclass)
            LevelUpOpen = false;
        if (LevelUpOpen)
        {
            _levelUp.Draw(canvas, size, world, pointer);
            hud.Block(_levelUp.Bounds);
        }
        if (world.Dialogue is { } talk)
        {
            _dialogue.Draw(canvas, size, talk, PortraitImage(talk.Portrait), pointer);
            hud.Block(_dialogue.Bounds);
        }
        var overPanel = pointer is { } over && ((InventoryOpen && Inventory.Bounds.Contains(over)) || (world.OpenVendor is not null && Trade.Bounds.Contains(over))
            || (world.OpenShrine is not null && _rift.Bounds.Contains(over)) || (world.Dialogue is not null && _dialogue.Bounds.Contains(over))
            || (JournalOpen && _journal.Bounds.Contains(over)) || (world.StashOpen && StashView.Bounds.Contains(over))
            || (LevelUpOpen && _levelUp.Bounds.Contains(over)) || (AbilitiesOpen && AbilitiesView.Bounds.Contains(over)));
        if (!overPanel && !Dragging && !(AbilitiesOpen && AbilitiesView.Held is not null))
            hud.DrawTooltip(canvas, size, pointer);
        DrawDragged(canvas);
        if (AbilitiesOpen)
            AbilitiesView.DrawHeld(canvas, pointer);
        Stats.Mark("text+hud");

        var now = System.Diagnostics.Stopwatch.GetTimestamp();
        if (_lastPresent != 0)
            _frameIntervals.Enqueue(System.Diagnostics.Stopwatch.GetElapsedTime(_lastPresent, now).TotalMilliseconds);
        _lastPresent = now;
        while (_frameIntervals.Count > 60)
            _frameIntervals.Dequeue();
        if (Stats.Frames >= 60)
        {
            _statsLine = $"{(gpu ? "GPU" : "CPU")} renderer, world at {scale:0.##}x of {deviceScale:0.##}x  ·  draw {Stats}"
                + (GpuCacheLine() is { } cache ? $"  ·  {cache}" : "");
            Stats.Reset();
        }
        if (ShowStats)
        {
            var fps = _frameIntervals.Count > 0 ? 1000 / _frameIntervals.Average() : 0;
            using var font = new SKFont(WorldOverlay.Serif, 13);
            using var ink = new SKPaint { IsAntialias = true, Color = new SKColor(255, 230, 140) };
            using var back = new SKPaint { Color = new SKColor(0, 0, 0, 170) };
            // Under the minimap, so it never covers it.
            var top = InventoryOpen ? 8f : 14 + Minimap.Height + 8;
            var lines = StatsLines($"{fps:0} fps", _statsLine, font, size.Width - 28);
            var width = lines.Max(line => font.MeasureText(line)) + 20;
            canvas.DrawRect(SKRect.Create(size.Width - 8 - width, top, width, 8 + 18 * lines.Count), back);
            for (var i = 0; i < lines.Count; i++)
                canvas.DrawText(lines[i], size.Width - 14, top + 18 * (i + 1), SKTextAlign.Right, font, ink);
        }
    }

    /// <summary>F3: frame rate and where the frame time goes.</summary>
    public bool ShowStats { get; set; }

    private readonly Queue<double> _frameIntervals = new();
    private long _lastPresent;
    private string _statsLine = "measuring...";

    /// <summary>The F3 overlay's lines: in a narrow window the stats line breaks before the timings, so none of it runs off the left edge.</summary>
    public static List<string> StatsLines(string fps, string stats, SKFont font, float maxWidth)
    {
        var lines = new List<string> { fps };
        if (font.MeasureText(stats) > maxWidth)
            lines.AddRange(stats.Split("  ·  "));
        else
            lines.Add(stats);
        return lines;
    }

    /// <summary>Shakes the world view for <paramref name="seconds"/> by up to <paramref name="pixels"/> (a critical hit).</summary>
    public void Shake(double seconds, float pixels)
    {
        _shakeUntil = _shakeClock.Elapsed.TotalSeconds + seconds;
        _shakeSeconds = seconds;
        _shakePixels = pixels;
    }

    private readonly System.Diagnostics.Stopwatch _shakeClock = System.Diagnostics.Stopwatch.StartNew();
    private double _shakeUntil, _shakeSeconds;
    private float _shakePixels;

    /// <summary>This frame's shake offset: a quick wobble that dies away (none in tests, which fix the time).</summary>
    private SKPoint ShakeOffset()
    {
        var now = _shakeClock.Elapsed.TotalSeconds;
        if (FixedTime is not null || now >= _shakeUntil || _shakeSeconds <= 0)
            return SKPoint.Empty;
        var left = (float)((_shakeUntil - now) / _shakeSeconds);
        return new SKPoint(_shakePixels * left * MathF.Sin((float)now * 90), _shakePixels * left * 0.6f * MathF.Cos((float)now * 70));
    }

    private void DrawWorld(SKCanvas canvas, SKSize size, GameWorld world)
    {
        var time = FixedTime ?? world.Time;
        _viewport = SKRect.Create(size);
        var shake = ShakeOffset();
        var shaken = canvas.Save();
        canvas.Translate(shake.X, shake.Y);
        // Frames are cached at the target's pixel density so drawing them is a straight copy.
        _deviceScale = Math.Max(0.25f, canvas.TotalMatrix.ScaleX);
        // The act's colour grade is baked into every cached sprite (SpriteCache), so no full-screen filter pass.
        DrawGround(canvas, size, world, time);
        DrawHazards(canvas, world);
        DrawAuras(canvas, world, time);
        DrawTargetMarks(canvas, world, time);
        DrawBursts(canvas, world);
        DrawTraps(canvas, world);
        Stats.Mark("ground");
        DrawDepthSorted(canvas, size, world);
        DrawSpells(canvas, world);
        DrawLootBeams(canvas, world);
        Stats.Mark("objects+actors");
        DrawDarkness(canvas, size, world, time);
        canvas.RestoreToCount(shaken);
        Stats.Mark("darkness");
        DrawAsh(canvas, size, time);
        Stats.Mark("ash");
        DrawVignette(canvas, size);
        Stats.Mark("vignette");
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
        // Poison bog: the grass darkened to a wet, rotten olive, with a sickly green that slowly swells and fades.
        var swell = (float)(0.5 + 0.5 * Math.Sin(time * 0.9));
        // Its edges step down in two shades, so a pool's rim doesn't show the tile grid: a bog cell beside dry land is a
        // little lighter, and dry land beside the bog a little darker and wetter.
        SKPaint Shade(float mul, float wet) => new()
        {
            ColorFilter = SKColorFilter.CreateLighting(
                new SKColor((byte)(255 - (255 - 92) * mul), (byte)(255 - (255 - 104) * mul), (byte)(255 - (255 - 58) * mul)),
                new SKColor((byte)((4 + 4 * swell) * wet), (byte)((14 + 10 * swell) * wet), 0)),
        };
        using var bog = Shade(1, 1);
        using var rim = Shade(0.8f, 0.8f);
        using var fringe = Shade(0.4f, 0.3f);
        var map = world.Map;
        var outdoors = world.Zone.Tileset == "tileset_grassland";
        int WetNeighbours(int x, int y) =>
            (map.IsBog(x + 1, y) ? 1 : 0) + (map.IsBog(x - 1, y) ? 1 : 0) + (map.IsBog(x, y + 1) ? 1 : 0) + (map.IsBog(x, y - 1) ? 1 : 0);
        for (var y = y0; y <= y1; y++)
        for (var x = x0; x <= x1; x++)
        {
            // Beyond the map's edge the forest simply carries on, so the view never shows a void.
            var id = map.InBounds(x, y) ? map.Ground(x, y) : outdoors ? OutsideGround(x, y) : -1;
            if (id < 0 || !tileset.Tiles.TryGetValue(id, out var tile))
                continue;
            var paint = map.IsBog(x, y) ? (WetNeighbours(x, y) < 4 ? rim : bog)
                : map.InBounds(x, y) && !map.IsWater(x, y) && WetNeighbours(x, y) > 0 ? fringe : null;
            DrawTile(canvas, tile, ToScreen(x + 0.5, y + 0.5), time, paint);
        }
    }

    /// <summary>The monster the hero is attacking or casting at, if it still stands (it carries the target ring).</summary>
    public MonsterActor? Targeted => World?.Hero.CurrentTarget is { IsDead: false } target ? target : null;

    /// <summary>
    /// Rings on the ground under the hero's target and under the monster the mouse is over, so in a crowd it is plain
    /// who the blows go to: the target's is an ember-red ring with four brackets turning slowly round it, the hovered
    /// monster's a thin pale ring (the one the next click would attack). Both lie under every sprite.
    /// </summary>
    private void DrawTargetMarks(SKCanvas canvas, GameWorld world, double time)
    {
        var target = Targeted;
        var hovered = _pointer is { } p && !Dragging ? MonsterUnder(p) : null;
        if (hovered is not null && hovered != target)
            DrawGroundRing(canvas, hovered, time, targeted: false);
        if (target is not null)
            DrawGroundRing(canvas, target, time, targeted: true);
        if (world.Hero.Prey is { IsDead: false } prey && world.IsLit(prey.X, prey.Y))
            DrawPreyMark(canvas, prey, time);
    }

    /// <summary>The Ranger's prey: a green hunter's mark, four arrowheads pointing in at its feet.</summary>
    private void DrawPreyMark(SKCanvas canvas, MonsterActor m, double time)
    {
        var z = (float)Zoom;
        var foot = ToScreen(m.X, m.Y);
        var r = TargetRingRadius * 1.25f * z * (float)m.Definition.Scale;
        var saved = canvas.Save();
        canvas.Translate(foot.X, foot.Y);
        canvas.Scale(1, 0.5f);
        var pulse = (float)(0.5 + 0.5 * Math.Sin(time * 3));
        using var fill = new SKPaint { IsAntialias = true, Color = new SKColor(140, 230, 110, (byte)(170 + 60 * pulse)) };
        for (var i = 0; i < 4; i++)
        {
            canvas.Save();
            canvas.RotateDegrees(45 + 90 * i);
            using var head = new SKPath();
            head.MoveTo(r - 2 * z, 0);
            head.LineTo(r + 16 * z, -10 * z);
            head.LineTo(r + 16 * z, 10 * z);
            head.Close();
            canvas.DrawPath(head, fill);
            canvas.Restore();
        }
        canvas.RestoreToCount(saved);
    }

    /// <summary>The target ring's radius on screen at zoom 1, for a creature of scale 1 (about two-thirds of a tile).</summary>
    public const float TargetRingRadius = 64;

    private void DrawGroundRing(SKCanvas canvas, MonsterActor m, double time, bool targeted)
    {
        var z = (float)Zoom;
        var foot = ToScreen(m.X, m.Y);
        // A circle on the ground, squashed 2:1 like the tiles; bigger creatures get a bigger ring.
        var r = TargetRingRadius * z * (float)m.Definition.Scale * (m.Creature.Stats.Rank == CreatureRank.Champion ? 1.15f : 1f);
        var saved = canvas.Save();
        canvas.Translate(foot.X, foot.Y);
        canvas.Scale(1, 0.5f);
        if (!targeted)
        {
            using var thin = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3 * z, Color = new SKColor(235, 225, 200, 140) };
            canvas.DrawCircle(0, 0, r, thin);
            canvas.RestoreToCount(saved);
            return;
        }
        var pulse = (float)(0.5 + 0.5 * Math.Sin(time * 5));
        var ember = new SKColor(235, 70, 40);
        using (var glow = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateRadialGradient(SKPoint.Empty, r * 1.15f,
                [ember.WithAlpha(0), ember.WithAlpha(0), ember.WithAlpha((byte)(70 + 40 * pulse)), ember.WithAlpha(0)], [0, 0.5f, 0.85f, 1f],
                SKShaderTileMode.Clamp),
        })
            canvas.DrawCircle(0, 0, r * 1.15f, glow);
        using var ring = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 4.5f * z, Color = ember.WithAlpha((byte)(200 + 55 * pulse)) };
        canvas.DrawCircle(0, 0, r, ring);
        // Four brackets just outside the ring, turning a quarter-turn every two seconds.
        using var bracket = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 7 * z, StrokeCap = SKStrokeCap.Round,
            Color = new SKColor(255, 170, 90).WithAlpha((byte)(200 + 55 * pulse)),
        };
        var outer = r + 13 * z;
        var spin = (float)(time * 45 % 360);
        var bounds = new SKRect(-outer, -outer, outer, outer);
        for (var i = 0; i < 4; i++)
            canvas.DrawArc(bounds, spin + i * 90 - 18, 36, false, bracket);
        canvas.RestoreToCount(saved);
    }

    /// <summary>
    /// A boss's rot pools on the ground: a pulsing ring while it gathers, then a sickly pool that fades as it ends. A
    /// circle on the map is an ellipse on screen, √2 half-tiles across per tile of radius.
    /// </summary>
    private void DrawHazards(SKCanvas canvas, GameWorld world)
    {
        var z = (float)Zoom;
        foreach (var h in world.Hazards)
        {
            var at = ToScreen(h.X, h.Y);
            var rx = (float)h.Radius * HalfTileWidth * z * 1.414f;
            var ry = (float)h.Radius * HalfTileHeight * z * 1.414f;
            var oval = new SKRect(at.X - rx, at.Y - ry, at.X + rx, at.Y + ry);
            // Rot is a sickly green; a Tainted monster's corruption a bruised purple.
            var taint = h.Type == AshenReliquary.Core.Combat.DamageType.Taint;
            if (!h.IsActive(world.Time))
            {
                var pulse = (float)(0.5 + 0.5 * Math.Sin(world.Time * 14));
                using var ring = new SKPaint
                {
                    IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = (2 + 2 * pulse) * z * 2,
                    Color = (taint ? new SKColor(190, 110, 230) : new SKColor(170, 230, 90)).WithAlpha((byte)(120 + 100 * pulse)),
                };
                canvas.DrawOval(oval, ring);
                continue;
            }
            var fade = (float)Math.Clamp(h.EndsAt - world.Time, 0, 1);
            using var pool = new SKPaint
            {
                IsAntialias = true,
                Shader = SKShader.CreateRadialGradient(at, rx, taint
                    ? [new SKColor(130, 60, 170, (byte)(170 * fade)), new SKColor(70, 25, 100, (byte)(160 * fade)), new SKColor(30, 10, 40, 0)]
                    : [new SKColor(120, 170, 40, (byte)(170 * fade)), new SKColor(70, 110, 20, (byte)(150 * fade)), new SKColor(40, 60, 10, 0)],
                    [0f, 0.7f, 1f], SKShaderTileMode.Clamp),
            };
            canvas.Save();
            canvas.Scale(1, ry / rx, at.X, at.Y);
            canvas.DrawCircle(at, rx, pool);
            canvas.Restore();
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
        canvas.Save();
        canvas.Scale(1 / _deviceScale);
        if (Zooming)
        {
            // Mid-glide: stretch the frame cut for the step being moved to, rather than cut and resample one for
            // every passing scale (which would empty the cache each frame of the glide).
            var settled = _sprites.Get(sheet, x, y, width, height, scale * (float)(ZoomTarget / Zoom) * _deviceScale);
            var dest = SKRect.Create(left * _deviceScale, top * _deviceScale, width * scale * _deviceScale, height * scale * _deviceScale);
            canvas.DrawImage(settled, dest, new SKSamplingOptions(SKFilterMode.Linear), paint);
        }
        else
        {
            var image = _sprites.Get(sheet, x, y, width, height, scale * _deviceScale);
            canvas.DrawImage(image, MathF.Round(left * _deviceScale), MathF.Round(top * _deviceScale), paint);
        }
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
                : world.Zone.Tileset == "tileset_grassland" && OutsideTree(x, y) is { } tree ? [tree] : [];
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
        foreach (var a in world.Allies)
        {
            var ally = a;
            // A fallen ally lies for a moment, then is gone until it comes back.
            if (!a.IsDead || a.ReturnIn > GameWorld.AllyReturnSeconds - 2)
                items.Add((a.IsDead ? a.X + a.Y - 0.6 : a.X + a.Y, a.X, () => DrawAlly(canvas, ally)));
        }
        foreach (var v in world.Vendors)
        {
            var vendor = v;
            items.Add((v.X + v.Y, v.X, () => DrawVendor(canvas, vendor)));
        }
        foreach (var c in world.Caches)
        {
            var cache = c;
            items.Add((c.X + c.Y, c.X, () => DrawCache(canvas, cache, tileset, world.Time)));
        }
        foreach (var i in world.Interactives)
        {
            var who = i;
            items.Add((i.X + i.Y, i.X, () => DrawInteractive(canvas, who, tileset, world.Time)));
        }
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
        var foot = ToScreen(m.X, m.Y);
        using var paint = new SKPaint();
        var hovered = MonsterUnderPointer() == m;
        var filter = m.Creature.Stats.Rank switch
        {
            CreatureRank.Champion => SKColorFilter.CreateLighting(new SKColor(255, 190, 170), new SKColor(hovered ? (byte)70 : (byte)40, 0, 0)),
            CreatureRank.Minion => SKColorFilter.CreateLighting(new SKColor(200, 220, 190), new SKColor(0, hovered ? (byte)30 : (byte)8, 0)),
            _ when m.HitFlinch > 0 => SKColorFilter.CreateLighting(SKColors.White, new SKColor(90, 30, 30)),
            _ when hovered => SKColorFilter.CreateLighting(SKColors.White, new SKColor(35, 35, 35)),
            _ => null,
        };
        // A boss fused with her cauldron burns a sickly green.
        if (m.Fused && !m.IsDead)
        {
            var glow = (byte)(40 + 30 * (0.5 + 0.5 * Math.Sin(world.Time * 5)));
            var green = SKColorFilter.CreateLighting(SKColors.White, new SKColor(0, glow, 0));
            filter = filter is null ? green : SKColorFilter.CreateCompose(green, filter);
        }
        // Vengeance glows a deepening red with each stack.
        if (m.Vengeance > 0 && !m.IsDead)
        {
            var glow = (byte)(30 + 25 * m.Vengeance * (0.75 + 0.25 * Math.Sin(world.Time * 6)));
            var red = SKColorFilter.CreateLighting(SKColors.White, new SKColor(glow, 0, 0));
            filter = filter is null ? red : SKColorFilter.CreateCompose(red, filter);
        }
        // Affixes show in the body: Hasted a hot pale shimmer, Tainted a bruised purple; a warded monster glows blue.
        if (!m.IsDead && (m.Has(MonsterAffix.Hasted) || m.Has(MonsterAffix.Tainted) || m.Ward > 0))
        {
            var beat = 0.75 + 0.25 * Math.Sin(world.Time * 7);
            var add = m.Ward > 0 ? new SKColor(0, (byte)(25 * beat), (byte)(70 * beat))
                : m.Has(MonsterAffix.Tainted) ? new SKColor((byte)(45 * beat), 0, (byte)(55 * beat))
                : new SKColor((byte)(55 * beat), (byte)(45 * beat), 0);
            var mark = SKColorFilter.CreateLighting(SKColors.White, add);
            filter = filter is null ? mark : SKColorFilter.CreateCompose(mark, filter);
        }
        // A creature's own tint (the plague crawler's bog brown) goes on first.
        if (m.Definition.Tint is { } tint && SKColor.TryParse(tint, out var colour))
        {
            var own = SKColorFilter.CreateLighting(colour, SKColors.Black);
            filter = filter is null ? own : SKColorFilter.CreateCompose(filter, own);
        }
        paint.ColorFilter = filter;
        if (m.IsDead)
            paint.Color = SKColors.White.WithAlpha((byte)(255 * Math.Clamp(1 - (m.DeadFor - (GameWorld.CorpseSeconds - 3)) / 3, 0, 1)));
        if (m.Definition.Look is { } look)
        {
            // A cultist: Flare's avatar layers in its robes, playing the monster's animation.
            foreach (var slot in _assets.LayerOrder(m.Direction))
            {
                var layer = look.GetValueOrDefault(slot) ?? (BareBody.Contains(slot) ? (slot == "head" ? "head_short" : $"default_{slot}") : null);
                if (layer is null || Pick(_assets.AvatarLayer(layer), m.Animation) is not { } part)
                    continue;
                DrawFrame(canvas, _assets.AvatarLayer(layer).Image, part.Frame(part.FrameAt(m.AnimationTime), m.Direction), foot, (float)m.Definition.Scale, paint);
            }
        }
        else
        {
            var set = _assets.Enemy(m.Definition.Sprite);
            if (Pick(set, m.Animation) is not { } anim)
                return;
            DrawFrame(canvas, set.Image, anim.Frame(anim.FrameAt(m.AnimationTime), m.Direction), foot, (float)m.Definition.Scale, paint);
        }
        if (!m.IsDead && (m.State != MonsterState.Idle || hovered) && m.Creature.HitPoints < m.Creature.Stats.HitPoints && world.IsLit(m.X, m.Y))
            WorldOverlay.DrawHealthBar(canvas, foot, (float)Zoom, m.Creature.HitPoints / (float)m.Creature.Stats.HitPoints,
                m.Creature.Stats.Rank == CreatureRank.Champion);
    }

    /// <summary>
    /// An ally (the Beastmaster's wolf): its creature sprite, flashing when bitten, a green health bar while hurt, and a
    /// faint green ring at its feet so it reads as the hero's.
    /// </summary>
    private void DrawAlly(SKCanvas canvas, AllyActor ally)
    {
        var foot = ToScreen(ally.X, ally.Y);
        var z = (float)Zoom;
        if (!ally.IsDead)
        {
            using var ring = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2 * z, Color = new SKColor(120, 210, 120, 120) };
            canvas.DrawOval(SKRect.Create(foot.X - 34 * z, foot.Y - 15 * z, 68 * z, 30 * z), ring);
        }
        using var paint = new SKPaint();
        if (ally.HitFlinch > 0)
            paint.ColorFilter = SKColorFilter.CreateLighting(SKColors.White, new SKColor(90, 30, 30));
        if (ally.IsDead)
            paint.Color = SKColors.White.WithAlpha((byte)(255 * Math.Clamp((ally.ReturnIn - (GameWorld.AllyReturnSeconds - 2)) / 1.0, 0, 1)));
        var set = _assets.Enemy(ally.Definition.Sprite);
        if (Pick(set, ally.Animation) is { } anim)
            DrawFrame(canvas, set.Image, anim.Frame(anim.FrameAt(ally.AnimationTime), ally.Direction), foot, (float)ally.Definition.Scale, paint);
        if (!ally.IsDead && ally.Creature.HitPoints < ally.Creature.Stats.HitPoints)
            WorldOverlay.DrawHealthBar(canvas, foot, z, ally.Creature.HitPoints / (float)ally.Creature.Stats.HitPoints, false, 95,
                new SKColor(90, 190, 80));
    }

    private void DrawHero(SKCanvas canvas, GameWorld world)
    {
        var hero = world.Hero;
        var foot = ToScreen(hero.X, hero.Y);
        if (hero.Ward is { } ward)
        {
            // A ward is a ring at the hero's feet: shield gold, or the Shield cantrip's force blue; it fades as it ends.
            var z = (float)Zoom;
            var fade = (float)Math.Clamp((ward.EndsAt - world.Time) / 1.0, 0.25, 1);
            var colour = ward.Ability.NeedsShield ? new SKColor(235, 200, 120) : new SKColor(150, 170, 255);
            using var ring = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3 * z, Color = colour.WithAlpha((byte)(170 * fade)) };
            canvas.DrawOval(SKRect.Create(foot.X - 46 * z, foot.Y - 20 * z, 92 * z, 40 * z), ring);
        }
        var layers = Session!.AppearanceLayers;
        var animation = hero.Animation;
        // In the shadows (Hide in Shadows) the hero is a dim, half-seen shape.
        using var hidden = hero.IsHidden
            ? new SKPaint { Color = SKColors.White.WithAlpha(150), ColorFilter = SKColorFilter.CreateLighting(new SKColor(170, 170, 215), new SKColor(10, 10, 30)) }
            : null;
        foreach (var slot in _assets.LayerOrder(hero.Direction))
        {
            if (!layers.TryGetValue(slot, out var layer))
                continue;
            var set = _assets.AvatarLayer(layer);
            if (Pick(set, animation) is not { } anim)
                continue;
            DrawFrame(canvas, set.Image, anim.Frame(anim.FrameAt(hero.AnimationTime), hero.Direction), foot, 1f, hidden);
        }
    }

    /// <summary>
    /// The Trickster's snares on the ground: a ring of iron teeth, dull while it arms and glinting once armed, with
    /// its burst's reach faintly marked.
    /// </summary>
    /// <summary>
    /// The Champion's auras: a faint disc on the ground round the hero, as wide as the aura reaches, edged by a dashed
    /// ring turning slowly; it flares as the aura pulses. Gold for Shield of Faith, lantern light for the Radiance,
    /// the Taint's green for Aura of Dread.
    /// </summary>
    private void DrawAuras(SKCanvas canvas, GameWorld world, double time)
    {
        if (world.Hero.IsDead)
            return;
        var z = (float)Zoom;
        var centre = ToScreen(world.Hero.X, world.Hero.Y);
        foreach (var aura in world.Auras)
        {
            var a = aura.Ability;
            var color = a.CostsTaint ? new SKColor(150, 210, 80) : a.Holy ? new SKColor(255, 214, 130) : new SKColor(240, 226, 180);
            // A pulse: bright when the aura ticks, fading over half a second.
            var sinceTick = (float)((world.Time - (aura.NextTick - GameWorld.AuraTickSeconds)) / 0.5);
            var flare = a.Dice is not null || a.AuraHeal > 0 ? Math.Max(0, 1 - sinceTick) : 0;
            var rx = (float)a.Radius * HalfTileWidth * z * 1.414f;
            var ry = (float)a.Radius * HalfTileHeight * z * 1.414f;
            var oval = SKRect.Create(centre.X - rx, centre.Y - ry, rx * 2, ry * 2);
            using var fill = new SKPaint
            {
                IsAntialias = true,
                BlendMode = SKBlendMode.Plus,
                Shader = SKShader.CreateRadialGradient(new SKPoint(centre.X, centre.Y), rx,
                    [color.WithAlpha(0), color.WithAlpha((byte)(18 + 40 * flare)), color.WithAlpha((byte)(40 + 70 * flare))], [0, 0.75f, 1f],
                    SKShaderTileMode.Clamp, SKMatrix.CreateScale(1, ry / rx, centre.X, centre.Y)),
            };
            canvas.DrawOval(oval, fill);
            using var ring = new SKPaint
            {
                IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2.5f * z, BlendMode = SKBlendMode.Plus,
                Color = color.WithAlpha((byte)(120 + 100 * flare)),
                PathEffect = SKPathEffect.CreateDash([18 * z, 12 * z], (float)(time * 30 * z)),
            };
            canvas.DrawOval(oval, ring);
        }
    }

    private void DrawTraps(SKCanvas canvas, GameWorld world)
    {
        var z = (float)Zoom;
        foreach (var trap in world.Traps)
        {
            var armed = trap.IsArmed(world.Time);
            var centre = ToScreen(trap.X, trap.Y);
            using var reach = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f * z,
                Color = new SKColor(230, 200, 140, (byte)(armed ? 70 : 30)), PathEffect = SKPathEffect.CreateDash([8 * z, 8 * z], 0) };
            var rx = (float)(trap.Ability.Radius * 64 * z);
            canvas.DrawOval(SKRect.Create(centre.X - rx, centre.Y - rx / 2, rx * 2, rx), reach);
            using var teeth = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3 * z, StrokeCap = SKStrokeCap.Round,
                Color = armed ? new SKColor(215, 205, 190) : new SKColor(120, 110, 100) };
            const int count = 10;
            for (var i = 0; i < count; i++)
            {
                var a = 2 * Math.PI * i / count;
                var inner = new SKPoint(centre.X + (float)Math.Cos(a) * 12 * z, centre.Y + (float)Math.Sin(a) * 6 * z);
                var outer = new SKPoint(centre.X + (float)Math.Cos(a) * 22 * z, centre.Y + (float)Math.Sin(a) * 11 * z - 6 * z);
                canvas.DrawLine(inner, outer, teeth);
            }
            teeth.StrokeWidth = 2 * z;
            canvas.DrawOval(SKRect.Create(centre.X - 14 * z, centre.Y - 7 * z, 28 * z, 14 * z), teeth);
        }
    }

    private static readonly string[] BareBody = ["head", "chest", "legs", "feet", "hands"];

    /// <summary>A trader: Flare's avatar in the clothes their zone file gives them, idling, brighter when hovered.</summary>
    private void DrawVendor(SKCanvas canvas, Vendor vendor) =>
        DrawPaperDoll(canvas, ToScreen(vendor.X, vendor.Y), vendor.Definition.Look, vendor.Definition.Facing,
            _pointer is { } p && VendorUnder(p) == vendor);

    /// <summary>Someone drawn from Flare's avatar layers (a trader, an NPC), idling, brighter when hovered.</summary>
    private void DrawPaperDoll(SKCanvas canvas, SKPoint foot, IReadOnlyDictionary<string, string> look, int direction, bool hovered, bool still = false)
    {
        using var paint = hovered ? new SKPaint { ColorFilter = SKColorFilter.CreateLighting(SKColors.White, new SKColor(35, 35, 35)) } : null;
        foreach (var slot in _assets.LayerOrder(direction))
        {
            var layer = look.GetValueOrDefault(slot) ?? (BareBody.Contains(slot) ? (slot == "head" ? "head_short" : $"default_{slot}") : null);
            if (layer is null)
                continue;
            var set = _assets.AvatarLayer(layer);
            if (Pick(set, "stance") is not { } anim)
                continue;
            DrawFrame(canvas, set.Image, anim.Frame(still ? 0 : anim.FrameAt(World!.Time), direction), foot, 1f, paint);
        }
    }

    /// <summary>
    /// A chest from the tileset: a plain cache is Flare's wooden chest (76, 77 by facing), dimmed once opened, as the art
    /// has no open one; a reliquary is the spiked skull chest, shut (297, 299) or open (298, 300). In a dungeon, the
    /// dungeon tileset's own. Brighter when hovered.
    /// </summary>
    private void DrawCache(SKCanvas canvas, Cache cache, FlareTileset tileset, double time)
    {
        var facing = cache.Definition.Facing == 1 ? 1 : 0;
        // The dungeon tileset has its own chests: wooden ones shut (144, 145) and open (160, 161), the skull chest at 288-291.
        var dungeon = World?.Zone.Tileset == "tileset_dungeon";
        var id = cache.Definition.Kind == CacheKind.Reliquary
            ? (dungeon ? 288 : 297) + (facing == 0 ? 0 : 2) + (cache.Opened ? 1 : 0)
            : dungeon ? (cache.Opened ? 160 : 144) + facing : 76 + facing;
        if (!tileset.Tiles.TryGetValue(id, out var tile))
            return;
        var hovered = _pointer is { } p && CacheUnder(p) == cache;
        using var paint = hovered
            ? new SKPaint { ColorFilter = SKColorFilter.CreateLighting(SKColors.White, new SKColor(40, 35, 25)) }
            : cache.Opened && cache.Definition.Kind == CacheKind.Cache
                ? new SKPaint { ColorFilter = SKColorFilter.CreateLighting(new SKColor(120, 115, 110), SKColors.Black) }
                : null;
        DrawTile(canvas, tile, ToScreen(cache.X, cache.Y), time, paint);
    }

    private void DrawInteractive(SKCanvas canvas, Interactive who, FlareTileset tileset, double time)
    {
        var hovered = _pointer is { } p && InteractiveUnder(p) == who;
        if (who.Npc is { } npc)
        {
            DrawPaperDoll(canvas, ToScreen(who.X, who.Y), npc.Look, npc.Facing, hovered, npc.Still);
            return;
        }
        if (who.Thing?.Tile is { } id && tileset.Tiles.TryGetValue(id, out var tile))
        {
            using var paint = hovered ? new SKPaint { ColorFilter = SKColorFilter.CreateLighting(SKColors.White, new SKColor(40, 35, 25)) } : null;
            DrawTile(canvas, tile, ToScreen(who.X, who.Y), time, paint);
        }
    }

    /// <summary>A person's name and calling, or a thing's name, when near or hovered.</summary>
    private void DrawInteractiveLabels(SKCanvas canvas, GameWorld world)
    {
        using var nameFont = new SKFont(WorldOverlay.Serif, 14);
        using var small = new SKFont(WorldOverlay.Serif, 11);
        using var ink = new SKPaint { IsAntialias = true, Color = new SKColor(200, 225, 255) };
        using var dim = new SKPaint { IsAntialias = true, Color = Palette.Dim };
        using var back = new SKPaint { Color = new SKColor(0, 0, 0, 160) };
        foreach (var who in world.Interactives)
        {
            var hovered = _pointer is { } p && InteractiveUnder(p) == who;
            if (!hovered && world.Hero.DistanceTo(who.X, who.Y) > (who.Npc is null ? 5 : 9))
                continue;
            var foot = ToScreen(who.X, who.Y);
            var top = foot.Y - (who.Npc is null ? 100 : 150) * (float)Zoom - 30;
            var width = Math.Max(nameFont.MeasureText(who.Name), who.Title is null ? 0 : small.MeasureText(who.Title)) + 16;
            var height = who.Title is null ? 20 : 32;
            canvas.DrawRect(SKRect.Create(foot.X - width / 2, top, width, height), back);
            canvas.DrawText(who.Name, foot.X, top + 14, SKTextAlign.Center, nameFont, ink);
            if (who.Title is { } title)
                canvas.DrawText(title, foot.X, top + 27, SKTextAlign.Center, small, dim);
        }
    }

    /// <summary>A chest's name when near (and still shut) or hovered.</summary>
    private void DrawCacheLabels(SKCanvas canvas, GameWorld world)
    {
        using var font = new SKFont(WorldOverlay.Serif, 13);
        using var ink = new SKPaint { IsAntialias = true };
        using var back = new SKPaint { Color = new SKColor(0, 0, 0, 160) };
        foreach (var c in world.Caches)
        {
            var hovered = _pointer is { } p && CacheUnder(p) == c;
            if (!hovered && (c.Opened || world.Hero.DistanceTo(c.X, c.Y) > 6))
                continue;
            var text = c.Opened ? $"{c.Name} (empty)" : c.Name;
            ink.Color = c.Opened ? Palette.Dim : c.Definition.Kind == CacheKind.Reliquary ? new SKColor(230, 170, 255) : Palette.Gold;
            var foot = ToScreen(c.X, c.Y);
            var top = foot.Y - 90 * (float)Zoom - 18;
            var width = font.MeasureText(text) + 14;
            canvas.DrawRect(SKRect.Create(foot.X - width / 2, top, width, 20), back);
            canvas.DrawText(text, foot.X, top + 14, SKTextAlign.Center, font, ink);
        }
    }

    /// <summary>A trader's name and calling over their head, when near or hovered.</summary>
    private void DrawVendorLabels(SKCanvas canvas, GameWorld world)
    {
        using var nameFont = new SKFont(WorldOverlay.Serif, 14);
        using var small = new SKFont(WorldOverlay.Serif, 11);
        using var gold = new SKPaint { IsAntialias = true, Color = Palette.Gold };
        using var dim = new SKPaint { IsAntialias = true, Color = Palette.Dim };
        using var back = new SKPaint { Color = new SKColor(0, 0, 0, 160) };
        foreach (var v in world.Vendors)
        {
            var hovered = _pointer is { } p && VendorUnder(p) == v;
            if (!hovered && world.Hero.DistanceTo(v.X, v.Y) > 9)
                continue;
            var foot = ToScreen(v.X, v.Y);
            var top = foot.Y - 150 * (float)Zoom - 30;
            var width = Math.Max(nameFont.MeasureText(v.Definition.Name), small.MeasureText(v.Definition.Title)) + 16;
            canvas.DrawRect(SKRect.Create(foot.X - width / 2, top, width, 32), back);
            canvas.DrawText(v.Definition.Name, foot.X, top + 14, SKTextAlign.Center, nameFont, gold);
            canvas.DrawText(v.Definition.Title, foot.X, top + 27, SKTextAlign.Center, small, dim);
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
    /// <summary>The loot filter (a setting): common gear on the ground shows no label and can't be clicked.</summary>
    public bool HideCommonLoot { get; set; }

    /// <summary>Alt held: every label shows, filtered or not.</summary>
    public bool ShowAllLoot { get; set; }

    /// <summary>Whether a ground item's label shows under the loot filter (gold, draughts and belt items always do).</summary>
    public bool LabelShows(GroundItem g) =>
        !HideCommonLoot || ShowAllLoot || g.Drop.Item is not { Rarity: Core.Items.Rarity.Common, Definition.IsBeltItem: false };

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
            if (!_viewport.Contains(at) || !LabelShows(g))
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
        AshenReliquary.Core.Combat.DamageType.Cold => new SKColor(140, 200, 255),
        AshenReliquary.Core.Combat.DamageType.Electricity => new SKColor(200, 215, 255),
        AshenReliquary.Core.Combat.DamageType.Acid => new SKColor(160, 235, 70),
        AshenReliquary.Core.Combat.DamageType.Taint => new SKColor(170, 80, 210),
        _ => new SKColor(255, 230, 160),
    };

    /// <summary>
    /// Bursts on the ground: the area fills in its damage type's colour and a bright edge runs out to the full radius
    /// as it fades. Drawn in map space (a circle or a cone of tiles), so it lies flat like the tiles.
    /// </summary>
    private void DrawBursts(SKCanvas canvas, GameWorld world)
    {
        foreach (var b in world.Bursts)
        {
            var age = (float)Math.Clamp((world.Time - b.At) / GameWorld.BurstSeconds, 0, 1);
            var radius = b.Radius * (0.35 + 0.65 * Math.Sqrt(age));
            var color = SpellColor(b.Type);
            using var fill = new SKPaint { IsAntialias = true, BlendMode = SKBlendMode.Plus, Color = color.WithAlpha((byte)(90 * (1 - age))) };
            using var edge = new SKPaint
            {
                IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 4 * (float)Zoom, BlendMode = SKBlendMode.Plus,
                Color = color.WithAlpha((byte)(230 * (1 - age))),
            };
            using var path = new SKPath();
            var cone = b.Arc < 360;
            var half = b.Arc / 2 * Math.PI / 180;
            if (cone)
                path.MoveTo(ToScreen(b.X, b.Y));
            const int steps = 40;
            for (var i = 0; i <= steps; i++)
            {
                var a = cone ? b.Direction - half + 2 * half * i / steps : 2 * Math.PI * i / steps;
                var at = ToScreen(b.X + radius * Math.Cos(a), b.Y + radius * Math.Sin(a));
                if (i == 0 && !cone)
                    path.MoveTo(at);
                else
                    path.LineTo(at);
            }
            path.Close();
            canvas.DrawPath(path, fill);
            canvas.DrawPath(path, edge);
        }
    }

    /// <summary>The hero's arrow: Flare's arrow sprite, turned to its line of flight.</summary>
    private void DrawArrow(SKCanvas canvas, Projectile p, SKPoint at, double time)
    {
        var set = _assets.Power("arrows");
        if (set.Animations.Values.FirstOrDefault() is not { } anim)
            return;
        var direction = Actor.DirectionOf(p.Target.X - p.X, p.Target.Y - p.Y);
        // Flare draws a power's sprite from its anchor at the ground, so lift the anchor to the flying height.
        DrawFrame(canvas, set.Image, anim.Frame(anim.FrameAt(time), direction), new SKPoint(at.X, at.Y + 95 * (float)Zoom), 1f, null);
    }

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
            if (p.IsArrow)
            {
                DrawArrow(canvas, p, at, world.Time);
                continue;
            }
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
            (ToScreen(world.Hero.X, world.Hero.Y), (float)world.HeroLightRadius, new SKColor(255, 190, 120, 28)),
        };
        VisibleCells(size, world, out var x0, out var y0, out var x1, out var y1);
        // The zone's own lights (torches, braziers, moonlight, shrines in a dungeon), firelight flickering.
        foreach (var l in world.Map.Lights)
        {
            if (l.X < x0 - 6 || l.X > x1 + 6 || l.Y < y0 - 6 || l.Y > y1 + 6)
                continue;
            var fire = l.Color is null;
            var flicker = fire ? (float)(Math.Sin(time * 9.1 + l.X * 3.7) * 0.25 + Math.Sin(time * 13.3 + l.Y) * 0.15) : 0;
            var glow = fire ? new SKColor(255, 140, 50, 70) : SKColor.Parse(l.Color).WithAlpha(60);
            lights.Add((ToScreen(l.X, l.Y), (float)l.Radius + flicker, glow));
        }
        // Outdoors, Flare's grassland campfire and shrine tiles give light of themselves.
        for (var y = world.Zone.Tileset == "tileset_grassland" ? y0 : y1 + 1; y <= y1; y++)
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
        // A faint glint on a shut chest, so it can be found in the dark.
        foreach (var c in world.Caches)
        {
            if (!c.Opened)
                lights.Add((ToScreen(c.X, c.Y), 1.5f + (float)Math.Sin(time * 2.1 + c.X) * 0.15f,
                    c.Definition.Kind == CacheKind.Reliquary ? new SKColor(200, 140, 255, 55) : new SKColor(255, 210, 120, 45)));
        }
        // Everyone in a town carries a little light, so faces read in the dark; in a dark place, things to look at glint.
        foreach (var who in world.Interactives)
        {
            if (who.Npc is { Still: false })
                lights.Add((ToScreen(who.X, who.Y), 2.6f + (float)Math.Sin(time * 5.1 + who.X) * 0.08f, new SKColor(255, 200, 130, 50)));
            else if (who.Npc is null && world.Zone.IsDark)
                lights.Add((ToScreen(who.X, who.Y), 1.6f + (float)Math.Sin(time * 2.3 + who.Y) * 0.12f, new SKColor(255, 210, 140, 55)));
        }
        // A trader's lantern, so the stall reads from across the camp.
        foreach (var v in world.Vendors)
            lights.Add((ToScreen(v.X, v.Y), 3.4f + (float)Math.Sin(time * 7.3 + v.X) * 0.1f, new SKColor(255, 190, 110, 60)));
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
        mask.Clear(new SKColor(6, 5, 12, (byte)Math.Round(255 * world.Zone.DarknessLevel)));
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

    /// <summary>
    /// On a GPU the gradient is drawn straight onto the canvas: one shaded rectangle, nearly free, where a
    /// window-sized cached image cost about 9 ms a frame on an Ice Lake laptop. A software renderer pays per pixel
    /// for the gradient, so there it is drawn once into an image (it only changes with the window size) and copied.
    /// </summary>
    private void DrawVignette(SKCanvas canvas, SKSize size)
    {
        if (_worldOnGpu)
        {
            using var shaded = VignettePaint(size);
            canvas.DrawRect(SKRect.Create(size), shaded);
            return;
        }
        int vw = Math.Max(1, (int)(size.Width * _deviceScale)), vh = Math.Max(1, (int)(size.Height * _deviceScale));
        if (_vignette is null || _vignette.Width != vw || _vignette.Height != vh)
        {
            _vignette?.Dispose();
            using var surface = SKSurface.Create(new SKImageInfo(vw, vh));
            surface.Canvas.Scale(_deviceScale);
            using var paint = VignettePaint(size);
            surface.Canvas.Clear(SKColors.Transparent);
            surface.Canvas.DrawRect(SKRect.Create(size), paint);
            _vignette = surface.Snapshot();
        }
        canvas.DrawImage(_vignette, SKRect.Create(size));
    }

    private static SKPaint VignettePaint(SKSize size)
    {
        var centre = new SKPoint(size.Width / 2, size.Height / 2);
        var radius = (float)Math.Sqrt(size.Width * size.Width + size.Height * size.Height) / 2;
        return new SKPaint
        {
            Shader = SKShader.CreateRadialGradient(centre, radius,
                [SKColors.Transparent, SKColors.Transparent, new SKColor(0, 0, 0, 215)], [0f, 0.5f, 1f], SKShaderTileMode.Clamp),
        };
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
