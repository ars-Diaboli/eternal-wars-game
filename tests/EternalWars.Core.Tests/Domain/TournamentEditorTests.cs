using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Generation;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>
/// The map editor's tournament (ADR-366, plan step 6): the General tab's switch, the fix that paints a missing start zone, and the template
/// inspector's flag.
/// </summary>
public class TournamentEditorTests
{
    /// <summary>
    /// Blue's ground the columns 0–7, a wall at column 8, Red's ground 9–16; Alpha and Bravo by the wall (unless <paramref name="last"/>, the
    /// bottom row, says otherwise). Only Blue's zone is painted.
    /// </summary>
    private static MapEditorSession Valleys(bool tournament = true, string last = ".......A#B.......", Action<GameState>? setup = null)
    {
        var state = Create([.. Enumerable.Repeat("........#........", 9).Append(last)]);
        setup?.Invoke(state);
        state.SetRules(tournament ? TournamentRules.Switched(ScenarioRules.Default, on: true) : ScenarioRules.Default);
        for (var y = 0; y < state.Map.Height; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                state.Map.SetStartZone(new MapPosition(x, y), Blue);
            }
        }

        return new MapEditorSession(state);
    }

    private static MapIssue NoZone(MapEditorSession editor, string player) =>
        Assert.Single(MapValidator.Validate(editor.Scenario), i => i.Message.StartsWith($"{player} has no start zone", StringComparison.Ordinal));

    [Fact]
    public void TheTournamentSwitch_SetsTheHeroLimitToOne_AndTurnsTheArenaOff_Undoably()
    {
        var editor = Valleys(tournament: false);
        editor.Map.SetRules(editor.Scenario.Rules with { Arena = true, MaxHeroes = 5 });

        Assert.Equal(EditResult.Ok, editor.Map.SetRules(TournamentRules.Switched(editor.Scenario.Rules, on: true)));
        Assert.Equal((true, false, 1), (editor.Scenario.Rules.Tournament, editor.Scenario.Rules.Arena, editor.Scenario.Rules.MaxHeroes));

        editor.Undo();
        Assert.Equal((false, true, 5), (editor.Scenario.Rules.Tournament, editor.Scenario.Rules.Arena, editor.Scenario.Rules.MaxHeroes));

        var off = TournamentRules.Switched(TournamentRules.Switched(ScenarioRules.Default, on: true), on: false);
        Assert.Equal((false, 1), (off.Tournament, off.MaxHeroes)); // the limit stays as it was
    }

    [Fact]
    public void TheTournamentSwitch_IsRefused_WhileAPlayerHasMoreHeroesThanOne()
    {
        var state = Create("A.C......#........B");
        var editor = new MapEditorSession(state);

        Assert.Equal(EditResult.HeroLimit, editor.Map.SetTournament(on: true));
        Assert.False(editor.Scenario.Rules.Tournament);
        Assert.Equal(0, editor.History.Count); // no step recorded
    }

    [Fact]
    public void TheTournamentSwitch_TurnedOff_KeepsTheHeroLimit_AsAStepOfItsOwn()
    {
        var editor = Valleys();

        Assert.Equal(EditResult.Ok, editor.Map.SetTournament(on: false));
        Assert.Equal((false, 1), (editor.Scenario.Rules.Tournament, editor.Scenario.Rules.MaxHeroes));
        Assert.Equal("Make the map no tournament", editor.History.UndoDescription);

        editor.Undo();
        Assert.True(editor.Scenario.Rules.Tournament);
    }

    [Fact]
    public void TheTournamentSwitch_TakesThePlayersOwnHeroLimits_InTheSameStep()
    {
        var editor = Valleys(tournament: false);
        editor.Players.SetHeroLimit(Blue, 3);

        Assert.Equal(EditResult.Ok, editor.Map.SetTournament(on: true));
        Assert.Equal((null, 1), (editor.Scenario.GetPlayer(Blue).MaxHeroes, editor.Scenario.HeroLimit(Blue)));
        Assert.DoesNotContain(MapValidator.Validate(editor.Scenario), i => i.Message.StartsWith("A tournament allows one hero", StringComparison.Ordinal));

        editor.Undo();
        Assert.Equal((false, (int?)3), (editor.Scenario.Rules.Tournament, editor.Scenario.GetPlayer(Blue).MaxHeroes));
    }

    [Fact]
    public void AMissingStartZone_IsPaintedByItsFix_OverTheGroundThePlayerReaches()
    {
        var editor = Valleys();
        var issue = NoZone(editor, "Red");
        var fix = Assert.IsType<StartZoneFix>(issue.Fix);
        Assert.Equal(new StartZoneFix("Paint Red's start zone over the ground it reaches", Red), fix);

        Assert.Equal(EditResult.Ok, editor.Fix(fix));

        var map = editor.Scenario.Map;
        var red = Enumerable.Range(0, map.Height).SelectMany(y => Enumerable.Range(0, map.Width).Select(x => new MapPosition(x, y))).Where(p => map.GetStartZone(p) == Red).ToList();
        Assert.Equal(8 * 10, red.Count);
        Assert.All(red, p => Assert.InRange(p.X, 9, 16));
        Assert.Null(map.GetStartZone(new MapPosition(8, 0))); // the wall stays in no zone
        Assert.DoesNotContain(MapValidator.Validate(editor.Scenario), i => i.Message.Contains("has no start zone", StringComparison.Ordinal));
        Assert.Equal(EditResult.InvalidTarget, editor.Fix(fix)); // Red has a zone now

        editor.Undo();
        Assert.Same(fix.GetType(), NoZone(editor, "Red").Fix?.GetType());
    }

    [Fact]
    public void AMissingStartZone_HasNoFix_WhenItsGroundRunsIntoTheOtherZone()
    {
        var editor = Valleys();
        editor.Terrain.Paint(new MapPosition(8, 3), TerrainType.Grass); // a gap in the wall

        Assert.Null(NoZone(editor, "Red").Fix);
    }

    [Fact]
    public void AMissingStartZone_HasNoFix_WhenAnotherPlayersHeroStandsOnItsGround()
    {
        var editor = Valleys(last: ".......A#B....C.."); // Charlie, Blue's, on Red's side of the wall

        Assert.Null(NoZone(editor, "Red").Fix);
    }

    [Fact]
    public void AMissingStartZone_HasNoFix_WhenThePlayersHeroStandsInAnotherZone()
    {
        // Bravo on Blue's ground, walled in by rock: no step leads anywhere, so only its own tile tells that the ground is not Red's.
        var editor = Valleys(last: "...B...A#........", setup: state =>
        {
            foreach (var (x, y) in new[] { (2, 9), (4, 9), (2, 8), (3, 8), (4, 8) })
            {
                state.Map.SetTerrain(new MapPosition(x, y), TerrainType.Rock);
            }
        });

        Assert.Null(NoZone(editor, "Red").Fix);
    }

    [Fact]
    public void AMissingStartZone_TakesTheWholeFootprint_OfATownWhoseEntranceItReaches()
    {
        Town town = null!;
        var editor = Valleys(last: ".......A#........", setup: state => town = AddTown(state, "haven", 10, 2, Red));

        Assert.Equal(EditResult.Ok, editor.Fix(NoZone(editor, "Red").Fix!));

        var map = editor.Scenario.Map;
        Assert.All(town.OccupiedCells().Select(c => c.Position).Where(map.Contains), p => Assert.Equal(Red, map.GetStartZone(p)));
    }

    [Fact]
    public void TheTemplatesTournamentFlag_SetsTheHeroLimitToOne_AndTheValidatorChecksEachPlayersPart()
    {
        var session = new TemplateEditorSession();
        session.UpdateTemplate(t => t with { MaxHeroes = 4 });

        session.UpdateTemplate(t => TemplateEditorSession.WithTournament(t, on: true));
        Assert.Equal((true, 1), (session.Template.Tournament, session.Template.MaxHeroes));
        Assert.DoesNotContain(session.Issues, i => i.Message.Contains("the hero limit of", StringComparison.Ordinal));

        session.Undo();
        Assert.Equal((false, 4), (session.Template.Tournament, session.Template.MaxHeroes));

        var off = TemplateEditorSession.WithTournament(TemplateEditorSession.WithTournament(session.Template, on: true), on: false);
        Assert.Equal((false, 1), (off.Tournament, off.MaxHeroes));
    }
}
