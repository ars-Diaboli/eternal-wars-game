using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Towns;
using Godot;

namespace EternalWars.Godot.Shared;

/// <summary>
/// Content the apps read at start-up rather than from the build, so designers can edit it and just restart (ADR-044).
/// </summary>
public static class GameContent
{
    /// <summary>The shipped creature table; Godot keeps it as a plain file (<c>creatures.csv.import</c>: importer "keep").</summary>
    public const string CreatureTablePath = "res://shared/data/creatures.csv";

    /// <summary>The shipped hero roster (ADR-048), kept as a plain file like the creature table.</summary>
    public const string HeroTablePath = "res://shared/data/heroes.csv";

    /// <summary>The shipped town buildings (ADR-046), kept as a plain file like the creature table.</summary>
    public const string BuildingTablePath = "res://shared/data/buildings.csv";

    private static CreatureCatalog? _creatures;
    private static HeroCatalog? _heroes;
    private static BuildingCatalog? _buildings;

    /// <summary>
    /// Creatures from <c>--creatures=path.csv</c>, else <see cref="CreatureTablePath"/>. A missing or broken table falls
    /// back to the copy embedded at build time and reports the error to the Godot output.
    /// </summary>
    public static CreatureCatalog Creatures => _creatures ??= LoadCreatures();

    /// <summary>
    /// Heroes from <c>--heroes=path.csv</c>, else <see cref="HeroTablePath"/>; checked against <see cref="Creatures"/>
    /// (preferred creatures). A missing or broken table falls back to the embedded copy and reports the error.
    /// </summary>
    public static HeroCatalog Heroes => _heroes ??= Load(LaunchArguments.HeroesPath ?? HeroTablePath, "Hero", text =>
    {
        var catalog = new HeroCatalog(HeroTable.Parse(text));
        foreach (var problem in catalog.Check(Creatures))
        {
            GD.PushWarning($"Hero table: {problem}");
        }

        return catalog;
    }, HeroCatalog.Default, c => c.All.Count());

    /// <summary>
    /// Town buildings from <c>--buildings=path.csv</c>, else <see cref="BuildingTablePath"/>. A missing or broken table falls
    /// back to the embedded copy and reports the error.
    /// </summary>
    public static BuildingCatalog Buildings => _buildings ??= Load(LaunchArguments.BuildingsPath ?? BuildingTablePath, "Building",
        text => new BuildingCatalog(BuildingTable.Parse(text)), BuildingCatalog.Default, c => c.Buildings.Count());

    private static CreatureCatalog LoadCreatures() =>
        Load(LaunchArguments.CreaturesPath ?? CreatureTablePath, "Creature", text => new CreatureCatalog(CreatureTable.Parse(text)),
            CreatureCatalog.Default, c => c.All.Count());

    private static T Load<T>(string path, string what, Func<string, T> parse, T fallback, Func<T, int> count)
    {
        try
        {
            var text = path.StartsWith("res://", StringComparison.Ordinal) || path.StartsWith("user://", StringComparison.Ordinal)
                ? global::Godot.FileAccess.FileExists(path) ? global::Godot.FileAccess.GetFileAsString(path) : throw new IOException("file not found")
                : File.ReadAllText(path);
            var loaded = parse(text);
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
