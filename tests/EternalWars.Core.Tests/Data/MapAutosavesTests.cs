using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Scenarios;

namespace EternalWars.Core.Tests.Data;

/// <summary>The editor's autosaves, its session marker and its recent files (EDIT-1, ADR-250).</summary>
public sealed class MapAutosavesTests : IDisposable
{
    private static readonly DateTime Start = new(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);
    private static readonly MapDocument Map = MapConverter.ToDocument(BlankScenario.Create(8, 8));

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "ew-autosave-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private MapAutosaves Autosaves(int keep = 5) => new(_folder, keep);

    private static bool NothingRuns(int process) => false;

    [Fact]
    public void Save_KeepsOnlyTheNewest()
    {
        var autosaves = Autosaves(keep: 3);
        for (var i = 0; i < 5; i++)
        {
            autosaves.Save(Map, Start.AddMinutes(2 * i));
        }

        var files = autosaves.Files();
        Assert.Equal(3, files.Count);
        Assert.Equal([Start.AddMinutes(8), Start.AddMinutes(6), Start.AddMinutes(4)], files.Select(MapAutosaves.TimeOf).Cast<DateTime>());
        Assert.Equal(MapSerializer.Serialize(Map), MapSerializer.Serialize(MapSerializer.Load(files[0])));
    }

    [Fact]
    public void BeginSession_AfterACleanEnd_OffersNothing()
    {
        var first = Autosaves();
        Assert.Null(first.BeginSession(1, NothingRuns, Start));
        first.Save(Map, Start.AddMinutes(2));
        first.EndSession();

        Assert.False(File.Exists(Path.Combine(_folder, MapAutosaves.MarkerName)));
        Assert.Null(Autosaves().BeginSession(2, NothingRuns, Start.AddHours(1)));
    }

    [Fact]
    public void BeginSession_AfterASessionThatNeverEnded_OffersItsNewestAutosave()
    {
        var crashed = Autosaves();
        crashed.BeginSession(1, NothingRuns, Start);
        crashed.Save(Map, Start.AddMinutes(2));
        var newest = crashed.Save(Map, Start.AddMinutes(4));

        var next = Autosaves();
        Assert.Equal(newest, next.BeginSession(2, NothingRuns, Start.AddHours(1)));

        // The offer is made once: the new session's marker is safe from its start.
        Assert.Null(Autosaves().BeginSession(3, NothingRuns, Start.AddHours(2)));
    }

    [Fact]
    public void BeginSession_OffersNothingOlderThanTheLastSafeTime()
    {
        var crashed = Autosaves();
        crashed.BeginSession(1, NothingRuns, Start);
        crashed.Save(Map, Start.AddMinutes(2));
        crashed.MarkSafe(Start.AddMinutes(3)); // the map was saved, then the editor died

        Assert.Null(Autosaves().BeginSession(2, NothingRuns, Start.AddHours(1)));
    }

    [Fact]
    public void BeginSession_WhileTheMarkersEditorRuns_OffersNothingAndLeavesItsMarker()
    {
        var running = Autosaves();
        running.BeginSession(1, NothingRuns, Start);
        running.Save(Map, Start.AddMinutes(2));
        var marker = File.ReadAllText(Path.Combine(_folder, MapAutosaves.MarkerName));

        var second = Autosaves();
        Assert.Null(second.BeginSession(2, process => process == 1, Start.AddMinutes(5)));
        second.MarkSafe(Start.AddMinutes(6));
        second.EndSession();

        Assert.Equal(marker, File.ReadAllText(Path.Combine(_folder, MapAutosaves.MarkerName)));
    }

    [Fact]
    public void BeginSession_WithAMarkerThatCannotBeRead_OffersTheNewestAutosave()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, MapAutosaves.MarkerName), "planted");
        File.WriteAllText(Path.Combine(_folder, "notes.txt"), "not an autosave");
        var autosave = Autosaves().Save(Map, Start);

        Assert.Equal(autosave, Autosaves().BeginSession(2, NothingRuns, Start.AddHours(1)));
    }

    [Fact]
    public void RecentFiles_PutTheNewestFirst_EachOnce_AtMostTen()
    {
        IReadOnlyList<string> recent = [];
        for (var i = 1; i <= 12; i++)
        {
            recent = RecentFiles.With(recent, $"map{i}");
        }

        Assert.Equal(RecentFiles.Capacity, recent.Count);
        Assert.Equal("map12", recent[0]);
        Assert.Equal("map3", recent[^1]);

        recent = RecentFiles.With(recent, "MAP5");
        Assert.Equal(RecentFiles.Capacity, recent.Count);
        Assert.Equal(["MAP5", "map12", "map11"], recent.Take(3));
        Assert.DoesNotContain("map5", recent);

        Assert.DoesNotContain("map12", RecentFiles.Without(recent, "Map12"));
    }
}
