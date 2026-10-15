using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Core.Events;

namespace EternalWars.Core.Tests.Domain;

public class TurnServiceTests
{
    [Fact]
    public void StartGame_PublishesStartSequenceAndGrantsIncomeToFirstPlayer()
    {
        var session = new GameSession(DemoScenario.Create());
        var events = new List<IEvent>();
        session.Events.Subscribe<IEvent>(events.Add);

        session.Turns.StartGame();

        Assert.IsType<GameStarted>(events[0]);
        Assert.IsType<DayStarted>(events[1]);
        Assert.Equal(new TurnStarted(new PlayerId(0), GameDate.First), events[^1]);
        Assert.Equal(500, session.State.Players[0].Resources.Gold);
        Assert.Equal(0, session.State.Players[1].Resources.Gold);
    }

    [Fact]
    public void EndTurn_ByLastPlayer_AdvancesDay()
    {
        var session = new GameSession(DemoScenario.Create());
        session.Turns.StartGame();
        var days = new List<GameDate>();
        session.Events.Subscribe<DayStarted>(e => days.Add(e.Date));

        session.Turns.EndTurn(); // Blue -> Red, same day
        session.Turns.EndTurn(); // Red -> Blue, next day

        Assert.Equal([new GameDate(2)], days);
        Assert.Equal(new PlayerId(0), session.State.CurrentPlayer.Id);
    }

    [Fact]
    public void WeekStarted_IsPublishedOnDayEight()
    {
        var session = new GameSession(DemoScenario.Create());
        session.Turns.StartGame();
        var weeks = new List<GameDate>();
        session.Events.Subscribe<WeekStarted>(e => weeks.Add(e.Date));

        for (var i = 0; i < 7 * session.State.Players.Count; i++)
        {
            session.Turns.EndTurn();
        }

        Assert.Equal([new GameDate(8)], weeks);
        Assert.Equal(2, session.State.Date.Week);
        Assert.Equal(1, session.State.Date.Day);
    }

    [Theory]
    [InlineData(1, 1, 1, 1)]
    [InlineData(7, 7, 1, 1)]
    [InlineData(8, 1, 2, 1)]
    [InlineData(29, 1, 1, 2)]
    public void GameDate_ComputesCalendar(int absoluteDay, int day, int week, int month)
    {
        var date = new GameDate(absoluteDay);

        Assert.Equal((day, week, month), (date.Day, date.Week, date.Month));
    }
}
