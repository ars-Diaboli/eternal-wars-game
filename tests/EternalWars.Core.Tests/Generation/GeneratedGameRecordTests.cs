using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Data.Templates;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Generation;

namespace EternalWars.Core.Tests.Generation;

/// <summary>A game on a generated map is saved as an ordinary map plus how it was generated (RMG-5: the generator version).</summary>
public class GeneratedGameRecordTests
{
    [Fact]
    public void AGeneratedGame_SavesAsAMap_WithItsGeneratorVersion_AndReplays()
    {
        var template = BuiltInTemplates.Find("duel")!;
        var (state, generation) = MapGenerator.Build(template, 72, 6, [new PlayerSetup(0, "Red", true, TownIds.Haven), new PlayerSetup(1, "Blue", false, TownIds.Inferno)],
            new GameOptions(Seed: 6));
        var initial = MapConverter.ToDocument(state);
        var session = new GameSession(state);
        session.Start();
        session.Execute(new EndTurnCommand(state.CurrentPlayer.Id)).ThrowIfRejected();

        var record = GameReplay.Record(initial, session, generated: GenerationInfo.Of(template.Name, 72, 6, generation.Attempt));
        var loaded = GameRecordSerializer.Deserialize(GameRecordSerializer.Serialize(record));

        Assert.Equal(GameRecordDocument.CurrentFormatVersion, loaded.FormatVersion);
        Assert.Equal(new GenerationInfo(template.Name, 72, 6, generation.Attempt, MapGenerator.Version), loaded.Generated);
        Assert.Null(loaded.Generated!.VersionWarning);
        Assert.NotNull((loaded.Generated with { GeneratorVersion = MapGenerator.Version + 1 }).VersionWarning);
        Assert.Equal(state.Map.Name, loaded.Map.Name);
        GameReplay.Replay(loaded); // the saved map rebuilds the same game
    }
}
