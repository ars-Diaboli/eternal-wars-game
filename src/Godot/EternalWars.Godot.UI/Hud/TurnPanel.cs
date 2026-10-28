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

    public void Bind(GameSession session)
    {
        AddThemeConstantOverride("separation", 16);
        AddChild(_date);
        AddChild(_player);

        var endTurn = new Button { Text = "End turn" };
        endTurn.Pressed += () => session.Execute(new EndTurnCommand(session.State.CurrentPlayer.Id));
        AddChild(endTurn);

        Refresh(session);
        this.SubscribeWhileInTree<TurnStarted>(session.Events, _ => Refresh(session));
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
