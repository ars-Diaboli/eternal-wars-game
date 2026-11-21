using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Templates;
using EternalWars.Core.Generation;

namespace EternalWars.Core.Tests.Data;

/// <summary>The template editor's autosaves and session marker (ADR-250): like the map editor's, in a folder of their own.</summary>
public sealed class TemplateAutosavesTests : IDisposable
{
    private static readonly DateTime Start = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "ew-template-autosave-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private static bool NothingRuns(int process) => false;

    [Fact]
    public void Save_WritesTemplateFiles_AndKeepsOnlyTheNewest()
    {
        var autosaves = new TemplateAutosaves(_folder, keep: 2);
        var template = TemplateEditorSession.Blank() with { Name = "Autosaved" };
        for (var i = 0; i < 4; i++)
        {
            autosaves.Save(template, Start.AddMinutes(i));
        }

        var files = autosaves.Files();
        Assert.Equal([Start.AddMinutes(3), Start.AddMinutes(2)], files.Select(TemplateAutosaves.TimeOf).Cast<DateTime>());
        Assert.All(files, f => Assert.EndsWith(TemplateSerializer.FileExtension, f));
        Assert.Equal("Autosaved", TemplateSerializer.Load(files[0]).Name);
        Assert.Null(MapAutosaves.TimeOf(files[0])); // a template autosave is not a map's
    }

    [Fact]
    public void ASessionThatNeverEnded_OffersItsNewestAutosave_AndACleanEndOffersNothing()
    {
        var crashed = new TemplateAutosaves(_folder);
        Assert.Null(crashed.BeginSession(1, NothingRuns, Start));
        crashed.Save(TemplateEditorSession.Blank(), Start.AddMinutes(2));
        var newest = crashed.Save(TemplateEditorSession.Blank(), Start.AddMinutes(4));

        var next = new TemplateAutosaves(_folder);
        Assert.Equal(newest, next.BeginSession(2, NothingRuns, Start.AddHours(1)));
        next.EndSession();
        Assert.Null(new TemplateAutosaves(_folder).BeginSession(3, NothingRuns, Start.AddHours(2)));
    }
}
