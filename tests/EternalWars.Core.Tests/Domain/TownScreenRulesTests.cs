using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Players;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Domain.Turns;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>
/// The town screen's step 2 rules (ADR-296): the town overview, Recruit all and Upgrade all as plans and commands, dismissing a stack, renaming a
/// town, the tavern's weekly rumour, any resource for any other, and sending resources to a teammate.
/// </summary>
public class TownScreenRulesTests
{
    /// <summary>A Haven town at (0, 0) has its entrance at (3, 6), where Blue's hero A stands; Red's hero B waits two tiles below.</summary>
    private static readonly string[] Rows =
    [
        "............",
        "............",
        "............",
        "............",
        "............",
        "............",
        "...A........",
        "............",
        "...B........",
        "............",
    ];

    private static readonly ResourceSet Rich = new(Gold: 1_000_000, Lumber: 1000, Ore: 1000, Crystal: 1000, Mercury: 1000, Sulfur: 1000);

    /// <summary>Starts the game, then sets Blue's treasury back to <paramref name="resources"/>: the first day's income is not part of these tests.</summary>
    private static GameSession Started(GameState state, ResourceSet resources)
    {
        var session = Start(state);
        state.GetPlayer(Blue).Resources = resources;
        return session;
    }

    private static (GameState State, Town Town) Haven(ResourceSet? resources = null)
    {
        var state = Create(Rows);
        var town = AddTown(state, TownIds.Haven, 0, 0, Blue);
        state.GetPlayer(Blue).Resources = resources ?? Rich;
        return (state, town);
    }

    private static CreatureDefinition Line(GameState state, string id) => state.Creatures.Get(id);

    // The overview -----------------------------------------------------------------------------------------

    [Fact]
    public void Overview_SplitsEachLinesGrowthIntoItsParts_ThatAddUpToWhatMondayBrings_BeforeNextWeeksEvent()
    {
        var (state, town) = Haven();
        var footman = Line(state, BuiltInCreatures.Footman);
        town.SetDwelling(footman.Id, 1);
        town.SetAvailable(footman.Id, 4);
        town.SetLevel(BuiltInBuildings.Fort, 3);
        town.SetLevel(BuiltInBuildings.Grail, 1);
        state.WeekEvent = new WeekEvent(WeekEventKind.WeekOfCreature, footman.UpgradesTo);

        var growth = TownOverview.Of(state, town).Lines.Single(l => l.Line == footman);

        // This week's event is not next week's (drawn on Monday): the forecast is the growth before it.
        Assert.Equal(TownRules.WeeklyGrowth(state, town, footman), growth.Week);
        Assert.Equal(growth.Week, growth.Parts.Sum(p => p.Amount));
        Assert.Equal([GrowthSource.Base, GrowthSource.Fort, GrowthSource.Grail], growth.Parts.Select(p => p.Source));
        Assert.Equal(footman.WeeklyGrowth, growth.Parts[1].Amount); // the Castle doubles the base
        Assert.Equal(4, growth.Waiting);
        Assert.Equal(0, TownOverview.Of(state, town).Lines.Single(l => l.Line.Id == BuiltInCreatures.Pikeman).Week); // not built
    }

    [Fact]
    public void Overview_TellsWhatWasBuiltToday_AndTheGovernorsDiscounts()
    {
        var (state, town) = Haven();
        var session = Start(state);

        session.Execute(new BuildCommand(Blue, town.Id, BuiltInBuildings.Tavern)).ThrowIfRejected();
        var overview = TownOverview.Of(state, town);

        Assert.Equal(town.LastBuiltName, overview.BuiltToday);
        Assert.Equal(PlotState.BuiltToday, overview.Plots.Single(p => p.Id == BuiltInBuildings.Tavern).State);
        Assert.Equal(TownRules.DailyIncome(state, town), overview.Income);
        Assert.Null(overview.Governor);
        Assert.Equal(0, overview.CreatureDiscountPercent);
    }

    [Fact]
    public void Plots_ShowUnusedLand_ForAClosedLine_AndCitizensHomesOnceTheCapitolStands()
    {
        var (state, town) = Haven();
        foreach (var id in new[] { "haven.footman", "haven.pikeman", "haven.zealot", "haven.archer", "haven.monk" })
        {
            town.SetDwelling(id, 1);
        }

        // Five built with tiers 4 and 5 still empty: a sixth in tier 3 would leave no room, so the Griffin's line is closed for good.
        PlotState GriffinPlot() => TownOverview.PlotsOf(state, town).Single(p => p.Line == "haven.griffin").State;
        Assert.Equal(PlotState.Closed, GriffinPlot());
        Assert.Equal(PlotState.NotBuilt, TownOverview.PlotsOf(state, town).Single(p => p.Line == "haven.lancer").State);
        Assert.Equal(PlotState.Built, TownOverview.PlotsOf(state, town).Single(p => p.Id == TownOverview.DwellingPlot("haven.monk")).State);

        town.SetLevel(BuiltInBuildings.TownHall, BuiltInBuildings.CapitolLevel);
        Assert.Equal(PlotState.CitizensHomes, GriffinPlot());
    }

    [Fact]
    public void BuildingKinds_PutEachBuildingInAHallColumn_FromTheTable()
    {
        var (state, town) = Haven();
        var catalog = TownRules.Buildings(state, town);

        Assert.Equal(BuildingKind.Core, catalog.KindOf(BuiltInBuildings.TownHall));
        Assert.Equal(BuildingKind.Military, catalog.KindOf(BuiltInBuildings.Fort));
        Assert.Equal(BuildingKind.Magic, catalog.KindOf(BuiltInBuildings.MageGuild));
        Assert.Equal(BuildingKind.Dwelling, catalog.KindOf(BuiltInBuildings.Dwelling(1)));
        Assert.Equal(BuildingKind.Dwelling, catalog.KindOf("growth-1"));
        Assert.Equal(BuildingKind.Unique, catalog.KindOf(BuiltInBuildings.Grail));
        Assert.Equal(BuildingKind.Unique, catalog.KindOf("stables"));
        Assert.Throws<FormatException>(() => BuildingTable.Parse("id,level,name,kind\nfort,1,Fort,keep\n"));
    }

    // Recruit all ------------------------------------------------------------------------------------------

    private static Town WithDwellings(GameState state, Town town)
    {
        town.SetDwelling(BuiltInCreatures.Footman, 2);
        town.SetDwelling("haven.zealot", 1);
        town.SetDwelling("haven.monk", 1);
        town.SetAvailable(BuiltInCreatures.Footman, 20);
        town.SetAvailable("haven.zealot", 10);
        town.SetAvailable("haven.monk", 5);
        return town;
    }

    [Fact]
    public void RecruitPlan_BuysTheTopTierFirst_TheUpgradedCreatureOfAnUpgradedDwelling_IntoTheGarrison()
    {
        var (state, town) = Haven();
        WithDwellings(state, town);

        var plan = TownPlans.Recruit(state, town);

        Assert.Equal(
            [new RecruitOrder("haven.monk", 5), new RecruitOrder("haven.zealot", 10), new RecruitOrder("haven.crusader", 20)],
            plan.Orders);
        Assert.Equal(
            TownRules.RecruitCost(state, town, Line(state, "haven.monk"), 5) + TownRules.RecruitCost(state, town, Line(state, "haven.zealot"), 10)
            + TownRules.RecruitCost(state, town, Line(state, "haven.crusader"), 20),
            plan.Cost);
    }

    [Fact]
    public void RecruitPlan_StopsWhereTheTreasuryRunsShort()
    {
        var (state, town) = Haven();
        WithDwellings(state, town);
        state.GetPlayer(Blue).Resources = TownRules.RecruitCost(state, town, Line(state, "haven.monk"), 5)
                                          + TownRules.RecruitCost(state, town, Line(state, "haven.zealot"), 3);

        var plan = TownPlans.Recruit(state, town);

        Assert.Equal([new RecruitOrder("haven.monk", 5), new RecruitOrder("haven.zealot", 3)], plan.Orders);
        Assert.Equal(state.GetPlayer(Blue).Resources, plan.Cost);
    }

    [Fact]
    public void RecruitPlan_SendsWhatTheFullGarrisonCannotTake_ToTheVisitingHero_AndSkipsWhatNeitherCan()
    {
        var (state, town) = Haven();
        WithDwellings(state, town);
        var others = new[] { "haven.pikeman", "haven.archer", "haven.griffin", "haven.lancer", "haven.paladin", "haven.radiant-spirit", "haven.guardian" };
        for (var slot = 0; slot < Army.SlotCount; slot++)
        {
            town.Garrison.Set(slot, new CreatureStack(others[slot], 1));
        }

        var hero = state.GetHero(A);
        hero.Army.Set(0, new CreatureStack("haven.monk", 1));
        for (var slot = 1; slot < Army.SlotCount - 1; slot++)
        {
            hero.Army.Set(slot, new CreatureStack(others[slot], 1));
        }

        var plan = TownPlans.Recruit(state, town);

        // The hero has one free slot: the monks join its monks, the zealots take the free slot, and the crusaders find no room.
        Assert.Equal([new RecruitOrder("haven.monk", 5, A), new RecruitOrder("haven.zealot", 10, A)], plan.Orders);
    }

    [Fact]
    public void RecruitMany_RecruitsEveryOrder_PaysOnce_AndPublishesOneEvent()
    {
        var (state, town) = Haven();
        WithDwellings(state, town);
        var session = Started(state, Rich);
        var plan = TownPlans.Recruit(state, town);
        var recruited = new List<CreaturesRecruitedMany>();
        var single = 0;
        var payments = 0;
        session.Events.Subscribe<CreaturesRecruitedMany>(recruited.Add);
        session.Events.Subscribe<CreaturesRecruited>(_ => single++);
        session.Events.Subscribe<ResourcesChanged>(_ => payments++);

        session.Execute(new RecruitManyCommand(Blue, town.Id, plan.Orders)).ThrowIfRejected();

        Assert.Equal(Rich - plan.Cost, state.GetPlayer(Blue).Resources);
        Assert.Equal(1, payments);
        Assert.Equal(0, single);
        Assert.Equal(plan.Orders, Assert.Single(recruited).Orders);
        Assert.Equal(20, town.Garrison.Stacks.Single(s => s.Stack.Creature == "haven.crusader").Stack.Count);
        Assert.Equal(0, town.AvailableOf(BuiltInCreatures.Footman));
        Assert.True(TownPlans.Recruit(state, town).IsEmpty);
    }

    [Fact]
    public void RecruitMany_IsAllOrNothing()
    {
        var (state, town) = Haven();
        WithDwellings(state, town);
        var session = Started(state, Rich);

        // Two orders from one line ask for more than waits: the second fails, so the first is not recruited either.
        var result = session.Execute(new RecruitManyCommand(Blue, town.Id, [new RecruitOrder("haven.monk", 3), new RecruitOrder("haven.monk", 3)]));

        Assert.Contains("Only 2", result.Error);
        Assert.Equal(Rich, state.GetPlayer(Blue).Resources);
        Assert.True(town.Garrison.IsEmpty);
        Assert.Equal(5, town.AvailableOf("haven.monk"));
        Assert.False(session.Execute(new RecruitManyCommand(Blue, town.Id, [])).IsAccepted);
        Assert.False(session.Execute(new RecruitManyCommand(Red, town.Id, [new RecruitOrder("haven.monk", 1)])).IsAccepted);
        Assert.Contains("not upgraded", session.Execute(new RecruitManyCommand(Blue, town.Id, [new RecruitOrder("haven.priest", 1)])).Error);
    }

    [Fact]
    public void RecruitMany_IntoTheVisitingHero_AndRefusedForAnotherHero()
    {
        var (state, town) = Haven();
        WithDwellings(state, town);
        var session = Started(state, Rich);

        session.Execute(new RecruitManyCommand(Blue, town.Id, [new RecruitOrder("haven.monk", 2, A), new RecruitOrder("haven.zealot", 3)])).ThrowIfRejected();

        Assert.Equal(new CreatureStack("haven.monk", 2), state.GetHero(A).Army.Stacks.Single(s => s.Stack.Creature == "haven.monk").Stack);
        Assert.Equal(new CreatureStack("haven.zealot", 3), town.Garrison.Stacks.Single().Stack);
        Assert.Contains("No own hero", session.Execute(new RecruitManyCommand(Blue, town.Id, [new RecruitOrder("haven.monk", 1, B)])).Error);
        Assert.Equal(3, town.AvailableOf("haven.monk"));
    }

    // Upgrade all ------------------------------------------------------------------------------------------

    private static (GameState State, Town Town) WithUpgradeableStacks(ResourceSet? resources = null)
    {
        var (state, town) = Haven(resources);
        town.SetDwelling(BuiltInCreatures.Footman, 2);
        town.SetDwelling(BuiltInCreatures.Pikeman, 2);
        town.SetDwelling(BuiltInCreatures.Archer, 2);
        town.SetDwelling("haven.zealot", 1);
        town.Garrison.Set(0, new CreatureStack(BuiltInCreatures.Pikeman, 5));
        town.Garrison.Set(1, new CreatureStack(BuiltInCreatures.Footman, 10));
        town.Garrison.Set(2, new CreatureStack("haven.zealot", 3)); // its dwelling is not upgraded
        state.GetHero(A).Army.Set(0, new CreatureStack(BuiltInCreatures.Archer, 4));
        return (state, town);
    }

    [Fact]
    public void UpgradePlan_TakesTheBestStacksFirst_FromBothRows()
    {
        var (state, town) = WithUpgradeableStacks();

        var plan = TownPlans.Upgrade(state, town);

        Assert.Equal(
            [(TownArmy.VisitingHero, 0, "haven.marksman"), (TownArmy.Garrison, 1, "haven.crusader"), (TownArmy.Garrison, 0, "haven.guardian")],
            plan.Orders.Select(o => (o.Army, o.Slot, o.To.Value)));
        Assert.Equal(plan.Orders.Aggregate(ResourceSet.Empty, (t, o) => t + o.Cost), plan.Cost);
    }

    [Fact]
    public void UpgradePlan_SkipsAStackItCannotPayFor_ButTakesCheaperOnes()
    {
        var (probe, probeTown) = WithUpgradeableStacks();
        var archers = TownRules.UpgradeCost(probe, probeTown, probe.Creatures.Get(BuiltInCreatures.Archer), 4);
        var pikemen = TownRules.UpgradeCost(probe, probeTown, probe.Creatures.Get(BuiltInCreatures.Pikeman), 5);
        var (state, town) = WithUpgradeableStacks(archers + pikemen);

        var plan = TownPlans.Upgrade(state, town);

        Assert.Equal(["haven.marksman", "haven.guardian"], plan.Orders.Select(o => o.To.Value));
    }

    [Fact]
    public void UpgradeAll_OfTheHerosRowAlone_TellsOnlyTheHerosArmy()
    {
        var (state, town) = WithUpgradeableStacks();
        for (var slot = 0; slot < Army.SlotCount; slot++)
        {
            town.Garrison.Set(slot, null);
        }

        var session = Started(state, Rich);
        var garrison = 0;
        var armies = new List<HeroId>();
        session.Events.Subscribe<GarrisonChanged>(_ => garrison++);
        session.Events.Subscribe<ArmyChanged>(e => armies.Add(e.Hero));

        session.Execute(new UpgradeAllCommand(Blue, town.Id)).ThrowIfRejected();

        Assert.Equal(0, garrison);
        Assert.Equal([A], armies);
        Assert.Equal(new CreatureStack("haven.marksman", 4), state.GetHero(A).Army[0]);
    }

    [Fact]
    public void UpgradeAll_UpgradesThePlannedStacks_AndIsRefusedWithNothingToDo()
    {
        var (state, town) = WithUpgradeableStacks();
        var session = Started(state, Rich);
        var plan = TownPlans.Upgrade(state, town);
        var upgraded = new List<StackUpgraded>();
        session.Events.Subscribe<StackUpgraded>(upgraded.Add);

        session.Execute(new UpgradeAllCommand(Blue, town.Id)).ThrowIfRejected();

        Assert.Equal(3, upgraded.Count);
        Assert.Equal(Rich - plan.Cost, state.GetPlayer(Blue).Resources);
        Assert.Equal(new CreatureStack("haven.crusader", 10), town.Garrison[1]);
        Assert.Equal(new CreatureStack("haven.marksman", 4), state.GetHero(A).Army[0]);
        Assert.Equal(new CreatureStack("haven.zealot", 3), town.Garrison[2]);
        Assert.Contains("Nothing", session.Execute(new UpgradeAllCommand(Blue, town.Id)).Error);
    }

    // Dismissing, renaming ---------------------------------------------------------------------------------

    [Fact]
    public void Dismissing_RemovesAStack_ButNeverAHerosLast_AndMayEmptyTheGarrison()
    {
        var (state, town) = Haven();
        var hero = state.GetHero(A);
        hero.Army.Set(0, new CreatureStack(BuiltInCreatures.Footman, 10));
        town.Garrison.Set(4, new CreatureStack(BuiltInCreatures.Pikeman, 3));
        var session = Start(state);
        var dismissed = new List<StackDismissed>();
        session.Events.Subscribe<StackDismissed>(dismissed.Add);

        Assert.Contains("last stack", session.Execute(new DismissStackCommand(Blue, 0, Hero: A)).Error);
        hero.Army.Set(2, new CreatureStack(BuiltInCreatures.Archer, 2));
        session.Execute(new DismissStackCommand(Blue, 2, Hero: A)).ThrowIfRejected();
        session.Execute(new DismissStackCommand(Blue, 4, Town: town.Id)).ThrowIfRejected();

        Assert.Equal(1, hero.Army.StackCount);
        Assert.True(town.Garrison.IsEmpty);
        Assert.Equal(
            [new StackDismissed(A, null, 2, new CreatureStack(BuiltInCreatures.Archer, 2)), new StackDismissed(null, town.Id, 4, new CreatureStack(BuiltInCreatures.Pikeman, 3))],
            dismissed);
        Assert.Contains("empty", session.Execute(new DismissStackCommand(Blue, 4, Town: town.Id)).Error);
        Assert.False(session.Execute(new DismissStackCommand(Blue, 0)).IsAccepted);
        Assert.False(session.Execute(new DismissStackCommand(Blue, 0, A, town.Id)).IsAccepted);
        Assert.False(session.Execute(new DismissStackCommand(Red, 0, Hero: A)).IsAccepted);
    }

    [Fact]
    public void Renaming_TrimsTheName_AndRefusesBlankLongOrForeignOnes()
    {
        var (state, town) = Haven();
        var session = Start(state);
        var renamed = new List<TownRenamed>();
        session.Events.Subscribe<TownRenamed>(renamed.Add);

        session.Execute(new RenameTownCommand(Blue, town.Id, "  Brightwatch  ")).ThrowIfRejected();

        Assert.Equal("Brightwatch", town.Name);
        Assert.Equal([new TownRenamed(town.Id, "Brightwatch")], renamed);
        Assert.Contains("needs a name", session.Execute(new RenameTownCommand(Blue, town.Id, "   ")).Error);
        Assert.Contains("at most", session.Execute(new RenameTownCommand(Blue, town.Id, new string('a', TownRules.MaxNameLength + 1))).Error);
        Assert.Contains("one line", session.Execute(new RenameTownCommand(Blue, town.Id, "Bright\nwatch")).Error);
        Assert.Contains("one line", session.Execute(new RenameTownCommand(Blue, town.Id, "Bright\u2028watch")).Error);
        Assert.False(session.Execute(new RenameTownCommand(Red, town.Id, "Mine")).IsAccepted);
        Assert.Equal("Brightwatch", town.Name);
    }

    // Rumours ----------------------------------------------------------------------------------------------

    [Fact]
    public void Rumours_TheTableIsDrawnPerPlayerAndWeek_TheSameAllWeek()
    {
        var (state, _) = Haven();
        Start(state);

        var first = TavernRules.Rumour(state, Blue);

        Assert.NotNull(first);
        Assert.Equal(first, Rumours.OfWeek(state, Blue));
        Assert.Null(TavernRules.Rumour(state)); // no player, no map rumours
        var weeks = Enumerable.Range(0, 12).Select(w => Rumours.OfWeek(WithWeek(state, w), Blue)).Distinct().Count();
        Assert.True(weeks > 1, "the rumour changes from week to week");
    }

    private static GameState WithWeek(GameState state, int weeks)
    {
        state.Date = new GameDate(1 + (7 * weeks));
        return state;
    }

    [Fact]
    public void Rumours_FillTheirNamesFromTheGame_AndSkipTextsItCannotFill()
    {
        var (state, _) = Haven();
        Start(state);
        string[] table = ["The {treasure} lies in the {where}."];

        Assert.Null(Rumours.OfWeek(state, Blue, table)); // nothing lies on the map

        var pickup = Add<ArtifactPickup>(state, BuiltInMapObjects.ArtifactId(global::EternalWars.Core.Domain.Artifacts.ArtifactTier.Minor), 11, 0);
        var artifact = state.Artifacts.All.OrderBy(a => a.Id).First();
        pickup.Artifact = artifact.Id;

        Assert.Equal($"The {artifact.Name} lies in the north-east.", Rumours.OfWeek(state, Blue, table));
        Assert.Equal(state.GetPlayer(Blue).Name + " is strong.", Rumours.OfWeek(state, Blue, ["{mosttowns} is strong."]));
    }

    [Fact]
    public void Rumours_TheMapsOwnRumoursComeFirst()
    {
        var (state, _) = Haven();
        state.SetRules(state.Rules with { Rumours = ["The bridge is out."] });
        Start(state);

        Assert.Equal("The bridge is out.", TavernRules.Rumour(state, Blue));
    }

    [Fact]
    public void Region_NamesTheThirdsOfTheMap()
    {
        var (state, _) = Haven();

        Assert.Equal("north-west", Rumours.Region(state, new MapPosition(0, 0)));
        Assert.Equal("middle", Rumours.Region(state, new MapPosition(5, 4)));
        Assert.Equal("south", Rumours.Region(state, new MapPosition(5, 9)));
        Assert.Equal("east", Rumours.Region(state, new MapPosition(11, 4)));
        Assert.Equal("depths", Rumours.Region(state, new MapPosition(0, 0, 1)));
    }

    // Trading ----------------------------------------------------------------------------------------------

    [Fact]
    public void Exchanging_GivesOneResourceForAnother_AtTheSellPriceOverTheBuyPrice()
    {
        var (state, town) = Haven(new ResourceSet(Gold: 0, Ore: 250));
        town.SetLevel(BuiltInBuildings.Marketplace, 1);
        var session = Started(state, new ResourceSet(Ore: 250));
        var ore = MarketplaceRules.TownRate(state, Blue, ResourceType.Ore)!.Value;
        var crystal = MarketplaceRules.TownRate(state, Blue, ResourceType.Crystal)!.Value;
        var cost = MarketplaceRules.ExchangeCost(ore, crystal, 1);
        var exchanged = new List<ResourcesExchanged>();
        session.Events.Subscribe<ResourcesExchanged>(exchanged.Add);

        session.Execute(new ExchangeResourcesCommand(Blue, ResourceType.Ore, ResourceType.Crystal, 1)).ThrowIfRejected();

        Assert.Equal((crystal.BuyPrice + ore.SellPrice - 1) / ore.SellPrice, cost);
        Assert.Equal(new ResourceSet(Ore: 250 - (int)cost, Crystal: 1), state.GetPlayer(Blue).Resources);
        Assert.Equal([new ResourcesExchanged(Blue, ResourceType.Ore, (int)cost, ResourceType.Crystal, 1, null)], exchanged);
        Assert.Equal(250 * ore.SellPrice / crystal.BuyPrice, MarketplaceRules.MaxExchange(ore, crystal, 250));
        Assert.Contains("Not enough", session.Execute(new ExchangeResourcesCommand(Blue, ResourceType.Ore, ResourceType.Crystal, 2)).Error);
        Assert.False(session.Execute(new ExchangeResourcesCommand(Blue, ResourceType.Gold, ResourceType.Ore, 1)).IsAccepted);
        Assert.False(session.Execute(new ExchangeResourcesCommand(Blue, ResourceType.Ore, ResourceType.Ore, 1)).IsAccepted);
        Assert.False(session.Execute(new ExchangeResourcesCommand(Blue, ResourceType.Ore, ResourceType.Lumber, 0)).IsAccepted);

        town.SetLevel(BuiltInBuildings.Marketplace, 0);
        Assert.Contains("marketplace", session.Execute(new ExchangeResourcesCommand(Blue, ResourceType.Ore, ResourceType.Lumber, 1)).Error);
    }

    [Fact]
    public void Sending_GoesToATeammateOnly_FromAPlayerWithAMarketplace()
    {
        var (state, town) = Haven(new ResourceSet(Gold: 1000, Ore: 5));
        var session = Started(state, new ResourceSet(Gold: 1000, Ore: 5));
        var gift = new ResourceSet(Gold: 300, Ore: 2);

        Assert.Contains("teammate", session.Execute(new SendResourcesCommand(Blue, Red, gift)).Error);
        Assert.Contains("teammate", session.Execute(new SendResourcesCommand(Blue, Blue, gift)).Error);
        state.GetPlayer(Red).Team = state.GetPlayer(Blue).Team;
        state.GetPlayer(Red).IsDefeated = true;
        Assert.Contains("out of the game", session.Execute(new SendResourcesCommand(Blue, Red, gift)).Error);
        state.GetPlayer(Red).IsDefeated = false;
        Assert.Contains("marketplace", session.Execute(new SendResourcesCommand(Blue, Red, gift)).Error);
        town.SetLevel(BuiltInBuildings.Marketplace, 1);
        Assert.Contains("Not enough", session.Execute(new SendResourcesCommand(Blue, Red, new ResourceSet(Gold: 5000))).Error);
        Assert.Contains("positive", session.Execute(new SendResourcesCommand(Blue, Red, ResourceSet.Empty)).Error);
        Assert.Contains("positive", session.Execute(new SendResourcesCommand(Blue, Red, new ResourceSet(Gold: 5, Ore: -1))).Error);
        var redBefore = state.GetPlayer(Red).Resources;

        // The last command: with the only two players on one team the game ends once a command is accepted.
        session.Execute(new SendResourcesCommand(Blue, Red, gift)).ThrowIfRejected();

        Assert.Equal(new ResourceSet(Gold: 700, Ore: 3), state.GetPlayer(Blue).Resources);
        Assert.Equal(redBefore + gift, state.GetPlayer(Red).Resources);
    }
}
