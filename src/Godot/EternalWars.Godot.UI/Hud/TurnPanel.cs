using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Turns;
using EternalWars.Godot.Shared.Events;
using EternalWars.Godot.Shared.Map;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>Calendar with the week's event (ADR-073), active player and the End Turn button.</summary>
public partial class TurnPanel : HBoxContainer
{
    private readonly Label _date = new();
    private readonly Label _player = new();

    /// <summary>Shows the session's date and player; <paramref name="endTurn"/> false leaves out the End turn button (the replay viewer, ADR-164).</summary>
    public void Bind(GameSession session, bool endTurn = true)
    {
        AddThemeConstantOverride("separation", UiSpacing.Section);
        AddChild(_date);
        AddChild(_player);

        var button = new Button { Text = "End turn", Visible = endTurn }.WithIcon(UiIcon.Hourglass);
        button.Pressed += () => session.Execute(new EndTurnCommand(session.State.CurrentPlayer.Id));
        AddChild(button);

        Refresh(session);
        this.SubscribeWhileInTree<TurnStarted>(session.Events, _ => Refresh(session));
        this.SubscribeWhileInTree<DayStarted>(session.Events, _ => Refresh(session)); // an arena's day 2 starts no turn (ADR-097)
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
