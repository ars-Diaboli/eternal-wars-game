using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.AI;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Pathfinding;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Tests.Scenarios;

/// <summary>The shipped two-player map of the slice (ADR-114): it validates, round-trips, and the AI plays it to a winner.</summary>
public class EmberMarchTests
{
    [Fact]
    public void TheMap_HasNoValidatorErrors_AndRoundTripsThroughTheMapFormat()
    {
        var state = EmberMarchScenario.Create();

        Assert.DoesNotContain(MapValidator.Validate(state), i => i.Severity == IssueSeverity.Error);
        var restored = TestFormat.RoundTrip(state);
        Assert.Equal(state.Objects.Count, restored.Objects.Count);
        Assert.Equal(EmberMarchScenario.Name, restored.Map.Name);
    }

    [Fact]
    public void HavenIsAPerson_InfernoTheComputer_EachWithItsTownAndAHero()
    {
        var state = EmberMarchScenario.Create();

        Assert.Equal([true, false], state.Players.Select(p => p.IsHuman));
        Assert.Equal(
            [(TownIds.Haven, state.Players[0].Id), (TownIds.Inferno, state.Players[1].Id)],
            state.ObjectsOf<Town>().OrderBy(t => t.Id.Value).Select(t => (t.TownType, t.Owner!.Value)));
        Assert.All(state.Players, p => Assert.Single(state.HeroesOf(p.Id)));
    }

    [Fact]
    public void EachHero_CanReachTheOtherTownsGate_OnlyThroughTheGuardedPasses()
    {
        var state = EmberMarchScenario.Create();
        var blue = state.HeroesOf(state.Players[0].Id).Single();
        var ashgate = state.ObjectsOf<Town>().Single(t => t.TownType == TownIds.Inferno);
        Assert.Null(new PathFinder(state).FindPath(blue.Id, ashgate.VisitPosition, respectFog: false)); // the pass guards stand in the way

        foreach (var monster in state.ObjectsOf<MonsterGuard>().ToList())
        {
            state.RemoveObject(monster);
        }

        var path = new PathFinder(state).FindPath(blue.Id, ashgate.VisitPosition, respectFog: false);
        Assert.NotNull(path);
        Assert.Contains(path!.Steps, s => s.Position.X is EmberMarchScenario.RidgeLeft or EmberMarchScenario.RidgeLeft + 1);
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    public void TwoComputerPlayers_PlayItToAWinner_WithinSixtyDays(ulong seed)
    {
        var state = MapConverter.ToGameState(MapConverter.ToDocument(EmberMarchScenario.Create()), options: new GameOptions(Seed: seed));
        var session = new GameSession(state);
        var ai = new AdventureAi(session, controlHumanSlots: true);
        session.Start();
        while (!state.IsOver && state.Date.AbsoluteDay <= 60)
        {
            Assert.True(ai.PlayTurn(), $"the AI got stuck on {state.Date}");
        }

        Assert.True(state.Outcome is { HasWinner: true }, $"day {state.Date.AbsoluteDay}: no winner");
        Assert.Equal(0, ai.Refused);
    }
}
