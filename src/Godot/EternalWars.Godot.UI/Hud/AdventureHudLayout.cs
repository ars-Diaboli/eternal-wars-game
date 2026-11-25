using EternalWars.Core.Domain.Settings;
using EternalWars.Godot.UI.Widgets;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// Places the adventure map's HUD parts (ADR-309, ADR-313, <see cref="HudArrangement"/>): a framed column (ADR-191) in the minimap's corner of
/// the screen (the top right, or the top left when mirrored) holding the minimap, as wide as its widest part; the town list
/// (<see cref="SetTownList"/>) in its own panel on that side, down on the bottom row and growing up to the column; the hero list
/// (<see cref="SetOpposite"/>) the same at the other edge, growing up to the top bar; and the bottom row above the status bar
/// (<see cref="SetBottom"/>): the hero card and the army strip from the column's far edge, the action buttons and End turn from the column's
/// edge, all as tall as the tallest; on a narrow screen the action buttons move up above the row (ADR-315). The LAN chat's place (<see cref="ChatRect"/>) is between the column and the town list when there is room
/// for it, else beside the column, towards the middle of the screen. A hidden part leaves its place empty and the others where they are. The layer covers the screen but lets the mouse through; each
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
    private Control? _towns;
    private Control? _townsContent;
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

    /// <summary>The least share of the army strip's width a narrow screen's cut may leave before the action buttons move up above it.</summary>
    public const float LeastStripShare = 0.5f;

    /// <summary>Whether the action buttons stand above the bottom row (a narrow screen, ADR-315); set at each placing.</summary>
    public bool ActionsLifted { get; private set; }

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
        UiFrames.Bevel(_sidePanel, BevelKind.Panel, padding: 0); // the HUD's bevelled panel (ADR-310)
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
    /// The part at the edge away from the column, down on the bottom row, growing up to the top bar (<see cref="ResourceTopBar"/>): the hero list's <paramref name="frame"/>,
    /// as tall as its <paramref name="content"/> (which scrolls inside it) up to the room above the bottom.
    /// </summary>
    public void SetOpposite(Control frame, Control content)
    {
        _opposite = frame;
        _oppositeContent = content;
        AddChild(frame);
        content.MinimumSizeChanged += QueueSort; // the frame's own minimum leaves the scrolled content out
    }

    /// <summary>
    /// The town list's <paramref name="frame"/> on the column's side, down on the bottom row and growing up to the column, as tall as its
    /// <paramref name="content"/> (which scrolls inside it) up to the room between them.
    /// </summary>
    public void SetTownList(Control frame, Control content)
    {
        _towns = frame;
        _townsContent = content;
        AddChild(frame);
        content.MinimumSizeChanged += QueueSort;
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
        var bottom = _bottom.Count == 0 ? screen.Y - _bottomReserve : rowTop - Margin; // where the lists stop

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

        var columnEnd = _column.Count == 0 ? 0 : y; // the column's panel ends a margin under its last part
        FitChildInRect(_sidePanel, new Rect2(panelX, 0, panelWidth, columnEnd));

        var lifted = PlaceBottomRow(screen, rowTop, rowHeight, new Rect2(panelX, 0, panelWidth, columnEnd));

        // The town list under the column, the hero list at the other edge, each down at the bottom just above the bottom row (the hero card
        // beside the hero list), or above the action buttons where they are lifted over its foot, and growing upwards: the towns' up to the
        // column, the heroes' up to the top bar.
        var townsTop = bottom - Margin;
        if (_towns is not null)
        {
            townsTop = Stack(_towns, _townsContent, columnEnd + Margin, Above(lifted, _towns, bottom, !left, screen), !left, screen).Position.Y;
        }

        if (_opposite is not null)
        {
            Stack(_opposite, _oppositeContent, ResourceTopBar.Height + Margin, Above(lifted, _opposite, bottom, left, screen), left, screen);
        }

        // The chat: between the column and the town list when there is the room, else beside the column towards the middle.
        var room = townsTop - Margin - columnEnd;
        ChatRect = room >= ChatHeight
            ? new Rect2(x, columnEnd, width, Math.Min(room, ChatMaxHeight))
            : new Rect2(left ? panelX + panelWidth + Margin : panelX - Margin - UiTheme.MapChatSize.X, last.Position.Y, UiTheme.MapChatSize.X, ChatHeight);

        Placed?.Invoke();
    }

    /// <summary>
    /// Places a list's <paramref name="frame"/> at the screen's right edge (<paramref name="right"/>) or its left, its foot a margin above
    /// <paramref name="bottom"/>, as tall as the frame and its <paramref name="content"/> up to <paramref name="top"/>.
    /// </summary>
    private Rect2 Stack(Control frame, Control? content, float top, float bottom, bool right, Vector2 screen)
    {
        var size = frame.GetCombinedMinimumSize();
        var listed = content?.GetCombinedMinimumSize().Y ?? 0;
        frame.Visible = content is null || listed > 0; // no towns, no heroes: no empty panel
        size.Y += listed;
        size.Y = Math.Min(size.Y, Math.Max(0, bottom - Margin - top));
        var rect = new Rect2(right ? screen.X - size.X - Margin : Margin, bottom - Margin - size.Y, size.X, size.Y);
        FitChildInRect(frame, rect);
        return rect;
    }

    /// <summary>
    /// Where a list at the right edge (<paramref name="right"/>) or the left one stops: <paramref name="bottom"/>, or above the
    /// <paramref name="lifted"/> action buttons where its <paramref name="frame"/> would stand over them.
    /// </summary>
    private static float Above(Rect2? lifted, Control frame, float bottom, bool right, Vector2 screen)
    {
        var width = frame.GetCombinedMinimumSize().X;
        var x = right ? screen.X - width - Margin : Margin;
        return lifted is { } rect && x < rect.End.X && x + width > rect.Position.X ? Math.Min(bottom, rect.Position.Y - Margin) : bottom;
    }

    /// <summary>
    /// The bottom row: the parts on the left packed from the left edge, those on the right from the right edge, in the row's order. A screen
    /// too narrow for the whole row cuts the army strip (<see cref="CutPart"/>) at its end towards the middle. Where the cut would leave it
    /// under <see cref="LeastStripShare"/> of its width, the action buttons move up, just above the place they leave (or beside the
    /// <paramref name="column"/>'s panel towards the middle, where they would cover it), and the strip takes that place; the row is placed
    /// without them (returned: where they stand, else null). One too narrow even then lets the right group cover the left one's end.
    /// </summary>
    private Rect2? PlaceBottomRow(Vector2 screen, float top, float height, Rect2 column)
    {
        var row = HudArrangement.BottomRow(_layout).Where(_bottom.ContainsKey).ToList();
        float Wanted(HudPart part) => _bottom[part] is CutPart cut ? cut.FullWidth : _bottom[part].GetCombinedMinimumSize().X;
        float Over() => Math.Max(0, row.Sum(Wanted) + (Margin * (row.Count + 1)) - screen.X);

        var strip = row.Where(p => _bottom[p] is CutPart).Select(p => (HudPart?)p).FirstOrDefault();
        var over = Over();
        ActionsLifted = strip is { } cutPart && row.Contains(HudPart.Actions) && Wanted(cutPart) - over < Wanted(cutPart) * LeastStripShare;
        if (ActionsLifted)
        {
            row.Remove(HudPart.Actions);
            over = Over();
        }

        float Width(HudPart part) => _bottom[part] is CutPart ? Math.Max(0, Wanted(part) - over) : Wanted(part);

        var x = Margin;
        foreach (var part in row.Where(p => HudArrangement.SideOf(_layout, p) == ScreenSide.Left))
        {
            Put(part, new Rect2(x, top, Width(part), height), cutAtRight: true, z: 0);
            x += Width(part) + Margin;
        }

        var leftEnd = x;
        x = screen.X - Margin;
        foreach (var part in Enumerable.Reverse(row.Where(p => HudArrangement.SideOf(_layout, p) == ScreenSide.Right).ToList()))
        {
            x -= Width(part);
            Put(part, new Rect2(x, top, Width(part), height), cutAtRight: false, z: 1); // drawn over the left group where they meet
            x -= Margin;
        }

        if (!ActionsLifted)
        {
            return null;
        }

        // Up a row, where they stood beside their neighbour towards the column's edge (End turn), over the strip's end that now fills it.
        var size = _bottom[HudPart.Actions].GetCombinedMinimumSize();
        var right = HudArrangement.SideOf(_layout, HudPart.Actions) == ScreenSide.Right;
        var lifted = new Rect2(right ? x - size.X : leftEnd, top - Margin - size.Y, size.X, size.Y);
        if (lifted.Intersects(column))
        {
            lifted.Position = new Vector2(right ? column.Position.X - Margin - size.X : column.End.X + Margin, lifted.Position.Y);
        }

        Put(HudPart.Actions, lifted, cutAtRight: false, z: 1);
        return lifted;
    }

    private void Put(HudPart part, Rect2 rect, bool cutAtRight, int z)
    {
        if (_bottom[part] is CutPart cut)
        {
            cut.Place(rect, cutAtRight);
            return;
        }

        FitChildInRect(_bottom[part], rect);
        _bottom[part].ZIndex = z;
    }
}
