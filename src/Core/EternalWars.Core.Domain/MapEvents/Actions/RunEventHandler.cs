using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;

namespace EternalWars.Core.Domain.MapEvents.Actions;

/// <summary>
/// Run event: another enabled event's actions run here (not a quest's); runs nested deeper than <see cref="MapEventService.MaxRunDepth"/> are
/// skipped.
/// </summary>
internal sealed class RunEventHandler : MapEventActionHandler<RunEventAction>
{
    public override bool IsReward => false;

    protected override bool Apply(MapEventActionContext context, RunEventAction action)
    {
        var run = context.Run;
        if (context.Data.Set.Find(action.Event) is { Kind: not MapEventKind.Quest } other && context.Data.IsEnabled(other.Name)
            && run.Frames.Count(f => f.Event is not null) < MapEventService.MaxRunDepth)
        {
            run.Frames.Push(new MapEventFrame(other.Actions) { Event = other });
            context.Events.Publish(new MapEventRan(other.Name, other.Kind, run.Player, run.Hero, run.Town));
        }

        return true;
    }

    protected override string Line(RunEventAction action, GameState? state) => $"Run the event '{action.Event}'";

    protected override EventWorth Worth(IMapEventValuer valuer, RunEventAction action) =>
        valuer.State.MapEvents.Set.Find(action.Event) is { Kind: not MapEventKind.Quest } other && valuer.State.MapEvents.IsEnabled(other.Name)
            ? EventWorth.Gain(valuer.RunWorth(other.Actions))
            : EventWorth.Nothing;
}
