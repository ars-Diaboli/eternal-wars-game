using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Templates;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;
using EternalWars.Core.Generation;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The most heroes a player may have, 1 to 8, a rule of the map (map format v15) and of templates (v5) (ADR-068).</summary>
public class HeroLimitTests
{
    [Fact]
    public void TheDefault_IsEight_AndOnlyOneToEightAreAllowed()
    {
        var state = Create("A.B");

        Assert.Equal(8, state.Rules.MaxHeroes);
        Assert.True(state.Rules.IsDefault);
        Assert.False((ScenarioRules.Default with { MaxHeroes = 3 }).IsDefault);
        Assert.Throws<ArgumentOutOfRangeException>(() => state.SetRules(ScenarioRules.Default with { MaxHeroes = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => state.SetRules(ScenarioRules.Default with { MaxHeroes = 9 }));
        state.SetRules(ScenarioRules.Default with { MaxHeroes = 1 });
        Assert.True(state.HeroLimitReached(Blue));
        Assert.True(state.HeroLimitReached(Red));
    }

    [Fact]
    public void TheTavern_RefusesAHire_AtTheMapsLimit()
    {
        var state = Create("..........", "..........", "..........", "..........", "..........", "..........", "..........", "..........", "A........B");
        var town = AddTown(state, TownIds.Haven, 0, 0, Blue);
        state.GetPlayer(Blue).Resources = new ResourceSet(Gold: 20_000);
        state.AddHero(Hero.Create(new HeroId(60), Blue, new MapPosition(5, 8), null, null, "Second"));
        state.SetRules(ScenarioRules.Default with { MaxHeroes = 3 });
        var session = Start(state);
        var offers = TavernRules.Offers(state, Blue, town);

        Assert.True(session.Execute(new HireHeroCommand(Blue, town.Id, offers[0].Id)).IsAccepted); // the third
        var heroOnEntrance = state.HeroAt(town.VisitPosition)!;
        state.RemoveHero(heroOnEntrance);
        state.AddHero(Hero.Create(heroOnEntrance.Id, Blue, new MapPosition(6, 8), null, null, heroOnEntrance.Name));

        var refusal = session.Execute(new HireHeroCommand(Blue, town.Id, TavernRules.Offers(state, Blue, town)[0].Id));
        Assert.Contains("at most 3 heroes", refusal.Error!, StringComparison.Ordinal);
    }

    [Fact]
    public void TheMapFormat_KeepsTheLimit_AndOmitsTheDefault()
    {
        var state = BlankScenario.Create(8, 6);
        var plain = MapSerializer.Serialize(MapConverter.ToDocument(state));
        state.SetRules(ScenarioRules.Default with { MaxHeroes = 4 });

        var json = MapSerializer.Serialize(MapConverter.ToDocument(state));
        var restored = MapConverter.ToGameState(MapSerializer.Deserialize(json));

        Assert.True(MapDocument.CurrentFormatVersion >= 15);
        Assert.DoesNotContain("maxHeroes", plain, StringComparison.Ordinal);
        Assert.Contains("\"maxHeroes\": 4", json, StringComparison.Ordinal);
        Assert.Equal(4, restored.Rules.MaxHeroes);
    }

    [Fact]
    public void AMapWithAnImpossibleLimit_DoesNotLoad()
    {
        const string bad = """
            {
              "formatVersion": 15, "name": "Bad", "width": 4, "height": 2, "terrain": [ [ "gggg", "gggg" ] ],
              "players": [ { "name": "Blue", "isHuman": true } ],
              "rules": { "maxHeroes": 12 }
            }
            """;

        Assert.Throws<FormatException>(() => MapConverter.ToGameState(MapSerializer.Deserialize(bad)));
    }

    [Fact]
    public void TheEditor_ChangesTheLimitUndoably_ButNeverBelowTheHeroesAPlayerHas()
    {
        var state = Create("A.C.B"); // Blue has two heroes, Red one
        var editor = new MapEditorSession(state);

        Assert.Equal(EditResult.HeroLimit, editor.SetScenarioRules(state.Rules with { MaxHeroes = 1 }));
        Assert.Equal(EditResult.InvalidValue, editor.SetScenarioRules(state.Rules with { MaxHeroes = 9 }));
        Assert.Equal(EditResult.Ok, editor.SetScenarioRules(state.Rules with { MaxHeroes = 2 }));
        Assert.Equal(2, editor.Scenario.Rules.MaxHeroes);

        Assert.Equal(EditResult.HeroLimit, editor.PlaceHero(Blue, new MapPosition(1, 0), "Third", out _));
        Assert.Equal(EditResult.Ok, editor.PlaceHero(Red, new MapPosition(3, 0), "Second", out _));
        editor.Undo();
        editor.Undo();
        Assert.Equal(8, editor.Scenario.Rules.MaxHeroes);
    }

    [Fact]
    public void APlayer_MayHaveItsOwnLimit_ForCampaignMaps_SavedInTheMap()
    {
        var state = Create("A.C.B");
        var editor = new MapEditorSession(state);

        Assert.Equal(EditResult.HeroLimit, editor.SetPlayerHeroLimit(Blue, 1));
        Assert.Equal(EditResult.InvalidValue, editor.SetPlayerHeroLimit(Red, 0));
        Assert.Equal(EditResult.Ok, editor.SetPlayerHeroLimit(Red, 1));
        Assert.Equal(1, state.HeroLimit(Red));
        Assert.Equal(8, state.HeroLimit(Blue));
        Assert.Equal(EditResult.HeroLimit, editor.PlaceHero(Red, new MapPosition(3, 0), "Second", out _));
        Assert.Equal(EditResult.Ok, editor.SetScenarioRules(state.Rules with { MaxHeroes = 2 })); // Red's own limit is not the map's

        var json = MapSerializer.Serialize(MapConverter.ToDocument(state));
        var restored = MapConverter.ToGameState(MapSerializer.Deserialize(json));
        Assert.Equal(1, restored.HeroLimit(new PlayerId(1)));
        Assert.Equal(2, restored.HeroLimit(Blue));
        Assert.Contains("\"maxHeroes\": 1", json, StringComparison.Ordinal);

        editor.Undo();
        editor.Undo();
        Assert.Null(state.GetPlayer(Red).MaxHeroes);
    }

    [Fact]
    public void AMapWithTooManyHeroes_IsFlagged_AndLosesTheNewestWhenTheGameStarts()
    {
        var state = Create("A.C.B");
        state.SetRules(ScenarioRules.Default with { MaxHeroes = 1 }); // not through the editor, as a hand-written map would
        var issue = Assert.Single(MapValidator.Validate(state), i => i.Message.Contains("more heroes than it may have", StringComparison.Ordinal));
        Assert.Equal(IssueSeverity.Warning, issue.Severity);

        var removed = new List<HeroId>();
        var session = new GameSession(state);
        session.Events.Subscribe<Core.Domain.Events.HeroRemoved>(e => removed.Add(e.Hero));
        session.Start();

        Assert.Equal(new[] { C }, removed); // C (id 3) is newer than A (id 1)
        Assert.Equal(new[] { A, B }, state.Heroes.Select(h => h.Id).OrderBy(h => h.Value));
    }

    [Fact]
    public void Templates_CarryTheLimit_TheValidatorChecksIt_AndTheGeneratorUsesItOrTheCallers()
    {
        var duel = BuiltInTemplates.Find("duel")!;
        Assert.Equal(8, duel.MaxHeroes);
        Assert.Equal(5, MapTemplate.CurrentFormatVersion);

        var limited = duel with { MaxHeroes = 2 };
        var restored = TemplateSerializer.Deserialize(TemplateSerializer.Serialize(limited));
        Assert.Equal(2, restored.MaxHeroes);
        Assert.Contains(TemplateValidator.Validate(duel with { MaxHeroes = 0 }), i => i.Severity == IssueSeverity.Error && i.Message.Contains("MaxHeroes", StringComparison.Ordinal));

        Assert.Equal(2, EditorMapGenerator.Generate(limited, 72, 5).Scenario.Rules.MaxHeroes);
        Assert.Equal(6, EditorMapGenerator.Generate(limited, 72, 5, maxHeroes: 6).Scenario.Rules.MaxHeroes);
        Assert.Equal(8, EditorMapGenerator.Generate(duel, 72, 5).Scenario.Rules.MaxHeroes);
    }
}
