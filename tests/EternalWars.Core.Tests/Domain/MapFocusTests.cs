using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>
/// Where the adventure map's camera goes and which hero is selected (ADR-090, ADR-164, ADR-231): a computer hero walking in sight, a map
/// event's place, a teleport to another level, a turn shown, the selected hero defeated.
/// </summary>
public class MapFocusTests
{
    private static readonly MapPosition Near = new(1, 0);
    private static readonly MapPosition Below = new(1, 0, 1);

    [Fact]
    public void AComputerHeroWalkingInSight_IsFollowedOnce_UntilATurnStarts()
    {
        var (focus, session) = Focus(redIsComputer: true);
        var red = session.State.GetHero(B);

        Assert.True(focus.Follows(red, Near, showComputerHeroes: true, levelShown: 0, _ => true));
        Assert.False(focus.Follows(red, Near, showComputerHeroes: true, levelShown: 0, _ => true)); // followed already

        focus.TurnStarted();
        Assert.True(focus.Follows(red, Near, showComputerHeroes: true, levelShown: 0, _ => true));
    }

    [Fact]
    public void AGame_FollowsNoPersonsHero_NorOneOutOfSightOrOnAnotherLevel_NorAnyWhenComputerHeroesAreNotShown()
    {
        var (focus, session) = Focus(redIsComputer: true);
        var red = session.State.GetHero(B);

        Assert.False(focus.Follows(session.State.GetHero(A), Near, showComputerHeroes: true, levelShown: 0, _ => true)); // Blue is a person
        Assert.False(focus.Follows(red, Near, showComputerHeroes: false, levelShown: 0, _ => true));
        Assert.False(focus.Follows(red, Below, showComputerHeroes: true, levelShown: 0, _ => true));
        Assert.False(focus.Follows(red, Near, showComputerHeroes: true, levelShown: 0, _ => false));
    }

    [Fact]
    public void TheReplayViewer_FollowsEveryHeroInSight_OnEitherLevel()
    {
        var (focus, session) = Focus(redIsComputer: false, watching: true);

        Assert.True(focus.Follows(session.State.GetHero(A), Below, showComputerHeroes: false, levelShown: 0, _ => true));
        Assert.False(focus.Follows(session.State.GetHero(B), Near, showComputerHeroes: false, levelShown: 0, _ => false));
    }

    [Fact]
    public void AMapEventsPlace_IsShown_WhenTheCurrentPlayerPlaysHere_AndTheComputersMovesAreShown()
    {
        var (focus, session) = Focus(redIsComputer: false);
        var place = new MapPosition(2, 0);
        var decision = new MapEventDecision(MapEventDecision.NoHero, "e", "A message.", [], []) { Place = place };

        Assert.Equal(place, focus.EventPlace(decision, showingComputerMoves: false));
        Assert.Null(focus.EventPlace(decision, showingComputerMoves: true));
        Assert.Null(focus.EventPlace(decision with { Place = null }, showingComputerMoves: false));
        Assert.Null(focus.EventPlace(null, showingComputerMoves: false));

        var elsewhere = new MapFocus(session, new GameDirector(session, new LocalSeats { PlaysElsewhere = p => p == Blue }));
        Assert.Null(elsewhere.EventPlace(decision, showingComputerMoves: false)); // Blue's turn, played on another computer
    }

    [Fact]
    public void TheCamera_FollowsTheSelectedHero_ThroughATeleportToAnotherLevel()
    {
        var teleport = new HeroTeleported(A, Near, Below);

        Assert.Equal(Below, MapFocus.AfterTeleport(teleport, selected: A, levelShown: 0));
        Assert.Null(MapFocus.AfterTeleport(teleport, selected: A, levelShown: 1)); // the level shown already
        Assert.Null(MapFocus.AfterTeleport(teleport, selected: B, levelShown: 0));
    }

    [Fact]
    public void ATurnShown_SelectsItsPlayersFirstHero_UnlessTheTurnHasPassedOn()
    {
        var (focus, session) = Focus(redIsComputer: false);

        Assert.Equal(A, focus.FirstHeroOf(Blue));
        Assert.True(focus.StillTheTurnOf(Blue));
        session.Execute(new EndTurnCommand(Blue)).ThrowIfRejected();
        Assert.False(focus.StillTheTurnOf(Blue));
    }

    [Fact]
    public void TheSelectedHeroDefeated_SelectsTheCurrentPlayersFirstHero()
    {
        var (focus, _) = Focus(redIsComputer: false);

        Assert.True(focus.Reselects(new HeroDefeated(B, Red, Retreated: false), selected: B, out var next));
        Assert.Equal(A, next); // Blue's turn
        Assert.False(focus.Reselects(new HeroDefeated(B, Red, Retreated: false), selected: A, out _));
    }

    private static (MapFocus Focus, GameSession Session) Focus(bool redIsComputer, bool watching = false)
    {
        var state = CreateLevels(["A...B"], ["....."]);
        state.GetPlayer(Red).IsHuman = !redIsComputer;
        var session = Start(state);
        return (new MapFocus(session, new GameDirector(session, watching: watching), watching), session);
    }
}
