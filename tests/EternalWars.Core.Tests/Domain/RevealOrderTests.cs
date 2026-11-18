using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Magic;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>
/// A reveal is made before it is told (ADR-214): the rule reveals through <c>IFogRevealer</c>, then publishes <see cref="AreaRevealed"/> or
/// <see cref="SurroundingsRevealed"/>, which ask for nothing; and <see cref="TurnEnded"/> comes once the turn's end is recorded.
/// </summary>
public class RevealOrderTests
{
    [Fact]
    public void ViewTerrain_HasRevealedTheArea_BeforeAreaRevealedIsPublished()
    {
        var state = Create(new[] { "A............................." }.Concat(Enumerable.Repeat("..............................", 29)).Append(".............................B").ToArray());
        var hero = state.GetHero(A);
        hero.HasSpellBook = true;
        hero.Knowledge = 10;
        hero.Learn(AdventureMagic.ViewTerrain);
        var session = Start(state, revealAll: false);
        var edge = new MapPosition(12, 0);
        var order = Order(session);
        bool? exploredWhenTold = null;
        session.Events.Subscribe<AreaRevealed>(_ => exploredWhenTold = state.Fog.IsExplored(Blue, edge));

        session.Execute(new CastAdventureSpellCommand(Blue, A, AdventureMagic.ViewTerrain, null)).ThrowIfRejected();

        Assert.True(exploredWhenTold);
        Assert.InRange(order.IndexOf(nameof(TilesRevealed)), 0, order.IndexOf(nameof(AreaRevealed)) - 1);
    }

    [Fact]
    public void AWindowOfTheMagi_HasRevealedAroundTheEye_BeforeSurroundingsRevealedIsPublished()
    {
        var rows = Enumerable.Repeat(new string('.', 40), 12).ToArray();
        rows[2] = "...A" + new string('.', 36);
        rows[11] = new string('.', 39) + "B";
        var state = Create(rows);
        Add<MagiWindow>(state, BuiltInMapObjects.ColouredId(BuiltInMapObjects.MagiWindow, ObjectColour.Red), 2, 0); // entrance (3, 1)
        Add<MagiEye>(state, BuiltInMapObjects.ColouredId(BuiltInMapObjects.MagiEye, ObjectColour.Red), 30, 6);
        var session = Start(state, revealAll: false);
        var order = Order(session);
        bool? exploredWhenTold = null;
        session.Events.Subscribe<SurroundingsRevealed>(_ => exploredWhenTold = state.Fog.IsExplored(Blue, new MapPosition(30, 6)));

        session.Step(A, Direction.North);

        Assert.True(exploredWhenTold);
        Assert.InRange(order.IndexOf(nameof(TilesRevealed)), 0, order.IndexOf(nameof(SurroundingsRevealed)) - 1);
    }

    [Fact]
    public void TurnEnded_PrecedesTheNextTurn_WithTheEndingPlayerStillCurrent()
    {
        var session = Start(Create("A.B"));
        var state = session.State;
        var order = Order(session);
        PlayerId? currentWhenTold = null;
        session.Events.Subscribe<TurnEnded>(_ => currentWhenTold = state.CurrentPlayer.Id);

        session.EndTurn();

        Assert.Equal(Blue, currentWhenTold);
        Assert.InRange(order.IndexOf(nameof(TurnEnded)), 0, order.IndexOf(nameof(TurnStarted)) - 1);
    }

    private static List<string> Order(GameSession session)
    {
        var order = new List<string>();
        session.Events.Subscribe<IEvent>(e => order.Add(e.GetType().Name));
        return order;
    }
}
