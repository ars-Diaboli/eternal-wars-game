using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Events;
using EternalWars.Core.Generation;
using EternalWars.Core.Network;

namespace EternalWars.Core.Tests.Network;

/// <summary>Every event says who may learn of it, and a LAN host passes on only what it says (ADR-202).</summary>
public class EventAudienceTests
{
    private static readonly PlayerId[] NoBattle = [];

    public static IEnumerable<Type> EventTypes() =>
        new[] { typeof(GameState).Assembly, typeof(MapTemplate).Assembly, typeof(GameHost).Assembly }
            .SelectMany(a => a.GetTypes())
            .Where(t => typeof(IEvent).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false })
            .OrderBy(t => t.FullName);

    private static IEnumerable<Type> Declaring(Audience audience) =>
        EventTypes().Where(t => PlayerViewFeed.EventShape.Of(t).Audience == audience);

    [Fact]
    public void EveryEvent_DeclaresItsAudience()
    {
        var undeclared = EventTypes().Where(t => PlayerViewFeed.EventShape.Of(t).Audience is null).Select(t => t.Name);

        Assert.True(!undeclared.Any(), $"Add [SeenBy(Audience...)] to: {string.Join(", ", undeclared)}");
    }

    [Fact]
    public void EveryOwnerEvent_NamesItsPlayer()
    {
        var unnamed = Declaring(Audience.Owner).Where(t => PlayerViewFeed.EventShape.Of(t).Owner is null).Select(t => t.Name);

        Assert.True(!unnamed.Any(), $"An Owner event needs a PlayerId property named Player or Owner: {string.Join(", ", unnamed)}");
    }

    [Fact]
    public void EveryWitnessEvent_NamesAHeroOrAnObject()
    {
        var aboutNothing = Declaring(Audience.Witnesses).Where(t => PlayerViewFeed.EventShape.Of(t).Subjects.Length == 0).Select(t => t.Name);

        Assert.True(!aboutNothing.Any(), $"A Witnesses event needs a HeroId or MapObjectId property: {string.Join(", ", aboutNothing)}");
    }

    [Fact]
    public void TheSpecialEvents_AreTheOnesTheHostDecides()
    {
        Assert.Equal(PlayerViewFeed.SpecialEvents.OrderBy(t => t.Name), Declaring(Audience.Special).OrderBy(t => t.Name));
    }

    [Fact]
    public void AnEventWithoutAnAudience_ReachesNoClient()
    {
        var feed = new PlayerViewFeed(TestWorld.Create("A...B"), TestWorld.Blue);

        Assert.False(feed.Shows(new UndeclaredEvent(), NoBattle));
    }

    [Fact]
    public void TheHostShowsEachEvent_ToItsAudience()
    {
        var state = TestWorld.Create("A...B");
        var blue = new PlayerViewFeed(state, TestWorld.Blue);
        var red = new PlayerViewFeed(state, TestWorld.Red);

        var turn = new TurnStarted(TestWorld.Red, state.Date);
        Assert.True(blue.Shows(turn, NoBattle));
        Assert.True(red.Shows(turn, NoBattle));

        var revealed = new TilesRevealed(TestWorld.Red, [new MapPosition(4, 0)]);
        Assert.False(blue.Shows(revealed, NoBattle));
        Assert.True(red.Shows(revealed, NoBattle));

        var variable = new MapVariableChanged("secret", 1);
        Assert.False(blue.Shows(variable, NoBattle));
        Assert.False(red.Shows(variable, NoBattle));

        var round = new BattleRoundStarted(2);
        Assert.False(blue.Shows(round, NoBattle));
        Assert.True(blue.Shows(round, [TestWorld.Blue]));
    }

    private sealed record UndeclaredEvent : IEvent;
}
