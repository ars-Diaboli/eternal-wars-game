using EternalWars.Core.Domain.MapEvents;
using EternalWars.Core.Domain.MapEvents.Actions;

namespace EternalWars.Core.Tests.Domain;

/// <summary>
/// Every map event action type has its one handler (<see cref="MapEventActionHandlers"/>): the service runs actions and shows a message's rewards
/// through it, so an action left out fails here, not in an event.
/// </summary>
public sealed class MapEventActionHandlersTests
{
    private static readonly Type[] ActionTypes =
        [.. typeof(EventAction).Assembly.GetTypes().Where(t => !t.IsAbstract && t.IsAssignableTo(typeof(EventAction))).OrderBy(t => t.FullName, StringComparer.Ordinal)];

    public static TheoryData<string> Actions() => [.. ActionTypes.Select(t => t.FullName!)];

    [Theory]
    [MemberData(nameof(Actions))]
    public void EveryActionType_HasExactlyOneHandler(string action)
    {
        var type = ActionTypes.Single(t => t.FullName == action);
        Assert.Single(MapEventActionHandlers.All, h => h.Action == type);
    }

    [Fact]
    public void TheRegistry_HoldsNothingElse() =>
        Assert.Equal(ActionTypes, MapEventActionHandlers.All.Select(h => h.Action).OrderBy(t => t.FullName, StringComparer.Ordinal));

    [Fact]
    public void AnAction_FindsItsHandler()
    {
        Assert.Equal(typeof(StopAction), MapEventActionHandlers.For(new StopAction()).Action);
        Assert.Equal(typeof(RevealAction), MapEventActionHandlers.For(new RevealAction(1, 2, 0, 3)).Action);
    }
}
