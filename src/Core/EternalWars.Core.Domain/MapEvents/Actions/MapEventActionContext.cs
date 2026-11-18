using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.MapEvents.Actions;

/// <summary>
/// What an action handler works with: the service and its run, and the run's hero and town as they were when the action came up (null when the
/// event has none, or it has left the map).
/// </summary>
internal sealed class MapEventActionContext(MapEventService service, MapEventRun run, Hero? hero, Town? town)
{
    public MapEventService Service { get; } = service;

    public MapEventRun Run { get; } = run;

    public Hero? Hero { get; } = hero;

    public Town? Town { get; } = town;

    public GameState State => Service.State;

    public IEventPublisher Events => Service.Events;

    /// <summary>The events' state (<see cref="GameState.MapEvents"/>).</summary>
    public MapEventState Data => Service.Data;

    /// <summary>The expression's value for the run (it may draw from <see cref="GameState.Random"/>).</summary>
    public int Evaluate(EventExpr expr) => Service.Evaluate(Run, expr);

    /// <summary>Whether the condition holds for the run.</summary>
    public bool Test(EventCondition condition) => Service.Test(Run, condition);
}
