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

/// <summary>The game's difficulty (starting resources) and daily mana (ADR-063, ADR-225).</summary>
public class DifficultyAndManaTests
{
    private static readonly string[] Rows = ["A..", "..B"];

    [Fact]
    public void WithoutADifficulty_TheMapsResourcesStay()
    {
        var state = Create(Rows);
        state.GetPlayer(Blue).Resources = new ResourceSet(Gold: 123);

        Start(state);

        Assert.Equal(new ResourceSet(Gold: 123), state.GetPlayer(Blue).Resources);
    }

    [Theory]
    [InlineData(Difficulty.Easy, 10_000)]
    [InlineData(Difficulty.Hard, 5_000)]
    [InlineData(Difficulty.Master, 2_500)]
    public void Difficulty_GivesEveryPlayerItsStartingResources(Difficulty difficulty, int gold)
    {
        var state = Create(new GameOptions(Difficulty: difficulty), Rows);
        state.GetPlayer(Blue).Resources = new ResourceSet(Gold: 123);

        Start(state);

        Assert.Equal(StartingResources.For(difficulty), state.GetPlayer(Blue).Resources);
        Assert.Equal(StartingResources.For(difficulty), state.GetPlayer(Red).Resources);
        Assert.Equal(gold, state.GetPlayer(Red).Resources.Gold);
    }

    [Fact]
    public void Complexities_GetPoorerWithTheLevel_AndTheHardestIsTheDesignersSet()
    {
        Assert.Equal(new ResourceSet(Gold: 2500, Lumber: 10, Ore: 10), StartingResources.For(Difficulty.Master));
        var golds = Enum.GetValues<Difficulty>().Select(c => StartingResources.For(c).Gold).ToList();
        Assert.Equal(golds.OrderByDescending(g => g), golds);
    }

    [Fact]
    public void Difficulty_SurvivesTheReplayFile()
    {
        var map = MapConverter.ToDocument(DemoScenario.Create());
        var session = new GameSession(MapConverter.ToGameState(map, options: new GameOptions(Seed: 3, Difficulty: Difficulty.Expert)));
        session.Start();

        var json = GameRecordSerializer.Serialize(GameReplay.Record(map, session));
        var replayed = GameReplay.Replay(GameRecordSerializer.Deserialize(json));

        Assert.Equal(Difficulty.Expert, replayed.State.Options.Difficulty);
        Assert.Equal(StartingResources.For(Difficulty.Expert), replayed.State.GetPlayer(Blue).Resources);
    }

    [Fact]
    public void Mana_IsRegainedAtTheStartOfTheOwnersTurn_UpToTheMaximum()
    {
        var state = Create(Rows);
        var hero = state.GetHero(A);
        hero.Raise(PrimaryStat.Knowledge);
        for (var i = 0; i < 14; i++)
        {
            hero.Raise(PrimaryStat.Knowledge); // Knowledge 15: pool 150 + 10 for level 1, 2 a day
        }

        var session = Start(state);
        var restored = new List<HeroManaRestored>();
        session.Events.Subscribe<HeroManaRestored>(restored.Add);
        hero.Mana = 100;

        session.EndTurn();
        session.EndTurn();
        Assert.Equal(102, hero.Mana);
        Assert.Equal(new HeroManaRestored(A, 102, 160), Assert.Single(restored));

        hero.Mana = 159;
        session.EndTurn();
        session.EndTurn();
        Assert.Equal(160, hero.Mana);

        session.EndTurn();
        session.EndTurn();
        Assert.Equal(2, restored.Count); // full pool: nothing published
    }
}
