using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.AI;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The Grail, the obelisks, the puzzle map, digging and the Grail building (ADR-126, ADR-130, ADR-140).</summary>
public class GrailTests
{
    private const string Grail = "grail";

    private static readonly string[] Open =
    [
        "..............................",
        "..............................",
        "...A..........................",
        "..............................",
        "..............................",
    ];

    [Fact]
    public void TheGrail_IsAnArtifactThatIsNeverRolled()
    {
        var grail = ArtifactCatalog.Default.Grail;

        Assert.NotNull(grail);
        Assert.Equal(Grail, grail.Id);
        Assert.True(grail.IsGrail);
        Assert.All(Enum.GetValues<ArtifactTier>(), tier => Assert.DoesNotContain(ArtifactCatalog.Default.OfTier(tier), a => a.IsGrail));
    }

    [Fact]
    public void AMapWithObelisks_BuriesTheGrailWhenTheGameStarts_AwayFromTowns_AMapWithoutKeepsNone()
    {
        var rows = Enumerable.Repeat(new string('.', 30), 30).ToArray();
        for (var seed = 1UL; seed <= 10; seed++)
        {
            var state = Create(new GameOptions(Seed: seed), rows);
            var town = AddTown(state, TownIds.Haven, 0, 0, Blue);
            Add<Obelisk>(state, BuiltInMapObjects.Obelisk, 20, 20);
            Start(state);

            var grail = Assert.IsType<MapPosition>(state.Grail.Position);
            Assert.Null(GrailRules.CheckGround(state, grail));
            Assert.True(Math.Max(Math.Abs(grail.X - town.VisitPosition.X), Math.Abs(grail.Y - town.VisitPosition.Y)) > GrailRules.TownDistance);
        }

        var none = Create(rows);
        Start(none);
        Assert.Null(none.Grail.Position);

        var placed = Create(rows);
        Add<Obelisk>(placed, BuiltInMapObjects.Obelisk, 20, 20);
        placed.SetGrail(new MapPosition(2, 2));
        Start(placed);
        Assert.Equal(new MapPosition(2, 2), placed.Grail.Position); // the author's spot is kept
    }

    [Fact]
    public void Obelisks_ShowThePuzzle_OuterPiecesFirst_AndTheSpotWhenAllAreVisited()
    {
        var state = Create(Open);
        var first = Add<Obelisk>(state, BuiltInMapObjects.Obelisk, 3, 0);
        Add<Obelisk>(state, BuiltInMapObjects.Obelisk, 6, 0);
        state.SetGrail(new MapPosition(20, 3));
        var session = Start(state);
        var visited = new List<ObeliskVisited>();
        session.Events.Subscribe<ObeliskVisited>(visited.Add);

        var none = GrailRules.Puzzle(state, Blue)!;
        Assert.Equal((0, 2, 25), (none.Revealed, none.Obelisks, none.Pieces.Count));
        Assert.Equal(new MapPosition(13, -4), none.Origin);

        session.Step(A, Direction.North);
        var half = GrailRules.Puzzle(state, Blue)!;
        Assert.Equal(first.Id, Assert.Single(visited).Obelisk);
        Assert.Equal(12, half.Revealed); // 25 × 1 / 2, rounded down
        Assert.All(half.Pieces.Where(p => p.Revealed), p => Assert.True(p.X is 0 or 12 || p.Y is 0 or 12)); // the outer ring
        Assert.Null(half.Grail);
        Assert.False(half.Shows(new MapPosition(20, 3)));
        Assert.Equal(0, GrailRules.Puzzle(state, Red)!.Revealed); // an enemy's visit shows it nothing

        session.MoveTo(A, new MapPosition(3, 2));
        session.Step(A, Direction.North); // the same obelisk again: nothing new
        Assert.Single(visited);

        session.MoveTo(A, new MapPosition(6, 1));
        var whole = GrailRules.Puzzle(state, Blue)!;
        Assert.True(whole.IsComplete);
        Assert.Equal(new MapPosition(20, 3), whole.Grail);
        Assert.True(whole.Shows(new MapPosition(20, 3)));
    }

    [Fact]
    public void Digging_TakesAWholeDay_LeavesAHole_AndOnTheSpotGivesTheGrail()
    {
        var state = Create(Open);
        state.SetGrail(new MapPosition(4, 2));
        Road(state, 4, 0, 5);
        var obelisk = Add<Obelisk>(state, BuiltInMapObjects.Obelisk, 10, 0);
        var session = Start(state);
        var hero = state.GetHero(A);
        var dug = new List<HoleDug>();
        session.Events.Subscribe<HoleDug>(dug.Add);

        session.Execute(new DigCommand(Blue, A)).ThrowIfRejected();
        Assert.Equal(0, hero.MovementPoints);
        Assert.Equal(new HoleDug(A, new MapPosition(3, 2), null), Assert.Single(dug));
        Assert.True(state.Grail.HasHole(new MapPosition(3, 2)));
        Assert.Contains("whole day", session.Execute(new DigCommand(Blue, A)).Error, StringComparison.Ordinal);

        Assert.Contains("road", GrailRules.CheckGround(state, new MapPosition(1, 4)), StringComparison.Ordinal);
        Assert.NotNull(GrailRules.CheckGround(state, obelisk.Position));
        Assert.NotNull(GrailRules.CheckGround(state, obelisk.VisitPosition));

        session.EndTurn();
        session.EndTurn();
        Assert.Contains("dug already", session.Execute(new DigCommand(Blue, A)).Error, StringComparison.Ordinal);
        session.Step(A, Direction.East);
        Assert.Contains("whole day", session.Execute(new DigCommand(Blue, A)).Error, StringComparison.Ordinal);

        session.EndTurn();
        session.EndTurn();
        session.Execute(new DigCommand(Blue, A)).ThrowIfRejected();
        Assert.Equal(Grail, dug[^1].Grail);
        Assert.True(state.Grail.IsFound);
        Assert.True(GrailRules.Carries(state, hero));
    }

    [Fact]
    public void BringingTheGrailIntoAnOwnTown_RaisesItsGrailBuilding_WhichCannotBeBoughtOtherwise()
    {
        var state = Create(".........", ".........", ".........", ".........", ".........", ".........", ".........", "...A.....");
        var town = AddTown(state, TownIds.Haven, 0, 0, Blue);
        Assert.Contains("only around the Grail", TownRules.CheckBuild(state, town, BuiltInBuildings.Grail), StringComparison.Ordinal);
        var line = TownRules.Lines(state, town)[0];
        var growth = TownRules.WeeklyGrowth(state, town, line);
        var income = TownRules.DailyIncome(state, town);
        var hero = state.GetHero(A);
        hero.Artifacts.AddToBackpack(Grail);
        var session = Start(state);
        var raised = new List<GrailBuildingRaised>();
        session.Events.Subscribe<GrailBuildingRaised>(raised.Add);

        session.Step(A, Direction.North);

        Assert.Equal(new GrailBuildingRaised(A, town.Id, "Dawnspire"), Assert.Single(raised));
        Assert.Equal(1, town.Level(BuiltInBuildings.Grail));
        Assert.False(GrailRules.Carries(state, hero));
        Assert.Null(town.LastBuilt); // not the day's building
        Assert.Equal(income + new ResourceSet(Gold: 5000), TownRules.DailyIncome(state, town));
        Assert.Equal(growth + (growth / 2), TownRules.WeeklyGrowth(state, town, line));
        Assert.Equal(UniqueBuildings.GrailSiege, UniqueBuildings.SiegeOf(town));
    }

    [Fact]
    public void ATownWithAGrailBuilding_LeavesTheHeroItsGrail()
    {
        var state = Create(".........", ".........", ".........", ".........", ".........", ".........", ".........", "...A.....");
        var town = AddTown(state, TownIds.Haven, 0, 0, Blue);
        town.SetLevel(BuiltInBuildings.Grail, 1);
        state.GetHero(A).Artifacts.AddToBackpack(Grail);
        var session = Start(state);

        session.Step(A, Direction.North);

        Assert.True(GrailRules.Carries(state, state.GetHero(A)));
    }

    [Fact]
    public void InASiege_TheGrailBuilding_GivesTheGarrisonInitiative()
    {
        var state = Create(".........", ".........", ".........", ".........", ".........", ".........", ".........", "...A.....");
        var town = AddTown(state, TownIds.Haven, 0, 0, Red);
        town.SetLevel(BuiltInBuildings.Grail, 1);
        town.Garrison.Set(0, new CreatureStack(BuiltInCreatures.Footman, 5));
        var session = Start(state);

        session.Step(A, Direction.North);

        var battle = session.State.Battle!;
        Assert.NotNull(battle.Siege);
        Assert.Equal(2, battle.Defender.Bonus.Initiative);
        Assert.Equal(0, battle.Attacker.Bonus.Initiative);
    }

    [Fact]
    public void TheAi_DigsWhereTheWholePuzzleShowsTheGrail()
    {
        var state = Create(Open);
        var obelisk = Add<Obelisk>(state, BuiltInMapObjects.Obelisk, 10, 0);
        obelisk.Visit(Blue);
        state.SetGrail(new MapPosition(3, 2));
        var session = Start(state);

        new AdventureAi(session, controlHumanSlots: true).PlayTurn();

        Assert.True(state.Grail.IsFound);
        Assert.True(GrailRules.Carries(state, state.GetHero(A)));
    }

    [Fact]
    public void TheEditor_BuriesTheGrailUndoably_AndTheValidatorWarnsWithoutObelisks()
    {
        var state = Create(Open);
        Road(state, 4, 0, 5);
        state.Map.SetTerrain(new MapPosition(9, 0), TerrainType.Rock);
        var editor = new MapEditorSession(state);

        Assert.Equal(EditResult.InvalidTarget, editor.SetGrail(new MapPosition(1, 4)));
        Assert.Equal(EditResult.ImpassableTerrain, editor.SetGrail(new MapPosition(9, 0)));
        Assert.Equal(EditResult.Ok, editor.SetGrail(new MapPosition(12, 2)));
        Assert.Equal(new MapPosition(12, 2), state.Grail.Position);
        Assert.Contains(MapValidator.Validate(state), i => i.Message.Contains("no obelisks", StringComparison.Ordinal));

        editor.Undo();
        Assert.Null(state.Grail.Position);
        editor.Redo();
        Assert.Equal(new MapPosition(12, 2), state.Grail.Position);
    }

    [Fact]
    public void TheGrailAndObelisks_AreInTheMapFormat_AndAGameWithADigReplays()
    {
        var state = Create(Open);
        Add<Obelisk>(state, BuiltInMapObjects.Obelisk, 10, 0);
        state.SetGrail(new MapPosition(3, 2));
        var map = MapConverter.ToDocument(state);
        var json = MapSerializer.Serialize(map);
        var restored = MapConverter.ToGameState(MapSerializer.Deserialize(json));

        Assert.Contains("\"obelisk\"", json, StringComparison.Ordinal);
        Assert.Equal(new MapPosition(3, 2), restored.Grail.Position);
        Assert.Single(restored.ObjectsOf<Obelisk>());
        Assert.Null(MapConverter.ToGameState(MapSerializer.Deserialize(MapSerializer.Serialize(MapConverter.ToDocument(Create(Open))))).Grail.Position);

        var session = new GameSession(MapConverter.ToGameState(map, options: new GameOptions(Seed: 4)));
        session.Start();
        session.Execute(new DigCommand(Blue, A)).ThrowIfRejected();
        Assert.True(session.State.Grail.IsFound);
        var record = GameRecordSerializer.Serialize(GameReplay.Record(map, session));
        var replayed = GameReplay.Replay(GameRecordSerializer.Deserialize(record));

        Assert.Contains("\"dig\"", record, StringComparison.Ordinal);
        Assert.Equal(GameStateDigest.Describe(session.State), GameStateDigest.Describe(replayed.State));
    }
}
