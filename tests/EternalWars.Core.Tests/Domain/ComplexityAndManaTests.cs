using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>Scenario complexity (starting resources) and daily mana (ADR-063).</summary>
public class ComplexityAndManaTests
{
    private static readonly string[] Rows = ["A..", "..B"];

    [Fact]
    public void WithoutAComplexity_TheMapsResourcesStay()
    {
        var state = Create(Rows);
        state.GetPlayer(Blue).Resources = new ResourceSet(Gold: 123);

        Start(state);

        Assert.Equal(new ResourceSet(Gold: 123), state.GetPlayer(Blue).Resources);
    }

    [Theory]
    [InlineData(Complexity.Beginner, 10_000)]
    [InlineData(Complexity.Normal, 5_000)]
    [InlineData(Complexity.Expert, 2_500)]
    public void Complexity_GivesEveryPlayerItsStartingResources(Complexity complexity, int gold)
    {
        var state = Create(new GameOptions(Complexity: complexity), Rows);
        state.GetPlayer(Blue).Resources = new ResourceSet(Gold: 123);

        Start(state);

        Assert.Equal(StartingResources.For(complexity), state.GetPlayer(Blue).Resources);
        Assert.Equal(StartingResources.For(complexity), state.GetPlayer(Red).Resources);
        Assert.Equal(gold, state.GetPlayer(Red).Resources.Gold);
    }

    [Fact]
    public void Complexities_GetPoorerWithTheLevel_AndTheHardestIsTheDesignersSet()
    {
        Assert.Equal(new ResourceSet(Gold: 2500, Lumber: 10, Ore: 10), StartingResources.For(Complexity.Expert));
        var golds = Enum.GetValues<Complexity>().Select(c => StartingResources.For(c).Gold).ToList();
        Assert.Equal(golds.OrderByDescending(g => g), golds);
    }

    [Fact]
    public void Complexity_SurvivesTheReplayFile()
    {
        var map = MapConverter.ToDocument(DemoScenario.Create());
        var session = new GameSession(MapConverter.ToGameState(map, options: new GameOptions(Seed: 3, Complexity: Complexity.Hard)));
        session.Start();

        var json = GameRecordSerializer.Serialize(GameReplay.Record(map, session));
        var replayed = GameReplay.Replay(GameRecordSerializer.Deserialize(json));

        Assert.Equal(Complexity.Hard, replayed.State.Options.Complexity);
        Assert.Equal(StartingResources.For(Complexity.Hard), replayed.State.GetPlayer(Blue).Resources);
    }

    [Fact]
    public void Mana_IsRegainedAtTheStartOfTheOwnersTurn_UpToTheMaximum()
    {
        var state = Create(Rows);
        var hero = state.GetHero(A);
        hero.Raise(PrimaryStat.Knowledge);
        for (var i = 0; i < 14; i++)
        {
            hero.Raise(PrimaryStat.Knowledge); // Knowledge 15: pool 150, 2 a day
        }

        var session = Start(state);
        var restored = new List<HeroManaRestored>();
        session.Events.Subscribe<HeroManaRestored>(restored.Add);
        hero.Mana = 100;

        session.EndTurn();
        session.EndTurn();
        Assert.Equal(102, hero.Mana);
        Assert.Equal(new HeroManaRestored(A, 102, 150), Assert.Single(restored));

        hero.Mana = 149;
        session.EndTurn();
        session.EndTurn();
        Assert.Equal(150, hero.Mana);

        session.EndTurn();
        session.EndTurn();
        Assert.Equal(2, restored.Count); // full pool: nothing published
    }
}
