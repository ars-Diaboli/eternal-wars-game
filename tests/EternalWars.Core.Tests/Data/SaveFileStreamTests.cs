using System.Text;
using System.Text.Json;
using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Game;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Data;

/// <summary>Saves written straight to a file stream, and save lists read from the label only (ADR-224).</summary>
public sealed class SaveFileStreamTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "ew-save-stream-" + Guid.NewGuid().ToString("N"));

    public SaveFileStreamTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private static (GameSession Session, MapDocument Initial) NewGame()
    {
        var state = Create("A.B", "...");
        state.GetHero(A).Army.Set(0, new CreatureStack(BuiltInCreatures.Champion, 10));
        var initial = MapConverter.ToDocument(state);
        return (Start(state, revealAll: false), initial);
    }

    private static GameRecordDocument SavedRecord(string name = "evening")
    {
        var (session, initial) = NewGame();
        session.Step(A, Direction.East);
        session.EndTurn();
        return GameReplay.Record(initial, session, name);
    }

    /// <summary>A record of <paramref name="commands"/> end-turn commands: several megabytes, its label far behind the first buffer.</summary>
    private static GameRecordDocument LargeRecord(int commands) => SavedRecord() with
    {
        Commands = [.. Enumerable.Range(0, commands).Select(i => new EndTurnCommand(new PlayerId(i % 2)))],
    };

    [Fact]
    public void Save_WritesTheBytesOfTheSerializedText()
    {
        var record = SavedRecord();
        var path = Path.Combine(_folder, "a" + GameRecordSerializer.SaveExtension);

        GameRecordSerializer.Save(record, path);

        // The bytes File.WriteAllText wrote before (UTF-8, no byte order mark).
        Assert.Equal(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(GameRecordSerializer.Serialize(record)), File.ReadAllBytes(path));
    }

    [Fact]
    public void Autosave_WritesTheSameFileAsSerializingItsRecord()
    {
        var (session, initial) = NewGame();
        session.EndTurn();
        GameSaves.Autosave(_folder, initial, session);
        var path = GameSaves.PathFor(_folder, GameSaves.AutosaveCurrentName);

        var bytes = File.ReadAllBytes(path);

        Assert.Equal(Encoding.UTF8.GetBytes(GameRecordSerializer.Serialize(GameRecordSerializer.Load(path))), bytes);
        Assert.Empty(Directory.GetFiles(_folder, "*.tmp"));
    }

    [Fact]
    public void ALargeSave_IsWrittenAndListedWhole()
    {
        var record = LargeRecord(100_000);
        var path = GameSaves.SaveRecord(_folder, "large", record);

        Assert.True(new FileInfo(path).Length > 2_000_000);
        Assert.Equal(Encoding.UTF8.GetBytes(GameRecordSerializer.Serialize(record)), File.ReadAllBytes(path));
        Assert.Equal(100_000, GameRecordSerializer.Load(path).Commands.Count);
        Assert.Equal(record.Save, Assert.Single(GameSaves.List(_folder)).Info);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(100)]
    [InlineData(4096)]
    public void ReadLabel_CutByAnyBufferSize_GivesTheRecordsLabel(int bufferSize)
    {
        var record = LargeRecord(300) with { Generated = GenerationInfo.Of("Duel", 72, 9, 0) };
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(GameRecordSerializer.Serialize(record)));

        var label = GameRecordLabel.Read(stream, bufferSize);

        Assert.Equal(record.Save, label.Save);
        Assert.Equal(record.Map.Name, label.MapName);
    }

    [Fact]
    public void List_ASaveWhoseCommandsAreDamaged_StillListsFromItsLabel()
    {
        var path = GameSaves.SaveRecord(_folder, "damaged", SavedRecord("damaged"));
        File.WriteAllText(path, File.ReadAllText(path).Replace("\"type\": \"step\"", "\"type\": \"no-such-command\"", StringComparison.Ordinal));
        Assert.ThrowsAny<Exception>(() => GameRecordSerializer.Load(path));

        var entry = Assert.Single(GameSaves.List(_folder));

        Assert.Null(entry.Error);
        Assert.Equal("damaged", entry.Info!.Name);
        Assert.True(SaveSlotText.Lines([entry])[0].Loadable);
    }

    [Fact]
    public void List_ASaveCutOffAfterItsLabel_StillLists()
    {
        var record = SavedRecord("cut") with { Generated = GenerationInfo.Of("Duel", 72, 9, 0) };
        var text = GameRecordSerializer.Serialize(record);
        var path = GameSaves.PathFor(_folder, "cut");
        File.WriteAllText(path, text[..(text.IndexOf("\"generated\"", StringComparison.Ordinal) + 5)]);

        Assert.Equal(record.Save, Assert.Single(GameSaves.List(_folder)).Info);
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{ \"map\": { \"name\": \"x\" }, \"commands\": [ { ")]
    [InlineData("{ \"map\": { \"name\": \"x\" }, \"commands\": [] ] }")]
    public void List_AFileWhoseLabelCannotBeRead_IsListedWithItsError(string text)
    {
        File.WriteAllText(GameSaves.PathFor(_folder, "broken"), text);

        var entry = Assert.Single(GameSaves.List(_folder));

        Assert.Null(entry.Info);
        Assert.False(string.IsNullOrEmpty(entry.Error));
        Assert.Contains("unreadable", SaveSlotText.Lines([entry])[0].Label, StringComparison.Ordinal);
    }

    [Fact]
    public void List_AnOldSaveWithoutALabel_ShowsItsFileNameAndMap()
    {
        // A version 1 record: no save label, a version 1 map, a byte order mark.
        var map = File.ReadAllText(Path.Combine(Pinned.Folder(), "FormatSamples", "v01.ewmap.json"));
        var path = GameSaves.PathFor(_folder, "old");
        File.WriteAllText(path, $"{{ \"formatVersion\": 1, \"map\": {map}, \"options\": {{ \"seed\": 3 }}, \"commands\": [] }}", new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        var info = Assert.Single(GameSaves.List(_folder)).Info!;

        Assert.Equal("old", info.Name);
        Assert.Equal("Version 1 sample", info.Map);
        Assert.Equal(GameRecordSerializer.Load(path).Map.Name, info.Map);
    }

    [Fact]
    public void ListReplays_NamesAReplayByItsFileAndMap()
    {
        var (session, initial) = NewGame();
        GameSaves.SaveReplay(_folder, "final", initial, session);

        var info = Assert.Single(GameSaves.ListReplays(_folder)).Info!;

        Assert.Equal("final", info.Name);
        Assert.Equal(initial.Name, info.Map);
        Assert.Equal("", info.Date);
    }
}
