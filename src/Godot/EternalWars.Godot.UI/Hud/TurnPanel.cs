using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Turns;
using EternalWars.Godot.Shared.Events;
using EternalWars.Godot.Shared.Map;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// Calendar with the week's event (ADR-073), active player and the End Turn button. In a row at the top of the map; with the side panel on
/// (<see cref="InPanel"/>) the calendar lines stack at the panel's bottom and the button stands with the panel's other actions (ADR-204).
/// </summary>
public partial class TurnPanel : BoxContainer
{
    private readonly Label _date = new();
    private readonly Label _player = new();
    private Button? _endTurn;

    /// <summary>The End turn button, which the HUD moves into the side panel's actions (<see cref="InPanel"/>); null before <see cref="Bind"/>.</summary>
    public Button? EndTurn => _endTurn;

    /// <summary>
    /// Whether the calendar stands at the bottom of the side panel: its lines one under another, wrapping to the panel's width; the End turn
    /// button is then the HUD's to place (<see cref="EndTurn"/>).
    /// </summary>
    public bool InPanel
    {
        get => Vertical;
        set
        {
            Vertical = value;
            var wrap = value ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off;
            _date.AutowrapMode = _player.AutowrapMode = wrap;
            AddThemeConstantOverride("separation", value ? 0 : UiSpacing.Section);
        }
    }

    /// <summary>Shows the session's date and player; <paramref name="endTurn"/> false leaves out the End turn button (the replay viewer, ADR-164).</summary>
    public void Bind(GameSession session, bool endTurn = true)
    {
        InPanel = false;
        AddChild(_date);
        AddChild(_player);

        _endTurn = new Button { Text = "End turn", Visible = endTurn }.WithIcon(UiIcon.EndTurn, UiIcons.BarSize);
        _endTurn.Pressed += () => session.Execute(new EndTurnCommand(session.State.CurrentPlayer.Id));
        AddChild(_endTurn);

        Refresh(session);
        this.SubscribeWhileInTree<TurnStarted>(session.Events, _ => Refresh(session));
        this.SubscribeWhileInTree<DayStarted>(session.Events, _ => Refresh(session)); // an arena's day 2 starts no turn (ADR-097)

        // A simultaneous day (ADR-197) opens every person's turn, then the host's colour is the current player; a turn ended moves it on.
        this.SubscribeWhileInTree<SimultaneousDayStarted>(session.Events, _ => Refresh(session));
        this.SubscribeWhileInTree<CommandExecuted>(session.Events, _ => Refresh(session));
    }

    private void Refresh(GameSession session)
    {
        var state = session.State;
        _date.Text = state.WeekEvent.Kind switch
        {
            WeekEventKind.WeekOfCreature or WeekEventKind.MonthOfCreature when state.Creatures.TryGet(state.WeekEvent.Creature!, out var creature) =>
                $"{state.Date} ({(state.WeekEvent.IsMonthly ? "month" : "week")} of the {creature.Name})",
            WeekEventKind.Plague => $"{state.Date} (plague)",
            WeekEventKind.Starvation => $"{state.Date} (starvation)",
            _ => state.Date.ToString(),
        };
        _player.Text = $"Turn: {state.CurrentPlayer.Name}";
        _player.AddThemeColorOverride("font_color", Palette.ForPlayer(state.CurrentPlayer.Id));
    }
}
