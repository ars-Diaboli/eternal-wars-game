using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Turns;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// The calendar on the adventure map's status bar, right of the resources (ADR-225): the date short ("Month 1, Week 2, Day 3" as
/// <c>M1 W2 D3</c>), the date in full and the week's event (ADR-073) in its hover text, which the status line shows.
/// </summary>
public partial class CalendarLabel : Label
{
    public CalendarLabel()
    {
        Name = "Calendar";
        VerticalAlignment = VerticalAlignment.Center;
        MouseFilter = MouseFilterEnum.Pass;
    }

    public void Bind(GameSession session)
    {
        Refresh(session);
        this.SubscribeWhileInTree<TurnStarted>(session.Events, _ => Refresh(session));
        this.SubscribeWhileInTree<DayStarted>(session.Events, _ => Refresh(session)); // an arena's day 2 starts no turn (ADR-097)
        this.SubscribeWhileInTree<SimultaneousDayStarted>(session.Events, _ => Refresh(session));
        this.SubscribeWhileInTree<CommandExecuted>(session.Events, _ => Refresh(session));
    }

    /// <summary>"Month 1, Week 2, Day 3: week of the Griffin" (or without the event in an ordinary week).</summary>
    public static string Describe(GameState state)
    {
        var week = state.WeekEvent.Kind switch
        {
            WeekEventKind.WeekOfCreature or WeekEventKind.MonthOfCreature when state.WeekEvent.Creature is { } shown && state.Creatures.TryGet(shown, out var creature) =>
                $"{(state.WeekEvent.IsMonthly ? "month" : "week")} of the {creature.Name}",
            WeekEventKind.Plague => "plague",
            WeekEventKind.Starvation => "starvation",
            _ => null,
        };
        return week is null ? state.Date.ToString() : $"{state.Date}: {week}";
    }

    private void Refresh(GameSession session)
    {
        var date = session.State.Date;
        Text = $"M{date.Month} W{date.Week} D{date.Day}";
        TooltipText = Describe(session.State);
    }
}
