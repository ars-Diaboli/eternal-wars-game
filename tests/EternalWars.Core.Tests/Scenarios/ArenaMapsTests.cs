using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.AI;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Pathfinding;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;

namespace EternalWars.Core.Tests.Scenarios;

/// <summary>The three shipped arena maps (ADR-180): each validates, keeps the enemies apart on day 1, and two computer players fight its battle.</summary>
public class ArenaMapsTests
{
    public static TheoryData<string> Maps() => [.. ArenaScenarios.All.Select(m => m.Name)];

    private static GameState Create(string name) => ArenaScenarios.All.Single(m => m.Name == name).Create(null);

    /// <summary>The map as the new-game screen starts it: through the map format, with a seed.</summary>
    private static GameState Started(string name, ulong seed) =>
        MapConverter.ToGameState(MapConverter.ToDocument(Create(name)), options: new GameOptions(Seed: seed));

    [Fact]
    public void ThereAreThree_EachWithItsOwnName()
    {
        Assert.Equal(3, ArenaScenarios.All.Count);
        Assert.Equal(3, ArenaScenarios.All.Select(m => m.Name).Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(Maps))]
    public void TheMap_IsAnArena_WithoutValidatorErrorsOrArenaWarnings_AndRoundTrips(string name)
    {
        var state = Create(name);

        Assert.True(state.Rules.Arena);
        Assert.Equal(name, state.Map.Name);
        Assert.NotEmpty(state.ObjectsOf<CombatPlace>());
        var issues = MapValidator.Validate(state);
        Assert.DoesNotContain(issues, i => i.Severity == IssueSeverity.Error);
        Assert.Empty(issues.Where(i => i.Severity == IssueSeverity.Warning).Select(i => i.Message));
        var restored = TestFormat.RoundTrip(state);
        Assert.Equal(state.Objects.Count, restored.Objects.Count);
        Assert.True(restored.Rules.Arena);
    }

    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryObject_CanBeReached_OnceItsGuardsAreBeaten(string name)
    {
        var state = Started(name, 1);
        new GameSession(state).Start();
        foreach (var monster in state.ObjectsOf<MonsterGuard>().ToList())
        {
            state.RemoveObject(monster); // what they guard is reached by beating them
        }

        foreach (var mapObject in state.Objects.Where(o => o.Definition.Footprint.HasVisit && o is not CombatPlace and not Town))
        {
            Assert.True(
                state.Heroes.Any(h => new PathFinder(state).FindPath(h.Id, mapObject.VisitPosition, respectFog: false) is not null),
                $"{mapObject.Definition.Name} at {mapObject.Position} cannot be reached");
        }
    }

    [Fact]
    public void ProvingGroundsAndFourBanners_GiveEachPlayerTheFactionItPicks()
    {
        foreach (var name in new[] { ProvingGroundsScenario.Name, FourBannersScenario.Name })
        {
            var state = Create(name);
            Assert.All(state.ObjectsOf<Town>(), t => Assert.True(t.IsRandom));
            Assert.All(state.Heroes, h => Assert.True(h.IsPlaceholder));
            Assert.All(state.Players, p => Assert.Single(state.ObjectsOf<Town>(), t => t.Owner == p.Id));
        }
    }

    [Fact]
    public void FourBanners_IsTwoAgainstTwo_EachHalfWithItsCombatPlace()
    {
        var state = Create(FourBannersScenario.Name);

        Assert.Equal(2, state.Players.Select(p => p.Team).Distinct().Count());
        Assert.Equal(state.Players[0].Team, state.Players[2].Team);
        Assert.Equal(2, state.ObjectsOf<CombatPlace>().Count());
    }

    [Fact]
    public void FrostfangPass_HasNoTowns_TheHeroesBuyAtTheMapDwellings()
    {
        var state = Create(FrostfangPassScenario.Name);

        Assert.Empty(state.ObjectsOf<Town>());
        Assert.Equal(8, state.ObjectsOf<CreatureDwelling>().Count());
        Assert.All(state.Heroes, h => Assert.True(h.HasSpellBook));
    }

    [Theory]
    [MemberData(nameof(Maps))]
    public void ComputerPlayers_PrepareOnDayOne_AndFightTheBattleOnDayTwo(string name)
    {
        var state = Started(name, 7);
        var session = new GameSession(state);
        var ai = new AdventureAi(session, controlHumanSlots: true);
        session.Start();
        long Best(TeamId team) => state.Players.Where(p => p.Team == team).SelectMany(p => state.HeroesOf(p.Id)).Max(h => ArenaRules.ArmyValue(state, h));
        var teams = state.Players.Select(p => p.Team).Distinct().ToList();
        var start = teams.ToDictionary(t => t, Best);
        var grown = new List<bool>();
        session.Events.Subscribe<ArenaChampionsChosen>(_ => grown.AddRange(teams.Select(t => Best(t) > start[t])));

        while (!state.IsOver && state.Date.AbsoluteDay <= 2)
        {
            Assert.True(ai.PlayTurn(), $"the AI got stuck on {state.Date}");
        }

        Assert.True(state.IsOver);
        Assert.NotEqual(ArenaResult.NoBattle, state.Arena!.Result);
        Assert.Equal(0, ai.Refused);
        Assert.Equal([true, true], grown); // each side's best hero is stronger than any of its heroes was at the start
    }
}
