using EternalWars.Godot.UI.Hud;
using Godot;

namespace EternalWars.Godot.UI.Battle;

/// <summary>A part of the battle HUD that <see cref="BattleHudLayout"/> places (ADR-314).</summary>
public enum BattleHudPart
{
    /// <summary>The attacker's plate, top left.</summary>
    AttackerPlate,

    /// <summary>The defender's plate, top right.</summary>
    DefenderPlate,

    /// <summary>The round plate, top centre; the turn timer's pill stands under it.</summary>
    RoundPlate,

    /// <summary>The stack panel, bottom left.</summary>
    StackPanel,

    /// <summary>The timeline: the bottom row's middle, or under the round plate.</summary>
    Timeline,

    /// <summary>The action grid, bottom right.</summary>
    Actions,

    /// <summary>The combat log, above the action grid.</summary>
    Log,

    /// <summary>"Start battle" during the deployment step, in the middle of the bottom row.</summary>
    Start,
}

/// <summary>
/// Places the battle HUD's parts (ADR-314): the attacker's plate top left, the defender's top right and the round plate top centre (as tall as
/// the map's turn banner, so the turn timer's pill stands under it as it does there); a bottom row above the status bar of panels of one
/// height, its tallest part's: the stack panel at the left, the action grid at the right and the timeline filling the room between them, or,
/// with <see cref="TimelineOnTop"/> (the Layout tab), under the round plate and the timer, between the plates. "Start battle" stands in the
/// middle of the bottom row, the log above the action grid, as wide as it or wider. The layer covers the screen and lets the mouse through;
/// each part is a child moved, never reparented (a part's event subscriptions last while it is in the tree), placed again whenever one's size
/// changes.
/// </summary>
public partial class BattleHudLayout : Container
{
    /// <summary>The gap to the screen edge and between parts.</summary>
    public const float Margin = UiSpacing.ScreenEdge;

    /// <summary>The round plate's height: the map's turn banner's, so the turn timer stands under it (<see cref="TurnTimerPanel"/>).</summary>
    public const float RoundPlateHeight = TurnBannerPanel.Height;

    /// <summary>The room under the round plate kept for the turn timer's pill when the timeline stands on top.</summary>
    public const float TimerRoom = 40;

    /// <summary>The log's least width, when the action grid is narrower.</summary>
    public const float LogWidth = 420;

    /// <summary>"Start battle"'s widest.</summary>
    public const float StartWidth = 360;

    private readonly Dictionary<BattleHudPart, Control> _parts = [];
    private bool _timelineOnTop;
    private float _bottomReserve;

    public BattleHudLayout()
    {
        Name = "BattleHudLayout";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    /// <summary>The timeline under the round plate (the Layout tab's "top"), else in the bottom row.</summary>
    public bool TimelineOnTop
    {
        get => _timelineOnTop;
        set
        {
            _timelineOnTop = value;
            QueueSort();
        }
    }

    /// <summary>Room kept free along the bottom edge (the status bar and the replay viewer's bar): the bottom row stands above it.</summary>
    public float BottomReserve
    {
        get => _bottomReserve;
        set
        {
            _bottomReserve = value;
            QueueSort();
        }
    }

    /// <summary>Where the parts stand, as last placed (for the smoke checks and the tests of the layout by eye).</summary>
    public IReadOnlyDictionary<BattleHudPart, Rect2> Placed => _placed;

    private readonly Dictionary<BattleHudPart, Rect2> _placed = [];

    /// <summary>Adds <paramref name="control"/> as <paramref name="part"/>.</summary>
    public void Set(BattleHudPart part, Control control)
    {
        _parts[part] = control;
        AddChild(control);
        control.MinimumSizeChanged += QueueSort;
    }

    public override void _Notification(int what)
    {
        if (what == NotificationSortChildren)
        {
            Place();
        }
    }

    private Vector2 Min(BattleHudPart part) => _parts.TryGetValue(part, out var c) ? c.GetCombinedMinimumSize() : Vector2.Zero;

    private void Put(BattleHudPart part, Rect2 rect)
    {
        if (_parts.TryGetValue(part, out var control))
        {
            FitChildInRect(control, rect);
            _placed[part] = rect;
        }
    }

    private void Place()
    {
        var screen = Size;

        // The plates and the round plate along the top.
        var attacker = new Rect2(new Vector2(Margin, Margin), Min(BattleHudPart.AttackerPlate));
        var defenderSize = Min(BattleHudPart.DefenderPlate);
        var defender = new Rect2(new Vector2(screen.X - Margin - defenderSize.X, Margin), defenderSize);
        Put(BattleHudPart.AttackerPlate, attacker);
        Put(BattleHudPart.DefenderPlate, defender);
        var roundWidth = Min(BattleHudPart.RoundPlate).X;
        Put(BattleHudPart.RoundPlate, new Rect2((screen.X - roundWidth) / 2, Margin, roundWidth, Math.Max(RoundPlateHeight, Min(BattleHudPart.RoundPlate).Y)));

        // The bottom row: one height, the tallest part's (the timeline counts only where it stands in the row).
        var parts = new List<BattleHudPart> { BattleHudPart.StackPanel, BattleHudPart.Actions, BattleHudPart.Start };
        if (!_timelineOnTop)
        {
            parts.Add(BattleHudPart.Timeline);
        }

        var height = parts.Select(p => Min(p).Y).DefaultIfEmpty(0).Max();
        var top = screen.Y - _bottomReserve - Margin - height;
        var stack = new Rect2(Margin, top, Min(BattleHudPart.StackPanel).X, height);
        var actionsWidth = Min(BattleHudPart.Actions).X;
        var actions = new Rect2(screen.X - Margin - actionsWidth, top, actionsWidth, height);
        Put(BattleHudPart.StackPanel, stack);
        Put(BattleHudPart.Actions, actions);
        var middleLeft = stack.End.X + Margin;
        var middleWidth = Math.Max(0, actions.Position.X - Margin - middleLeft);
        var startWidth = Math.Min(middleWidth, Math.Max(StartWidth, Min(BattleHudPart.Start).X));
        Put(BattleHudPart.Start, new Rect2(middleLeft + ((middleWidth - startWidth) / 2), top, startWidth, height));

        if (_timelineOnTop)
        {
            var left = attacker.End.X + Margin;
            var width = Math.Max(0, defender.Position.X - Margin - left);
            Put(BattleHudPart.Timeline, new Rect2(left, Margin + RoundPlateHeight + TimerRoom, width, Min(BattleHudPart.Timeline).Y));
        }
        else
        {
            Put(BattleHudPart.Timeline, new Rect2(middleLeft, top, middleWidth, height));
        }

        // The log above the action grid, at the right edge.
        var log = Min(BattleHudPart.Log);
        var logWidth = Math.Max(actionsWidth, LogWidth);
        Put(BattleHudPart.Log, new Rect2(screen.X - Margin - logWidth, top - Margin - log.Y, logWidth, log.Y));
    }
}
