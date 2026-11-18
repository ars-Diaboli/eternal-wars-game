using System.Text.Json.Nodes;
using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Scenarios;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>What the scenario rules remember from the start lives in <see cref="GameState.Tracking"/> and the digest (ADR-213).</summary>
public class ScenarioTrackingTests
{
    [Fact]
    public void Start_RemembersTheTeamsAndHeroes_InTheStateAndItsDigest()
    {
        var state = Create("A.B", "...", "C..");
        Assert.Same(ScenarioTracking.None, state.Tracking);

        Start(state);

        Assert.Equal(2, state.Tracking.TeamsAtStart);
        Assert.Equal([A, B, C], state.Tracking.Heroes.Keys);
        Assert.Equal(new TrackedHero(Red, "Bravo"), state.Tracking.Heroes[B]);
        Assert.Contains("tracking teams 2 heroes 1:0:Alpha,2:1:Bravo,3:0:Charlie prisoners \n", GameStateDigest.Describe(state), StringComparison.Ordinal);
    }

    [Fact]
    public void ADefeatedHero_StaysTracked_SoItsConditionStillNamesIt()
    {
        var state = Create("A.B");
        state.SetRules(new ScenarioRules([ScenarioCondition.DefeatHero(B)], [], StandardVictory: false, Elimination: false));
        state.GetHero(A).Army.Set(0, new CreatureStack(BuiltInCreatures.Champion, 10));
        var session = Start(state);

        session.MoveTo(A, new MapPosition(2, 0));
        session.AutoResolve();

        Assert.DoesNotContain(state.Heroes, h => h.Id == B);
        Assert.Equal("Bravo", state.Tracking.Heroes[B].Name);
        Assert.Equal([Blue], state.Outcome!.Winners);
    }

    [Fact]
    public void APrisonAConditionNames_IsTracked_WithItsPrisoner()
    {
        var state = Create("......", "......", "...A..", "......", ".....B");
        var prison = Add<Prison>(state, BuiltInMapObjects.Prison, 2, 1);
        state.SetRules(new ScenarioRules([ScenarioCondition.ReleaseHero(prison.Id)], []));

        Start(state);

        Assert.Equal(prison.Hero, state.Tracking.Prisoners[prison.Id]);
        Assert.Contains($"prisoners {prison.Id.Value}:{prison.Hero}\n", GameStateDigest.Describe(state), StringComparison.Ordinal);
    }

    [Fact]
    public void DigestVersion1_LeavesTheTrackingOut()
    {
        var state = Create("A.B");
        Start(state);

        var current = GameStateDigest.Describe(state);
        var first = GameStateDigest.Describe(state, GameStateDigest.FirstVersion);

        Assert.DoesNotContain("tracking", first, StringComparison.Ordinal);
        Assert.Equal(first, string.Join('\n', current.Split('\n').Where(l => !l.StartsWith("tracking ", StringComparison.Ordinal))));
        Assert.NotEqual(GameStateDigest.Compute(state), GameStateDigest.Compute(state, GameStateDigest.FirstVersion));
        Assert.Throws<ArgumentOutOfRangeException>(() => GameStateDigest.Describe(state, GameStateDigest.CurrentVersion + 1));
    }

    [Fact]
    public void ANewRecord_KeepsTheCurrentDigestVersion_ThroughJson()
    {
        var (_, record) = PlayedRecord();

        var json = GameRecordSerializer.Serialize(record);
        var loaded = GameRecordSerializer.Deserialize(json);

        Assert.Equal(GameStateDigest.CurrentVersion, loaded.DigestVersion);
        GameReplay.Replay(loaded);
        ReplayPlayer.Open(loaded);
    }

    [Fact]
    public void AnOlderRecord_WithoutADigestVersion_StillChecks_WithTheFirstVersion()
    {
        var (session, record) = PlayedRecord();
        var node = JsonNode.Parse(GameRecordSerializer.Serialize(record with { FinalDigest = GameStateDigest.Compute(session.State, GameStateDigest.FirstVersion) }))!.AsObject();
        Assert.True(node.Remove("digestVersion"));
        var older = GameRecordSerializer.Deserialize(node.ToJsonString());

        Assert.Equal(GameStateDigest.FirstVersion, older.DigestVersion);
        GameReplay.Replay(older);
        ReplayPlayer.Open(older);

        // The current digest under the first version does not match: the version is what the check uses.
        Assert.Throws<InvalidOperationException>(() => GameReplay.Replay(older with { FinalDigest = record.FinalDigest }));
    }

    [Fact]
    public void ARecordWithAnUnknownDigestVersion_IsRefusedLikeOtherContent()
    {
        var (_, record) = PlayedRecord();

        Assert.Throws<InvalidOperationException>(() => GameReplay.Replay(record with { DigestVersion = GameStateDigest.CurrentVersion + 1 }));
        Assert.Throws<InvalidOperationException>(() => ReplayPlayer.Open(record with { DigestVersion = 0 }));
    }

    private static (GameSession Session, GameRecordDocument Record) PlayedRecord()
    {
        var map = MapConverter.ToDocument(DemoScenario.Create());
        var session = new GameSession(MapConverter.ToGameState(map, options: new GameOptions(Seed: 7)));
        session.Start();
        for (var i = 0; i < 4; i++)
        {
            session.Execute(new Core.Domain.Commands.EndTurnCommand(session.State.CurrentPlayer.Id));
        }

        return (session, GameReplay.Record(map, session));
    }
}
