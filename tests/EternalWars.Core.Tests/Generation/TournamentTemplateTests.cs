using EternalWars.Core.Data.Templates;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Generation;

namespace EternalWars.Core.Tests.Generation;

/// <summary>
/// Tournament templates (ADR-366, tournament-plan step 2): the template's flag, its validator's per-player checks, the generator's tournament
/// (the rules, one hero, each player's part as its start zone) and the generated map's isolation check.
/// </summary>
public class TournamentTemplateTests
{
    private static MapTemplate Duel() => BuiltInTemplates.Find("duel")!;

    /// <summary>The shipped duel without its centre: two parts, a start and a reserve behind it each, nothing between them.</summary>
    private static MapTemplate Apart() => Duel() with
    {
        Name = "Apart",
        Tournament = true,
        MaxHeroes = 1,
        Zones = Duel().Zones.Where(z => z.Id != "centre").ToList(),
        Connections = Duel().Connections.Where(c => c.From != "centre" && c.To != "centre").ToList(),
    };

    private static IEnumerable<string> Errors(MapTemplate template) =>
        TemplateValidator.Validate(template).Where(i => i.Severity == IssueSeverity.Error).Select(i => i.Message);

    private static IEnumerable<string> Warnings(MapTemplate template) =>
        TemplateValidator.Validate(template).Where(i => i.Severity == IssueSeverity.Warning).Select(i => i.Message);

    private static readonly PlayerSetup[] Players =
    [
        new(0, "Red", IsHuman: true, TownIds.Haven),
        new(1, "Blue", IsHuman: false, TownIds.Inferno),
    ];

    [Fact]
    public void TwoPartsWithNothingBetweenThem_AreAValidTournament_ThoughNotAConnectedGraph()
    {
        Assert.Empty(Errors(Apart()));
        Assert.Contains(Errors(Apart() with { Tournament = false }), e => e.Contains("not connected", StringComparison.Ordinal));
    }

    [Fact]
    public void AWayBetweenTheStartZones_IsAnError_NamingIt()
    {
        var error = Assert.Single(Errors(Duel() with { Tournament = true }));

        Assert.Contains("'start-1' and 'start-2' are joined (start-1 – centre – start-2)", error, StringComparison.Ordinal);
    }

    [Fact]
    public void AGateOrPortalBetweenTheParts_IsAnError()
    {
        foreach (var kind in new[] { ConnectionKind.Gate, ConnectionKind.Portal })
        {
            var template = Apart() with { Connections = [.. Apart().Connections, new TemplateConnection { From = "reserve-1", To = "reserve-2", Kind = kind, GuardValue = 1000 }] };

            Assert.Contains(Errors(template), e => e.Contains("(start-1 – reserve-1 – reserve-2 – start-2)", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void AZoneNoStartReaches_IsNoPlayers()
    {
        var template = Apart() with
        {
            Zones = [.. Apart().Zones, new TemplateZone { Id = "lost", Type = ZoneType.Junction }, new TemplateZone { Id = "lost-too", Type = ZoneType.Junction }],
            Connections = [.. Apart().Connections, new TemplateConnection { From = "lost", To = "lost-too" }],
        };

        Assert.Equal(
            ["Zone 'lost' is no player's: no connection leads to it from a start zone.", "Zone 'lost-too' is no player's: no connection leads to it from a start zone."],
            Errors(template));
    }

    [Fact]
    public void APartOfOneZone_NeedsNoConnection()
    {
        var template = Apart() with { Zones = Apart().Zones.Where(z => z.Type == ZoneType.PlayerStart).ToList(), Connections = [] };

        Assert.Empty(Errors(template));
    }

    [Fact]
    public void OtherThanTwoPlayers_Allies_OrAHeroLimit_AreCaught()
    {
        Assert.Contains("A tournament template is for exactly two players.", Errors(Apart() with { MaxPlayers = 3 }));
        var allied = Apart() with { Zones = Apart().Zones.Select(z => z.Type == ZoneType.PlayerStart ? z with { Team = 0 } : z).ToList() };
        Assert.Contains(Errors(allied), e => e.Contains("cannot be allies", StringComparison.Ordinal));
        Assert.Empty(Warnings(Apart()));
        Assert.Contains("A tournament allows one hero a player: the hero limit of 8 is ignored.", Warnings(Apart() with { MaxHeroes = 8 }));
    }

    [Fact]
    public void TheFlag_RoundTrips_AndAnOlderFileIsNoTournament()
    {
        Assert.Equal(10, MapTemplate.CurrentFormatVersion);
        var json = TemplateSerializer.Serialize(Apart());
        Assert.Contains("\"tournament\": true", json, StringComparison.Ordinal);
        Assert.True(TemplateSerializer.Deserialize(json).Tournament);

        var older = TemplateSerializer.Deserialize("""{ "formatVersion": 9, "name": "Old", "zones": [] }""");
        Assert.False(older.Tournament);
    }

    [Fact]
    public void TheGenerator_MakesATournament_WithOneHero_AndEachPartAsItsPlayersStartZone()
    {
        var (state, generation) = MapGenerator.Build(Apart() with { MaxHeroes = 8 }, 72, 7, Players, maxHeroes: 4);

        Assert.Empty(generation.Problems);
        Assert.True(state.Rules.Tournament);
        Assert.Equal(1, state.Rules.MaxHeroes);
        Assert.Empty(TournamentRules.MapProblems(state));
        var layout = generation.Map.Layout;
        foreach (var (zone, owner) in new[] { ("start-1", 0), ("reserve-1", 0), ("start-2", 1), ("reserve-2", 1) })
        {
            var tiles = layout.Tiles(layout.ZoneIds.ToList().IndexOf(zone)).Select(t => new MapPosition(t.X, t.Y, t.Level)).ToList();
            Assert.NotEmpty(tiles);
            Assert.All(tiles, t => Assert.Equal(new PlayerId(owner), state.Map.GetStartZone(t)));
        }

        Assert.All(generation.Map.Fairness!.Starts, s => Assert.Equal(0, s.GuardPower)); // no rival to walk to
        Assert.Null(TournamentRules.WhyNot(state));
    }

    [Fact]
    public void AnOrdinaryTemplate_StaysNoTournament_WithItsOwnStartZonesOnly()
    {
        var (state, generation) = MapGenerator.Build(Duel(), 72, 7, Players, maxHeroes: 4);

        Assert.False(state.Rules.Tournament);
        Assert.Equal(4, state.Rules.MaxHeroes);
        var layout = generation.Map.Layout;
        var reserve = layout.Tiles(layout.ZoneIds.ToList().IndexOf("reserve-1")).Select(t => new MapPosition(t.X, t.Y, t.Level));
        Assert.All(reserve, t => Assert.Null(state.Map.GetStartZone(t)));
    }

    [Fact]
    public void AGeneratedMapWhoseStartsMeet_FailsTheTournamentsCheck()
    {
        var map = MapGenerator.Generate(Duel(), 72, 7); // with the centre, which in Apart is in neither part

        var problems = MapFairness.Check(Apart(), map).Problems;

        Assert.Contains("The start town of slot 0 can walk to the start town of slot 1: a tournament's players stay apart.", problems);
        Assert.Contains(problems, p => p.Contains("in zone centre can be walked to from the start town of slot 0", StringComparison.Ordinal));
        Assert.DoesNotContain(MapFairness.Check(Duel(), map).Problems, p => p.Contains("can walk to", StringComparison.Ordinal));
    }

    /// <summary>The shipped mirrored duel without its middle, its start-to-treasure passages held by border guards of one colour on both sides.</summary>
    private static MapTemplate MirroredApart()
    {
        var mirrored = BuiltInTemplates.Find("mirror-duel")!;
        return mirrored with
        {
            Name = "Mirrored apart",
            Tournament = true,
            MaxHeroes = 1,
            Zones = mirrored.Zones.Where(z => z.Id != "middle").ToList(),
            Connections = mirrored.Connections.Where(c => c.From != "middle" && c.To != "middle")
                .Select(c => c with { Guard = ConnectionGuard.BorderGuard, Colour = ObjectColour.Red, GuardValue = 500 }).ToList(),
        };
    }

    [Fact]
    public void EachPart_GetsItsOwnKeymastersTent_ForABorderGuardOfAColourBothHave()
    {
        var template = MirroredApart();
        Assert.Empty(Errors(template));

        var map = MapGenerator.Generate(template, 72, 2);

        var zones = map.Objects.OfType<KeymasterTent>().Select(t => map.Layout.IdAt(t.Position.X, t.Position.Y, t.Position.Level)).Order(StringComparer.Ordinal);
        Assert.Equal(["start-1", "start-2"], zones);
    }

    [Fact]
    public void TheTemplateBatch_PassesATournament_ThoughItsPlayersNeverMeet()
    {
        var batch = TemplateBatch.Check(Apart(), 72, 7);

        Assert.True(batch.Passed, string.Join("; ", batch.Unreachable.Concat(batch.MapErrors)));
        Assert.DoesNotContain(batch.Map.Issues, i => i.Message.Contains("touch", StringComparison.Ordinal)); // the parts touch only across the wall
    }

    [Fact]
    public void AlliesOrOnePlayer_AreRefusedBeforeAnyMapIsMade()
    {
        Assert.Throws<ArgumentException>(() => MapGenerator.Build(Apart(), 72, 7, [Players[0] with { Team = 0 }, Players[1] with { Team = 0 }]));
        Assert.Throws<ArgumentException>(() => MapGenerator.Build(Apart(), 72, 7, [Players[0]]));
    }
}
