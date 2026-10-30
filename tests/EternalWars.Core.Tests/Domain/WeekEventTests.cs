using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Templates;
using EternalWars.Core.Domain.AI;
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
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Domain.Turns;
using EternalWars.Core.Events;
using EternalWars.Core.Generation;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>Week and month events (ADR-073, ADR-075) and the Astrologer's Tower (MAP-32; ADR-084).</summary>
public class WeekEventTests
{
    private static readonly GameDate SecondWeek = new(8);
    private static readonly GameDate FifthWeek = new(29); // a month's first week

    [Fact]
    public void TheWeeklyTable_IsAWeekOfACreatureOrNothing_TheMonthlyTableHasAllFourOutcomes()
    {
        var random = new GameRandom(7);
        var weekly = Enumerable.Range(0, 2000).Select(_ => WeekEventRules.Roll(WeekEventSettings.Default, CreatureCatalog.Default, SecondWeek, random)).ToList();
        var monthly = Enumerable.Range(0, 2000).Select(_ => WeekEventRules.Roll(WeekEventSettings.Default, CreatureCatalog.Default, FifthWeek, random)).ToList();

        Assert.All(weekly, e => Assert.Contains(e.Kind, new[] { WeekEventKind.WeekOfCreature, WeekEventKind.None }));
        Assert.InRange(weekly.Count(e => e.Kind == WeekEventKind.WeekOfCreature), 1400, 1600); // 75 %

        // 45 % month of a creature, 15 % plague, 15 % starvation, and the remaining 25 % roll the weekly table (75 % of it a week of a creature).
        Assert.InRange(monthly.Count(e => e.Kind == WeekEventKind.MonthOfCreature), 800, 1000);
        Assert.InRange(monthly.Count(e => e.Kind == WeekEventKind.Plague), 230, 370);
        Assert.InRange(monthly.Count(e => e.Kind == WeekEventKind.Starvation), 230, 370);
        Assert.InRange(monthly.Count(e => e.Kind == WeekEventKind.WeekOfCreature), 290, 460);
        Assert.All(monthly.Where(e => e.Kind is WeekEventKind.WeekOfCreature or WeekEventKind.MonthOfCreature),
            e => Assert.Contains(CreatureCatalog.Default.Get(e.Creature!).Town, BuiltInMapObjects.PlayableTowns));
    }

    [Fact]
    public void EventsOff_OrSkippedWeeks_RollNothing_AndDrawNothing()
    {
        var random = new GameRandom(3);
        var before = random.State;

        Assert.Equal(WeekEvent.None, WeekEventRules.Roll(new WeekEventSettings(Enabled: false), CreatureCatalog.Default, SecondWeek, random));
        Assert.Equal(WeekEvent.None, WeekEventRules.Roll(WeekEventSettings.Default, CreatureCatalog.Default, GameDate.First, random));
        Assert.Equal(WeekEvent.None, WeekEventRules.Roll(new WeekEventSettings(SkipWeeks: 4), CreatureCatalog.Default, new GameDate(22), random));
        Assert.Equal(before, random.State);

        WeekEventRules.Roll(new WeekEventSettings(SkipWeeks: 0), CreatureCatalog.Default, GameDate.First, random);
        Assert.NotEqual(before, random.State);
    }

    [Fact]
    public void TheFactions_DecideTheCreatures_NeutralMeansCreaturesOfNoTown()
    {
        var neutral = WeekEventRules.Candidates(CreatureCatalog.Default, [WeekEventSettings.Neutral]);
        var conflux = WeekEventRules.Candidates(CreatureCatalog.Default, [TownIds.Conflux]);

        Assert.NotEmpty(neutral);
        Assert.All(neutral, c => Assert.Null(c.Town));
        Assert.All(conflux, c => Assert.Equal(TownIds.Conflux, c.Town));
        Assert.Contains(WeekEventRules.Candidates(CreatureCatalog.Default, BuiltInMapObjects.PlayableTowns), c => c.Id == BuiltInCreatures.Crusader);
        Assert.Empty(WeekEventRules.Candidates(CreatureCatalog.Default, []));

        var settings = new WeekEventSettings(WeeklyFactions: [TownIds.Conflux]);
        var random = new GameRandom(11);
        var rolled = Enumerable.Range(0, 200).Select(_ => WeekEventRules.Roll(settings, CreatureCatalog.Default, SecondWeek, random)).Where(e => e.Creature is not null);
        Assert.All(rolled, e => Assert.Equal(TownIds.Conflux, CreatureCatalog.Default.Get(e.Creature!).Town));
    }

    [Fact]
    public void AWeekOfACreature_DoublesItsLine_InTownsAndMapDwellings_TheUpgradeNamedOrNot()
    {
        var (state, session, town, dwelling) = Kingdom();
        var footman = state.Creatures.Get(BuiltInCreatures.Footman);
        var announced = new List<WeekEventAnnounced>();
        session.Events.Subscribe<WeekEventAnnounced>(announced.Add);
        var townBefore = town.AvailableOf(BuiltInCreatures.Footman);
        var mapBefore = dwelling.Available!.Value;
        var lancerBefore = town.AvailableOf(BuiltInCreatures.Lancer);

        state.NextWeekEvent = new WeekEvent(WeekEventKind.WeekOfCreature, BuiltInCreatures.Crusader);
        NextWeek(session);

        Assert.Equal(new WeekEvent(WeekEventKind.WeekOfCreature, BuiltInCreatures.Crusader), state.WeekEvent);
        Assert.Equal([new WeekEventAnnounced(state.Date, state.WeekEvent)], announced);
        Assert.Equal(townBefore + (2 * TownRules.WeeklyGrowth(state, town, footman)), town.AvailableOf(BuiltInCreatures.Footman));
        Assert.Equal(mapBefore + (2 * footman.WeeklyGrowth), dwelling.Available);
        Assert.Equal(lancerBefore + TownRules.WeeklyGrowth(state, town, state.Creatures.Get(BuiltInCreatures.Lancer)), town.AvailableOf(BuiltInCreatures.Lancer));
        Assert.Equal("Week of the Crusader: its growth doubles.", state.WeekEvent.Describe(state.Creatures));
    }

    [Fact]
    public void APlague_HalvesTheTowns_AndStopsAllGrowth_StarvationOnlyStopsIt()
    {
        var (state, session, town, dwelling) = Kingdom();
        town.SetAvailable(BuiltInCreatures.Footman, 15);
        var mapBefore = dwelling.Available;

        state.NextWeekEvent = new WeekEvent(WeekEventKind.Plague);
        NextWeek(session);

        Assert.Equal(7, town.AvailableOf(BuiltInCreatures.Footman));
        Assert.Equal(mapBefore, dwelling.Available);

        state.NextWeekEvent = new WeekEvent(WeekEventKind.Starvation);
        NextWeek(session);

        Assert.Equal(7, town.AvailableOf(BuiltInCreatures.Footman));
        Assert.Equal(mapBefore, dwelling.Available);
    }

    [Fact]
    public void AMonthOfACreature_PutsStacksOfItBesideMapObjects_SeenByWhoeverSeesThem()
    {
        var state = Create(
            "A.........................",
            "..........................",
            "..........................",
            "..........................",
            "..........................",
            "..........................",
            ".........................B");
        var sites = new List<MapObject>();
        for (var i = 0; i < 12; i++)
        {
            sites.Add(Add<ResourcePile>(state, BuiltInMapObjects.PileId(ResourceType.Gold), 2 + (2 * i % 22), 2 + (2 * (i / 11))));
        }

        var session = Start(state);
        var spawned = new List<MonstersSpawned>();
        session.Events.Subscribe<MonstersSpawned>(spawned.Add);

        state.NextWeekEvent = new WeekEvent(WeekEventKind.MonthOfCreature, BuiltInCreatures.Wolf);
        NextWeek(session);

        var stacks = Assert.Single(spawned).Monsters.Select(id => (MonsterGuard)state.GetObject(id)).ToList();
        Assert.Equal(WeekEventRules.SpawnedStacks(12), stacks.Count);
        Assert.All(stacks, m =>
        {
            Assert.Equal(BuiltInCreatures.Wolf, m.Creature);
            Assert.InRange(m.Count!.Value, GuardService.CountRange(1).Min, GuardService.CountRange(1).Max);
            Assert.Contains(sites, s => m.GuardedTiles().Contains(s.VisitPosition));
        });
        Assert.All(stacks, m => Assert.Contains(state.Memory.Of(Blue), k => k.Id == m.Id));
    }

    [Fact]
    public void ByDefault_TheFirstWeekHasNoEvent_AndSkippedWeeksStayQuiet()
    {
        var state = Create("A....", "....B");
        state.SetRules(ScenarioRules.Default with { WeekEvents = new WeekEventSettings(SkipWeeks: 2) });
        var session = Start(state);

        Assert.Equal(WeekEvent.None, state.WeekEvent);
        Assert.Equal(WeekEvent.None, state.NextWeekEvent); // week 2 is skipped too
        NextWeek(session);
        Assert.Equal(WeekEvent.None, state.WeekEvent);
    }

    [Fact]
    public void TheAstrologersTower_TellsNextWeeksEvent_WhichComesTrue_AndIsInTheMapFormat()
    {
        var state = Create(".........", ".........", "...A.....", ".........", "........B");
        var tower = Add<AstrologersTower>(state, BuiltInMapObjects.AstrologersTower, 2, 0);
        var session = Start(state);
        var told = new List<AstrologerConsulted>();
        session.Events.Subscribe<AstrologerConsulted>(told.Add);
        state.NextWeekEvent = new WeekEvent(WeekEventKind.Starvation);

        Assert.Equal(MoveResult.Moved, session.Step(A, Direction.North));
        Assert.Equal([new AstrologerConsulted(A, tower.Id, new WeekEvent(WeekEventKind.Starvation))], told);
        NextWeek(session);
        Assert.Equal(WeekEventKind.Starvation, state.WeekEvent.Kind);

        var json = MapSerializer.Serialize(MapConverter.ToDocument(state));
        Assert.Contains("\"kind\": \"astrologersTower\"", json, StringComparison.Ordinal);
        Assert.Single(MapConverter.ToGameState(MapSerializer.Deserialize(json)).ObjectsOf<AstrologersTower>());
    }

    [Fact]
    public void TheSettings_AreInTheMapFormat_TheEditor_TheValidator_AndTheTemplates()
    {
        var plain = MapSerializer.Serialize(MapConverter.ToDocument(Create("A..", "..B")));
        Assert.DoesNotContain("weekEvents", plain, StringComparison.Ordinal);

        var editor = new MapEditorSession(Create("A..", "..B"));
        var settings = new WeekEventSettings(SkipWeeks: 3, WeeklyFactions: [TownIds.Haven, WeekEventSettings.Neutral], MonthlyFactions: []);
        Assert.Equal(EditResult.Ok, editor.SetScenarioRules(editor.Scenario.Rules with { WeekEvents = settings }));
        Assert.Equal(EditResult.InvalidValue, editor.SetScenarioRules(editor.Scenario.Rules with { WeekEvents = new WeekEventSettings(SkipWeeks: 9) }));
        Assert.Equal(EditResult.InvalidValue, editor.SetScenarioRules(editor.Scenario.Rules with { WeekEvents = new WeekEventSettings(WeeklyFactions: ["elves"]) }));
        Assert.Contains(MapValidator.Validate(editor.Scenario), i => i.Severity == IssueSeverity.Warning && i.Message.Contains("monthly", StringComparison.Ordinal));

        var json = MapSerializer.Serialize(MapConverter.ToDocument(editor.Scenario));
        Assert.Contains("\"weekEvents\"", json, StringComparison.Ordinal);
        Assert.Equal(settings, MapConverter.ToGameState(MapSerializer.Deserialize(json)).Rules.WeekEvents);
        editor.Undo();
        Assert.True(editor.Scenario.Rules.WeekEvents.IsDefault);

        var off = BuiltInTemplates.Find("duel")! with { WeekEvents = new TemplateWeekEvents { Enabled = false } };
        var restored = TemplateSerializer.Deserialize(TemplateSerializer.Serialize(off));
        Assert.False(restored.WeekEvents.Enabled);
        Assert.False(MapGenerator.Build(restored, 72, 1, [new PlayerSetup(0, "Red", true, TownIds.Haven), new PlayerSetup(1, "Blue", false, TownIds.Inferno)]).State.Rules.WeekEvents.Enabled);
        Assert.Contains(TemplateValidator.Validate(off with { WeekEvents = new TemplateWeekEvents { SkipWeeks = -1 } }), i => i.Severity == IssueSeverity.Error);
    }

    [Fact]
    public void TheAi_RecruitsTheWeeksCreatureFirst()
    {
        static string? FirstRecruit(WeekEvent week)
        {
            var (state, session, town, _) = Kingdom();
            town.Garrison.Set(0, new CreatureStack(BuiltInCreatures.Footman, 1));
            town.SetAvailable(BuiltInCreatures.Footman, 20);
            town.SetAvailable(BuiltInCreatures.Lancer, 4);
            state.GetPlayer(Blue).Resources = new ResourceSet(Gold: 100_000, Lumber: 50, Ore: 50, Crystal: 50, Mercury: 50, Sulfur: 50);
            state.WeekEvent = week;
            new AdventureAi(session, controlHumanSlots: true).PlayTurn();
            return session.CommandLog.OfType<RecruitCommand>().FirstOrDefault()?.Creature;
        }

        Assert.NotEqual(BuiltInCreatures.Footman, FirstRecruit(WeekEvent.None)); // higher tiers first
        Assert.Equal(BuiltInCreatures.Footman, FirstRecruit(new WeekEvent(WeekEventKind.WeekOfCreature, BuiltInCreatures.Footman)));
    }

    /// <summary>A Haven town with footmen and lancers, and a map dwelling of footmen, both stocked.</summary>
    private static (GameState State, GameSession Session, Town Town, CreatureDwelling Dwelling) Kingdom()
    {
        var state = Create("...........", "...........", "...........", "...........", "...........", "...........", "...........", "A.........B");
        var town = AddTown(state, TownIds.Haven, 0, 0, Blue);
        town.SetDwelling(BuiltInCreatures.Footman, 1);
        town.SetDwelling(BuiltInCreatures.Lancer, 1);
        var dwelling = Add<CreatureDwelling>(state, BuiltInMapObjects.Dwelling, 8, 0);
        dwelling.Creature = BuiltInCreatures.Footman;
        dwelling.Owner = Blue;
        var session = Start(state);
        return (state, session, town, dwelling);
    }

    private static void NextWeek(GameSession session)
    {
        do
        {
            session.EndTurn();
        }
        while (!(session.State.Date.IsFirstDayOfWeek && session.State.CurrentPlayerIndex == 0));
    }
}
