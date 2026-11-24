using EternalWars.Core.Domain.Settings;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// Places the adventure map's HUD parts (ADR-309, <see cref="HudArrangement"/>): a framed column (ADR-191) down the minimap's side of the
/// screen (the right, or the left when mirrored) holding the minimap and the town list, as wide as its widest part; the hero list
/// (<see cref="SetOpposite"/>) at the other edge, under the top bar; and the bottom row above the status bar (<see cref="SetBottom"/>): the
/// hero card and the army strip from the column's far edge, the action buttons and End turn from the column's edge, all as tall as the
/// tallest. The LAN chat's place (<see cref="ChatRect"/>) is under the column's last part (the town list) when the column has room for it,
/// else beside that part, towards the middle of the screen. A hidden part leaves its place empty and the others where they are. The layer covers the screen but lets the mouse through; each
/// part is a child that is moved, never reparented (a part's event subscriptions last while it is in the tree). It re-places the parts
/// whenever one's size changes.
/// </summary>
public partial class AdventureHudLayout : Container
{
    /// <summary>The gap to the screen edge and between parts.</summary>
    public const float Margin = UiSpacing.ScreenEdge;

    private readonly List<Control> _column = [];
    private readonly Panel _sidePanel = new() { Name = "SidePanel", MouseFilter = MouseFilterEnum.Stop };
    private readonly Dictionary<HudPart, Control> _bottom = [];
    private Control? _opposite;
    private Control? _oppositeContent;
    private LayoutSettings _layout = LayoutSettings.Default;

    /// <summary>
    /// Room kept free along the bottom edge (the status bar, ADR-186, and the replay viewer's bar, ADR-164): the column stops above it.
    /// </summary>
    public float BottomReserve
    {
        get => _bottomReserve;
        set
        {
            _bottomReserve = value;
            QueueSort();
        }
    }

    private float _bottomReserve;

    /// <summary>The chat's height when it stands beside the town list, and the least room under the town list that takes it.</summary>
    public static float ChatHeight => UiTheme.MapChatSize.Y;

    /// <summary>The chat's tallest, in the column on a tall screen.</summary>
    public const float ChatMaxHeight = 300;

    /// <summary>Where the LAN chat goes over the map (ADR-309), in the layer's coordinates; set at each placing.</summary>
    public Rect2 ChatRect { get; private set; }

    /// <summary>The parts were placed again (the screen's size, a part's size, the mirror): the chat's place may have moved.</summary>
    public event Action? Placed;

    public AdventureHudLayout()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        var frame = UiTheme.Box(UiTheme.Background, UiTheme.Border, 0, borderWidth: 0);
        frame.BorderWidthLeft = frame.BorderWidthRight = UiTheme.FrameWidth;
        frame.BorderWidthBottom = UiTheme.FrameWidth;
        _sidePanel.AddThemeStyleboxOverride("panel", frame);
        AddChild(_sidePanel); // first, so it is drawn behind the parts
    }

    /// <summary>One of the bottom row's parts (<see cref="HudArrangement.BottomRow"/>): the hero card, the army strip, the actions, End turn.</summary>
    public void SetBottom(HudPart part, Control control)
    {
        _bottom[part] = control;
        AddChild(control);
    }

    /// <summary>The bottom row's height: its tallest part's, the parts hidden or not, so the row does not jump when the hero card hides.</summary>
    public float BottomRowHeight => _bottom.Values.Select(p => p.GetCombinedMinimumSize().Y).DefaultIfEmpty(0).Max();

    /// <summary>
    /// The part at the edge away from the column, under the top bar (<see cref="ResourceTopBar"/>): the hero list's <paramref name="frame"/>,
    /// as tall as its <paramref name="content"/> (which scrolls inside it) up to the room above the bottom.
    /// </summary>
    public void SetOpposite(Control frame, Control content)
    {
        _opposite = frame;
        _oppositeContent = content;
        AddChild(frame);
        content.MinimumSizeChanged += QueueSort; // the frame's own minimum leaves the scrolled content out
    }

    /// <summary>Adds <paramref name="control"/> to the column, under the parts added before it.</summary>
    public void AddToColumn(Control control)
    {
        _column.Add(control);
        AddChild(control);
    }

    /// <summary>Moves the column to the side <paramref name="layout"/> puts it on.</summary>
    public void Apply(LayoutSettings layout)
    {
        _layout = layout;
        QueueSort();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationSortChildren)
        {
            Place();
        }
    }

    private void Place()
    {
        var screen = Size;
        var left = HudArrangement.SideOf(_layout, HudPart.Minimap) == ScreenSide.Left;
        var width = _column.Select(p => p.GetCombinedMinimumSize().X).DefaultIfEmpty(0).Max();
        var panelWidth = width + (2 * Margin);
        var panelX = left ? 0 : screen.X - panelWidth;
        var rowHeight = BottomRowHeight;
        var rowTop = screen.Y - _bottomReserve - Margin - rowHeight;
        var bottom = _bottom.Count == 0 ? screen.Y - _bottomReserve : rowTop - Margin; // where the column and the hero list stop
        FitChildInRect(_sidePanel, new Rect2(panelX, 0, panelWidth, bottom));

        var x = panelX + Margin;
        var y = Margin;
        var last = new Rect2(x, y, width, 0);
        foreach (var part in _column)
        {
            var height = part.GetCombinedMinimumSize().Y;
            last = new Rect2(x, y, width, height);
            FitChildInRect(part, last);
            y += height + Margin;
        }

        // The chat: between the town list and the bottom row when the column has the room, else beside the town list towards the middle.
        var room = bottom - Margin - y;
        ChatRect = room >= ChatHeight
            ? new Rect2(x, y, width, Math.Min(room, ChatMaxHeight))
            : new Rect2(left ? panelX + panelWidth + Margin : panelX - Margin - UiTheme.MapChatSize.X, last.Position.Y, UiTheme.MapChatSize.X, ChatHeight);

        if (_opposite is not null)
        {
            var size = _opposite.GetCombinedMinimumSize();
            size.Y += _oppositeContent?.GetCombinedMinimumSize().Y ?? 0;
            var top = ResourceTopBar.Height + Margin;
            size.Y = Math.Min(size.Y, Math.Max(0, bottom - Margin - top));
            FitChildInRect(_opposite, new Rect2(left ? screen.X - size.X - Margin : Margin, top, size.X, size.Y));
        }

        PlaceBottomRow(screen, rowTop, rowHeight);
        Placed?.Invoke();
    }

    /// <summary>The bottom row: the parts on the left packed from the left edge, those on the right from the right edge, in the row's order.</summary>
    private void PlaceBottomRow(Vector2 screen, float top, float height)
    {
        var row = HudArrangement.BottomRow(_layout).Where(_bottom.ContainsKey).ToList();
        var x = Margin;
        foreach (var part in row.Where(p => HudArrangement.SideOf(_layout, p) == ScreenSide.Left))
        {
            var width = _bottom[part].GetCombinedMinimumSize().X;
            FitChildInRect(_bottom[part], new Rect2(x, top, width, height));
            _bottom[part].ZIndex = 0;
            x += width + Margin;
        }

        // A screen too narrow for both groups keeps the right group whole and lets it cover the left one's end (step 4 of the plan cuts
        // the army strip with a fade instead).
        x = screen.X - Margin;
        foreach (var part in Enumerable.Reverse(row.Where(p => HudArrangement.SideOf(_layout, p) == ScreenSide.Right).ToList()))
        {
            var width = _bottom[part].GetCombinedMinimumSize().X;
            x -= width;
            FitChildInRect(_bottom[part], new Rect2(x, top, width, height));
            _bottom[part].ZIndex = 1; // drawn over the left group where they meet
            x -= Margin;
        }
    }
}
