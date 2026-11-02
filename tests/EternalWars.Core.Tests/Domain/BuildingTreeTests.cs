using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Towns;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The town screen's building tree and dwelling choice warnings (ADR-114).</summary>
public class BuildingTreeTests
{
    private static (GameSession Session, Town Town) Haven(ResourceSet resources)
    {
        var state = Create([.. Enumerable.Repeat("....................", 8), "A..................B"]);
        var town = AddTown(state, TownIds.Haven, 0, 0, Blue);
        var session = Start(state);
        state.GetPlayer(Blue).Resources = resources;
        return (session, town);
    }

    private static readonly ResourceSet Rich = new(Gold: 100000, Lumber: 100, Ore: 100, Crystal: 100, Mercury: 100, Sulfur: 100);

    private static BuildingNode Node(GameSession session, Town town, string building) =>
        BuildingTree.For(session.State, town).Single(n => n.Building == building);

    [Fact]
    public void TheTree_ListsEveryTownBuilding_ByTheDepthOfItsRequirements()
    {
        var (session, town) = Haven(Rich);
        var tree = BuildingTree.For(session.State, town);

        Assert.Equal(TownRules.Buildings(session.State, town).TownBuildings.Order(StringComparer.Ordinal), tree.Select(n => n.Building).Order(StringComparer.Ordinal));
        Assert.Equal(tree.Select(n => n.Depth).Order(), tree.Select(n => n.Depth));
        Assert.Equal(0, Node(session, town, BuiltInBuildings.Tavern).Depth);
        Assert.Equal(1, Node(session, town, BuiltInBuildings.Fort).Depth); // needs the tavern
        Assert.Equal(1, Node(session, town, BuiltInBuildings.Caravan).Depth); // needs town hall 2
        Assert.Equal(1, Node(session, town, BuiltInBuildings.SpellSchool(TownIds.Academy)).Depth); // needs guild 2
        Assert.Equal(3, Node(session, town, BuiltInBuildings.Growth(1)).Depth); // tier-1 dwelling → fort → tavern
    }

    [Fact]
    public void ANode_ShowsItsNextLevel_CostAndWhichRequirementsAreMet()
    {
        var (session, town) = Haven(Rich);

        var caravan = Node(session, town, BuiltInBuildings.Caravan);
        Assert.Equal((BuildingStatus.NeedsBuildings, 0, "Caravan"), (caravan.Status, caravan.Level, caravan.Name));
        Assert.Equal([("Town Hall", false)], caravan.Requires.Select(r => (r.Name, r.Met)));
        Assert.Equal(new ResourceSet(Gold: 1000, Lumber: 5), caravan.Cost);

        var fort = Node(session, town, BuiltInBuildings.Fort);
        Assert.Equal((BuildingStatus.Available, 1, "Citadel"), (fort.Status, fort.Level, fort.Name));
        Assert.Null(fort.Refusal);

        var growth = Node(session, town, BuiltInBuildings.Growth(1));
        Assert.Equal([("a tier 1 dwelling", false)], growth.Requires.Select(r => (r.Name, r.Met)));
    }

    [Fact]
    public void AfterABuild_TheOthersWaitForTomorrow_AndTheTownKnowsWhatItBuilt()
    {
        var (session, town) = Haven(Rich);

        session.Execute(new BuildCommand(Blue, town.Id, BuiltInBuildings.MageGuild)).ThrowIfRejected();

        Assert.Equal("Mage Guild", town.LastBuiltName);
        Assert.Equal(BuildingStatus.BuiltToday, Node(session, town, BuiltInBuildings.Blacksmith).Status);
        Assert.Equal(BuildingStatus.NeedsBuildings, Node(session, town, BuiltInBuildings.Caravan).Status); // missing buildings say so first
        Assert.Equal((BuildingStatus.BuiltToday, 1, "Mage Guild 2"), (Node(session, town, BuiltInBuildings.MageGuild).Status, Node(session, town, BuiltInBuildings.MageGuild).Level, Node(session, town, BuiltInBuildings.MageGuild).Name));

        session.Execute(new BuildDwellingCommand(Blue, town.Id, "haven.footman"));
        Assert.Equal("Mage Guild", town.LastBuiltName); // refused: one a day
    }

    [Fact]
    public void ADwellingBuild_IsNamed_AndAPoorTownCannotAfford_AndAFullBuildingIsComplete()
    {
        var (session, town) = Haven(Rich);
        session.Execute(new BuildDwellingCommand(Blue, town.Id, "haven.footman")).ThrowIfRejected();
        Assert.Equal("Footman dwelling", town.LastBuiltName);

        session.State.GetPlayer(Blue).Resources = new(Gold: 10);
        town.LastBuilt = null;
        town.SetLevel(BuiltInBuildings.Tavern, 2);

        Assert.Equal(BuildingStatus.CannotAfford, Node(session, town, BuiltInBuildings.Blacksmith).Status);
        var tavern = Node(session, town, BuiltInBuildings.Tavern);
        Assert.Equal((BuildingStatus.Complete, null, "Brotherhood"), (tavern.Status, tavern.Next, tavern.Name));
    }

    [Fact]
    public void ADwellingChoice_NamesTheLinesItWouldClose_AndTheTiersARefusedOneWouldLeaveOut()
    {
        var (session, town) = Haven(Rich);
        foreach (var line in new[] { "haven.footman", "haven.pikeman", "haven.zealot", "haven.archer", "haven.monk" })
        {
            town.SetDwelling(line, 1);
        }

        var choices = BuildingTree.DwellingChoices(session.State, town).ToDictionary(c => c.Line.Id);

        Assert.Equal(["haven.griffin", "haven.lancer", "haven.paladin", "haven.radiant-spirit"], choices.Keys.Order(StringComparer.Ordinal));
        Assert.Equal([5], choices["haven.griffin"].TiersLeftOut); // refused: it leaves no room for tier 5
        Assert.NotNull(TownRules.CheckDwelling(session.State, town, "haven.griffin"));
        Assert.Empty(choices["haven.lancer"].TiersLeftOut);
        Assert.Equal(["haven.griffin", "haven.paladin"], choices["haven.lancer"].ClosedLines.Select(l => l.Id));
        Assert.Equal(["haven.griffin"], choices["haven.radiant-spirit"].ClosedLines.Select(l => l.Id)); // tier 4 still needs the last place
    }
}
