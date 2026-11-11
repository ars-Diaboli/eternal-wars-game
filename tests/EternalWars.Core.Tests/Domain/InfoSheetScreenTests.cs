using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Info;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The information windows of the kingdom overview, the tavern and the decision prompts (ADR-154, ADR-178).</summary>
public class InfoSheetScreenTests
{
    private static GameState Kingdom() =>
        Create("...........", "...........", "...........", "...........", "...........", "...........", "...........", "A.........B");

    [Fact]
    public void AKingdomRow_SaysWhatItPays_ThenShowsItsSource()
    {
        var state = Kingdom();
        var town = AddTown(state, TownIds.Haven, 0, 0, Blue);
        var mine = Add<Mine>(state, BuiltInMapObjects.MineId(ResourceType.Ore), 8, 0);
        mine.Owner = Blue;
        var wheel = Add<ResourceProvider>(state, BuiltInMapObjects.WaterWheel, 8, 5);
        wheel.Owner = Blue;
        state.GetHero(A).Skills.Set(BuiltInSkills.Treasury, SkillLevel.Basic);
        Start(state);
        var overview = KingdomOverviews.For(state, Blue);

        var townSheet = InfoSheets.Income(state, Blue, overview.Rows.Single(r => r.Object == town.Id), detailed: false);
        var mineSheet = InfoSheets.Income(state, Blue, overview.Rows.Single(r => r.Object == mine.Id), detailed: false);
        var wheelSheet = InfoSheets.Income(state, Blue, overview.Rows.Single(r => r.Object == wheel.Id), detailed: false);
        var heroSheet = InfoSheets.Income(state, Blue, overview.Rows.Single(r => r.Hero == state.GetHero(A).Id), detailed: false);

        Assert.Equal(town.Name, townSheet.Title);
        Assert.StartsWith($"Pays {TownRules.DailyIncome(state, town).Gold} gold a day (", townSheet.Lines[0], StringComparison.Ordinal);
        Assert.Contains("Owner: you", townSheet.Lines);
        Assert.Equal($"Pays {mine.DailyYield} ore a day (at 8, 0).", mineSheet.Lines[0]);
        Assert.Contains($"Gives {mine.DailyYield} ore a day.", mineSheet.Lines);
        Assert.StartsWith("A week brings 500 gold; waiting there: ", wheelSheet.Lines[0], StringComparison.Ordinal);
        Assert.EndsWith("a day (Treasury).", heroSheet.Lines[0], StringComparison.Ordinal);
        Assert.Equal(state.GetHero(A).Name, heroSheet.Title);
    }

    [Fact]
    public void TheKingdomTotal_SplitsTheDailyIncomeByKind()
    {
        var state = Kingdom();
        AddTown(state, TownIds.Haven, 0, 0, Blue);
        Add<Mine>(state, BuiltInMapObjects.MineId(ResourceType.Ore), 8, 0).Owner = Blue;
        Start(state);
        var overview = KingdomOverviews.For(state, Blue);

        var sheet = InfoSheets.KingdomTotal(overview);

        Assert.Equal("Kingdom income", sheet.Kind);
        Assert.Contains(sheet.Lines, l => l.StartsWith("Towns (1): ", StringComparison.Ordinal));
        Assert.Contains("Mines (1): 2 ore a day", sheet.Lines);
        Assert.Contains(sheet.Lines, l => l.StartsWith("Every day: ", StringComparison.Ordinal));
    }

    [Fact]
    public void ATavernHero_ShowsItsSkillsInWords_WhatItBrings_AndWhyItIsRefused()
    {
        var state = Kingdom();
        var town = AddTown(state, TownIds.Haven, 0, 0, Blue);
        Start(state);
        var offer = TavernRules.Offers(state, Blue, town)[0];

        var sheet = InfoSheets.TavernHero(state, Blue, town, offer, detailed: true);

        Assert.Equal(offer.Name, sheet.Title);
        Assert.EndsWith(", for hire", sheet.Kind, StringComparison.Ordinal);
        Assert.Contains($"Attack {offer.Attack}   Defense {offer.Defense}   Spell Power {offer.SpellPower}   Knowledge {offer.Knowledge}", sheet.Lines);
        foreach (var (skill, level) in offer.StartingSkills)
        {
            Assert.Contains($"{BuiltInSkills.Get(skill).Name} {level}: {BuiltInSkills.Get(skill).Description}", sheet.Lines);
        }

        Assert.Contains(sheet.Lines, l => l.StartsWith("Brings ", StringComparison.Ordinal));
        Assert.Contains(sheet.Lines, l => l.StartsWith("Cannot be hired: ", StringComparison.Ordinal)); // no gold yet
        Assert.Contains(sheet.Lines, l => l.StartsWith($"Id {offer.Id}", StringComparison.Ordinal));

        state.GetPlayer(Blue).Resources = new ResourceSet(Gold: 10_000);
        Assert.Contains("Hire for 2500 gold.", InfoSheets.TavernHero(state, Blue, town, offer, detailed: false).Lines);
    }

    [Fact]
    public void AGuardsOffer_ShowsTheStackWithItsCount()
    {
        var state = Kingdom();
        Start(state);
        var offer = new GuardEncounterDecision(state.GetHero(A).Id, new MapObjectId(99), GuardOffer.Join, BuiltInCreatures.Footman, 14);

        var sheet = InfoSheets.Decision(state, Blue, offer, detailed: false)!;

        Assert.Equal(state.Creatures.Get(BuiltInCreatures.Footman).Name, sheet.Title);
        Assert.Equal("Neutral stack, offering to join", sheet.Kind);
        Assert.Equal("Count: 14", sheet.Lines[0]);
    }

    [Fact]
    public void AVisitPrompt_ShowsTheObject_AMessageHasNoWindow()
    {
        var state = Kingdom();
        var mine = Add<Mine>(state, BuiltInMapObjects.MineId(ResourceType.Ore), 8, 0);
        Start(state);
        var hero = state.GetHero(A).Id;

        var deposit = InfoSheets.Decision(state, Blue, new DepositDecision(hero, mine.Id, ResourceSet.Empty), detailed: false)!;

        Assert.Equal(mine.Definition.Name, deposit.Title);
        Assert.Null(InfoSheets.Decision(state, Blue, new MapEventDecision(hero, "e", "Hello", [], []), detailed: false));
        Assert.Equal(state.GetHero(A).Name, InfoSheets.Decision(state, Blue, new LevelUpDecision(hero, 2, [BuiltInSkills.Scouting]), detailed: false)!.Title);
    }

    [Fact]
    public void AWitchHutPrompt_SaysWhatItTeaches_InsteadOfVisitItToSee()
    {
        var state = Kingdom();
        var hut = Add<WitchHut>(state, BuiltInMapObjects.WitchHut, 5, 0);
        Start(state);
        var hero = state.GetHero(A);
        var prompt = new WitchHutDecision(hero.Id, hut.Id, [BuiltInSkills.Scouting, BuiltInSkills.Treasury]);

        var sheet = InfoSheets.Decision(state, Blue, prompt, detailed: false)!;

        Assert.Equal(hut.Definition.Name, sheet.Title);
        Assert.Equal($"Teaches {hero.Name} one of: {BuiltInSkills.Get(BuiltInSkills.Scouting).Name}, {BuiltInSkills.Get(BuiltInSkills.Treasury).Name}.", sheet.Lines[0]);
        Assert.DoesNotContain("Visit it to see what it holds.", sheet.Lines);
    }

    [Fact]
    public void ASkillOnOffer_SaysTheLevelItWouldReach()
    {
        var state = Kingdom();
        var hero = state.GetHero(A);
        hero.Skills.Set(BuiltInSkills.Scouting, SkillLevel.Basic);
        var max = BuiltInSkills.Get(BuiltInSkills.Scouting).MaxLevel;

        var learned = InfoSheets.SkillChoice(hero, BuiltInSkills.Scouting, detailed: false);
        var fresh = InfoSheets.SkillChoice(hero, BuiltInSkills.Treasury, detailed: false);

        Assert.Equal($"{hero.Name} would learn it to Advanced of {max} (now Basic).", learned.Lines[0]);
        Assert.Contains(BuiltInSkills.Get(BuiltInSkills.Scouting).Description, learned.Lines);
        Assert.StartsWith($"{hero.Name} would learn it to Basic of ", fresh.Lines[0], StringComparison.Ordinal);
    }
}
