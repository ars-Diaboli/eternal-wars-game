using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Data.Templates;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Generation;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Data;

/// <summary>Saves, replays, the ranking and template folders in words (ADR-058, ADR-101, ADR-117).</summary>
public sealed class SaveSlotTextTests : IDisposable
{
    private const string Folder = "saves";
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    private readonly string _templates = Path.Combine(Path.GetTempPath(), "ew-templates-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_templates))
        {
            Directory.Delete(_templates, recursive: true);
        }
    }

    private static SaveInfo Info(string name, string date = "Month 1, week 1, day 2", bool inBattle = false) =>
        new(name, "2026-10-29T12:30:00+00:00", date, "Blue", inBattle, "Demo Valley");

    [Fact]
    public void SaveLines_NameTheSlot_TheMapAndTheTurn_AndOnlyOrdinarySavesCanBeSavedOver()
    {
        var autosave = GameSaves.PathFor(Folder, GameSaves.AutosaveCurrentName);
        var previous = GameSaves.PathFor(Folder, GameSaves.AutosavePreviousName);
        var mine = GameSaves.PathFor(Folder, "mine");
        var broken = GameSaves.PathFor(Folder, "broken");
        var lines = SaveSlotText.Lines(
            [new SaveEntry(autosave, Info("x"), null), new SaveEntry(previous, Info("y"), null), new SaveEntry(mine, Info("mine", inBattle: true), null),
             new SaveEntry(broken, null, "bad json")],
            Utc);

        Assert.Equal(
            new SaveSlotLine(autosave, "Autosave: start of the latest turn   Demo Valley, Month 1, week 1, day 2, Blue's turn   (2026-10-29 12:30)", null, true),
            lines[0]);
        Assert.StartsWith("Autosave: start of the turn before", lines[1].Label);
        Assert.Equal(
            new SaveSlotLine(mine, "mine   Demo Valley, Month 1, week 1, day 2, Blue's turn, in battle   (2026-10-29 12:30)", "mine", true), lines[2]);
        Assert.Equal(new SaveSlotLine(broken, $"{Path.GetFileName(broken)}   unreadable: bad json", null, false), lines[3]);
    }

    [Fact]
    public void AReplayLine_HasNoTurn_AndContinueNamesTheLatestSave()
    {
        var replay = Path.Combine(Folder, "game" + GameRecordSerializer.FileExtension);
        Assert.Equal("game   Demo Valley   (2026-10-29 12:30)", SaveSlotText.Lines([new SaveEntry(replay, Info("game", date: ""), null)], Utc)[0].Label);

        var autosave = GameSaves.PathFor(Folder, GameSaves.AutosaveCurrentName);
        Assert.Equal(
            "Autosave: start of the latest turn, Demo Valley, Month 1, week 1, day 2 (2026-10-29 12:30)",
            SaveSlotText.Continue(new SaveEntry(autosave, Info("x"), null), Utc));
        Assert.Null(SaveSlotText.Continue(null));
        Assert.Null(SaveSlotText.Continue(new SaveEntry(autosave, null, "bad")));
    }

    [Fact]
    public void Times_AreShownInTheZone_AndUnreadableOnesAsTheyAre()
    {
        Assert.Equal("2026-10-29 14:30", SaveSlotText.LocalTime("2026-10-29T12:30:00+00:00", TimeZoneInfo.CreateCustomTimeZone("plus2", TimeSpan.FromHours(2), "plus2", "plus2")));
        Assert.Equal("not a time", SaveSlotText.LocalTime("not a time"));
    }

    [Fact]
    public void Loaded_NamesTheSave_AndARandomMapsSeed()
    {
        var state = Create("A.....", ".....B");
        var initial = MapConverter.ToDocument(state);
        var session = Start(state);
        var record = GameReplay.Record(initial, session, saveName: "evening");

        Assert.Equal("Loaded 'evening'.", SaveSlotText.Loaded("x/evening.ewsave.json", record));
        Assert.Equal("Loaded 'file'.", SaveSlotText.Loaded("x/file.json", record with { Save = null }));
        Assert.Equal(
            "Loaded 'evening'. Ring 36×36, seed 7. ",
            SaveSlotText.Loaded("x", record with { Generated = GenerationInfo.Of("Ring", 36, 7, 1) }));
    }

    [Fact]
    public void RankingRows_AreRankedFromOne_WithTheFactionAndMapInWords()
    {
        var hand = new RankingEntry("Ann", TownIds.Haven, "Two Towns", null, null, true, Complexity.Hard, null, 30, 250, "Imp", "2026-10-29T12:30:00+00:00");
        var random = hand with { Template = "Ring", Size = 36, Custom = false, Faction = "no-such-town" };
        var rows = RankingText.Rows([hand, random], new HashSet<int> { 1 }, Utc);

        Assert.Equal("Maps", RankingText.TableName(RankingTable.Maps));
        Assert.Equal("Random maps", RankingText.TableName(RankingTable.RandomMaps));
        Assert.Equal(
            new RankingRowText(1, "Ann", RankingText.FactionName(TownIds.Haven), "Two Towns (custom)", "Hard", 30, 250, "Imp", "2026-10-29 12:30", false),
            rows[0]);
        Assert.NotEqual(TownIds.Haven, rows[0].Faction); // the town's name, not its id
        Assert.Equal((2, "no-such-town", "Ring 36×36", true), (rows[1].Rank, rows[1].Faction, rows[1].Map, rows[1].Highlight));
        Assert.Equal("Demo", RankingText.MapLabel(hand with { Map = "Demo", Custom = false }));
    }

    [Fact]
    public void ATemplateFolder_GivesItsValidTemplates_AndWarnsAboutTheRest()
    {
        var warnings = new List<string>();
        Assert.Empty(TemplateFolder.LoadValid(_templates, warnings.Add)); // no folder

        Directory.CreateDirectory(_templates);
        var valid = BuiltInTemplates.All.First(t => TemplateValidator.IsValid(t));
        TemplateSerializer.Save(valid, Path.Combine(_templates, "b" + TemplateSerializer.FileExtension));
        File.WriteAllText(Path.Combine(_templates, "a" + TemplateSerializer.FileExtension), "{ not json");
        File.WriteAllText(Path.Combine(_templates, "other.txt"), "ignored");

        var found = TemplateFolder.LoadValid(_templates, warnings.Add);

        Assert.Equal([Path.Combine(_templates, "b" + TemplateSerializer.FileExtension)], found.Select(f => f.Path));
        Assert.Equal(valid.Name, found[0].Template.Name);
        Assert.Single(warnings);
        Assert.StartsWith($"Skipping template {Path.Combine(_templates, "a" + TemplateSerializer.FileExtension)}: ", warnings[0]);
    }
}
