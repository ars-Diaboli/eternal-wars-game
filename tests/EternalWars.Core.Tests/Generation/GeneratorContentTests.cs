using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Data.Templates;
using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Generation;

namespace EternalWars.Core.Tests.Generation;

/// <summary>The generator, map loading and saves take every content table from the <see cref="ContentCatalogs"/> they are given.</summary>
public sealed class GeneratorContentTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "ew-content-" + Guid.NewGuid().ToString("N"));

    private static MapTemplate Duel() => BuiltInTemplates.Find("duel")!;

    private static PlayerSetup[] Players() =>
    [
        new(0, "Red", IsHuman: true, TownIds.Haven),
        new(1, "Blue", IsHuman: false, TownIds.Inferno),
    ];

    private static GameState Build(ContentCatalogs? content) => MapGenerator.Build(Duel(), 72, 5, Players(), new GameOptions(Seed: 5), content).State;

    /// <summary>Copies of the built-in tables: the same content in other instances.</summary>
    private static ContentCatalogs Copies() => new(
        new CreatureCatalog(CreatureCatalog.Default.All),
        new HeroCatalog(HeroCatalog.Default.All),
        new BuildingCatalog(BuildingTable.Parse(BuiltInBuildings.EmbeddedTable())),
        new ArtifactCatalog(ArtifactCatalog.Default.All, ArtifactCatalog.Default.Sets),
        MapObjectCatalog.Default.With([]),
        TerrainCatalog.Default.With([]));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void LeftOut_TheContentIsTheBuiltInTables()
    {
        var content = new ContentCatalogs();

        Assert.Same(CreatureCatalog.Default, content.Creatures);
        Assert.Same(HeroCatalog.Default, content.Heroes);
        Assert.Same(BuildingCatalog.Default, content.Buildings);
        Assert.Same(ArtifactCatalog.Default, content.Artifacts);
        Assert.Same(MapObjectCatalog.Default, content.Objects);
        Assert.Same(TerrainCatalog.Default, content.Terrains);
    }

    [Fact]
    public void AGeneratedGame_HasTheTablesItWasGeneratedFrom_AndTheSameMapAsWithTheBuiltInOnes()
    {
        var content = Copies();
        var state = Build(content);

        Assert.Same(content.Creatures, state.Creatures);
        Assert.Same(content.Heroes, state.HeroRoster);
        Assert.Same(content.Buildings, state.Buildings);
        Assert.Same(content.Artifacts, state.Artifacts);
        Assert.Same(content.Objects, state.ObjectCatalog);
        Assert.Same(content.Terrains, state.Terrains);
        Assert.Equal(GameStateDigest.Compute(Build(null)), GameStateDigest.Compute(state));
        Assert.Equal(GameStateDigest.Compute(Build(null)), GameStateDigest.Compute(Build(ContentCatalogs.Default)));
    }

    [Fact]
    public void EveryObjectTheGeneratorPlaces_ComesFromTheGivenObjectTable()
    {
        var objects = MapObjectCatalog.Default.With(MapObjectCatalog.Default.All.Select(d => d with { Name = d.Name + " (mod)" }).ToList());
        var state = Build(new ContentCatalogs(objects: objects));
        var plain = Build(null);

        Assert.NotEmpty(state.Objects);
        Assert.All(state.Objects, o => Assert.Same(objects.Get(o.Definition.Id), o.Definition));
        Assert.Equal(plain.Objects.Select(o => (o.Id, o.Definition.Id, o.Position)), state.Objects.Select(o => (o.Id, o.Definition.Id, o.Position)));
    }

    [Fact]
    public void AStartZone_IsPaintedWithTheNativeTerrainOfTheGivenTerrainTable()
    {
        // Haven is native to lava only: its start zone is painted lava.
        var terrains = TerrainCatalog.Default.With(TerrainCatalog.Default.All
            .Where(d => d.NativeTowns.Contains(TownIds.Haven) || d.Terrain == TerrainType.Lava)
            .Select(d => d with { NativeTowns = d.Terrain == TerrainType.Lava ? [TownIds.Inferno, TownIds.Haven] : d.NativeTowns.Where(t => t != TownIds.Haven).ToList() })
            .ToList());
        Assert.Equal([TerrainType.Lava], TerrainPainter.NativeTerrains(TownIds.Haven, terrains));

        var state = Build(new ContentCatalogs(terrains: terrains));
        var plain = Build(null);

        Assert.All(HavenTown(state).OccupiedCells(), c => Assert.Equal(TerrainType.Lava, state.Map.GetTerrain(c.Position)));
        Assert.All(HavenTown(plain).OccupiedCells(), c => Assert.Contains(plain.Map.GetTerrain(c.Position), TerrainPainter.NativeTerrains(TownIds.Haven)));
    }

    [Fact]
    public void TheEditorGenerator_UsesTheGivenTables()
    {
        var content = Copies();
        var map = EditorMapGenerator.Generate(Duel(), 72, 5, content: content);

        Assert.Same(content.Objects, map.Scenario.ObjectCatalog);
        Assert.Same(content.Creatures, map.Scenario.Creatures);
    }

    [Fact]
    public void ALoadedMap_AndALoadedSave_HaveTheGivenTables()
    {
        var content = Copies();
        var document = MapConverter.ToDocument(Build(null));
        var state = MapConverter.ToGameState(document, content, new GameOptions(Seed: 5));

        Assert.Same(content.Objects, state.ObjectCatalog);
        Assert.Same(content.Terrains, state.Terrains);
        Assert.Same(content.Heroes, state.HeroRoster);
        Assert.All(state.Objects, o => Assert.Same(content.Objects.Get(o.Definition.Id), o.Definition));

        var session = new GameSession(MapConverter.ToGameState(document, options: new GameOptions(Seed: 5)));
        session.Start();
        var path = GameSaves.Save(_folder, "content", document, session);
        var (loaded, _) = GameSaves.Load(path, content);

        Assert.Same(content.Creatures, loaded.State.Creatures);
        Assert.Same(content.Objects, loaded.State.ObjectCatalog);
        Assert.Equal(GameStateDigest.Compute(session.State), GameStateDigest.Compute(loaded.State));
    }

    private static Town HavenTown(GameState state) => state.Objects.OfType<Town>().First(t => t.Owner == new PlayerId(0));
}
