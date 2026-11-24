using EternalWars.Core.Domain.Settings;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// Places the adventure map's HUD parts: a framed column (ADR-191) down the minimap's side of the screen (<see cref="HudArrangement"/>; the right,
/// or the left in the mirrored layout, ADR-309), from the top to the status bar, holding the minimap, the hero and town lists and the action
/// buttons from the top, and the turn block at its bottom (ADR-204). The column is as wide as its widest part, and every part fills that width;
/// it takes the clicks on it. The layer covers the screen but lets the mouse through; each part is a child that is moved, never reparented (a
/// part's event subscriptions last while it is in the tree). It re-places the parts whenever one's size changes. The arrangement of ADR-309
/// replaces this column in the plan's step 3.
/// </summary>
public partial class AdventureHudLayout : Container
{
    /// <summary>The gap to the screen edge and between parts.</summary>
    public const float Margin = UiSpacing.ScreenEdge;

    private readonly List<Control> _column = [];
    private readonly Panel _sidePanel = new() { Name = "SidePanel", MouseFilter = MouseFilterEnum.Stop };
    private Control? _info;
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

    /// <summary>The turn block, at the column's bottom.</summary>
    public void SetInfo(Control info)
    {
        _info = info;
        AddChild(info);
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
        FitChildInRect(_sidePanel, new Rect2(panelX, 0, panelWidth, screen.Y - _bottomReserve));

        var x = panelX + Margin;
        var y = Margin;
        foreach (var part in _column)
        {
            var height = part.GetCombinedMinimumSize().Y;
            FitChildInRect(part, new Rect2(x, y, width, height));
            y += height + Margin;
        }

        if (_info is not null)
        {
            _info.Size = new Vector2(width, 0); // the wrapping lines take their height at this width
            var height = _info.GetCombinedMinimumSize().Y;
            FitChildInRect(_info, new Rect2(x, screen.Y - _bottomReserve - Margin - height, width, height));
        }
    }
}
