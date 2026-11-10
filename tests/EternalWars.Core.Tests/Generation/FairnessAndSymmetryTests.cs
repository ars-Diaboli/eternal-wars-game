using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Templates;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Players;
using EternalWars.Core.Generation;

namespace EternalWars.Core.Tests.Generation;

/// <summary>Phase 4 close-out (ADR-117): the whole-map fairness check, team counts, and mirrored templates.</summary>
public class FairnessAndSymmetryTests
{
    private static MapTemplate Shipped(string key) => BuiltInTemplates.Find(key)!;

    private static GeneratedMap Fair(string key, int size, ulong seed) => MapGenerator.GenerateBest(Shipped(key), size, seed).Map;

    // Fairness ---------------------------------------------------------------------------------

    [Fact]
    public void AGeneratedMap_MeasuresEveryStart_AndTheBestAttemptOfAShippedTemplateIsFair()
    {
        var map = Fair("duel", 72, 1);
        Assert.Empty(map.Problems);
        var report = map.Fairness!;
        Assert.Equal([0, 1], report.Starts.Select(s => s.Slot));
        Assert.All(report.Starts, s => Assert.True(s.MineDistance > 0 && s.TreasureValue > 0 && s.GuardPower > 0, s.ToString()));
        Assert.Empty(report.Problems);
    }

    [Fact]
    public void ATreasureHeapNextToOneStart_MakesTheMapUnfair()
    {
        var map = Fair("duel", 72, 1);
        var town = map.Towns.First(t => t.Slot == 0).Town;
        var free = Enumerable.Range(-6, 13).SelectMany(dx => Enumerable.Range(-6, 13).Select(dy => town.VisitPosition.Offset(dx, dy)))
            .First(p => map.Fits(MapObjectCatalog.Default.Get(BuiltInMapObjects.PileId(EternalWars.Core.Domain.Economy.ResourceType.Gold)), p));
        var pile = map.Add(new ResourcePile(map.NextId(), MapObjectCatalog.Default.Get(BuiltInMapObjects.PileId(EternalWars.Core.Domain.Economy.ResourceType.Gold)), free, 50000));
        map.RecordTreasure(pile, 50000);

        var problems = MapFairness.Check(Shipped("duel"), map).Problems;
        Assert.Contains(problems, p => p.StartsWith("Unfair start 'start-1'", StringComparison.Ordinal) && p.Contains("treasure"));
    }

    [Fact]
    public void AStartTownWalledIn_IsReportedAsUnreachable()
    {
        var map = Fair("duel", 72, 1);
        var town = map.Towns.First(t => t.Slot == 1).Town;
        foreach (var direction in DirectionExtensions.All)
        {
            map.Map.SetTerrain(town.VisitPosition.Step(direction), TerrainPainter.WallTerrain);
        }

        var problems = MapFairness.Check(Shipped("duel"), map).Problems;
        Assert.Contains(problems, p => p.Contains("cannot walk to the start town of slot 1"));
    }

    [Fact]
    public void Allies_AreComparedAsATeam_SoWhatLiesBetweenThemIsShared()
    {
        // In "Two against two" allies share a camp: one may get most of it, but the two teams must come out alike.
        var map = Fair("2v2", 96, 2);
        Assert.Empty(map.Problems);
        var starts = map.Fairness!.Starts;
        var teamA = starts.Where(s => s.Slot is 0 or 1).Sum(s => s.TreasureValue);
        var teamB = starts.Where(s => s.Slot is 2 or 3).Sum(s => s.TreasureValue);
        Assert.InRange(Math.Abs(teamA - teamB), 0, Math.Max(MapFairness.TreasureSlack, (teamA + teamB) / 2 * MapFairness.TolerancePercent / 100) * 2);
    }

    // Team counts ------------------------------------------------------------------------------

    private static GameState FourPlayers() =>
        new(new AdventureMap(8, 8), Enumerable.Range(0, 4).Select(i => new Player(new PlayerId(i), $"P{i}", false)), [], []);

    [Fact]
    public void TeamCounts_LimitTheSetup_AndAreInTheMapFormat_AndTheEditor()
    {
        var state = FourPlayers();
        state.SetTeamCounts(2, 2);
        var error = Assert.Throws<InvalidOperationException>(() => state.AssignTeams(state.Players.ToDictionary(p => p.Id, p => new TeamId(p.Id.Value))));
        Assert.Contains("2 teams, not 4", error.Message);
        state.AssignTeams(state.Players.ToDictionary(p => p.Id, p => new TeamId(p.Id.Value % 2)));
        Assert.True(state.AreAllies(new PlayerId(0), new PlayerId(2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => state.SetTeamCounts(1, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => state.SetTeamCounts(4, 3));

        var loaded = MapConverter.ToGameState(MapSerializer.Deserialize(MapSerializer.Serialize(MapConverter.ToDocument(state))));
        Assert.Equal((2, 2), (loaded.MinTeams!.Value, loaded.MaxTeams!.Value));
        Assert.Null(MapConverter.ToGameState(MapSerializer.Deserialize(MapSerializer.Serialize(MapConverter.ToDocument(EternalWars.Core.Domain.Scenarios.BlankScenario.Create(8, 8))))).MinTeams);

        var editor = new MapEditorSession(loaded);
        Assert.False(editor.Players.SetTeamCounts(3, 2));
        Assert.True(editor.Players.SetTeamCounts(null, 3));
        Assert.Equal((null, 3), (editor.Scenario.MinTeams, editor.Scenario.MaxTeams));
        editor.Undo();
        Assert.Equal((2, 2), (editor.Scenario.MinTeams!.Value, editor.Scenario.MaxTeams!.Value));

        // Locked teams that do not fit cannot be fixed at setup: an error; unlocked, a warning.
        editor.Players.SetTeamCounts(3, 4);
        Assert.Contains(MapValidator.Validate(editor.Scenario), i => i.Severity == IssueSeverity.Warning && i.Message.Contains("team counts"));
        editor.Players.SetTeamsLocked(true);
        Assert.Contains(MapValidator.Validate(editor.Scenario), i => i.Severity == IssueSeverity.Error && i.Message.Contains("team counts"));
    }

    [Fact]
    public void TeamCounts_InATemplate_AreValidated_CarriedToTheGame_AndRefuseOtherSetups()
    {
        var twoOnTwo = Shipped("2v2");
        Assert.Equal((2, 2), (twoOnTwo.MinTeams!.Value, twoOnTwo.MaxTeams!.Value));
        Assert.Contains(TemplateValidator.Validate(twoOnTwo with { MinTeams = 1 }), i => i.Severity == IssueSeverity.Error && i.Message.Contains("team counts"));
        Assert.Contains(TemplateValidator.Validate(twoOnTwo with { MinTeams = 3, MaxTeams = 4 }), i => i.Severity == IssueSeverity.Error);
        Assert.Contains(TemplateValidator.Validate(Shipped("duel") with { MinTeams = 2, MaxTeams = 2, MinPlayers = 2 } with { MinTeams = 3 }), i => i.Severity == IssueSeverity.Error);

        var open = twoOnTwo with { TeamsLocked = false };
        PlayerSetup Player(int slot, int team) => new(slot, $"P{slot}", false, "haven", Team: team);
        var refused = Assert.Throws<ArgumentException>(() => MapGenerator.Build(open, 96, 3, [Player(0, 0), Player(1, 1), Player(2, 2), Player(3, 2)]));
        Assert.Contains("2 teams, not 3", refused.Message);

        var (state, _) = MapGenerator.Build(open, 96, 3, [Player(0, 0), Player(1, 1), Player(2, 0), Player(3, 1)]);
        Assert.Equal((2, 2), (state.MinTeams!.Value, state.MaxTeams!.Value));
        Assert.True(state.AreAllies(new PlayerId(0), new PlayerId(2)));
    }

    // Mirrored templates -----------------------------------------------------------------------

    [Theory]
    [InlineData("ffa-4", 96)]
    [InlineData("ffa-8", 128)]
    [InlineData("mirror-duel", 72)]
    public void AMirroredTemplate_IsLaidOutSymmetric_TileForTile(string key, int size)
    {
        var template = Shipped(key);
        var seed = 4UL;
        var layout = ZoneLayouter.Layout(template, size, seed);
        var symmetry = template.SymmetryFor(seed);
        Assert.NotEqual(TemplateSymmetry.None, symmetry);
        Assert.Equal(symmetry, layout.Symmetry);
        var image = template.MirrorOf();
        for (var level = 0; level < layout.Levels; level++)
        {
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var (ix, iy) = ZoneLayout.Image(symmetry, x, y, size);
                    if ((ix, iy) == (x, y))
                    {
                        continue; // on a diagonal axis: its own image, a wall between a zone and its mirror
                    }

                    Assert.Equal(image[layout.ZoneAt(x, y, level)], layout.ZoneAt(ix, iy, level));
                }
            }
        }

        Assert.Empty(layout.UnmetConnections);
    }

    [Fact]
    public void MirroredZones_ShareTheirRolls_SoTheyHoldTheSameTreasure()
    {
        var template = Shipped("ffa-4");
        var map = MapGenerator.Generate(template, 96, 2);
        int ZoneOf(MapObject o) => map.Layout.ZoneAt(o.Position.X, o.Position.Y, o.Position.Level);
        var values = Enumerable.Range(0, 4).Select(z => map.Objects.Where(o => map.TreasureValues.ContainsKey(o.Id) && ZoneOf(o) == z)
            .Select(o => map.TreasureValues[o.Id]).Order().ToList()).ToList();
        Assert.All(values.Skip(1), v => Assert.Equal(values[0], v));
        Assert.Equal(0, template.RollZone(3));
        Assert.Equal(8, template.RollZone(8)); // the hub is its own image
        Assert.Equal(3, Shipped("duel").RollZone(3)); // no symmetry: every zone rolls on its own
    }

    [Fact]
    public void ARandomAxis_IsDrawnFromTheSeed()
    {
        var template = Shipped("mirror-duel");
        var axes = Enumerable.Range(1, 30).Select(s => template.SymmetryFor((ulong)s)).ToHashSet();
        Assert.Equal([TemplateSymmetry.Vertical, TemplateSymmetry.Horizontal, TemplateSymmetry.Diagonal], axes.Order());
        Assert.Equal(template.SymmetryFor(7), template.SymmetryFor(7));
    }

    [Fact]
    public void TheValidator_ChecksMirrors()
    {
        var ffa = Shipped("ffa-4");
        TemplateZone Zone(string id) => ffa.Zone(id)!;
        MapTemplate WithZone(string id, TemplateZone zone) => ffa with { Zones = [.. ffa.Zones.Select(z => z.Id == id ? zone : z)] };
        IEnumerable<string> Errors(MapTemplate t) => TemplateValidator.Validate(t).Where(i => i.Severity == IssueSeverity.Error).Select(i => i.Message);

        Assert.Empty(Errors(ffa));
        Assert.Contains(Errors(ffa with { Symmetry = TemplateSymmetry.Vertical }), e => e.Contains("for 2 players"));
        Assert.Contains(Errors(WithZone("start-4", Zone("start-4") with { Mirror = "nowhere" })), e => e.Contains("does not exist"));
        Assert.Contains(Errors(WithZone("start-4", Zone("start-4") with { Mirror = "start-2" })), e => e.Contains("same mirror"));
        Assert.Contains(Errors(WithZone("reserve-3", Zone("reserve-3") with { Size = 3 })), e => e.Contains("differ"));
        Assert.Contains(Errors(ffa with { Connections = [.. ffa.Connections.Select((c, i) => i == 5 ? c with { GuardValue = 9999 } : c)] }), e => e.Contains("no mirrored connection"));
        Assert.Contains(Errors(Shipped("duel") with { Zones = [.. Shipped("duel").Zones.Select(z => z with { Mirror = z.Id })] }), e => e.Contains("no symmetry"));

        // Two cycles of two under a quarter turn do not fit it.
        var pairs = ffa with
        {
            Zones = [.. ffa.Zones.Select(z => z.Id switch { "start-1" => z with { Mirror = "start-2" }, "start-2" => z with { Mirror = "start-1" }, "start-3" => z with { Mirror = "start-4" }, "start-4" => z with { Mirror = "start-3" }, _ => z })],
        };
        Assert.Contains(Errors(pairs), e => e.Contains("cycle of 2"));
    }

    [Fact]
    public void TheNewTemplateFields_SurviveTheFileFormat_AndAnOldFileLoads()
    {
        var template = Shipped("ffa-4") with { MinTeams = 2, MaxTeams = 4 };
        template = template with { Zones = [template.Zones[0] with { GraphX = 1.5, GraphY = -2 }, .. template.Zones.Skip(1)] };
        var loaded = TemplateSerializer.Deserialize(TemplateSerializer.Serialize(template));
        Assert.Equal(MapTemplate.CurrentFormatVersion, loaded.FormatVersion);
        Assert.Equal(TemplateSymmetry.QuarterTurn, loaded.Symmetry);
        Assert.Equal("start-2", loaded.Zones[0].Mirror);
        Assert.Equal((1.5, -2.0), (loaded.Zones[0].GraphX!.Value, loaded.Zones[0].GraphY!.Value));
        Assert.Equal((2, 4), (loaded.MinTeams!.Value, loaded.MaxTeams!.Value));

        var old = Shipped("duel"); // a v1 file
        Assert.Equal(TemplateSymmetry.None, old.Symmetry);
        Assert.Null(old.MinTeams);
    }
}
