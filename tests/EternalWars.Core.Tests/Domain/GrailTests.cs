using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Data.Templates;
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
using EternalWars.Core.Generation;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The Grail, the obelisks, the puzzle map, digging and the Grail building (ADR-126, ADR-130, ADR-140).</summary>
public class GrailTests
{
    private const string Grail = "grail";

    /// <summary>A 30×30 map with hero A at (3, 2).</summary>
    private static readonly string[] Wide = [.. Enumerable.Range(0, 30).Select(y => y == 2 ? "...A" + new string('.', 26) : new string('.', 30))];

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
            Assert.True(GrailRules.FarFromEdge(state.Map, grail));
            var origin = Assert.IsType<MapPosition>(state.Grail.PuzzleOrigin);
            Assert.InRange(grail.X - origin.X, 8, 11); // in the puzzle's centre 4×4 cells
            Assert.InRange(grail.Y - origin.Y, 8, 11);
            Assert.True(state.Map.Contains(origin) && state.Map.Contains(origin.Offset(GrailRules.PuzzleSize - 1, GrailRules.PuzzleSize - 1)));
        }

        var small = Create(Enumerable.Repeat(new string('.', 23), 23).ToArray());
        Add<Obelisk>(small, BuiltInMapObjects.Obelisk, 10, 10);
        Start(small);
        Assert.Null(small.Grail.Position); // under 24×24: no Grail

        var none = Create(rows);
        Start(none);
        Assert.Null(none.Grail.Position);

        var placed = Create(rows);
        Add<Obelisk>(placed, BuiltInMapObjects.Obelisk, 20, 20);
        placed.SetGrail(new MapPosition(2, 2));
        Start(placed);
        Assert.Equal(new MapPosition(2, 2), placed.Grail.Position); // the author's spot is kept
    }

    [Theory]
    [InlineData(25, 4, 4)]
    [InlineData(11, 9, 10)]
    [InlineData(2, 50, 50)]
    [InlineData(7, 14, 15)]
    public void TheHundredPieces_AreSplitEvenly_TheFirstObelisksOpeningFewer(int obelisks, int first, int last)
    {
        var shares = Enumerable.Range(1, obelisks).Select(v => GrailRules.Share(obelisks, v)).ToList();

        Assert.Equal(GrailRules.PieceCount, shares.Sum());
        Assert.Equal(first, shares[0]);
        Assert.Equal(last, shares[^1]);
        Assert.True(shares.Max() - shares.Min() <= 1);
        Assert.Equal(shares.Order().ToList(), shares);
    }

    [Fact]
    public void Obelisks_OpenTheOuterRingFirst_ThenTheMidRing_AndTheLastOneTheCentre_EachPlayerItsOwnPuzzle()
    {
        var state = Create(Wide);
        foreach (var x in new[] { 3, 6, 9, 12 })
        {
            Add<Obelisk>(state, BuiltInMapObjects.Obelisk, x, 0);
        }

        state.SetGrail(new MapPosition(15, 15));
        var session = Start(state);
        var visited = new List<ObeliskVisited>();
        session.Events.Subscribe<ObeliskVisited>(visited.Add);
        var none = GrailRules.Puzzle(state, Blue)!;
        Assert.Equal((0, 4, 100), (none.Revealed, none.Obelisks, none.Pieces.Count));

        static PuzzleRing RingAt(int i) => GrailRules.RingOf(i % GrailRules.PiecesPerSide, i / GrailRules.PiecesPerSide);

        session.Step(A, Direction.North); // the obelisk at (3, 0)
        Assert.Equal(25, state.Grail.PiecesOf(Blue).Count);
        session.MoveTo(A, new MapPosition(6, 1));
        Assert.Equal(50, state.Grail.PiecesOf(Blue).Count);
        Assert.All(state.Grail.PiecesOf(Blue), i => Assert.Equal(PuzzleRing.Outer, RingAt(i))); // the first half: the outer ring only
        Assert.Equal(0, GrailRules.Puzzle(state, Red)!.Revealed); // another player's puzzle stays closed

        session.MoveTo(A, new MapPosition(6, 2));
        session.MoveTo(A, new MapPosition(6, 1)); // the same obelisk again: nothing new
        Assert.Equal(2, visited.Count);

        session.MoveTo(A, new MapPosition(9, 1));
        var third = state.Grail.PiecesOf(Blue);
        Assert.Equal(75, third.Count);
        Assert.DoesNotContain(third, i => RingAt(i) == PuzzleRing.Centre);
        Assert.Null(GrailRules.Puzzle(state, Blue)!.Grail);

        session.MoveTo(A, new MapPosition(12, 1));
        var whole = GrailRules.Puzzle(state, Blue)!;
        Assert.True(whole.IsComplete);
        Assert.Equal(new MapPosition(15, 15), whole.Grail);
        Assert.True(whole.Shows(new MapPosition(15, 15)));
    }

    [Fact]
    public void ThePuzzle_ShowsTheObjectsThatStay_NotThoseThatGo()
    {
        Assert.True(GrailRules.ShowsOnPuzzle(MapObject.Create(new MapObjectId(1), MapObjectCatalog.Default.Get(BuiltInMapObjects.Obelisk), new MapPosition(0, 0))));
        Assert.False(GrailRules.ShowsOnPuzzle(MapObject.Create(new MapObjectId(2), MapObjectCatalog.Default.Get(BuiltInMapObjects.TreasureChest), new MapPosition(0, 0))));
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
        state.Grail.Open(Blue, Enumerable.Range(0, GrailRules.PieceCount));

        new AdventureAi(session, controlHumanSlots: true).PlayTurn();

        Assert.True(state.Grail.IsFound);
        Assert.True(GrailRules.Carries(state, state.GetHero(A)));
    }

    [Fact]
    public void TheEditor_BuriesTheGrailUndoably_AndTheValidatorWarnsWithoutObelisks()
    {
        var state = Create(Wide);
        Road(state, 14, 10, 15);
        state.Map.SetTerrain(new MapPosition(16, 16), TerrainType.Rock);
        var editor = new MapEditorSession(state);

        Assert.Equal(EditResult.InvalidTarget, editor.SetGrail(new MapPosition(11, 14)));
        Assert.Equal(EditResult.ImpassableTerrain, editor.SetGrail(new MapPosition(16, 16)));
        Assert.Equal(EditResult.TooNearEdge, editor.SetGrail(new MapPosition(9, 15)));
        Assert.Equal(EditResult.TooNearEdge, editor.SetGrail(new MapPosition(15, 20)));
        Assert.Equal(EditResult.Ok, editor.SetGrail(new MapPosition(12, 12)));
        Assert.Equal(new MapPosition(12, 12), state.Grail.Position);
        Assert.Contains(MapValidator.Validate(state), i => i.Message.Contains("no obelisks", StringComparison.Ordinal));

        editor.Undo();
        Assert.Null(state.Grail.Position);
        editor.Redo();
        Assert.Equal(new MapPosition(12, 12), state.Grail.Position);
    }

    [Fact]
    public void AMap_HoldsAtMost25Obelisks()
    {
        var state = Create(Wide);
        var editor = new MapEditorSession(state);
        for (var i = 0; i < GrailRules.MaxObelisks; i++)
        {
            Assert.Equal(EditResult.Ok, editor.PlaceObject(BuiltInMapObjects.Obelisk, new MapPosition(i % 12 * 2, 4 + (i / 12 * 3)), out _));
        }

        Assert.Equal(EditResult.TooManyObelisks, editor.PlaceObject(BuiltInMapObjects.Obelisk, new MapPosition(0, 20), out _));
        Add<Obelisk>(state, BuiltInMapObjects.Obelisk, 0, 20); // past the editor, the validator reports it
        Assert.Contains(MapValidator.Validate(state), i => i.Severity == IssueSeverity.Error && i.Message.Contains("obelisks", StringComparison.Ordinal));
    }

    [Fact]
    public void TheGenerator_PutsTheMapsObelisks_WhereTheTemplateSays_25ByDefault()
    {
        var duel = BuiltInTemplates.Find("duel")!;
        var counts = TreasurePlacer.ObeliskCounts(duel);
        Assert.Equal(GrailRules.MaxObelisks, counts.Sum());
        Assert.True(counts.Max() - counts.Min() <= 1);

        var set = duel with { Zones = [.. duel.Zones.Select((z, i) => i == 0 ? z with { Obelisks = 3 } : i == 1 ? z with { Obelisks = 0 } : z)] };
        var setCounts = TreasurePlacer.ObeliskCounts(set);
        Assert.Equal((3, 0), (setCounts[0], setCounts[1]));
        Assert.Equal(GrailRules.MaxObelisks, setCounts.Sum());

        var map = MapGenerator.Generate(duel, 72, 5);
        Assert.Equal(GrailRules.MaxObelisks, map.Objects.OfType<Obelisk>().Count());

        Assert.Equal(3, TemplateSerializer.Deserialize(TemplateSerializer.Serialize(set)).Zones[0].Obelisks); // template format v9
        Assert.Null(TemplateSerializer.Deserialize(TemplateSerializer.Serialize(duel)).Zones[0].Obelisks);
        var tooMany = duel with { Zones = [duel.Zones[0] with { Obelisks = 26 }, .. duel.Zones.Skip(1)] };
        Assert.Contains(TemplateValidator.Validate(tooMany), i => i.Severity == IssueSeverity.Error && i.Message.Contains("obelisks", StringComparison.Ordinal));
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
