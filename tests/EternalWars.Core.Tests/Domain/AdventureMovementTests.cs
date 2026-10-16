using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>Click-to-move and object visits, end to end through <c>GameSession</c>.</summary>
public class AdventureMovementTests
{
    [Fact]
    public void MoveTo_PublishesOneHeroMovedPerStep()
    {
        var session = Start(Create("A....", "....B"));
        var steps = new List<HeroMoved>();
        session.Events.Subscribe<HeroMoved>(steps.Add);

        var outcome = session.MoveTo(A, new MapPosition(3, 0));

        Assert.Equal(new MovementOutcome(3, MoveResult.Moved, Arrived: true), outcome);
        Assert.Equal([1, 2, 3], steps.Select(s => s.To.X));
    }

    [Fact]
    public void MoveTo_StopsAtDayBoundaryAndContinuesNextTurn()
    {
        var session = Start(Create("A...................", "B..................."));
        var target = new MapPosition(19, 0);

        var first = session.MoveTo(A, target);
        session.EndTurn();
        session.EndTurn();
        var second = session.MoveTo(A, target);

        Assert.Equal(new MovementOutcome(15, MoveResult.NotEnoughMovementPoints, Arrived: false), first);
        Assert.Equal(new MovementOutcome(4, MoveResult.Moved, Arrived: true), second);
    }

    [Fact]
    public void MoveTo_UnreachableTarget_ReportsNoPath()
    {
        var session = Start(Create("A~.", "B~."));

        Assert.Equal(MoveResult.NoPath, session.MoveTo(A, new MapPosition(2, 0)).StopReason);
    }

    [Fact]
    public void VisitingResourcePile_GrantsResourcesAndRemovesPile()
    {
        var state = Create("A..", "..B");
        var pile = Add<ResourcePile>(state, BuiltInMapObjects.PileId(ResourceType.Lumber), 2, 0);
        var session = Start(state);
        var events = new List<IEvent>();
        session.Events.Subscribe<IEvent>(events.Add);

        session.MoveTo(A, pile.VisitPosition);

        Assert.Equal(7, session.State.GetPlayer(Blue).Resources.Lumber);
        Assert.Empty(session.State.Objects);
        Assert.Contains(new ResourcePileCollected(A, pile.Id, ResourceType.Lumber, 7), events);
        Assert.Contains(new MapObjectRemoved(pile.Id), events);
    }

    [Fact]
    public void CapturedMine_ProducesIncomeOnOwnersNextTurn()
    {
        var state = Create(
            "......",
            "......",
            "A....B");
        var mine = Add<Mine>(state, BuiltInMapObjects.MineId(ResourceType.Gold), 1, 0);
        var session = Start(state);
        MineOwnerChanged? captured = null;
        session.Events.Subscribe<MineOwnerChanged>(e => captured = e);

        session.MoveTo(A, mine.VisitPosition);
        session.EndTurn();
        session.EndTurn();

        Assert.Equal(new MineOwnerChanged(mine.Id, null, Blue), captured);
        Assert.Equal(1000, session.State.GetPlayer(Blue).Resources.Gold); // no base income, only the mine
    }

    [Theory]
    [InlineData(TreasureChestReward.Gold, 1500, 0)]
    [InlineData(TreasureChestReward.Experience, 0, 1000)]
    public void TreasureChest_AsksForChoiceAndBlocksUntilResolved(TreasureChestReward reward, int expectedGold, int expectedExperience)
    {
        var state = Create("A...", "...B");
        var chest = Add<TreasureChest>(state, BuiltInMapObjects.TreasureChest, 2, 0);
        chest.FixedValues = new ChestValues(1500, 1000);
        var session = Start(state);

        var outcome = session.MoveTo(A, chest.VisitPosition);

        Assert.Equal(MoveResult.DecisionPending, outcome.StopReason);
        Assert.IsType<TreasureChestDecision>(session.State.PendingDecision);
        Assert.Equal(MoveResult.DecisionPending, session.Step(A, Direction.East));
        Assert.Throws<InvalidOperationException>(session.EndTurn);

        session.ResolveChest(reward);

        // 1000 experience is level 2: a level-up choice follows the chest.
        Assert.Equal(reward == TreasureChestReward.Experience ? typeof(LevelUpDecision) : null, session.State.PendingDecision?.GetType());
        Assert.Empty(session.State.Objects);
        Assert.Equal(expectedGold, session.State.GetPlayer(Blue).Resources.Gold);
        Assert.Equal(expectedExperience, session.State.GetHero(A).Experience);
    }

    [Fact]
    public void Gate_TeleportsHeroToLinkedGateAndPathContinues()
    {
        var state = CreateLevels(
            ["A....", ".....", "....B"],
            [".....", ".....", "....."]);
        var surface = Add<TwoWayGate>(state, BuiltInMapObjects.SubterraneanGate, 2, 0);
        var underground = Add<TwoWayGate>(state, BuiltInMapObjects.SubterraneanGate, 0, 0, 1);
        Link(surface, underground);
        var session = Start(state);
        HeroTeleported? teleported = null;
        session.Events.Subscribe<HeroTeleported>(e => teleported = e);

        var outcome = session.MoveTo(A, new MapPosition(4, 2, 1));

        Assert.True(outcome.Arrived);
        Assert.Equal(new HeroTeleported(A, surface.VisitPosition, underground.VisitPosition), teleported);
        Assert.Equal(new MapPosition(4, 2, 1), session.State.GetHero(A).Position);
    }

    [Fact]
    public void Gate_WithOccupiedExit_DoesNotTeleport()
    {
        var state = CreateLevels(
            ["A....", ".....", "....."],
            [".....", ".....", "....."]);
        var surface = Add<TwoWayGate>(state, BuiltInMapObjects.SubterraneanGate, 2, 0);
        var underground = Add<TwoWayGate>(state, BuiltInMapObjects.SubterraneanGate, 0, 0, 1);
        Link(surface, underground);
        state.GetHero(A).Position = new MapPosition(2, 2);
        var session = Start(state);
        state.AddHero(new Hero(B, "Bravo", Red, underground.VisitPosition));

        session.Step(A, Direction.NorthEast);

        Assert.Equal(surface.VisitPosition, session.State.GetHero(A).Position);
    }
}
