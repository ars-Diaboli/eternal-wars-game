using EternalWars.Core.Data.Templates;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Generation;

namespace EternalWars.Core.Tests.Generation;

/// <summary>The last generator stage: the scenario for the players set up at game start (RMG-5).</summary>
public class ScenarioAssemblerTests
{
    private static MapTemplate Duel() => BuiltInTemplates.Find("duel")!;

    private static string HeroOf(string town) => HeroCatalog.Default.All.Where(h => h.Town == town).OrderBy(h => h.Id, StringComparer.Ordinal).First().Id;

    [Fact]
    public void PlayersGetTheirTowns_OfThePickedType_AndThePickedHeroAtTheEntrance_WithAStarterArmy()
    {
        var state = MapGenerator.Build(Duel(), 72, 4,
        [
            new PlayerSetup(0, "Red", IsHuman: true, TownIds.Necropolis, HeroOf(TownIds.Necropolis)),
            new PlayerSetup(1, "Blue", IsHuman: false, TownIds.Inferno),
        ]);

        Assert.Equal(["Red", "Blue"], state.Players.Select(p => p.Name));
        var towns = state.Objects.OfType<Town>().ToList();
        var red = towns.Single(t => t.Owner == new PlayerId(0));
        var blue = towns.Single(t => t.Owner == new PlayerId(1));
        Assert.Equal(TownIds.Necropolis, red.TownType);
        Assert.Equal(TownIds.Inferno, blue.TownType);

        var redHero = state.Heroes.Single(h => h.Owner == new PlayerId(0));
        var blueHero = state.Heroes.Single(h => h.Owner == new PlayerId(1));
        Assert.Equal(HeroOf(TownIds.Necropolis), redHero.Definition);
        Assert.Equal(TownIds.Inferno, HeroCatalog.Default.Get(blueHero.Definition!).Town); // not picked: one of the town's heroes
        Assert.Equal(red.VisitPosition, redHero.Position);
        Assert.Equal(blue.VisitPosition, blueHero.Position);
        Assert.All(state.Heroes, h =>
        {
            var stack = Assert.Single(h.Army.Stacks).Stack;
            Assert.Equal(h.PreferredCreature, stack.Creature);
            Assert.InRange(stack.Count * state.Creatures.Get(stack.Creature).Value, ScenarioAssembler.StarterArmyValue / 2, ScenarioAssembler.StarterArmyValue * 3 / 2 + state.Creatures.Get(stack.Creature).Value);
        });

        var session = new GameSession(state);
        session.Start(); // a playable game
    }

    [Fact]
    public void ASlotNobodyTakes_KeepsItsTownInPlace_AsANeutralTown()
    {
        var map = MapGenerator.Generate(Duel(), 72, 4);
        var empty = map.Towns.Single(t => t.Slot == 1).Town;

        var state = ScenarioAssembler.Build(Duel(), map, [new PlayerSetup(0, "Red", true, TownIds.Haven)], 4);

        var town = Assert.IsType<Town>(state.Objects.Single(o => o.Id == empty.Id));
        Assert.Null(town.Owner);
        Assert.Equal(empty.Position, town.Position);
        Assert.Single(state.Players);
        Assert.Single(state.Heroes);
    }

    [Fact]
    public void BadSetups_AreRefused()
    {
        var ai = Duel() with { MaxHumans = 1, Zones =[.. Duel().Zones.Select(z => z.Id == "start-2" ? z with { Type = ZoneType.AiStart } : z)] };
        PlayerSetup Haven(int slot, string? hero = null, bool human = true) => new(slot, $"P{slot}", human, TownIds.Haven, hero);

        Assert.Throws<ArgumentException>(() => MapGenerator.Build(Duel(), 72, 1, []));
        Assert.Throws<ArgumentException>(() => MapGenerator.Build(Duel(), 72, 1, [Haven(0), Haven(0)]));
        Assert.Throws<ArgumentException>(() => MapGenerator.Build(Duel(), 72, 1, [Haven(5)]));
        Assert.Throws<ArgumentException>(() => MapGenerator.Build(ai, 72, 1, [Haven(0), Haven(1)])); // an AI-only slot
        Assert.Throws<ArgumentException>(() => MapGenerator.Build(Duel(), 72, 1, [Haven(0, HeroOf(TownIds.Inferno))])); // a hero of another town
        Assert.Throws<ArgumentException>(() => MapGenerator.Build(Duel(), 72, 1, [Haven(0, HeroOf(TownIds.Haven)), Haven(1, HeroOf(TownIds.Haven))]));
        Assert.Throws<ArgumentException>(() => MapGenerator.Build(Duel(), 72, 1, [new PlayerSetup(0, "X", true, "atlantis")]));
        Assert.NotNull(MapGenerator.Build(ai, 72, 1, [Haven(0), Haven(1, human: false)]));
    }

    [Fact]
    public void TheSameSetup_GivesTheSameGame()
    {
        PlayerSetup[] players = [new(0, "Red", true, TownIds.Sylvan), new(1, "Blue", false, TownIds.Sylvan)];

        var a = MapGenerator.Build(Duel(), 72, 9, players);
        var b = MapGenerator.Build(Duel(), 72, 9, players);

        Assert.Equal(a.Heroes.Select(h => h.Definition), b.Heroes.Select(h => h.Definition));
        Assert.NotEqual(a.Heroes[0].Definition, a.Heroes[1].Definition); // two players of one town get different heroes
    }
}
