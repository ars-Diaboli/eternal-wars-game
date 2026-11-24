using EternalWars.Core.Domain.Game;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// The status bar (ADR-186, ADR-204, ADR-309): a narrow stripe along the bottom of the screen, as wide as the screen up to 16:9 and a centred
/// 16:9 width on a wider screen. On the left the subject: the icon and name of what the pointer is over (a HUD control's icon and label, or
/// the map's cell through <see cref="ShowSubject"/>), then a divider; then the detail (<see cref="Message"/>): the subject's description (a
/// control's tooltip, the map's wording by ownership) while the pointer is over it, else the latest message; the hotkey hints are the first.
/// On the right the calendar (the adventure map's, ADR-225), then <see cref="RightReserve"/> left free for the app's menu button
/// (<see cref="MenuSlot"/>). The resources are on the adventure map's top bar (<see cref="ResourceTopBar"/>). The adventure map, the battle
/// and the screens before a game each have one.
/// </summary>
public partial class StatusBar : PanelContainer
{
    /// <summary>The bar's height: one line of text with its icons.</summary>
    public const float Height = 32;

    /// <summary>The width of the menu button on the bar's right end.</summary>
    public const float MenuButtonWidth = Height + 8; // a square icon button (ADR-204)

    /// <summary>The widest the bar gets, as a share of the screen's height: 16:9.</summary>
    private const float MaxAspect = 16f / 9f;

    private readonly HBoxContainer _subject = new() { Name = "Subject", Visible = false, MouseFilter = MouseFilterEnum.Ignore };

    private readonly TextureRect _subjectIcon = new()
    {
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        CustomMinimumSize = new Vector2(UiIcons.StatusBarSize, UiIcons.StatusBarSize),
        MouseFilter = MouseFilterEnum.Ignore,
    };

    private readonly Label _subjectName = new() { VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };

    /// <summary>The subject's icon and name, and the description it came with: what the pointer was last over.</summary>
    private (Texture2D? Icon, string? Name, string? Detail) _hover;

    /// <summary>The subject's name, as shown (empty when there is none).</summary>
    public string Subject => _subject.Visible ? _subjectName.Text : "";

    /// <summary>The subject was last set by the pointer over a HUD control (or over none of them), not by <see cref="ShowSubject"/>.</summary>
    public bool ControlSubject { get; private set; }

    /// <summary>The detail: the latest message or the subject's description; the HUD and the map input write their messages here.</summary>
    public Label Message { get; } = new()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
        VerticalAlignment = VerticalAlignment.Center,
        ClipText = true,
        TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
        MouseFilter = MouseFilterEnum.Pass,
    };

    /// <summary>Room kept free below the bar (the replay viewer's bar, ADR-164).</summary>
    public float BottomOffset
    {
        get => _bottomOffset;
        set
        {
            _bottomOffset = value;
            if (IsInsideTree())
            {
                Place();
            }
        }
    }

    private float _bottomOffset;

    /// <summary>Width kept free at the bar's right end, for the menu button the app puts there.</summary>
    public float RightReserve { get; set; }

    /// <summary>Where the menu button goes on a bar at the bottom edge of <paramref name="screen"/>: its right end, <see cref="MenuButtonWidth"/> wide.</summary>
    public static Rect2 MenuSlot(Vector2 screen)
    {
        var bar = Bounds(screen, 0);
        return new Rect2(bar.End.X - MenuButtonWidth, bar.Position.Y, MenuButtonWidth, Height);
    }

    /// <summary>The bar's place on <paramref name="screen"/>, <paramref name="bottomOffset"/> above the bottom edge.</summary>
    public static Rect2 Bounds(Vector2 screen, float bottomOffset)
    {
        var width = Math.Min(screen.X, screen.Y * MaxAspect);
        return new Rect2((screen.X - width) / 2, screen.Y - bottomOffset - Height, width, Height);
    }

    /// <param name="calendar">The game whose calendar shows on the right (<see cref="CalendarLabel"/>, ADR-225): the adventure map's bar; null for
    /// none (the battle, the screens before a game).</param>
    public void Bind(GameSession? calendar)
    {
        Name = "StatusBar";
        var style = UiTheme.Box(UiTheme.Background, UiTheme.Border, 2, borderWidth: 0);
        style.BorderWidthTop = 2;
        style.ContentMarginLeft = style.ContentMarginRight = UiSpacing.Gap;
        AddThemeStyleboxOverride("panel", style);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", UiSpacing.Section);
        AddChild(row);
        _subject.AddThemeConstantOverride("separation", UiSpacing.Cell);
        _subject.AddChild(_subjectIcon);
        _subject.AddChild(_subjectName);
        _subject.AddChild(new VSeparator { MouseFilter = MouseFilterEnum.Ignore });
        row.AddChild(_subject);
        row.AddChild(Message);
        if (calendar is not null)
        {
            var date = new CalendarLabel();
            row.AddChild(date);
            date.Bind(calendar);
        }

        if (RightReserve > 0)
        {
            row.AddChild(new Control { CustomMinimumSize = new Vector2(RightReserve - UiSpacing.Section, 0), MouseFilter = MouseFilterEnum.Ignore });
        }
    }

    /// <summary>
    /// Shows what the pointer is over: <paramref name="name"/> (after <paramref name="icon"/>) as the subject, null for nothing; and
    /// <paramref name="detail"/>, its description, as the detail, covering the latest message at once. A description leaves with its
    /// subject: the latest message (at first the hotkey hints) shows again, unless a newer one replaced the description meanwhile.
    /// </summary>
    public void ShowSubject(string? name, string? detail = null, Texture2D? icon = null)
    {
        ControlSubject = false;
        Show(name, detail, icon);
    }

    private void Show(string? name, string? detail, Texture2D? icon)
    {
        if (string.IsNullOrEmpty(name))
        {
            (name, icon) = (null, null);
        }

        if (_hover == (icon, name, detail))
        {
            return;
        }

        if (_described is null || Message.Text != _described)
        {
            (_message, _described) = (Message.Text, null); // the detail is a message, not the last subject's description
        }

        _hover = (icon, name, detail);
        _subject.Visible = name is not null;
        _subjectName.Text = name ?? "";
        _subjectIcon.Texture = icon;
        _subjectIcon.Visible = icon is not null;
        if (!string.IsNullOrEmpty(detail))
        {
            Message.Text = _described = detail;
        }
        else if (_described is not null)
        {
            (Message.Text, _described) = (_message, null);
        }
    }

    /// <summary>The latest message, which a subject's description covers while the pointer is over it.</summary>
    private string _message = "";

    /// <summary>The description in the detail, null when the detail is a message.</summary>
    private string? _described;

    public override void _EnterTree()
    {
        GetViewport().SizeChanged += Place; // not _Ready: the adventure HUD moves the bar to another layer (ADR-204)
        Place();
    }

    public override void _ExitTree() => GetViewport().SizeChanged -= Place;

    /// <summary>A control's label (an icon-only button's, ADR-204) and tooltip go to the status line as the pointer comes over it.</summary>
    public override void _Input(InputEvent @event)
    {
        if (!IsVisibleInTree() || @event is not InputEventMouseMotion || GetViewport().GuiGetHoveredControl() is not { } control)
        {
            return;
        }

        ControlSubject = true;
        for (Node? node = control; node is Control item; node = item.GetParent())
        {
            var (label, tip) = UiIcons.StatusParts(item);
            if (label.Length > 0 || tip.Length > 0)
            {
                Show(label, tip, label.Length > 0 ? (item as Button)?.Icon : null); // a tooltip with no label: the description alone
                return;
            }
        }

        Show(null, null, null); // over a control with nothing to say: the last subject leaves
    }

    private void Place()
    {
        var bounds = Bounds(GetViewportRect().Size, BottomOffset);
        Position = bounds.Position;
        Size = bounds.Size;
    }
}
