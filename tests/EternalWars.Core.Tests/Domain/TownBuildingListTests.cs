using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>Per-town building lists (the <c>town</c> column of <c>buildings.csv</c>) and the Capitol (ADR-070, TOWN-3).</summary>
public class TownBuildingListTests
{
    /// <summary>Towns at (0, 0) and (0, 10) have their entrances at (3, 6) and (3, 16); hero A waits just below the first.</summary>
    private static readonly string[] Rows =
    [
        .. Enumerable.Repeat("............", 7),
        "...A........",
        .. Enumerable.Repeat("............", 11),
        "...........B",
    ];

    private static readonly ResourceSet Rich = new(Gold: 100_000, Lumber: 100, Ore: 100, Crystal: 50, Mercury: 50, Sulfur: 50);

    private static readonly string Dwellings = string.Concat(Enumerable.Range(1, 5).Select(t => $"dwelling-{t},,1,D{t},500\ndwelling-{t},,2,U{t},1000\n"));

    private const string Header = "id,town,level,name,gold,requires\n";

    [Fact]
    public void TownRows_ReplaceTheSharedBuilding_OrAddOneOnlyThatTownHas()
    {
        var catalog = new BuildingCatalog(BuildingTable.Parse(Header + Dwellings +
            "tavern,,1,Tavern,750\n" +
            "tavern,,2,Brotherhood,2000\n" +
            $"tavern,{TownIds.Necropolis},1,Crypt Inn,600\n" +
            $"amplifier,{TownIds.Necropolis},1,Amplifier,1500,tavern 1\n"));

        Assert.Null(catalog.Town);
        Assert.Equal([TownIds.Necropolis], catalog.TownTypes);
        Assert.Same(catalog, catalog.For(TownIds.Haven));
        Assert.Same(catalog, catalog.For(null));

        var necropolis = catalog.For(TownIds.Necropolis);
        Assert.Equal(TownIds.Necropolis, necropolis.Town);
        Assert.Equal(1, necropolis.MaxLevel(BuiltInBuildings.Tavern)); // its own rows replace both shared levels
        Assert.Equal(("Crypt Inn", 600), (necropolis.Get(BuiltInBuildings.Tavern, 1).Name, necropolis.Get(BuiltInBuildings.Tavern, 1).Cost.Gold));
        Assert.True(necropolis.Contains("amplifier"));
        Assert.False(catalog.Contains("amplifier"));
        Assert.Equal(2, catalog.MaxLevel(BuiltInBuildings.Tavern));
    }

    [Fact]
    public void TownRows_AreCheckedWithTheSharedOnes()
    {
        // A town's building may require only what that town has: the shared list plus its own rows.
        Assert.Contains($"({TownIds.Haven})", Assert.Throws<FormatException>(() => new BuildingCatalog(BuildingTable.Parse(Header + Dwellings +
            $"chapel,{TownIds.Haven},1,Chapel,500,amplifier 1\n" +
            $"amplifier,{TownIds.Necropolis},1,Amplifier,1500\n"))).Message);
        Assert.Throws<FormatException>(() => new BuildingCatalog(BuildingTable.Parse(Header + Dwellings + $"chapel,{TownIds.Haven},2,Chapel,500\n")));
    }

    [Fact]
    public void Towns_BuildFromTheirOwnList()
    {
        var catalog = new BuildingCatalog(BuildingTable.Parse(
            BuiltInBuildings.EmbeddedTable() + $"blacksmith,{TownIds.Necropolis},1,Bone Forge,400,\n"));
        var (session, haven, necropolis) = TwoTowns(catalog);

        Assert.Equal("Blacksmith", TownRules.NextLevel(session.State, haven, BuiltInBuildings.Blacksmith)!.Name);
        Assert.Equal("Bone Forge", TownRules.NextLevel(session.State, necropolis, BuiltInBuildings.Blacksmith)!.Name);

        var gold = session.State.GetPlayer(Blue).Resources.Gold;
        session.Execute(new BuildCommand(Blue, necropolis.Id, BuiltInBuildings.Blacksmith)).ThrowIfRejected();
        Assert.Equal(gold - 400, session.State.GetPlayer(Blue).Resources.Gold);
    }

    [Fact]
    public void TownHall_HasFourLevels_TheFourthIsTheCapitol()
    {
        var catalog = BuildingCatalog.Default;

        Assert.Equal(BuiltInBuildings.CapitolLevel, catalog.MaxLevel(BuiltInBuildings.TownHall));
        Assert.Equal(["Village Hall", "Town Hall", "City Hall", "Capitol"],
            Enumerable.Range(1, 4).Select(l => catalog.Get(BuiltInBuildings.TownHall, l).Name));
        Assert.Equal(new ResourceSet(Gold: 4000), catalog.Get(BuiltInBuildings.TownHall, 4).Income);
    }

    [Fact]
    public void Capitol_OnlyOnePerPlayer()
    {
        var (session, haven, necropolis) = TwoTowns(BuildingCatalog.Default);
        haven.SetLevel(BuiltInBuildings.TownHall, BuiltInBuildings.CapitolLevel);
        necropolis.SetLevel(BuiltInBuildings.TownHall, BuiltInBuildings.CapitolLevel - 1);
        necropolis.SetLevel(BuiltInBuildings.Fort, 3);
        BuildCapitolDwellings(session.State, necropolis);

        var refused = session.Execute(new BuildCommand(Blue, necropolis.Id, BuiltInBuildings.TownHall));
        Assert.Contains("only one", refused.Error);

        haven.SetLevel(BuiltInBuildings.TownHall, BuiltInBuildings.CapitolLevel - 1);
        session.Execute(new BuildCommand(Blue, necropolis.Id, BuiltInBuildings.TownHall)).ThrowIfRejected();
        Assert.Equal(BuiltInBuildings.CapitolLevel, necropolis.Level(BuiltInBuildings.TownHall));
        Assert.Same(necropolis, TownRules.CapitolOf(session.State, Blue));
    }

    [Fact]
    public void CapturedCapitol_DropsToCityHall_WhenTheCaptorHasOne()
    {
        var state = Create(Rows);
        var enemy = AddTown(state, TownIds.Haven, 0, 0, Red);
        var own = AddTown(state, TownIds.Necropolis, 0, 10, Blue);
        enemy.SetLevel(BuiltInBuildings.TownHall, BuiltInBuildings.CapitolLevel);
        own.SetLevel(BuiltInBuildings.TownHall, BuiltInBuildings.CapitolLevel);
        var session = Start(state);
        var downgrades = Record<TownBuildingDowngraded>(session);

        session.Step(A, Direction.North);

        Assert.Equal(Blue, enemy.Owner);
        Assert.Equal(BuiltInBuildings.CapitolLevel - 1, enemy.Level(BuiltInBuildings.TownHall));
        Assert.Equal([new TownBuildingDowngraded(enemy.Id, BuiltInBuildings.TownHall, BuiltInBuildings.CapitolLevel - 1)], downgrades);
    }

    [Fact]
    public void CapturedCapitol_StaysOne_WhenTheCaptorHasNone()
    {
        var state = Create(Rows);
        var enemy = AddTown(state, TownIds.Haven, 0, 0, Red);
        enemy.SetLevel(BuiltInBuildings.TownHall, BuiltInBuildings.CapitolLevel);
        var session = Start(state);
        var downgrades = Record<TownBuildingDowngraded>(session);

        session.Step(A, Direction.North);

        Assert.Equal(Blue, enemy.Owner);
        Assert.Equal(BuiltInBuildings.CapitolLevel, enemy.Level(BuiltInBuildings.TownHall));
        Assert.Empty(downgrades);
    }

    [Fact]
    public void Validator_ReportsASecondCapitol()
    {
        var state = Create(Rows);
        var first = AddTown(state, TownIds.Haven, 0, 0, Blue);
        var second = AddTown(state, TownIds.Necropolis, 0, 10, Blue);
        foreach (var town in new[] { first, second })
        {
            town.SetLevel(BuiltInBuildings.Fort, 3);
            town.SetLevel(BuiltInBuildings.Blacksmith, 1);
            town.SetLevel(BuiltInBuildings.Marketplace, 1);
            town.SetLevel(BuiltInBuildings.MageGuild, 1);
            town.SetLevel(BuiltInBuildings.TownHall, BuiltInBuildings.CapitolLevel);
            BuildCapitolDwellings(state, town);
        }

        var issue = Assert.Single(MapValidator.Validate(state), i => i.Message.Contains("Capitol", StringComparison.Ordinal));
        Assert.Contains(second.Name, issue.Message);
    }

    [Fact]
    public void Capitol_NeedsSixDwellingsOfTiersOneToFour()
    {
        var (session, haven, _) = TwoTowns(BuildingCatalog.Default);
        haven.SetLevel(BuiltInBuildings.TownHall, BuiltInBuildings.CapitolLevel - 1);
        haven.SetLevel(BuiltInBuildings.Fort, 3);
        var lines = TownRules.Lines(session.State, haven);
        foreach (var line in lines.Where(l => l.Tier == 5).Concat(lines.Where(l => l.Tier <= 4).Take(5)))
        {
            haven.SetDwelling(line.Id, 1); // the tier 5 dwelling does not count
        }

        var refused = session.Execute(new BuildCommand(Blue, haven.Id, BuiltInBuildings.TownHall));
        Assert.Contains("needs 6 dwellings of tiers 1–4 (5 built)", refused.Error);
        Assert.Equal(BuildingStatus.NeedsBuildings, BuildingTree.For(session.State, haven).Single(n => n.Building == BuiltInBuildings.TownHall).Status);

        haven.SetDwelling(lines.Where(l => l.Tier <= 4).ElementAt(5).Id, 1);
        session.Execute(new BuildCommand(Blue, haven.Id, BuiltInBuildings.TownHall)).ThrowIfRejected();
        Assert.Equal(BuiltInBuildings.CapitolLevel, haven.Level(BuiltInBuildings.TownHall));
    }

    [Fact]
    public void Editor_KeepsTheCapitolsDwellings()
    {
        var state = Create(Rows);
        var town = AddTown(state, TownIds.Haven, 0, 0, Blue);
        town.SetLevel(BuiltInBuildings.Fort, 3);
        town.SetLevel(BuiltInBuildings.Blacksmith, 1);
        town.SetLevel(BuiltInBuildings.Marketplace, 1);
        town.SetLevel(BuiltInBuildings.MageGuild, 1);
        town.SetLevel(BuiltInBuildings.TownHall, BuiltInBuildings.CapitolLevel - 1);
        var editor = new MapEditorSession(state);

        Assert.Equal(EditResult.RequirementMissing, editor.Towns.SetBuilding(town.Id, BuiltInBuildings.TownHall, BuiltInBuildings.CapitolLevel));

        BuildCapitolDwellings(state, town);
        Assert.Equal(EditResult.Ok, editor.Towns.SetBuilding(town.Id, BuiltInBuildings.TownHall, BuiltInBuildings.CapitolLevel));
        var built = TownRules.Lines(state, town).First(l => town.DwellingLevel(l.Id) > 0);
        Assert.Equal(EditResult.RequirementMissing, editor.Towns.SetDwelling(town.Id, built.Id, 0));
    }

    [Fact]
    public void Validator_ReportsACapitolWithoutItsDwellings()
    {
        var state = Create(Rows);
        var town = AddTown(state, TownIds.Haven, 0, 0, Blue);
        town.SetLevel(BuiltInBuildings.Fort, 3);
        town.SetLevel(BuiltInBuildings.Blacksmith, 1);
        town.SetLevel(BuiltInBuildings.Marketplace, 1);
        town.SetLevel(BuiltInBuildings.MageGuild, 1);
        town.SetLevel(BuiltInBuildings.TownHall, BuiltInBuildings.CapitolLevel);

        Assert.Contains(MapValidator.Validate(state), i => i.Message.Contains("needs 6 dwellings", StringComparison.Ordinal));
    }

    /// <summary>Both dwellings of tiers 1 and 2 and one of tiers 3 and 4: the six the Capitol needs (ADR-291).</summary>
    private static void BuildCapitolDwellings(GameState state, Town town)
    {
        var lines = TownRules.Lines(state, town);
        foreach (var line in lines.Where(l => l.Tier <= 2).Concat(lines.Where(l => l.Tier is 3 or 4).GroupBy(l => l.Tier).Select(g => g.First())))
        {
            town.SetDwelling(line.Id, 1);
        }
    }

    private static (GameSession Session, Town Haven, Town Necropolis) TwoTowns(BuildingCatalog catalog)
    {
        var basic = Create(Rows);
        var state = new GameState(basic.Map, basic.Players, basic.Heroes, buildings: catalog);
        var haven = AddTown(state, TownIds.Haven, 0, 0, Blue);
        var necropolis = AddTown(state, TownIds.Necropolis, 0, 10, Blue);
        var session = Start(state);
        state.GetPlayer(Blue).Resources = Rich;
        return (session, haven, necropolis);
    }

    private static List<T> Record<T>(GameSession session)
        where T : IEvent
    {
        var events = new List<T>();
        session.Events.Subscribe<T>(events.Add);
        return events;
    }
}
