using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Network;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Network;

/// <summary>
/// A simultaneous day's conflict on a LAN host (MP-2, ADR-197): the day goes back to its start, from the host's record, and is played again turn
/// by turn in a new session.
/// </summary>
public class SimultaneousDayRewinderTests
{
    private static readonly GameOptions Simultaneous = new(FogMode.ShroudWithRefog) { SimultaneousFirstWeek = true };

    [Fact]
    public void WithoutAConflict_TheDayGoesOn()
    {
        var (session, recordOf) = Duel();

        Assert.Null(SimultaneousDayRewinder.PlayDayAgain(session, recordOf));
    }

    [Fact]
    public void AConflict_TakesTheDayBack_ToBePlayedAgainTurnByTurn()
    {
        var (session, recordOf) = Duel();
        var gold = session.State.GetPlayer(Blue).Resources.Gold;
        var reason = Conflict(session);

        var (again, announcement) = SimultaneousDayRewinder.PlayDayAgain(session, recordOf)!.Value;

        Assert.NotSame(session, again);
        Assert.False(again.State.IsSimultaneous);
        Assert.True(again.State.SimultaneousEnded);
        Assert.Equal(Blue, again.State.CurrentPlayer.Id);
        Assert.Equal(gold, again.State.GetPlayer(Blue).Resources.Gold); // the day's start, no second income
        Assert.Equal($"{reason} The day is played again, turn by turn, and so is every day after it.", announcement);
    }

    [Fact]
    public void AHostThatKeepsNoRecord_CannotTakeTheDayBack()
    {
        var (session, _) = Duel();
        Conflict(session);

        Assert.Null(SimultaneousDayRewinder.PlayDayAgain(session, null));
    }

    /// <summary>Blue's hero on the left, Red's on the right, their start zones 8 tiles wide with one tile between.</summary>
    private static (GameSession Session, Func<string?, GameRecordDocument> RecordOf) Duel()
    {
        const int zoneWidth = 8;
        var width = (2 * zoneWidth) + 1;
        var state = Create(Simultaneous with { HostPlayer = Blue }, "A" + new string('.', width - 2) + "B", new string('.', width));
        for (var y = 0; y < 2; y++)
        {
            for (var x = 0; x < zoneWidth; x++)
            {
                state.Map.SetStartZone(new MapPosition(x, y), Blue);
                state.Map.SetStartZone(new MapPosition(width - 1 - x, y), Red);
            }
        }

        var initial = MapConverter.ToDocument(state);
        var session = new GameSession(state);
        session.Start();
        return (session, name => GameReplay.Record(initial, session, name));
    }

    /// <summary>Blue's hero walks into Red's start zone; returns the conflict.</summary>
    private static string Conflict(GameSession session)
    {
        while (session.State.SimultaneousConflict is null
               && session.Execute(new StepHeroCommand(Blue, A, Direction.East)) is { IsAccepted: true, Outcome: MoveResult.Moved })
        {
        }

        return Assert.IsType<string>(session.State.SimultaneousConflict);
    }
}
