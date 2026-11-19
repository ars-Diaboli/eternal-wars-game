using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.Common;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Data;

/// <summary>
/// The main menu's Continue (ADR-232): the newest save that loads. A save whose label reads but whose commands no longer load is passed over
/// and named, since the list reads labels alone (ADR-224).
/// </summary>
public sealed class ContinueSaveTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "ew-continue-" + Guid.NewGuid().ToString("N"));

    public ContinueSaveTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    /// <summary>A save named <paramref name="name"/> written <paramref name="minutesAgo"/> minutes ago; damaged ones' commands do not load.</summary>
    private string Save(string name, int minutesAgo, bool damaged = false)
    {
        var state = Create("A.B", "...");
        var initial = MapConverter.ToDocument(state);
        var session = Start(state);
        session.Step(A, Direction.East);
        var path = GameSaves.Save(_folder, name, initial, session);
        if (damaged)
        {
            File.WriteAllText(path, File.ReadAllText(path).Replace("\"type\": \"step\"", "\"type\": \"no-such-command\"", StringComparison.Ordinal));
        }

        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(-minutesAgo));
        return path;
    }

    [Fact]
    public void Continue_LoadsTheNewestSave()
    {
        Save("older", 10);
        var newest = Save("newest", 1);

        var loaded = GameSaves.LoadNewest(GameSaves.List(_folder), null, out var skipped);

        Assert.Equal(newest, loaded!.Path);
        Assert.Empty(skipped);
        Assert.Equal("", SaveSlotText.Skipped(skipped));
    }

    [Fact]
    public void Continue_PassesOverANewerSaveThatNoLongerLoads_AndNamesIt()
    {
        var older = Save("older", 10);
        var broken = Save("broken", 1, damaged: true);
        File.WriteAllText(Path.Combine(_folder, "garbage" + GameRecordSerializer.SaveExtension), "[]"); // no label: never offered
        File.SetLastWriteTimeUtc(Path.Combine(_folder, "garbage" + GameRecordSerializer.SaveExtension), DateTime.UtcNow);

        var loaded = GameSaves.LoadNewest(GameSaves.List(_folder), null, out var skipped);

        Assert.Equal(older, loaded!.Path);
        Assert.Equal(broken, Assert.Single(skipped).Entry.Path);
        Assert.StartsWith("'broken' did not load: ", SaveSlotText.Skipped(skipped), StringComparison.Ordinal);
    }

    [Fact]
    public void Continue_WhenNoSaveLoads_IsNull_WithEveryOneTried()
    {
        Save("one", 10, damaged: true);
        Save("two", 1, damaged: true);

        Assert.Null(GameSaves.LoadNewest(GameSaves.List(_folder), null, out var skipped));
        Assert.Equal(["two", "one"], skipped.Select(s => s.Entry.Info!.Name));
    }

    [Fact]
    public void TryLoad_AMissingFile_SaysWhy()
    {
        Assert.Null(GameSaves.TryLoad(Path.Combine(_folder, "gone" + GameRecordSerializer.SaveExtension), null, out var error));
        Assert.NotEqual("", error);
    }
}
