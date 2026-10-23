using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Towns;
using Godot;

namespace EternalWars.Godot.Shared;

/// <summary>
/// The content tables the apps use: the copies embedded in core (<c>src/Core/data</c>, ADR-071), or a designer's edited file
/// given with <c>--creatures=</c>, <c>--heroes=</c>, <c>--buildings=</c>, <c>--artifacts=</c> or <c>--artifact-sets=</c>, so an
/// edit needs only a restart. A missing or broken file falls back to the embedded copy and reports the error to the Godot output.
/// </summary>
public static class GameContent
{
    private static ArtifactCatalog? _artifacts;
    private static CreatureCatalog? _creatures;
    private static HeroCatalog? _heroes;
    private static BuildingCatalog? _buildings;

    public static CreatureCatalog Creatures => _creatures ??= Load(LaunchArguments.CreaturesPath, "Creature",
        path => new CreatureCatalog(CreatureTable.Parse(ReadText(path))), CreatureCatalog.Default, c => c.All.Count());

    /// <summary>Heroes, checked against <see cref="Creatures"/> (preferred creatures).</summary>
    public static HeroCatalog Heroes => _heroes ??= Load(LaunchArguments.HeroesPath, "Hero", path =>
    {
        var catalog = new HeroCatalog(HeroTable.Parse(ReadText(path)));
        foreach (var problem in catalog.Check(Creatures))
        {
            GD.PushWarning($"Hero table: {problem}");
        }

        return catalog;
    }, HeroCatalog.Default, c => c.All.Count());

    public static BuildingCatalog Buildings => _buildings ??= Load(LaunchArguments.BuildingsPath, "Building",
        path => new BuildingCatalog(BuildingTable.Parse(ReadText(path))), BuildingCatalog.Default, c => c.Buildings.Count());

    /// <summary>Artifacts and their sets; either flag replaces its table, the other stays embedded.</summary>
    public static ArtifactCatalog Artifacts => _artifacts ??= LoadArtifacts();

    private static ArtifactCatalog LoadArtifacts()
    {
        var artifactsPath = LaunchArguments.ArtifactsPath;
        var setsPath = LaunchArguments.ArtifactSetsPath;
        if (artifactsPath is null && setsPath is null)
        {
            return ArtifactCatalog.Default;
        }

        return Load($"{artifactsPath} {setsPath}".Trim(), "Artifact", _ => new ArtifactCatalog(
                ArtifactTable.ParseArtifacts(artifactsPath is null ? BuiltInArtifacts.EmbeddedTable(BuiltInArtifacts.ArtifactsResource) : ReadText(artifactsPath)),
                ArtifactTable.ParseSets(setsPath is null ? BuiltInArtifacts.EmbeddedTable(BuiltInArtifacts.SetsResource) : ReadText(setsPath))),
            ArtifactCatalog.Default, c => c.All.Count());
    }

    private static string ReadText(string path) =>
        path.StartsWith("res://", StringComparison.Ordinal) || path.StartsWith("user://", StringComparison.Ordinal)
            ? global::Godot.FileAccess.FileExists(path) ? global::Godot.FileAccess.GetFileAsString(path) : throw new IOException("file not found")
            : File.ReadAllText(path);

    /// <summary>The embedded <paramref name="fallback"/> without a flag; otherwise the parsed file, or the fallback if it is broken.</summary>
    private static T Load<T>(string? path, string what, Func<string, T> parse, T fallback, Func<T, int> count)
    {
        if (path is null)
        {
            return fallback;
        }

        try
        {
            var loaded = parse(path);
            GD.Print($"{what}s: {count(loaded)} from {path}");
            return loaded;
        }
        catch (Exception e) when (e is FormatException or IOException or ArgumentException or UnauthorizedAccessException)
        {
            GD.PushError($"{what} table {path}: {e.Message} (using the built-in table instead)");
            return fallback;
        }
    }
}
