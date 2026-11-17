using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.MapEvents;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Core.Domain.Towns;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The new-game screen's tabs for hand-made maps (ADR-201).</summary>
public class MapKindsTests
{
    private static readonly string[] Rows =
    [
        "....................", "....................", "....................", "....................",
        "....................", "....................", "....................",
        "...A...............B",
    ];

    /// <summary>Two human players, each with a Haven town.</summary>
    private static GameState TwoTowns(Action<GameState>? adjust = null)
    {
        var state = Create(GameOptions.Default, Rows);
        AddTown(state, TownIds.Haven, 0, 0, Blue);
        AddTown(state, TownIds.Haven, 12, 0, Red);
        adjust?.Invoke(state);
        return state;
    }

    [Fact]
    public void AMapWhereEveryPersonHasATown_IsAScenario() => Assert.Equal(MapKind.Scenario, MapKinds.Of(TwoTowns()));

    [Fact]
    public void AComputerPlayerWithoutATown_KeepsItAScenario()
    {
        var state = Create(GameOptions.Default, Rows);
        AddTown(state, TownIds.Haven, 0, 0, Blue);
        state.GetPlayer(Red).IsHuman = false;

        Assert.Equal(MapKind.Scenario, MapKinds.Of(state));
    }

    [Fact]
    public void APersonStartingWithoutATown_MakesItCampaignLike()
    {
        var state = Create(GameOptions.Default, Rows);
        AddTown(state, TownIds.Haven, 0, 0, Blue);

        Assert.Equal(MapKind.Campaign, MapKinds.Of(state));
    }

    [Fact]
    public void AnEventGivingSomethingOnlyToPeople_MakesItCampaignLike()
    {
        var set = new MapEventSet
        {
            Events = [new MapEventDefinition("e", MapEventKind.Player,
                [new IfAction(new PlayerIsHumanCondition(), [new ResourceAction(ResourceType.Gold, new NumberExpr(500))])])],
        };

        Assert.Equal(MapKind.Campaign, MapKinds.Of(TwoTowns(s => s.SetMapEvents(set, check: false))));
    }

    [Fact]
    public void CampaignVariables_MakeItCampaignLike()
    {
        var set = new MapEventSet { Variables = new Dictionary<string, int> { ["deeds"] = 0 }, KeptVariables = ["deeds"] };

        Assert.Equal(MapKind.Campaign, MapKinds.Of(TwoTowns(s => s.SetMapEvents(set))));
    }

    [Fact]
    public void AnArenaMap_IsAnArenaEvenWithoutTowns()
    {
        var state = Create(GameOptions.Default, Rows);
        state.SetRules(ScenarioRules.Default with { Arena = true });

        Assert.Equal(MapKind.Arena, MapKinds.Of(state));
    }

    [Fact]
    public void TheShippedMaps_FallInTheirTabs()
    {
        // Demo Valley (no towns at all) is not ranked, so the rule reads it as campaign-like; the game lists it with the scenarios by hand.
        Assert.Equal(MapKind.Campaign, MapKinds.Of(DemoScenario.Create()));
        Assert.Equal(MapKind.Scenario, MapKinds.Of(TownsScenario.Create()));
        Assert.Equal(MapKind.Scenario, MapKinds.Of(EmberMarchScenario.Create()));
        Assert.All(ArenaScenarios.All, arena => Assert.Equal(MapKind.Arena, MapKinds.Of(arena.Create(null))));
    }
}
