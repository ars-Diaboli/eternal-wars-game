using EternalWars.Core.Data.Templates;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Generation;

namespace EternalWars.Core.Tests.Generation;

/// <summary>The last generator stage: the scenario for the players set up at game start (RMG-5, RMG-7).</summary>
public class ScenarioAssemblerTests
{
    private static MapTemplate Duel() => BuiltInTemplates.Find("duel")!;

    private static readonly CreatureCatalog Creatures = CreatureCatalog.Default;

    private static string HeroOf(string town) => HeroCatalog.Default.All.Where(h => h.Town == town).OrderBy(h => h.Id, StringComparer.Ordinal).First().Id;

    private static List<CreatureDefinition> Tier1(string town) => Creatures.BaseCreaturesOf(town).Where(c => c.Tier == 1).OrderBy(c => c.Id, StringComparer.Ordinal).ToList();

    private static GameState Game(MapTemplate template, ulong seed, params PlayerSetup[] players) => MapGenerator.Build(template, 72, seed, players).State;

    [Fact]
    public void PlayersGetTheirTowns_OfThePickedType_AndThePickedHeroAtTheEntrance_WithAHomm3StyleArmy()
    {
        var state = Game(Duel(), 4,
            new PlayerSetup(0, "Red", IsHuman: true, TownIds.Necropolis, HeroOf(TownIds.Necropolis)),
            new PlayerSetup(1, "Blue", IsHuman: false, TownIds.Inferno));

        Assert.Equal(["Red", "Blue"], state.Players.Select(p => p.Name));
        var red = state.Objects.OfType<Town>().Single(t => t.Owner == new PlayerId(0));
        var blue = state.Objects.OfType<Town>().Single(t => t.Owner == new PlayerId(1));
        Assert.Equal((TownIds.Necropolis, TownIds.Inferno), (red.TownType, blue.TownType));

        var redHero = state.Heroes.Single(h => h.Owner == new PlayerId(0));
        var blueHero = state.Heroes.Single(h => h.Owner == new PlayerId(1));
        Assert.Equal(HeroOf(TownIds.Necropolis), redHero.Definition);
        Assert.Equal(TownIds.Inferno, HeroCatalog.Default.Get(blueHero.Definition!).Town); // not picked: one of the town's heroes
        Assert.Equal((red.VisitPosition, blue.VisitPosition), (redHero.Position, blueHero.Position));
        foreach (var (hero, town) in new[] { (redHero, red), (blueHero, blue) })
        {
            var stacks = hero.Army.Stacks.Select(s => s.Stack).ToList();
            Assert.InRange(stacks.Count, 2, 3);
            Assert.All(stacks, s => Assert.Contains(Creatures.Get(s.Creature), Creatures.BaseCreaturesOf(town.TownType).Where(c => c.Tier <= 2)));
            Assert.InRange(stacks.Sum(s => s.Count * Creatures.Get(s.Creature).Value), ScenarioAssembler.StarterArmyMin * 0.8, ScenarioAssembler.StarterArmyMax * 1.25);
            if (Creatures.Get(hero.PreferredCreature!).Tier <= 2)
            {
                Assert.Equal(hero.PreferredCreature, stacks[0].Creature);
            }
        }

        Assert.Equal(ScenarioRules.Default, state.Rules);
        Assert.Contains("#4", state.Map.Name);
        new GameSession(state).Start(); // a playable game
    }

    [Fact]
    public void StartTowns_GetThePickedTier1Dwelling_ARandomOneForTheAi_WithAWeekOfCreatures()
    {
        var picked = Tier1(TownIds.Haven)[1].Id;

        var state = Game(Duel(), 4, new PlayerSetup(0, "Red", true, TownIds.Haven, Tier1Dwelling: picked), new PlayerSetup(1, "Blue", false, TownIds.Haven));

        var red = state.Objects.OfType<Town>().Single(t => t.Owner == new PlayerId(0));
        var blue = state.Objects.OfType<Town>().Single(t => t.Owner == new PlayerId(1));
        Assert.Equal([picked], red.Dwellings.Keys);
        Assert.Equal(Creatures.Get(picked).WeeklyGrowth, red.Available[picked]);
        var aiLine = Assert.Single(blue.Dwellings.Keys);
        Assert.Equal(1, Creatures.Get(aiLine).Tier);
        Assert.Equal(BuiltInBuildings.DefaultStart, red.Buildings);
    }

    [Fact]
    public void TheTemplatesStartBuildingsAndDwellings_AreBuilt_AndTheFortsBonusFillsThem()
    {
        var template = Duel() with
        {
            Zones = [.. Duel().Zones.Select(z => z.Type == ZoneType.PlayerStart ? z with { StartBuildings = new Dictionary<string, int> { [BuiltInBuildings.Fort] = 2 }, StartDwellings = [1, 2] } : z)],
        };

        var state = Game(template, 4, new PlayerSetup(0, "Red", true, TownIds.Haven, Tier1Dwelling: Tier1(TownIds.Haven)[1].Id), new PlayerSetup(1, "Blue", false, TownIds.Inferno));

        var red = state.Objects.OfType<Town>().Single(t => t.Owner == new PlayerId(0));
        Assert.Equal(2, red.Level(BuiltInBuildings.Fort));
        Assert.Equal([1, 2], red.Dwellings.Keys.Select(l => Creatures.Get(l).Tier).Order());
        Assert.All(red.Dwellings.Keys, l => Assert.Equal(Creatures.Get(l).WeeklyGrowth * 3 / 2, red.Available[l])); // the Citadel's +50 %
        Assert.DoesNotContain(TemplateValidator.Validate(template), i => i.Severity == IssueSeverity.Error);
        Assert.Equal(3, TemplateSerializer.Deserialize(TemplateSerializer.Serialize(template)).FormatVersion);
        Assert.Equal([1, 2], TemplateSerializer.Deserialize(TemplateSerializer.Serialize(template)).Zones[0].StartDwellings);
    }

    [Fact]
    public void BadStartBuildings_AreTemplateErrors()
    {
        MapTemplate With(Func<TemplateZone, TemplateZone> change) => Duel() with { Zones = [.. Duel().Zones.Select(z => z.Id == "start-1" ? change(z) : z)] };
        bool Fails(MapTemplate t) => TemplateValidator.Validate(t).Any(i => i.Severity == IssueSeverity.Error && i.Message.Contains("start"));

        Assert.True(Fails(With(z => z with { StartBuildings = new Dictionary<string, int> { ["castle-of-dreams"] = 1 } })));
        Assert.True(Fails(With(z => z with { StartBuildings = new Dictionary<string, int> { [BuiltInBuildings.Fort] = 9 } })));
        Assert.True(Fails(With(z => z with { StartBuildings = new Dictionary<string, int> { ["dwelling-1"] = 1 } })));
        Assert.True(Fails(With(z => z with { StartDwellings = [1, 1, 1] })));
        Assert.True(Fails(With(z => z with { StartDwellings = [6] })));
        Assert.True(Fails(With(z => z with { StartDwellings = [1, 1, 2, 2, 3, 3] }))); // tiers 4 and 5 would be out of reach
        Assert.True(Fails(Duel() with { Zones = [.. Duel().Zones.Select(z => z.Id == "centre" ? z with { StartDwellings = [1] } : z)] }));
    }

    [Fact]
    public void ASlotNobodyTakes_KeepsItsTownInPlace_AsANeutralTownWithAGarrison()
    {
        var (state, generation) = MapGenerator.Build(Duel(), 72, 4, [new PlayerSetup(0, "Red", true, TownIds.Haven)]);
        var empty = generation.Map.Towns.Single(t => t.Slot == 1);

        var town = state.Objects.OfType<Town>().Single(t => t.Id == empty.Town.Id);
        Assert.Null(town.Owner);
        Assert.Equal(empty.Town.Position, town.Position);
        Assert.Equal(2, town.Garrison.StackCount);
        Assert.Single(state.Players);
        Assert.Single(state.Heroes);
    }

    [Fact]
    public void StartZones_ArePaintedWithTheirTownsNativeTerrains_AndRandomTownsAreDrawnFirst()
    {
        foreach (var (town, seed) in new[] { (TownIds.Academy, 1UL), (TownIds.Inferno, 2UL), (PlayerSetup.RandomTown, 3UL) })
        {
            var (state, generation) = MapGenerator.Build(Duel(), 72, seed, [new PlayerSetup(0, "Red", true, town), new PlayerSetup(1, "Blue", false, TownIds.Haven)]);
            var red = state.Objects.OfType<Town>().Single(t => t.Owner == new PlayerId(0));
            var zone = generation.Map.Towns.Single(t => t.Slot == 0).Zone;
            var natives = TerrainPainter.NativeTerrains(red.TownType);

            Assert.NotEqual(PlayerSetup.RandomTown, red.TownType);
            var ground = generation.Map.Layout.Tiles(zone).Select(t => generation.Map.Map.GetTerrain(new MapPosition(t.X, t.Y, t.Level))).Where(t => t != TerrainPainter.WallTerrain).ToList();
            Assert.NotEmpty(ground);
            Assert.True(ground.Count(natives.Contains) >= ground.Count * 0.97, $"{red.TownType}: {ground.Count(natives.Contains)} of {ground.Count}"); // passages through the wall may take the neighbour's ground
        }

        PlayerSetup[] random = [new(0, "A", true, PlayerSetup.RandomTown), new(1, "B", false, PlayerSetup.RandomTown)];
        Assert.Equal(ScenarioAssembler.ResolveTowns(random, 8).Select(p => p.TownType), ScenarioAssembler.ResolveTowns(random, 8).Select(p => p.TownType));
    }

    [Fact]
    public void BadSetups_AreRefused()
    {
        var ai = Duel() with { MaxHumans = 1, Zones = [.. Duel().Zones.Select(z => z.Id == "start-2" ? z with { Type = ZoneType.AiStart } : z)] };
        PlayerSetup Haven(int slot, string? hero = null, bool human = true, string? dwelling = null) => new(slot, $"P{slot}", human, TownIds.Haven, hero, Tier1Dwelling: dwelling);

        Assert.Throws<ArgumentException>(() => Game(Duel(), 1));
        Assert.Throws<ArgumentException>(() => Game(Duel(), 1, Haven(0), Haven(0)));
        Assert.Throws<ArgumentException>(() => Game(Duel(), 1, Haven(5)));
        Assert.Throws<ArgumentException>(() => Game(ai, 1, Haven(0), Haven(1))); // an AI-only slot
        Assert.Throws<ArgumentException>(() => Game(Duel(), 1, Haven(0, HeroOf(TownIds.Inferno)))); // a hero of another town
        Assert.Throws<ArgumentException>(() => Game(Duel(), 1, Haven(0, HeroOf(TownIds.Haven)), Haven(1, HeroOf(TownIds.Haven))));
        Assert.Throws<ArgumentException>(() => Game(Duel(), 1, Haven(0, dwelling: Tier1(TownIds.Inferno)[0].Id))); // another town's dwelling
        Assert.Throws<ArgumentException>(() => Game(Duel(), 1, new PlayerSetup(0, "X", true, "atlantis")));
        Assert.NotNull(Game(ai, 1, Haven(0), Haven(1, human: false)));
    }

    [Fact]
    public void TheSameSetup_GivesTheSameGame()
    {
        PlayerSetup[] players = [new(0, "Red", true, TownIds.Sylvan), new(1, "Blue", false, TownIds.Sylvan)];

        var a = MapGenerator.Build(Duel(), 72, 9, players).State;
        var b = MapGenerator.Build(Duel(), 72, 9, players).State;

        Assert.Equal(a.Heroes.Select(h => h.Definition), b.Heroes.Select(h => h.Definition));
        Assert.Equal(GameStateDigest.Compute(a), GameStateDigest.Compute(b));
        Assert.NotEqual(a.Heroes[0].Definition, a.Heroes[1].Definition); // two players of one town get different heroes
    }

    [Fact]
    public void AMapWithProblems_IsRetriedWithSubSeeds_AndTheBestIsKept()
    {
        var clean = MapGenerator.GenerateBest(Duel(), 72, 4);
        Assert.Equal((0, 4UL), (clean.Attempt, clean.AttemptSeed));
        Assert.Empty(clean.Problems);

        var crowded = Duel() with { Zones = [.. Duel().Zones.Select(z => z.Id == "reserve-1" ? z with { ExtraPiles = 5000 } : z)] };
        var best = MapGenerator.GenerateBest(crowded, 72, 4);
        Assert.NotEmpty(best.Problems); // no attempt can hold 5000 piles: the best one is kept, with its problems to show
        Assert.Equal(MapGenerator.AttemptSeed(4, best.Attempt), best.AttemptSeed);
        Assert.Equal(best.Attempt, MapGenerator.GenerateBest(crowded, 72, 4).Attempt);
        Assert.Equal(MapGenerator.Attempts, Enumerable.Range(0, MapGenerator.Attempts).Select(i => MapGenerator.AttemptSeed(4, i)).Distinct().Count());
    }
}
