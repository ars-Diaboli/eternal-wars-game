using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.MapEvents;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>Campaign variables (EVT-1 f, FLOW-1): "kept for the next map" and "taken from the last map", recorded in the map.</summary>
public class CampaignVariableTests
{
    private static readonly string[] Rows = [".........", "...A.....", "........B"];

    /// <summary>A map whose player event adds 1 to "deeds" every turn; "deeds" is kept and taken, "other" only taken, "plain" neither.</summary>
    private static MapEventSet Events() => new()
    {
        Events = [new MapEventDefinition("count", MapEventKind.Player,
            [new IfAction(new PlayerIsCondition(0), [new SetVariableAction("deeds", VariableOp.Add, new NumberExpr(1))])])],
        Variables = new Dictionary<string, int> { ["deeds"] = 10, ["other"] = 3, ["plain"] = 4 },
        KeptVariables = ["deeds"],
        TakenVariables = ["deeds", "other"],
    };

    [Fact]
    public void TakenVariables_StartWithTheCarriedValue_OthersWithTheirOwn()
    {
        var carried = new Dictionary<string, int> { ["deeds"] = 40, ["plain"] = 99, ["unknown"] = 1 };
        var state = Create(new GameOptions { CampaignVariables = carried }, Rows);
        state.SetMapEvents(Events());

        Assert.Equal(40, state.MapEvents.Variable("deeds"));
        Assert.Equal(3, state.MapEvents.Variable("other")); // taken, but nothing carried for it
        Assert.Equal(4, state.MapEvents.Variable("plain")); // carried, but not taken
        Assert.Equal(0, state.MapEvents.Variable("unknown"));
    }

    [Fact]
    public void WithoutACampaign_EveryVariableStartsWithItsOwnValue()
    {
        var state = Create(Rows);
        state.SetMapEvents(Events());

        Assert.Equal(10, state.MapEvents.Variable("deeds"));
    }

    [Fact]
    public void KeptVariables_AreWhatTheCampaignCarriesOn_WithTheirCurrentValues()
    {
        var state = Create(new GameOptions { CampaignVariables = new Dictionary<string, int> { ["deeds"] = 40 } }, Rows);
        state.SetMapEvents(Events());
        Start(state);

        Assert.Equal(new Dictionary<string, int> { ["deeds"] = 41 }, state.MapEvents.CampaignVariables);
    }

    [Fact]
    public void TheMarks_AreSavedInTheMap_AndTheCarriedValuesInTheRecord()
    {
        var state = Create(Rows);
        state.SetMapEvents(Events());
        var document = MapSerializer.Deserialize(MapSerializer.Serialize(MapConverter.ToDocument(state)));
        Assert.Equal(MapDocument.CurrentFormatVersion, document.FormatVersion);
        Assert.Equal(["deeds"], document.Events!.KeptVariables);
        Assert.Equal(["deeds", "other"], document.Events.TakenVariables);

        var session = new GameSession(MapConverter.ToGameState(document, options: new GameOptions { CampaignVariables = new Dictionary<string, int> { ["deeds"] = 7 } }));
        session.Start();
        var record = GameRecordSerializer.Deserialize(GameRecordSerializer.Serialize(GameReplay.Record(document, session)));
        var replayed = GameReplay.Replay(record);

        Assert.Equal(8, replayed.State.MapEvents.Variable("deeds"));
        Assert.Equal(session.State.MapEvents.CampaignVariables, replayed.State.MapEvents.CampaignVariables);
    }

    [Fact]
    public void TheEditor_MarksVariables_Undoably_AndRemovingAVariableDropsItsMarks()
    {
        var editor = new MapEditorSession(Create(Rows));
        editor.MapEvents.SetVariable("deeds", 0);

        Assert.Equal(EditResult.InvalidTarget, editor.MapEvents.SetCampaignVariable("nope", true, false));
        Assert.Equal(EditResult.Ok, editor.MapEvents.SetCampaignVariable("deeds", kept: true, taken: true));
        Assert.True(editor.Scenario.MapEvents.Set.IsKept("deeds"));
        Assert.True(editor.Scenario.MapEvents.Set.IsTaken("deeds"));
        Assert.Equal(EditResult.Ok, editor.MapEvents.SetCampaignVariable("deeds", kept: false, taken: true));
        Assert.Empty(editor.Scenario.MapEvents.Set.KeptVariables);
        editor.Undo();
        Assert.True(editor.Scenario.MapEvents.Set.IsKept("deeds"));

        editor.MapEvents.RemoveVariable("deeds");
        Assert.Empty(editor.Scenario.MapEvents.Set.KeptVariables);
        Assert.Empty(editor.Scenario.MapEvents.Set.TakenVariables);
    }

    [Fact]
    public void AnUndeclaredCampaignVariable_IsAnError_AndAKeptOne_NeedNotBeRead()
    {
        var state = Create(Rows);
        var set = new MapEventSet { Variables = new Dictionary<string, int> { ["deeds"] = 0, ["idle"] = 0 }, KeptVariables = ["deeds", "ghost"] };

        Assert.Equal(["The campaign variable 'ghost' is not declared."], MapEventRules.Problems(state, set));
        var warnings = MapEventChecks.Warnings(state, set).Select(w => w.Message).ToList();
        Assert.Contains("The variable 'idle' is never read.", warnings);
        Assert.DoesNotContain("The variable 'deeds' is never read.", warnings);
    }

    [Fact]
    public void AnOlderMap_LoadsWithoutCampaignVariables()
    {
        var state = Create(Rows);
        state.SetMapEvents(new MapEventSet { Variables = new Dictionary<string, int> { ["x"] = 1 } });
        var json = MapSerializer.Serialize(MapConverter.ToDocument(state) with { FormatVersion = 33 });

        var loaded = MapConverter.ToGameState(MapSerializer.Deserialize(json));

        Assert.Empty(loaded.MapEvents.Set.KeptVariables);
        Assert.Empty(loaded.MapEvents.CampaignVariables);
    }

    [Fact]
    public void ARecordWithoutCarriedValues_StillReplays()
    {
        var state = Create(Rows);
        state.SetMapEvents(Events());
        var document = MapConverter.ToDocument(state);
        var session = new GameSession(MapConverter.ToGameState(document));
        session.Start();
        session.Execute(new EndTurnCommand(Blue)).ThrowIfRejected();

        var json = GameRecordSerializer.Serialize(GameReplay.Record(document, session));

        Assert.DoesNotContain("campaignVariables", json);
        Assert.Equal(session.State.MapEvents.Variable("deeds"), GameReplay.Replay(GameRecordSerializer.Deserialize(json)).State.MapEvents.Variable("deeds"));
    }
}
