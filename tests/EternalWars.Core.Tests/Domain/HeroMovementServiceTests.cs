using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Players;
using EternalWars.Core.Events;

namespace EternalWars.Core.Tests.Domain;

public class HeroMovementServiceTests
{
    private static readonly HeroId Blue = new(1);
    private static readonly HeroId Red = new(2);

    private static GameSession CreateSession()
    {
        var map = new AdventureMap(5, 5);
        map.SetTerrain(new MapPosition(3, 1), TerrainType.Water);
        map.SetTerrain(new MapPosition(1, 2), TerrainType.Swamp);
        var players = new[] { new Player(new PlayerId(0), "Blue", true), new Player(new PlayerId(1), "Red", true) };
        var heroes = new[]
        {
            new Hero(Blue, "Blue hero", players[0].Id, new MapPosition(2, 1)),
            new Hero(Red, "Red hero", players[1].Id, new MapPosition(2, 0)),
        };
        var session = new GameSession(new GameState(map, players, heroes));
        session.Turns.StartGame();
        return session;
    }

    [Fact]
    public void TryStep_OntoGrass_MovesAndPublishes()
    {
        var session = CreateSession();
        HeroMoved? moved = null;
        session.Events.Subscribe<HeroMoved>(e => moved = e);

        var result = session.Movement.TryStep(Blue, Direction.West);

        Assert.Equal(MoveResult.Moved, result);
        Assert.Equal(new HeroMoved(Blue, new MapPosition(2, 1), new MapPosition(1, 1), 1400), moved);
    }

    [Fact]
    public void TryStep_Diagonally_OntoSwamp_CostsMore()
    {
        var session = CreateSession();

        session.Movement.TryStep(Blue, Direction.SouthWest);

        Assert.Equal(1500 - 247, session.State.GetHero(Blue).MovementPoints);
    }

    [Theory]
    [InlineData(Direction.East, MoveResult.Impassable)]
    [InlineData(Direction.North, MoveResult.Occupied)]
    public void TryStep_RejectsInvalidTargets(Direction direction, MoveResult expected)
    {
        var session = CreateSession();

        Assert.Equal(expected, session.Movement.TryStep(Blue, direction));
        Assert.Equal(new MapPosition(2, 1), session.State.GetHero(Blue).Position);
    }

    [Fact]
    public void TryStep_OutOfTurn_IsRejected()
    {
        var session = CreateSession();

        Assert.Equal(MoveResult.NotYourTurn, session.Movement.TryStep(Red, Direction.West));
    }

    [Fact]
    public void MovementPoints_AreRestoredAtOwnersNextTurn()
    {
        var session = CreateSession();
        session.Movement.TryStep(Blue, Direction.West);

        session.Turns.EndTurn();
        session.Turns.EndTurn();

        Assert.Equal(Hero.DefaultMaxMovementPoints, session.State.GetHero(Blue).MovementPoints);
    }
}
