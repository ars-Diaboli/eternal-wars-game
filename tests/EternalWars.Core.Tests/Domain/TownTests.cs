using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>Towns (ADR-046): buildings, the 7-of-9 dwelling rule (ADR-030), recruiting, garrisons and capture.</summary>
public class TownTests
{
    /// <summary>A Haven town at (0, 0) has its entrance at (3, 6); hero A waits just below it.</summary>
    private static readonly string[] Rows =
    [
        "............",
        "............",
        "............",
        "............",
        "............",
        "............",
        "............",
        "...A........",
        "............",
        "...........B",
    ];

    private static readonly ResourceSet Rich = new(Gold: 100_000, Lumber: 100, Ore: 100, Crystal: 50, Mercury: 50, Sulfur: 50);

    [Fact]
    public void Town_IsSevenBySevenWithFreeCorners_AndItsTownHallPaysEveryDay()
    {
        var state = Create(Rows);
        var town = AddTown(state, TownIds.Haven, 0, 0, Blue);

        var cells = town.OccupiedCells().ToList();
        Assert.Equal(45, cells.Count); // 44 blocked + the entrance
        Assert.Equal(44, cells.Count(c => c.Cell == FootprintCell.Blocked));
        Assert.Equal(new MapPosition(3, 6), Assert.Single(cells, c => c.Cell == FootprintCell.Visit).Position);
        Assert.Null(state.ObjectCellAt(new MapPosition(0, 0)));
        Assert.Equal(BuiltInBuildings.DefaultStart, town.Buildings);

        var session = Start(state);

        Assert.Equal(new ResourceSet(Gold: 500), state.GetPlayer(Blue).Resources);
        Assert.Equal(new ResourceSet(Gold: 500), session.Economy.DailyIncome(Blue));
        Assert.Equal(ResourceSet.Empty, session.Economy.DailyIncome(Red));
    }

    [Fact]
    public void Building_IsOncePerDay_NeedsItsRequirements_AndIsPaidFor()
    {
        var (session, town) = Setup(Rich);
        var blue = session.State.GetPlayer(Blue);
        var built = Record<TownBuilt>(session);

        var refused = session.Execute(new BuildCommand(Blue, town.Id, BuiltInBuildings.TownHall));
        Assert.Contains("Blacksmith", refused.Error);

        var before = blue.Resources;
        session.Execute(new BuildCommand(Blue, town.Id, BuiltInBuildings.Blacksmith)).ThrowIfRejected();
        Assert.Equal(before - new ResourceSet(Gold: 1000, Lumber: 5, Ore: 5), blue.Resources);
        Assert.Contains("already built", session.Execute(new BuildCommand(Blue, town.Id, BuiltInBuildings.Fort)).Error);

        NextDay(session);
        session.Execute(new BuildCommand(Blue, town.Id, BuiltInBuildings.Marketplace)).ThrowIfRejected();
        NextDay(session);
        session.Execute(new BuildCommand(Blue, town.Id, BuiltInBuildings.MageGuild)).ThrowIfRejected();
        NextDay(session);
        session.Execute(new BuildCommand(Blue, town.Id, BuiltInBuildings.TownHall)).ThrowIfRejected();

        Assert.Equal(2, town.Level(BuiltInBuildings.TownHall));
        Assert.Equal(new ResourceSet(Gold: 1000), session.Economy.DailyIncome(Blue));
        Assert.Equal(new TownBuilt(town.Id, BuiltInBuildings.TownHall, 2), built[^1]);
        Assert.Equal(4, built.Count);
        Assert.False(session.Execute(new BuildCommand(Red, town.Id, BuiltInBuildings.Fort)).IsAccepted); // not Red's
    }

    [Fact]
    public void Building_NeedsTheResources()
    {
        var (session, town) = Setup(ResourceSet.Empty);

        var refused = session.Execute(new BuildCommand(Blue, town.Id, BuiltInBuildings.Blacksmith));

        Assert.Contains("Not enough", refused.Error);
        Assert.Equal(0, town.Level(BuiltInBuildings.Blacksmith));
    }

    [Fact]
    public void Dwellings_SevenOfNine_EveryTierStaysReachable_AndTheChoiceIsFinal()
    {
        var (session, town) = Setup(Rich);
        var s = session.State;
        foreach (var line in new[] { BuiltInCreatures.Footman, BuiltInCreatures.Pikeman, BuiltInCreatures.Zealot, BuiltInCreatures.Archer, BuiltInCreatures.Monk })
        {
            town.SetDwelling(line, 1);
        }

        // Six built with tiers 4 and 5 still empty would leave one dwelling for two tiers.
        Assert.Contains("tier 4", TownRules.CheckDwelling(s, town, BuiltInCreatures.Griffin));
        Assert.Null(TownRules.CheckDwelling(s, town, BuiltInCreatures.Lancer));

        town.SetDwelling(BuiltInCreatures.Lancer, 1);
        town.SetDwelling(BuiltInCreatures.RadiantSpirit, 1);

        Assert.Contains("at most 7", TownRules.CheckDwelling(s, town, BuiltInCreatures.Paladin));
        Assert.Contains("at most 7", session.Execute(new BuildDwellingCommand(Blue, town.Id, BuiltInCreatures.Griffin)).Error);
        Assert.Null(TownRules.CheckDwelling(s, town, BuiltInCreatures.Lancer)); // upgrades are still open
        Assert.Contains("no dwelling", TownRules.CheckDwelling(s, town, "inferno.imp"));
    }

    [Fact]
    public void NewDwelling_OpensWithAWeekOfCreatures_WhichAccumulate_AndAreRecruitedIntoGarrisonOrVisitingHero()
    {
        var (session, town) = Setup(Rich);
        var blue = session.State.GetPlayer(Blue);

        session.Execute(new BuildDwellingCommand(Blue, town.Id, BuiltInCreatures.Footman)).ThrowIfRejected();
        Assert.Equal((1, 18), (town.DwellingLevel(BuiltInCreatures.Footman), town.AvailableOf(BuiltInCreatures.Footman)));

        var before = blue.Resources;
        session.Execute(new RecruitCommand(Blue, town.Id, BuiltInCreatures.Footman, 10)).ThrowIfRejected();
        Assert.Equal(before - new ResourceSet(Gold: 550), blue.Resources);
        Assert.Equal(new CreatureStack(BuiltInCreatures.Footman, 10), town.Garrison[0]);

        Assert.False(session.Execute(new RecruitCommand(Blue, town.Id, BuiltInCreatures.Footman, 8, A)).IsAccepted); // A is outside
        Assert.Equal(MoveResult.Moved, session.Step(A, Direction.North));
        session.Execute(new RecruitCommand(Blue, town.Id, BuiltInCreatures.Footman, 8, A)).ThrowIfRejected();
        Assert.Equal(new CreatureStack(BuiltInCreatures.Footman, 18), session.State.GetHero(A).Army[0]);
        Assert.Contains("Only 0", session.Execute(new RecruitCommand(Blue, town.Id, BuiltInCreatures.Footman, 1)).Error);

        NextWeek(session);
        NextWeek(session);

        Assert.Equal(36, town.AvailableOf(BuiltInCreatures.Footman));
    }

    [Fact]
    public void UpgradedDwelling_RecruitsTheUpgrade_AndUpgradesStacksForThePriceDifference()
    {
        var (session, town) = Setup(Rich);
        var blue = session.State.GetPlayer(Blue);
        town.SetDwelling(BuiltInCreatures.Footman, 1);
        town.SetDwelling(BuiltInCreatures.Pikeman, 1);
        town.SetAvailable(BuiltInCreatures.Footman, 10);
        town.Garrison.Set(0, new CreatureStack(BuiltInCreatures.Footman, 10));

        Assert.Contains("not upgraded", session.Execute(new RecruitCommand(Blue, town.Id, BuiltInCreatures.Crusader, 1)).Error);
        Assert.Contains("not upgraded", session.Execute(new UpgradeStackCommand(Blue, town.Id, TownArmy.Garrison, 0)).Error);

        session.Execute(new BuildDwellingCommand(Blue, town.Id, BuiltInCreatures.Footman)).ThrowIfRejected();
        Assert.Equal(2, town.DwellingLevel(BuiltInCreatures.Footman));

        var before = blue.Resources;
        session.Execute(new UpgradeStackCommand(Blue, town.Id, TownArmy.Garrison, 0)).ThrowIfRejected();
        Assert.Equal(new CreatureStack(BuiltInCreatures.Crusader, 10), town.Garrison[0]);
        Assert.Equal(before - new ResourceSet(Gold: 150), blue.Resources); // (70 − 55) × 10

        session.Execute(new RecruitCommand(Blue, town.Id, BuiltInCreatures.Crusader, 4)).ThrowIfRejected();
        Assert.Equal((14, 6), (town.Garrison[0]!.Count, town.AvailableOf(BuiltInCreatures.Footman)));
    }

    [Fact]
    public void GarrisonStacks_MoveToAndFromTheVisitingHero_WhoKeepsOneStack()
    {
        var (session, town) = Setup(Rich);
        town.Garrison.Set(2, new CreatureStack(BuiltInCreatures.Pikeman, 6));
        Assert.False(session.Execute(new MoveTownStackCommand(Blue, town.Id, TownArmy.Garrison, 2, TownArmy.VisitingHero, 1)).IsAccepted);

        session.Step(A, Direction.North);
        var hero = session.State.GetHero(A);

        Assert.Contains("keep at least one", session.Execute(new MoveTownStackCommand(Blue, town.Id, TownArmy.VisitingHero, 0, TownArmy.Garrison, 0)).Error);
        session.Execute(new MoveTownStackCommand(Blue, town.Id, TownArmy.Garrison, 2, TownArmy.VisitingHero, 1, 4)).ThrowIfRejected();
        session.Execute(new MoveTownStackCommand(Blue, town.Id, TownArmy.VisitingHero, 0, TownArmy.Garrison, 0)).ThrowIfRejected();

        Assert.Equal([null, new CreatureStack(BuiltInCreatures.Pikeman, 4)], hero.Army.Slots.Take(2));
        Assert.Equal([new CreatureStack(BuiltInCreatures.Footman, 10), null, new CreatureStack(BuiltInCreatures.Pikeman, 2)], town.Garrison.Slots.Take(3));
    }

    [Fact]
    public void EnemyTown_WithoutGarrison_IsCapturedByWalkingIn()
    {
        var state = Create(Rows);
        var town = AddTown(state, TownIds.Haven, 0, 0, Red);
        var session = Start(state);
        var changes = Record<TownOwnerChanged>(session);

        session.Step(A, Direction.North);

        Assert.Equal(Blue, town.Owner);
        Assert.Equal([new TownOwnerChanged(town.Id, Red, Blue)], changes);
        Assert.Equal(new ResourceSet(Gold: 500), session.Economy.DailyIncome(Blue));
        Assert.Contains(state.Memory.Of(Red), k => k.Id == town.Id); // Red still remembers its town (now Blue's)
    }

    [Fact]
    public void Garrison_IsFoughtFor_BeatingItCapturesTheTown()
    {
        var state = Create(Rows);
        var town = AddTown(state, TownIds.Haven, 0, 0, Red);
        town.Garrison.Set(3, new CreatureStack(BuiltInCreatures.Pikeman, 1));
        state.GetHero(A).Army.Set(1, new CreatureStack(BuiltInCreatures.Champion, 5));
        var session = Start(state);

        Assert.Equal(MoveResult.Moved, session.Step(A, Direction.North));
        Assert.Equal((town.Id, (HeroId?)null), (state.Battle!.Defender.Guard, state.Battle.Defender.Hero));
        session.AutoResolve();

        Assert.Null(state.Battle);
        Assert.Equal(Blue, town.Owner);
        Assert.True(town.Garrison.IsEmpty);
        Assert.Equal(new MapPosition(3, 6), state.GetHero(A).Position);
    }

    [Fact]
    public void StrongGarrison_Survives_AndKeepsItsSurvivors()
    {
        var state = Create(Rows);
        var town = AddTown(state, TownIds.Haven, 0, 0, Red);
        town.Garrison.Set(0, new CreatureStack(BuiltInCreatures.Champion, 20));
        var session = Start(state);

        session.Step(A, Direction.North);
        session.AutoResolve();

        Assert.Equal(Red, town.Owner);
        Assert.DoesNotContain(state.Heroes, h => h.Id == A);
        Assert.Equal(BuiltInCreatures.Champion, town.Garrison[0]!.Creature);
        Assert.InRange(town.Garrison[0]!.Count, 1, 20);
    }

    [Fact]
    public void HeroWithoutArmy_CannotAttackAGarrison()
    {
        var state = Create(Rows);
        var town = AddTown(state, TownIds.Haven, 0, 0, Red);
        town.Garrison.Set(0, new CreatureStack(BuiltInCreatures.Footman, 1));
        state.GetHero(A).Army.Set(0, null);
        var session = Start(state);

        Assert.Equal(MoveResult.NoArmy, session.Step(A, Direction.North));
        Assert.Equal(Red, town.Owner);
    }

    [Fact]
    public void WeeklyGrowth_FillsEveryBuiltDwelling_OwnedOrNot()
    {
        var state = Create(Rows);
        var town = AddTown(state, TownIds.Haven, 0, 0);
        town.SetDwelling(BuiltInCreatures.Pikeman, 2);
        town.SetDwelling(BuiltInCreatures.RadiantSpirit, 1);
        var session = Start(state);
        var grew = Record<TownCreaturesGrew>(session);

        NextWeek(session);

        Assert.Equal((16, 1, 0), (town.AvailableOf(BuiltInCreatures.Pikeman), town.AvailableOf(BuiltInCreatures.RadiantSpirit), town.AvailableOf(BuiltInCreatures.Footman)));
        Assert.Single(grew);
    }

    [Fact]
    public void Dwellings_NeedTheFort_InPlayAndInTheEditor()
    {
        var (session, town) = Setup(Rich);
        town.SetLevel(BuiltInBuildings.Fort, 0);

        Assert.Contains("Fort", session.Execute(new BuildDwellingCommand(Blue, town.Id, BuiltInCreatures.Footman)).Error);
        Assert.Equal(0, town.DwellingLevel(BuiltInCreatures.Footman));

        town.SetLevel(BuiltInBuildings.Fort, 1);
        session.Execute(new BuildDwellingCommand(Blue, town.Id, BuiltInCreatures.Footman)).ThrowIfRejected();
        Assert.Equal(1, town.DwellingLevel(BuiltInCreatures.Footman));

        var scenario = Create(Rows);
        var editor = new MapEditorSession(scenario);
        editor.PlaceObject(BuiltInMapObjects.TownId(TownIds.Haven), new MapPosition(0, 0), out var id);
        Assert.Equal(EditResult.Ok, editor.SetTownBuilding(id, BuiltInBuildings.Fort, 0));
        Assert.Equal(EditResult.RequirementMissing, editor.SetTownDwelling(id, BuiltInCreatures.Footman, 1));
        Assert.Equal(EditResult.RequirementMissing, editor.SetTownDwelling(id, BuiltInCreatures.Footman, 2));

        var fortless = (Town)scenario.GetObject(id);
        fortless.SetDwelling(BuiltInCreatures.Footman, 1);
        Assert.Contains(MapValidator.Validate(scenario), i => i.Severity == IssueSeverity.Error && i.Message.Contains("Fort", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(1, 18, 1)] // Fort: base growth
    [InlineData(2, 27, 1)] // Citadel: +50 %, rounded down (1 → 1)
    [InlineData(3, 36, 2)] // Castle: +100 %
    public void FortLevel_RaisesTheWeeklyGrowthOfTownDwellings(int fort, int footmen, int spirits)
    {
        var state = Create(Rows);
        var town = AddTown(state, TownIds.Haven, 0, 0, Blue);
        town.SetLevel(BuiltInBuildings.Fort, fort);
        town.SetDwelling(BuiltInCreatures.RadiantSpirit, 1);
        var session = Start(state);
        state.GetPlayer(Blue).Resources = Rich;
        Assert.Equal((18, 1), (state.Creatures.Get(BuiltInCreatures.Footman).WeeklyGrowth, state.Creatures.Get(BuiltInCreatures.RadiantSpirit).WeeklyGrowth));

        // A new dwelling opens with one week of the bonus growth too.
        session.Execute(new BuildDwellingCommand(Blue, town.Id, BuiltInCreatures.Footman)).ThrowIfRejected();
        Assert.Equal(footmen, town.AvailableOf(BuiltInCreatures.Footman));
        Assert.Equal(footmen, TownRules.WeeklyGrowth(town, state.Creatures.Get(BuiltInCreatures.Footman)));

        NextWeek(session);

        Assert.Equal((footmen * 2, spirits), (town.AvailableOf(BuiltInCreatures.Footman), town.AvailableOf(BuiltInCreatures.RadiantSpirit)));
    }

    [Fact]
    public void MapDwellings_IgnoreTheFortBonus()
    {
        var state = Create(Rows);
        var town = AddTown(state, TownIds.Haven, 0, 0, Blue);
        town.SetLevel(BuiltInBuildings.Fort, 3);
        var dwelling = Add<CreatureDwelling>(state, BuiltInMapObjects.Dwelling, 9, 2);
        dwelling.Creature = BuiltInCreatures.Footman;
        dwelling.Owner = Blue;
        var session = Start(state);
        Assert.Equal(18, dwelling.Available);

        NextWeek(session);

        Assert.Equal(36, dwelling.Available);
    }

    [Fact]
    public void Towns_RoundTripThroughTheMapFormat_AndOmittedBuildingsAreTheDefaultStart()
    {
        var state = Create(Rows);
        var town = AddTown(state, TownIds.Inferno, 0, 0, Red);
        town.Name = "Ashgate";
        town.SetLevel(BuiltInBuildings.Blacksmith, 1);
        town.SetDwelling("inferno.imp", 2);
        town.SetAvailable("inferno.imp", 7);
        town.Garrison.Set(4, new CreatureStack("inferno.familiar", 12));
        town.LastBuilt = new GameDate(3);

        var json = MapSerializer.Serialize(MapConverter.ToDocument(state));
        var restored = (Town)MapConverter.ToGameState(MapSerializer.Deserialize(json)).GetObject(town.Id);

        Assert.Contains("\"kind\": \"town\"", json);
        Assert.Equal((Red, "Ashgate", TownIds.Inferno, new GameDate(3)), (restored.Owner!.Value, restored.Name, restored.TownType, restored.LastBuilt!.Value));
        Assert.Equal(town.Buildings, restored.Buildings);
        Assert.Equal(town.Dwellings, restored.Dwellings);
        Assert.Equal(town.Available, restored.Available);
        Assert.Equal(town.Garrison.Slots, restored.Garrison.Slots);

        var bare = MapConverter.ToDocument(state) with
        {
            Objects = [new TownDocument { Id = 1, Definition = BuiltInMapObjects.TownId(TownIds.Haven), X = 0, Y = 0 }],
        };
        var fresh = (Town)MapConverter.ToGameState(bare).GetObject(new MapObjectId(1));
        Assert.Equal((null, "Haven"), (fresh.Owner, fresh.Name));
        Assert.Equal(BuiltInBuildings.DefaultStart, fresh.Buildings);
    }

    [Fact]
    public void Editor_PrebuildsTowns_UnderTheDwellingRule_AndUndoes()
    {
        var scenario = Create(Rows);
        var editor = new MapEditorSession(scenario);
        Assert.Equal(EditResult.Ok, editor.PlaceObject(BuiltInMapObjects.TownId(TownIds.Haven), new MapPosition(0, 0), out var id));
        var town = (Town)scenario.GetObject(id);

        Assert.Equal(EditResult.Ok, editor.SetTownOwner(id, Red));
        Assert.Equal(EditResult.Ok, editor.SetTownBuilding(id, BuiltInBuildings.MageGuild, 2));
        Assert.Equal(EditResult.InvalidValue, editor.SetTownBuilding(id, BuiltInBuildings.Tavern, 3));
        Assert.Equal(EditResult.InvalidTarget, editor.SetTownBuilding(id, "dwelling-1", 1));
        Assert.Equal(EditResult.RequirementMissing, editor.SetTownBuilding(id, BuiltInBuildings.TownHall, 2)); // no blacksmith, marketplace
        Assert.Equal(EditResult.RequirementMissing, editor.SetTownBuilding(id, BuiltInBuildings.Tavern, 0)); // the fort needs it
        Assert.Equal(EditResult.Ok, editor.SetTownBuilding(id, BuiltInBuildings.Fort, 0));
        Assert.Equal(EditResult.Ok, editor.SetTownBuilding(id, BuiltInBuildings.Tavern, 0));
        Assert.Equal(EditResult.RequirementMissing, editor.SetTownBuilding(id, BuiltInBuildings.Fort, 3));
        Assert.True(editor.Undo());
        Assert.True(editor.Undo());
        foreach (var line in new[] { BuiltInCreatures.Footman, BuiltInCreatures.Pikeman, BuiltInCreatures.Zealot, BuiltInCreatures.Archer, BuiltInCreatures.Monk })
        {
            Assert.Equal(EditResult.Ok, editor.SetTownDwelling(id, line, 1));
        }

        Assert.Equal(EditResult.DwellingRule, editor.SetTownDwelling(id, BuiltInCreatures.Griffin, 1));
        Assert.Equal(EditResult.DwellingRule, editor.SetTownDwelling(id, "inferno.imp", 1));
        Assert.Equal(EditResult.Ok, editor.SetTownDwelling(id, BuiltInCreatures.Lancer, 2));
        Assert.Equal(EditResult.Ok, editor.SetGarrisonSlot(id, 0, new CreatureStack(BuiltInCreatures.Footman, 5)));
        Assert.Equal(EditResult.PlayerInUse, editor.RemoveLastPlayer()); // Red owns the town
        Assert.DoesNotContain(MapValidator.Validate(scenario), i => i.Severity == IssueSeverity.Error);

        Assert.True(editor.Undo());
        Assert.True(editor.Undo());

        Assert.Equal(0, town.DwellingLevel(BuiltInCreatures.Lancer));
        Assert.True(town.Garrison.IsEmpty);
        Assert.Equal((Red, 2), (town.Owner!.Value, town.Level(BuiltInBuildings.MageGuild)));
    }

    [Fact]
    public void Validator_FlagsTownsThatBreakABuildingRequirement()
    {
        var scenario = Create(Rows);
        var town = AddTown(scenario, TownIds.Haven, 0, 0, Blue);
        Assert.DoesNotContain(MapValidator.Validate(scenario), i => i.Severity == IssueSeverity.Error);

        town.SetLevel(BuiltInBuildings.Tavern, 0);

        Assert.Contains(MapValidator.Validate(scenario), i => i.Severity == IssueSeverity.Error && i.Message.Contains("Tavern", StringComparison.Ordinal));
    }

    [Fact]
    public void TownCommands_ReplayExactly()
    {
        var state = Create(Rows);
        var town = AddTown(state, TownIds.Haven, 0, 0, Blue);
        var map = MapConverter.ToDocument(state);
        var session = new GameSession(MapConverter.ToGameState(map, options: new GameOptions(Seed: 5)));
        session.Start();

        // Only the town hall's 500 gold a day to spend.
        session.Execute(new BuildDwellingCommand(Blue, town.Id, BuiltInCreatures.Footman)).ThrowIfRejected();
        session.Execute(new EndTurnCommand(Blue)).ThrowIfRejected();
        session.Execute(new EndTurnCommand(Red)).ThrowIfRejected();
        session.Execute(new RecruitCommand(Blue, town.Id, BuiltInCreatures.Footman, 5)).ThrowIfRejected();
        session.Execute(new StepHeroCommand(Blue, A, Direction.North)).ThrowIfRejected();
        session.Execute(new MoveTownStackCommand(Blue, town.Id, TownArmy.Garrison, 0, TownArmy.VisitingHero, 3, 2)).ThrowIfRejected();
        session.Execute(new RecruitCommand(Blue, town.Id, BuiltInCreatures.Footman, 3, A)).ThrowIfRejected();
        Assert.False(session.Execute(new UpgradeStackCommand(Blue, town.Id, TownArmy.VisitingHero, 0)).IsAccepted); // not logged

        var json = GameRecordSerializer.Serialize(GameReplay.Record(map, session));
        var replayed = GameReplay.Replay(GameRecordSerializer.Deserialize(json));

        Assert.Contains("\"type\": \"recruit\"", json);
        Assert.Equal(GameStateDigest.Describe(session.State), GameStateDigest.Describe(replayed.State));
    }

    [Fact]
    public void BuildingTable_HasTheDraftCosts_AndRejectsBrokenLevels()
    {
        var catalog = BuildingCatalog.Default;

        Assert.Equal(new ResourceSet(Gold: 4000, Lumber: 8, Ore: 8, Mercury: 2), catalog.Get(BuiltInBuildings.Dwelling(4), 2).Cost);
        Assert.Equal(new ResourceSet(Gold: 2000), catalog.Get(BuiltInBuildings.TownHall, 3).Income);
        Assert.Equal([new BuildingRequirement(BuiltInBuildings.Tavern, 1)], catalog.Get(BuiltInBuildings.Fort, 1).Requires);
        Assert.DoesNotContain(catalog.TownBuildings, b => b.StartsWith("dwelling", StringComparison.Ordinal));

        var dwellings = string.Concat(Enumerable.Range(1, 5).Select(t => $"dwelling-{t},1,D,1\ndwelling-{t},2,U,1\n"));
        const string header = "id,level,name,gold,requires\n";
        Assert.Throws<FormatException>(() => new BuildingCatalog(BuildingTable.Parse(header + dwellings + "fort,2,F,1\n")));
        Assert.Throws<FormatException>(() => new BuildingCatalog(BuildingTable.Parse(header + dwellings + "fort,1,F,1,tavern 1\n")));
        Assert.Throws<FormatException>(() => new BuildingCatalog(BuildingTable.Parse(header + "fort,1,F,1\n")));
        Assert.Contains("column 'requires'", Assert.Throws<FormatException>(() => BuildingTable.Parse(header + "fort,1,F,1,tavern\n")).Message);
    }

    [Fact]
    public void TownsScenario_IsValid_AndEachSideStartsWithTownAndMineIncome()
    {
        var scenario = TownsScenario.Create();

        Assert.DoesNotContain(MapValidator.Validate(scenario), i => i.Severity == IssueSeverity.Error);
        var session = new GameSession(MapConverter.ToGameState(MapConverter.ToDocument(scenario)));
        session.Start();

        Assert.Equal(new ResourceSet(Gold: 1500), session.Economy.DailyIncome(Blue));
        Assert.Equal(new ResourceSet(Gold: 1500), session.Economy.DailyIncome(Red));
        var blueTown = session.State.ObjectsOf<Town>().Single(t => t.Owner == Blue);
        Assert.Equal(blueTown.VisitPosition.Offset(0, 1), session.State.GetHero(new HeroId(1)).Position);
        Assert.Equal(2, blueTown.Dwellings.Count);
        Assert.Contains(session.State.ObjectsOf<Town>(), t => t.Owner is null && !t.Garrison.IsEmpty);
    }

    [Fact]
    public void MaxAffordable_IsLimitedByTheScarcestResource()
    {
        Assert.Equal(3, TownRules.MaxAffordable(new ResourceSet(Gold: 10_000, Mercury: 3), new ResourceSet(Gold: 1000, Mercury: 1), 20));
        Assert.Equal(5, TownRules.MaxAffordable(new ResourceSet(Gold: 10_000), new ResourceSet(Gold: 100), 5));
        Assert.Equal(0, TownRules.MaxAffordable(ResourceSet.Empty, new ResourceSet(Gold: 100), 5));
    }

    private static (GameSession Session, Town Town) Setup(ResourceSet resources)
    {
        var state = Create(Rows);
        var town = AddTown(state, TownIds.Haven, 0, 0, Blue);
        var session = Start(state);
        state.GetPlayer(Blue).Resources = resources;
        return (session, town);
    }

    private static void NextDay(GameSession session)
    {
        session.EndTurn();
        session.EndTurn();
    }

    private static void NextWeek(GameSession session)
    {
        do
        {
            NextDay(session);
        }
        while (!session.State.Date.IsFirstDayOfWeek);
    }

    private static List<T> Record<T>(GameSession session)
        where T : IEvent
    {
        var events = new List<T>();
        session.Events.Subscribe<T>(events.Add);
        return events;
    }
}
