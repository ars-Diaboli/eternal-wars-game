using EternalWars.Core.Data.Campaigns;
using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.Campaigns;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.MapEvents;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Core.Domain.Towns;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>
/// Campaigns (ADR-241, FLOW-1): maps played one after another, heroes and campaign variables carried from a won map into the next, the record
/// keeping the campaign, and the campaign file. Played on the shipped sample campaign, Border Watch.
/// </summary>
public class CampaignTests
{
    private static readonly string Maren = BorderWatchCampaign.Maren;

    /// <summary>A Border Watch map started as a campaign map with <paramref name="options"/>, through the map format as the game starts it.</summary>
    private static GameSession Play(string map, GameOptions options, Action<GameState>? setup = null)
    {
        var state = MapConverter.ToGameState(MapConverter.ToDocument(BorderWatchCampaign.CreateMap(map)), options: options);
        setup?.Invoke(state);
        return Start(state);
    }

    /// <summary>Answers every message and dialogue the person is shown until none is left.</summary>
    private static void Read(GameSession session)
    {
        while (session.State.PendingDecision is MapEventDecision { IsQuestion: false })
        {
            session.Execute(new AnswerMapEventCommand(session.State.CurrentPlayer.Id, 0)).ThrowIfRejected();
        }
    }

    /// <summary>The map's events replaced by one that ends the game on Blue's first turn, after setting the scouts variable to 1.</summary>
    private static void EndsAtOnce(GameState state, bool won) => state.SetMapEvents(state.MapEvents.Set with
    {
        Cells = [],
        Events = [new MapEventDefinition("end", MapEventKind.Player, [new IfAction(new PlayerIsCondition(0),
            [new SetVariableAction(BorderWatchCampaign.Scouts, VariableOp.Set, new NumberExpr(1)), new EndGameAction(won)])])],
    });

    private static CarriedHero Veteran(IReadOnlyList<CarriedStack>? army = null) => new(Maren, "Maren the Bold")
    {
        Level = 5,
        Experience = 5_000,
        Attack = 4,
        Defense = 5,
        SpellPower = 2,
        Knowledge = 3,
        Skills = new Dictionary<string, SkillLevel> { [BuiltInSkills.Logistics] = SkillLevel.Expert, ["defense.armor"] = SkillLevel.Advanced },
        SpellBook = true,
        Spells = new Dictionary<string, int> { ["light.bless"] = 2 },
        Artifacts = [],
        Backpack = [],
        Army = army ?? [new CarriedStack(0, "haven.pikeman", 40), new CarriedStack(2, "haven.archer", 15)],
    };

    [Fact]
    public void ACarriedHero_TakesThePersonsHeroPlaceholder_WithWhatItGained()
    {
        var session = Play(BorderWatchCampaign.AshenGate, new GameOptions { CarriedHeroes = [Veteran()], CampaignVariables = new Dictionary<string, int>() });
        var maren = session.State.GetHero(new HeroId(1));

        Assert.Equal((Maren, "Maren the Bold", 5, 5_000), (maren.Definition, maren.Name, maren.Level, maren.Experience));
        Assert.False(maren.IsPlaceholder);
        Assert.Equal((4, 5, 2, 3), (maren.Attack, maren.Defense, maren.SpellPower, maren.Knowledge));
        Assert.Equal(SkillLevel.Expert, maren.SkillLevelOf(BuiltInSkills.Logistics));
        Assert.Equal(2, maren.Spells["light.bless"]);
        Assert.Equal(40, maren.Army[0]!.Count);
        Assert.Equal("haven.archer", maren.Army[2]!.Creature);
        Assert.Empty(session.State.CarriedWaiting);
    }

    [Fact]
    public void ACarriedHero_BecomesThePersonsHeroOfItsRosterHero_AndKeepsTheMapsArmyWhenItsOwnIsNotCarried()
    {
        var session = Play(BorderWatchCampaign.GreyFord, new GameOptions { CarriedHeroes = [Veteran() with { Army = null }] });
        var maren = session.State.GetHero(new HeroId(1));

        Assert.Equal((5, "Maren the Bold"), (maren.Level, maren.Name));
        Assert.Equal(24, maren.Army[0]!.Count); // the map's pikemen
    }

    [Fact]
    public void ACarriedHeroTheMapHasNoPlaceFor_WaitsForALaterMap()
    {
        var other = new CarriedHero("haven.ansel", "Ansel Wren") { Level = 3 };
        var session = Play(BorderWatchCampaign.GreyFord, new GameOptions { CarriedHeroes = [Veteran(), other] }, s => EndsAtOnce(s, won: true));

        Assert.Equal([other], session.State.CarriedWaiting);
        var next = CampaignRules.Finish(TwoMaps(new CampaignMap("b", "B") { CarryStrongest = 2 }), CampaignRules.Start(Difficulty.Normal), session.State)!;
        Assert.Equal([Maren, "haven.ansel"], next.Heroes.Select(h => h.Hero));
    }

    private static Campaign TwoMaps(CampaignMap second) => new("Test", "", [new CampaignMap("a", "A"), second]);

    [Fact]
    public void AWonMap_CarriesTheNamedHeroes_ThenTheStrongest_TheKeptVariables_AndItsDays()
    {
        var session = Play(BorderWatchCampaign.GreyFord, CampaignRules.Options(CampaignRules.Start(Difficulty.Hard) with
        {
            Variables = new Dictionary<string, int> { ["earlier"] = 3 },
        }, seed: 7), s => EndsAtOnce(s, won: true));
        Assert.Equal([Blue], session.State.Outcome!.Winners);

        var next = CampaignRules.Finish(BorderWatchCampaign.Campaign, CampaignRules.Start(Difficulty.Hard) with
        {
            Variables = new Dictionary<string, int> { ["earlier"] = 3 },
        }, session.State)!;

        Assert.Equal(1, next.Map);
        Assert.Equal(Difficulty.Hard, next.Difficulty);
        Assert.Equal(new Dictionary<string, int> { ["earlier"] = 3, [BorderWatchCampaign.Scouts] = 1 }, next.Variables);
        Assert.Equal([1], next.Days);
        var maren = Assert.Single(next.Heroes);
        Assert.Equal(Maren, maren.Hero);
        Assert.Equal(24, maren.Army!.Single(s => s.Slot == 0).Count); // the Ashen Gate keeps the army
        Assert.NotNull(maren.Artifacts);
    }

    [Fact]
    public void Carrying_PicksTheNamedHeroesFirst_ThenTheStrongestOthers_AndDropsWhatTheNextMapDoesNotKeep()
    {
        var session = Play(BorderWatchCampaign.GreyFord, GameOptions.Default, s =>
        {
            EndsAtOnce(s, won: true);
            s.AddHero(Hero.Create(new HeroId(5), Blue, new MapPosition(8, 12), null, s.HeroRoster.Get("haven.ansel")));
            s.AddHero(Hero.Create(new HeroId(6), Blue, new MapPosition(9, 12), null, s.HeroRoster.Get("haven.corwin")));
            s.GetHero(new HeroId(6)).Experience = 9_000;
            s.GetHero(new HeroId(6)).Level = 8;
        });

        var named = CampaignRules.Finish(TwoMaps(new CampaignMap("b", "B") { CarryHeroes = ["haven.ansel"], CarryStrongest = 1, KeepArtifacts = false }),
            CampaignRules.Start(Difficulty.Normal), session.State)!;

        Assert.Equal(["haven.ansel", "haven.corwin"], named.Heroes.Select(h => h.Hero));
        Assert.All(named.Heroes, h => Assert.Null(h.Army));
        Assert.All(named.Heroes, h => Assert.Null(h.Artifacts));
    }

    [Fact]
    public void ALostMap_CarriesNothing()
    {
        var session = Play(BorderWatchCampaign.GreyFord, CampaignRules.Options(CampaignRules.Start(Difficulty.Normal), seed: 1), s => EndsAtOnce(s, won: false));

        Assert.True(session.State.IsOver);
        Assert.DoesNotContain(Blue, session.State.Outcome!.Winners);
        Assert.Null(CampaignRules.Finish(BorderWatchCampaign.Campaign, CampaignRules.Start(Difficulty.Normal), session.State));
    }

    [Fact]
    public void ACampaignMap_IsNeverRanked()
    {
        var single = Play(BorderWatchCampaign.GreyFord, GameOptions.Default);
        var campaign = Play(BorderWatchCampaign.GreyFord, CampaignRules.Options(CampaignRules.Start(Difficulty.Normal), seed: 1));

        Assert.True(CampaignRules.InCampaign(campaign.State.Options));
        Assert.False(CampaignRules.InCampaign(single.State.Options));
        Assert.False(ScoreRules.IsRanked(campaign.State));
    }

    [Fact]
    public void BorderWatch_PlaysThrough_MarenAndTheScoutsCarryIntoTheAshenGate()
    {
        var campaign = BorderWatchCampaign.Campaign;
        var progress = CampaignRules.Start(Difficulty.Normal);
        var first = Play(campaign.Maps[0].Map, CampaignRules.Options(progress, seed: 3), s => EndsAtOnce(s, won: true));
        progress = CampaignRules.Finish(campaign, progress, first.State)!;

        var second = Play(campaign.Maps[1].Map, CampaignRules.Options(progress, seed: 4));
        Assert.Equal(1, second.State.MapEvents.Variable(BorderWatchCampaign.Scouts));
        Assert.Equal(Maren, second.State.GetHero(new HeroId(1)).Definition);
        Read(second); // the arrival, then the scouts at the camp

        var camp = second.State.ObjectsOf<Town>().Single(t => t.Owner == Blue);
        Assert.Contains(camp.Garrison.Stacks, s => s.Stack.Creature == "haven.archer" && s.Stack.Count == 12);
        Assert.Null(second.State.PendingDecision);
    }

    [Fact]
    public void ARecord_KeepsTheCampaign_AndItsCarriedHeroes_AndReplaysTheSame()
    {
        var progress = CampaignRules.Start(Difficulty.Easy) with { Map = 1, Heroes = [Veteran()], Variables = new Dictionary<string, int> { ["scouts"] = 1 }, Days = [9] };
        var map = MapConverter.ToDocument(BorderWatchCampaign.CreateMap(BorderWatchCampaign.AshenGate));
        var session = new GameSession(MapConverter.ToGameState(map, options: CampaignRules.Options(progress, seed: 11)));
        session.Start();

        var json = GameRecordSerializer.Serialize(GameReplay.Record(map, session, campaign: new CampaignRecord("builtin:Border Watch", progress)));
        var record = GameRecordSerializer.Deserialize(json);

        Assert.Equal(GameRecordDocument.CurrentFormatVersion, record.FormatVersion);
        Assert.Equal("builtin:Border Watch", record.Campaign!.Source);
        Assert.Equal(progress, record.Campaign.Progress);
        Assert.Equal([Veteran()], record.Options.CarriedHeroes!);
        var replayed = GameReplay.Replay(record);
        Assert.Equal(5, replayed.State.GetHero(new HeroId(1)).Level);
    }

    [Fact]
    public void ACampaignFile_RoundTrips_AndOneThatBreaksARuleIsRefused()
    {
        var campaign = BorderWatchCampaign.Campaign;
        var read = CampaignSerializer.Deserialize(CampaignSerializer.Serialize(campaign));

        Assert.Equal(campaign.Name, read.Name);
        Assert.Equal(campaign.Maps, read.Maps, (a, b) => a.Map == b.Map && a.Title == b.Title && a.KeepArmy == b.KeepArmy && a.CarryHeroes.SequenceEqual(b.CarryHeroes));
        Assert.Throws<FormatException>(() => CampaignSerializer.Deserialize("""{ "name": "Empty", "description": "", "maps": [] }"""));
        Assert.Throws<FormatException>(() => CampaignSerializer.Deserialize("""{ "formatVersion": 99, "name": "New", "description": "", "maps": [{ "map": "a", "title": "A" }] }"""));
        Assert.Throws<FormatException>(() => CampaignSerializer.Deserialize("not json"));
    }

    [Fact]
    public void ACampaignFolder_ListsItsCampaigns_AndSkipsOnesThatDoNotLoad()
    {
        var folder = Directory.CreateTempSubdirectory("ew-campaigns").FullName;
        try
        {
            CampaignSerializer.Save(BorderWatchCampaign.Campaign, Path.Combine(folder, "a" + CampaignSerializer.FileExtension));
            File.WriteAllText(Path.Combine(folder, "b" + CampaignSerializer.FileExtension), "{}");
            var warnings = new List<string>();

            var found = CampaignSerializer.LoadFolder(folder, warnings.Add);

            Assert.Equal(BorderWatchCampaign.Name, Assert.Single(found).Campaign.Name);
            Assert.Single(warnings);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void TheSampleMaps_PassTheEventChecks_AndPlayOnTheirOwn()
    {
        foreach (var map in BorderWatchCampaign.Maps)
        {
            var state = BorderWatchCampaign.CreateMap(map);
            Assert.Empty(MapEventRules.Problems(state, state.MapEvents.Set));
            Assert.Empty(MapEventChecks.Warnings(state, state.MapEvents.Set));
            var session = Start(state);
            Assert.All(session.State.Heroes, h => Assert.False(h.IsPlaceholder));
        }
    }
}
