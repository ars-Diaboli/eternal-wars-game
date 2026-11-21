using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.MapEvents;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Core.Domain.Towns;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The map's content (ADR-257, EDIT-12, map format v44): rumours, the factions a player may take, object names and custom heroes.</summary>
public class MapContentTests
{
    private static readonly string[] WideMap = [.. Enumerable.Repeat("..............................", 14), "A............................B"];

    private static MapEditorSession Editor() => new(BlankScenario.Create(30, 20));

    private static GameState RoundTrip(GameState scenario) => MapConverter.ToGameState(MapSerializer.Deserialize(MapSerializer.Serialize(MapConverter.ToDocument(scenario))));

    // Rumours -----------------------------------------------------------------------------------------

    [Fact]
    public void TheTaverns_TellTheMapsRumoursOneAWeek_InTurn()
    {
        var state = Create(WideMap);
        Assert.Null(TavernRules.Rumour(state));

        state.SetRules(state.Rules with { Rumours = ["first", "second", "third"] });
        var told = new List<string?>();
        foreach (var day in new[] { 1, 7, 8, 15, 22, 29 })
        {
            state.Date = new GameDate(day);
            told.Add(TavernRules.Rumour(state));
        }

        Assert.Equal(["first", "first", "second", "third", "first", "second"], told);
    }

    [Fact]
    public void Rumours_AreTextsOfALimitedLength_AndCount()
    {
        var editor = Editor();
        var rules = editor.Scenario.Rules;

        Assert.Equal(EditResult.InvalidValue, editor.Map.SetRules(rules with { Rumours = ["fine", " "] }));
        Assert.Equal(EditResult.InvalidValue, editor.Map.SetRules(rules with { Rumours = ["two\nlines"] }));
        Assert.Throws<ArgumentException>(() => editor.Scenario.SetRules(rules with { Rumours = ["two\nlines"] }));
        Assert.Equal(EditResult.InvalidValue, editor.Map.SetRules(rules with { Rumours = [new string('x', ScenarioRules.RumourLength + 1)] }));
        Assert.Equal(EditResult.InvalidValue, editor.Map.SetRules(rules with { Rumours = [.. Enumerable.Repeat("x", ScenarioRules.MaxRumours + 1)] }));
        Assert.Empty(editor.Scenario.Rules.Rumours);

        Assert.Equal(EditResult.Ok, editor.Map.SetRules(rules with { Rumours = ["The bridge is guarded."] }));
        Assert.Equal("Edit the rumours", editor.History.UndoDescription);
        Assert.False(editor.Scenario.Rules.IsDefault);
        Assert.Equal(["The bridge is guarded."], RoundTrip(editor.Scenario).Rules.Rumours);
    }

    // Factions ----------------------------------------------------------------------------------------

    [Fact]
    public void APlayersFactions_AreKeptInTheTownsOrder_AndEveryTownMeansAny()
    {
        var editor = Editor();
        Assert.Equal(EditResult.Ok, editor.Players.SetFactions(Blue, [TownIds.Inferno, TownIds.Haven, TownIds.Haven]));
        Assert.Equal(BuiltInMapObjects.PlayableTowns.Where(t => t is TownIds.Haven or TownIds.Inferno), editor.Scenario.GetPlayer(Blue).Factions);
        Assert.Equal([TownIds.Haven, TownIds.Inferno], RoundTrip(editor.Scenario).GetPlayer(Blue).Factions!.Order(StringComparer.Ordinal));

        Assert.Equal(EditResult.InvalidValue, editor.Players.SetFactions(Blue, []));
        Assert.Equal(EditResult.InvalidValue, editor.Players.SetFactions(Blue, ["atlantis"]));
        Assert.Equal(EditResult.UnknownPlayer, editor.Players.SetFactions(new PlayerId(7), null));

        Assert.Equal(EditResult.Ok, editor.Players.SetFactions(Blue, BuiltInMapObjects.PlayableTowns));
        Assert.Null(editor.Scenario.GetPlayer(Blue).Factions);
        Assert.Equal(BuiltInMapObjects.PlayableTowns, PlayerFactions.Allowed(editor.Scenario.GetPlayer(Blue)));
    }

    [Fact]
    public void AChoiceOutsideAPlayersFactions_IsDrawnAmongThem()
    {
        foreach (var seed in new ulong[] { 1, 2, 3, 4, 5 })
        {
            var state = Create(GameOptions.Default with { Seed = seed, StartTowns = new Dictionary<int, string> { [0] = TownIds.Haven } }, WideMap);
            state.SetFactions(Blue, [TownIds.Inferno, TownIds.Necropolis]);
            var town = AddTown(state, TownIds.Haven, 2, 2, Blue);
            town.IsRandom = true;
            Start(state);

            Assert.Contains(((Town)state.FindObject(town.Id)!).TownType, new[] { TownIds.Inferno, TownIds.Necropolis });
        }
    }

    [Fact]
    public void AChoiceAmongAPlayersFactions_IsTaken()
    {
        var state = Create(GameOptions.Default with { StartTowns = new Dictionary<int, string> { [0] = TownIds.Necropolis } }, WideMap);
        state.SetFactions(Blue, [TownIds.Inferno, TownIds.Necropolis]);
        var town = AddTown(state, TownIds.Haven, 2, 2, Blue);
        town.IsRandom = true;
        Start(state);

        Assert.Equal(TownIds.Necropolis, ((Town)state.FindObject(town.Id)!).TownType);
    }

    [Fact]
    public void AFixedTownOfAFactionThePlayerMayNotTake_IsAWarning()
    {
        var editor = Editor();
        Assert.Equal(EditResult.Ok, editor.Objects.Place(BuiltInMapObjects.TownId(TownIds.Haven), new MapPosition(10, 10), out var town));
        Assert.Equal(EditResult.Ok, editor.Towns.SetOwner(town, Blue));
        Assert.DoesNotContain(MapValidator.Validate(editor.Scenario), i => i.Message.Contains("may take only", StringComparison.Ordinal));

        Assert.Equal(EditResult.Ok, editor.Players.SetFactions(Blue, [TownIds.Inferno]));
        var issue = Assert.Single(MapValidator.Validate(editor.Scenario), i => i.Message.Contains("may take only", StringComparison.Ordinal));
        Assert.Equal(IssueSeverity.Warning, issue.Severity);
        Assert.Equal(IssueKind.Players, issue.Kind);
    }

    // Object names ------------------------------------------------------------------------------------

    [Fact]
    public void AnObjectsName_IsTrimmed_BlankTakesItAway_AndATownGoesBackToItsDefinitionsName()
    {
        var editor = Editor();
        Assert.Equal(EditResult.Ok, editor.Objects.Place(BuiltInMapObjects.Sign, new MapPosition(5, 5), out var sign));
        Assert.Equal(EditResult.Ok, editor.Objects.Place(BuiltInMapObjects.TownId(TownIds.Haven), new MapPosition(10, 10), out var townId));
        var signpost = editor.Scenario.FindObject(sign)!;
        var town = (Town)editor.Scenario.FindObject(townId)!;

        Assert.Null(signpost.Name);
        Assert.Equal(EditResult.Ok, editor.Objects.SetName(sign, "  The old signpost "));
        Assert.Equal("The old signpost", signpost.Name);
        Assert.Equal("The old signpost", signpost.Label);
        Assert.Equal(EditResult.InvalidValue, editor.Objects.SetName(sign, new string('x', MapObject.NameLength + 1)));
        Assert.Equal(EditResult.Ok, editor.Objects.SetName(sign, " "));
        Assert.Null(signpost.Name);
        Assert.StartsWith("the Sign at ", signpost.Label, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(EditResult.Ok, editor.Objects.SetName(townId, "Brightwater"));
        Assert.Equal(("Brightwater", "Brightwater"), (town.Name, town.GivenName));
        Assert.Equal(EditResult.Ok, editor.Objects.SetName(townId, null));
        Assert.Equal((town.Definition.Name, (string?)null), (town.Name, town.GivenName));
        Assert.Equal(EditResult.InvalidTarget, editor.Objects.SetName(new MapObjectId(999), "x"));
    }

    [Fact]
    public void AName_IsAtMostItsLength_AndNotOnlyDigits_InBothEditors_AndInTheMapFile()
    {
        var editor = Editor();
        Assert.Equal(EditResult.Ok, editor.Objects.Place(BuiltInMapObjects.Sign, new MapPosition(5, 5), out var sign));
        Assert.Equal(EditResult.Ok, editor.Objects.Place(BuiltInMapObjects.TownId(TownIds.Haven), new MapPosition(10, 10), out var town));
        var tooLong = new string('x', MapObject.NameLength + 1);

        Assert.Equal(EditResult.InvalidValue, editor.Objects.SetName(sign, "42"));
        Assert.Equal(EditResult.InvalidValue, editor.Towns.SetName(town, "42"));
        Assert.Equal(EditResult.InvalidValue, editor.Towns.SetName(town, tooLong));
        Assert.Equal(EditResult.Ok, editor.Objects.SetName(sign, "Gate 42"));
        Assert.Equal(EditResult.InvalidValue, editor.Heroes.SetName(editor.Scenario.Heroes.First().Id, "7"));

        var document = MapConverter.ToDocument(editor.Scenario);
        var named = document.Objects.Select(o => o.Id == sign.Value ? o with { Name = tooLong } : o).ToList();
        Assert.Throws<FormatException>(() => MapConverter.ToGameState(document with { Objects = named }));
        var heroes = document.Heroes.Select((h, i) => i == 0 ? h with { Name = new string('h', Hero.NameLength + 1) } : h).ToList();
        Assert.Throws<FormatException>(() => MapConverter.ToGameState(document with { Heroes = heroes }));
    }

    [Fact]
    public void AGivenName_ThatATownsDefaultNameHasToo_IsReported()
    {
        var editor = Editor();
        Assert.Equal(EditResult.Ok, editor.Objects.Place(BuiltInMapObjects.TownId(TownIds.Haven), new MapPosition(4, 4), out var town));
        Assert.Equal(EditResult.Ok, editor.Objects.Place(BuiltInMapObjects.Sign, new MapPosition(5, 12), out var sign));
        Assert.Equal(EditResult.Ok, editor.Objects.SetName(sign, editor.Scenario.FindObject(town)!.Name));

        Assert.Single(MapValidator.Validate(editor.Scenario), i => i.Message.Contains("are named", StringComparison.Ordinal));
    }

    [Fact]
    public void Names_AreSaved_Loaded_AndCopied()
    {
        var editor = new MapEditorSession(BlankScenario.Create(30, 20), copier: MapObjectCopier.Instance);
        Assert.Equal(EditResult.Ok, editor.Objects.Place(BuiltInMapObjects.Sign, new MapPosition(5, 5), out var sign));
        Assert.Equal(EditResult.Ok, editor.Objects.SetName(sign, "The old signpost"));

        Assert.Equal("The old signpost", RoundTrip(editor.Scenario).FindObject(sign)!.Name);
        var copy = MapConverter.Copy(editor.Scenario.FindObject(sign)!, new MapObjectId(500), new MapPosition(8, 8), editor.Scenario);
        Assert.Equal("The old signpost", copy.Name);
    }

    [Fact]
    public void TwoObjectsOfOneName_AreAnError_IgnoringCase_ButTownsNamedAsTheirDefinitionAreNot()
    {
        var editor = Editor();
        Assert.Equal(EditResult.Ok, editor.Objects.Place(BuiltInMapObjects.TownId(TownIds.Haven), new MapPosition(4, 4), out _));
        Assert.Equal(EditResult.Ok, editor.Objects.Place(BuiltInMapObjects.TownId(TownIds.Haven), new MapPosition(12, 4), out _));
        Assert.Equal(EditResult.Ok, editor.Objects.Place(BuiltInMapObjects.Sign, new MapPosition(5, 12), out var first));
        Assert.Equal(EditResult.Ok, editor.Objects.Place(BuiltInMapObjects.TreasureChest, new MapPosition(9, 12), out var second));
        Assert.DoesNotContain(MapValidator.Validate(editor.Scenario), i => i.Message.Contains("are named", StringComparison.Ordinal));

        Assert.Equal(EditResult.Ok, editor.Objects.SetName(first, "Hoard"));
        Assert.Equal(EditResult.Ok, editor.Objects.SetName(second, "HOARD"));
        var issue = Assert.Single(MapValidator.Validate(editor.Scenario), i => i.Message.Contains("are named", StringComparison.Ordinal));
        Assert.Equal(IssueSeverity.Error, issue.Severity);
        Assert.Equal(editor.Scenario.FindObject(second)!.VisitPosition, issue.Position);
    }

    [Fact]
    public void ATextNamesAnObjectOrAHero_ByIdOrByItsOneName()
    {
        var editor = Editor();
        Assert.Equal(EditResult.Ok, editor.Monsters.Place(new MapPosition(10, 10), BuiltInCreatures.Wolf, 5, GuardDisposition.Aggressive, false, out var wolves));
        Assert.Equal(EditResult.Ok, editor.Monsters.Place(new MapPosition(14, 10), BuiltInCreatures.Wolf, 5, GuardDisposition.Aggressive, false, out var more));
        Assert.Equal(EditResult.Ok, editor.Objects.SetName(wolves, "The pack"));
        var scenario = editor.Scenario;

        Assert.Equal(wolves, ObjectNames.Find(scenario, " the PACK ")!.Id);
        Assert.Equal(more, ObjectNames.Find(scenario, more.Value.ToString(System.Globalization.CultureInfo.InvariantCulture))!.Id);
        Assert.Null(ObjectNames.Find(scenario, "nobody"));
        Assert.Equal(EditResult.Ok, editor.Objects.SetName(more, "the pack"));
        Assert.Null(ObjectNames.Find(scenario, "The pack")); // two share it

        var hero = scenario.Heroes.First();
        Assert.Equal(hero.Id, ObjectNames.FindHero(scenario, hero.Name.ToUpperInvariant())!.Id);
        Assert.Equal(hero.Id, ObjectNames.FindHero(scenario, hero.Id.Value.ToString(System.Globalization.CultureInfo.InvariantCulture))!.Id);
    }

    [Fact]
    public void ConditionsAndEvents_ShowAnObjectsName()
    {
        var editor = Editor();
        Assert.Equal(EditResult.Ok, editor.Monsters.Place(new MapPosition(10, 10), BuiltInCreatures.Wolf, 5, GuardDisposition.Aggressive, false, out var wolves));
        Assert.Equal(EditResult.Ok, editor.Objects.Place(BuiltInMapObjects.TownId(TownIds.Haven), new MapPosition(4, 4), out var town));
        var beaten = new MonsterDefeatedCondition(wolves.Value);
        Assert.StartsWith($"monster {wolves.Value} ", MapEventText.Describe(beaten, editor.Scenario), StringComparison.Ordinal);

        Assert.Equal(EditResult.Ok, editor.Objects.SetName(wolves, "The pack"));
        Assert.Equal(EditResult.Ok, editor.Objects.SetName(town, "Brightwater"));
        Assert.StartsWith($"monster The pack (#{wolves.Value}) ", MapEventText.Describe(beaten, editor.Scenario), StringComparison.Ordinal);
        Assert.StartsWith($"town Brightwater (#{town.Value}) ", MapEventText.Describe(new TownOwnerCondition(town.Value, null), editor.Scenario), StringComparison.Ordinal);
        Assert.Contains("The pack", ScenarioRules.Describe(editor.Scenario, ScenarioCondition.CaptureObject(wolves)), StringComparison.Ordinal);
    }

    // Custom heroes -----------------------------------------------------------------------------------

    [Fact]
    public void AHerosNameBiographyPortraitAndSkills_AreEditedSavedAndLoaded()
    {
        var editor = Editor();
        var id = editor.Scenario.Heroes.First().Id;

        Assert.Equal(EditResult.Ok, editor.Heroes.SetName(id, "  Mira the Bold "));
        Assert.Equal(EditResult.InvalidValue, editor.Heroes.SetName(id, " "));
        Assert.Equal(EditResult.InvalidValue, editor.Heroes.SetName(id, new string('x', Hero.NameLength + 1)));
        Assert.Equal(EditResult.Ok, editor.Heroes.SetBiography(id, " Born under a red moon. "));
        Assert.Equal(EditResult.InvalidValue, editor.Heroes.SetBiography(id, new string('x', Hero.BiographyLength + 1)));
        Assert.Equal(EditResult.Ok, editor.Heroes.SetPortrait(id, "haven.aldric"));
        Assert.Equal(EditResult.InvalidValue, editor.Heroes.SetPortrait(id, "nobody"));
        Assert.Equal(EditResult.Ok, editor.Heroes.SetSkill(id, BuiltInSkills.Scouting, SkillLevel.Advanced));
        Assert.Equal(EditResult.InvalidValue, editor.Heroes.SetSkill(id, "juggling", SkillLevel.Basic));
        Assert.Equal(EditResult.InvalidValue, editor.Heroes.SetSkill(id, BuiltInSkills.Scouting, SkillLevel.Master + 1));

        var hero = RoundTrip(editor.Scenario).GetHero(id);
        Assert.Equal("Mira the Bold", hero.Name);
        Assert.Equal("Born under a red moon.", hero.Biography);
        Assert.Equal("haven.aldric", hero.PortraitHero);
        Assert.Equal(SkillLevel.Advanced, hero.Skills.Level(BuiltInSkills.Scouting));

        Assert.Equal(EditResult.Ok, editor.Heroes.SetSkill(id, BuiltInSkills.Scouting, SkillLevel.None));
        Assert.Equal(EditResult.Ok, editor.Heroes.SetBiography(id, ""));
        Assert.Equal(EditResult.Ok, editor.Heroes.SetPortrait(id, null));
        hero = editor.Scenario.GetHero(id);
        Assert.Equal((SkillLevel.None, (string?)null, (string?)null), (hero.Skills.Level(BuiltInSkills.Scouting), hero.Biography, hero.Portrait));
    }

    [Fact]
    public void AHerosSkills_KeepToTheGroupLimit()
    {
        var editor = Editor();
        var id = editor.Scenario.Heroes.First().Id;
        var groups = BuiltInSkills.All.GroupBy(s => s.Group).Select(g => g.First().Id).ToList();
        foreach (var skill in groups.Take(HeroSkills.MaxGroups))
        {
            Assert.Equal(EditResult.Ok, editor.Heroes.SetSkill(id, skill, SkillLevel.Basic));
        }

        Assert.Equal(EditResult.InvalidValue, editor.Heroes.SetSkill(id, groups[HeroSkills.MaxGroups], SkillLevel.Basic));
        Assert.Equal(EditResult.Ok, editor.Heroes.SetSkill(id, groups[0], SkillLevel.Advanced)); // a group it has
    }

    [Fact]
    public void AnUnknownPortrait_IsRefusedWhenTheMapLoads()
    {
        var document = MapConverter.ToDocument(Editor().Scenario);
        document = document with { Heroes = [document.Heroes[0] with { Portrait = "nobody" }, .. document.Heroes.Skip(1)] };
        Assert.Throws<FormatException>(() => MapConverter.ToGameState(document));
    }

    [Fact]
    public void APlaceholder_BecomesItsRosterHero_WithoutTheAuthorsBiographyOrPortrait()
    {
        var state = Create(WideMap);
        AddTown(state, TownIds.Haven, 10, 2, Blue);
        var alpha = state.GetHero(A);
        (alpha.IsPlaceholder, alpha.Biography, alpha.Portrait) = (true, "Someone else.", "haven.aldric");
        Start(state);

        Assert.False(alpha.IsPlaceholder);
        Assert.Null(alpha.Biography);
        Assert.Equal(alpha.Definition, alpha.PortraitHero);
    }
}
