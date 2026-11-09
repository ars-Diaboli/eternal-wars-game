using EternalWars.Core.Domain.Settings;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// Places the adventure map's HUD parts on the screen as <see cref="HudArrangement"/> says for the current <see cref="LayoutSettings"/>
/// (the "Layout" tab, ADR-156). It covers the screen but lets the mouse through; each part is a child that is moved, never reparented
/// (a part's event subscriptions last while it is in the tree). A stack in a corner is as wide as its widest part, and every part in it
/// fills that width. The turn, resources and status block stands at the top left, beside the top-left stack. Horizontal lists run along the
/// bottom between the bottom stacks. The bottom right leaves room for the menu button. It re-places the parts whenever one's size changes.
/// </summary>
public partial class AdventureHudLayout : Container
{
    /// <summary>The gap to the screen edge and between parts.</summary>
    public const float Margin = 8;

    /// <summary>Room left at the bottom right for the menu button (on its own layer).</summary>
    public const float MenuButtonHeight = 44;

    /// <summary>Width left at the bottom right for the menu button, for the bottom row.</summary>
    public const float MenuButtonWidth = 148;

    private readonly Dictionary<HudPart, Control> _parts = [];
    private Control? _info;
    private LayoutSettings _layout = LayoutSettings.Default;

    public HeroListPanel? HeroList { get; set; }

    public TownListPanel? TownList { get; set; }

    public AdventureHudLayout()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    /// <summary>The turn, resources and status block (top left).</summary>
    public void SetInfo(Control info)
    {
        _info = info;
        AddChild(info);
    }

    public void SetPart(HudPart part, Control control)
    {
        _parts[part] = control;
        AddChild(control);
    }

    /// <summary>Moves the parts to where <paramref name="layout"/> puts them.</summary>
    public void Apply(LayoutSettings layout)
    {
        _layout = layout;
        var horizontal = HudArrangement.ListsInBottomRow(layout);
        if (HeroList is not null && HeroList.Horizontal != horizontal)
        {
            HeroList.Horizontal = horizontal;
        }

        if (TownList is not null && TownList.Horizontal != horizontal)
        {
            TownList.Horizontal = horizontal;
        }

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
        var stackWidths = new Dictionary<ScreenCorner, float>();
        foreach (var corner in Enum.GetValues<ScreenCorner>())
        {
            var parts = HudArrangement.Stack(_layout, corner).Where(_parts.ContainsKey).Select(p => _parts[p]).ToList();
            var width = parts.Count == 0 ? 0 : parts.Max(p => p.GetCombinedMinimumSize().X);
            stackWidths[corner] = width;
            var left = HudArrangement.IsLeft(corner);
            var x = left ? Margin : screen.X - Margin - width;
            if (HudArrangement.IsTop(corner))
            {
                var y = Margin;
                foreach (var part in parts)
                {
                    var height = part.GetCombinedMinimumSize().Y;
                    FitChildInRect(part, new Rect2(x, y, width, height));
                    y += height + Margin;
                }
            }
            else
            {
                var y = screen.Y - Margin - (left ? 0 : MenuButtonHeight);
                foreach (var part in parts)
                {
                    var height = part.GetCombinedMinimumSize().Y;
                    y -= height;
                    FitChildInRect(part, new Rect2(x, y, width, height));
                    y -= Margin;
                }
            }
        }

        if (_info is not null)
        {
            var x = Margin + (stackWidths[ScreenCorner.TopLeft] > 0 ? stackWidths[ScreenCorner.TopLeft] + Margin : 0);
            FitChildInRect(_info, new Rect2(new Vector2(x, Margin), _info.GetCombinedMinimumSize()));
        }

        if (HudArrangement.ListsInBottomRow(_layout) && _parts.TryGetValue(HudPart.Lists, out var lists))
        {
            var left = Margin + (stackWidths[ScreenCorner.BottomLeft] > 0 ? stackWidths[ScreenCorner.BottomLeft] + Margin : 0);
            var right = screen.X - Margin - Math.Max(MenuButtonWidth, stackWidths[ScreenCorner.BottomRight] + Margin);
            var size = lists.GetCombinedMinimumSize();
            FitChildInRect(lists, new Rect2(left, screen.Y - Margin - size.Y, Math.Max(size.X, right - left), size.Y));
        }
    }
}
