using System.Reflection;
using EternalWars.Core.Domain.MapEvents;
using EternalWars.Core.Domain.MapEvents.Actions;

namespace EternalWars.Core.Tests.Domain;

/// <summary>
/// Every map event action type has its one handler (<see cref="MapEventActionHandlers"/>): the service runs actions and shows a message's rewards
/// through it, so an action left out fails here, not in an event. What an action holds is its handler's to tell (ADR-237), so a handler that
/// forgets a field of its action fails here too.
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

    public static TheoryData<string> Handlers() => [.. MapEventActionHandlers.All.Select(h => h.GetType().Name)];

    [Theory]
    [MemberData(nameof(Handlers))]
    public void ARewardWithAnAmount_DrawsItInFix(string name)
    {
        var handler = MapEventActionHandlers.All.Single(h => h.GetType().Name == name);
        if (handler.IsReward && Holds<EventExpr>(handler.Action))
        {
            Assert.True(Overrides(handler, "Fix"), $"{name} gives an amount but does not draw it in Fix: a message would show the formula.");
        }
    }

    [Theory]
    [MemberData(nameof(Handlers))]
    public void AHandler_TellsWhatItsActionHolds(string name)
    {
        var handler = MapEventActionHandlers.All.Single(h => h.GetType().Name == name);
        Assert.Equal(Holds<EventExpr>(handler.Action), Overrides(handler, "Expressions"));
        Assert.Equal(Holds<EventCondition>(handler.Action), Overrides(handler, "Conditions"));
        var lists = Holds<IReadOnlyList<EventAction>>(handler.Action) || handler.Action == typeof(QuestionAction);
        Assert.Equal(lists, Overrides(handler, "Lists"));
        Assert.Equal(lists, Overrides(handler, "ListNames"));
        Assert.Equal(lists, Overrides(handler, "WithLists"));
    }

    [Fact]
    public void AFixedReward_HoldsNoRandomNumber()
    {
        var game = new EternalWars.Core.Domain.Game.GameSession(TestWorld.Create("A.."));
        var reward = new ResourceAction(EternalWars.Core.Domain.Economy.ResourceType.Gold, new RandomExpr(100, 200));
        var context = new MapEventActionContext(game.MapEvents, new MapEventRun(new MapEventDefinition("test", MapEventKind.Player, [reward]), TestWorld.Blue, null, null, null, startOfTurn: false), null, null);

        var fixedReward = MapEventActionHandlers.For(reward).Fix(context, reward);

        var amount = Assert.IsType<NumberExpr>(Assert.Single(MapEventActionHandlers.For(fixedReward).Expressions(fixedReward)));
        Assert.InRange(amount.Value, 100, 200);
    }

    private static bool Holds<T>(Type action) => action.GetProperties().Any(p => p.PropertyType == typeof(T));

    private static bool Overrides(IMapEventActionHandler handler, string method) =>
        handler.GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Any(m => m.Name == method);
}
