using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Cursors;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Godot.Shared.Events;
using EternalWars.Godot.Shared.Map;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// End turn (ADR-204, ADR-309, ADR-310): an hourglass with no label (its name and key, "End turn [Z]", on the status bar) and no frame of its
/// own (the tile's frame is the button's), ringed by seven dots for the days of the week, the first at the top and on clockwise, today's lit
/// in the colour of the player whose HUD it is. The hourglass brightens under the pointer and greys while it is not this screen's player's turn.
/// The adventure HUD asks first when a hero can still move (<see cref="EndTurnPressed"/>); without a listener it ends the turn at once.
/// The calendar is on the status bar (<see cref="CalendarLabel"/>, ADR-225).
/// </summary>
public partial class TurnPanel : BoxContainer
{
    private Button? _endTurn;
    private readonly WeekDots _dots = new();
    private GameSession _session = null!;
    private Func<bool> _mine = () => true;
    private Func<PlayerId> _listed = null!;

    /// <summary>The End turn button; null before <see cref="Bind"/>.</summary>
    public Button? EndTurn => _endTurn;

    /// <summary>The button was pressed: the HUD asks first when a hero can still move, then calls <see cref="EndTurnNow"/>.</summary>
    public event Action? EndTurnPressed;

    public TurnPanel()
    {
        Name = "EndTurn";
        Vertical = true;
        AddThemeConstantOverride("separation", UiSpacing.Cell);
    }

    /// <summary>
    /// <paramref name="endTurn"/> false leaves out the End turn button (the replay viewer, ADR-164); <paramref name="mine"/> says whether the
    /// turn under way is this screen's player's (default: always); <paramref name="listed"/> is whose colour lights today's dot (default: the
    /// current player's).
    /// </summary>
    public void Bind(GameSession session, bool endTurn = true, Func<bool>? mine = null, Func<PlayerId>? listed = null)
    {
        _session = session;
        _mine = mine ?? (() => true);
        _listed = listed ?? (() => session.State.CurrentPlayer.Id);
        _endTurn = new Button { Text = AdventureHotkeys.Of(AdventureAction.EndTurn).Hint, Visible = endTurn, FocusMode = FocusModeEnum.None }
            .WithIcon(UiIcon.EndTurn, UiIcons.BarSize);
        _endTurn.SizeFlagsHorizontal = SizeFlags.Fill;
        _endTurn.SizeFlagsVertical = SizeFlags.ExpandFill;
        _endTurn.CustomMinimumSize = new Vector2(WeekDots.Side, WeekDots.Side); // room for the ring round the hourglass
        foreach (var state in (string[])["normal", "hover", "pressed", "hover_pressed", "disabled", "focus"])
        {
            _endTurn.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        }

        _endTurn.AddThemeColorOverride("icon_normal_color", new Color(0.88f, 0.88f, 0.88f));
        _endTurn.AddThemeColorOverride("icon_hover_color", Colors.White);
        _endTurn.AddThemeColorOverride("icon_pressed_color", new Color(0.7f, 0.7f, 0.7f));
        _endTurn.AddThemeColorOverride("icon_hover_pressed_color", new Color(0.7f, 0.7f, 0.7f));
        _endTurn.AddThemeColorOverride("icon_disabled_color", new Color(1, 1, 1, 0.4f));
        _endTurn.Pressed += () =>
        {
            if (EndTurnPressed is { } ask)
            {
                ask();
            }
            else
            {
                EndTurnNow();
            }
        };
        AddChild(_endTurn);
        _endTurn.AddChild(_dots); // over the button, round its hourglass
        _dots.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Refresh();
        this.SubscribeWhileInTree<TurnStarted>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<DayStarted>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<SimultaneousDayStarted>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<SimultaneousDaysEnded>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<PlayerHandedToComputer>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<PlayerHandedToPerson>(session.Events, _ => Refresh());
    }

    /// <summary>Ends the current player's turn.</summary>
    public void EndTurnNow() => _session.Execute(new EndTurnCommand(_session.State.CurrentPlayer.Id));

    /// <summary>Whether End turn can be pressed now: this screen's player's turn.</summary>
    public bool CanEndTurn => _endTurn is { Visible: true } && _mine();

    private void Refresh()
    {
        if (_endTurn is not null)
        {
            _endTurn.Disabled = !_mine();
            _endTurn.TooltipText = (_mine() ? "Ends the turn; asks first while a hero can still move" : "Not your turn")
                + $" (day {_session.State.Date.Day} of the week)";
        }

        _dots.Today = _session.State.Date.Day;
        _dots.Lit = Palette.ForPlayer(_session.State, _listed());
    }

    /// <summary>The week's seven dots on a ring round the hourglass, the first at the top and on clockwise, today's lit.</summary>
    private sealed partial class WeekDots : Control
    {
        private const float Radius = 3.5f;

        /// <summary>How far in from the button's edge the dots' centres lie.</summary>
        private const float Inset = Radius + 5;

        /// <summary>The button's least side: the hourglass, a gap, and a dot on each side.</summary>
        public const float Side = UiIcons.BarSize + (4 * Inset);

        public WeekDots()
        {
            MouseFilter = MouseFilterEnum.Ignore; // the button under it takes the clicks
        }

        public int Today
        {
            get;
            set
            {
                field = value;
                QueueRedraw();
            }
        }

        public Color Lit
        {
            get;
            set
            {
                field = value;
                QueueRedraw();
            }
        }

        public override void _Draw()
        {
            var middle = Size / 2;
            var ring = (Math.Min(Size.X, Size.Y) / 2) - Inset;
            for (var day = 1; day <= GameDate.DaysPerWeek; day++)
            {
                var angle = (-Mathf.Pi / 2) + (Mathf.Tau * (day - 1) / GameDate.DaysPerWeek);
                var centre = middle + (ring * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)));
                DrawCircle(centre, Radius + 1, Colors.Black);
                DrawCircle(centre, Radius, day == Today ? Lit : UiTheme.BorderDim);
            }
        }
    }
}
