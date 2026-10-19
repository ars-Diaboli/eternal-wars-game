using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Generation;

/// <summary>
/// A map generated for the editor: the scenario to open in a new <see cref="MapEditorSession"/>, how it was generated, and what to show the designer
/// (the generator's problems if every attempt had some, and the map validator's findings).
/// </summary>
public sealed record EditorMap(GameState Scenario, GenerationResult Generation, IReadOnlyList<MapIssue> Issues)
{
    public IReadOnlyList<string> Problems => Generation.Problems;
}

/// <summary>
/// The editor's "Generate from template" (ADR-064, RMG-7): generates a map and assembles it like the new-game screen would, as an ordinary,
/// editable scenario. Like "New" it starts a new map (and a new undo history) rather than being an edit of the open one.
/// <list type="bullet">
/// <item>Players: one per start slot, the first <c>players</c> slots (default: all); the other slots keep their towns, neutral. Players are named by
/// colour (<see cref="PlayerNames"/>); a player-start slot is human up to the template's <see cref="MapTemplate.MaxHumans"/>, an AI-only slot is a
/// computer player. Teams come from the start zones.</item>
/// <item>Towns: the designer's pick per slot (<c>towns</c>, slot → town type), else drawn from the seed; either way the start zone is painted native.
/// A town changed in the editor afterwards keeps the terrain it was generated with.</item>
/// <item>Start towns, start heroes and their armies, garrisons and rules are the game's (<see cref="ScenarioAssembler"/>), so the generated map is
/// playable as it is; the designer can change any of it.</item>
/// <item>The map's description records the template, size, seed, attempt and generator version, so it can be generated again.</item>
/// </list>
/// </summary>
public static class EditorMapGenerator
{
    /// <summary>Player names by slot, in the order of the editor's player colours.</summary>
    public static IReadOnlyList<string> PlayerNames { get; } = ["Blue", "Red", "Yellow", "Green", "Orange", "Purple", "Teal", "Pink"];

    /// <summary>The players the editor generates for: <paramref name="players"/> start slots from slot 0 (null = every slot of the template).</summary>
    public static IReadOnlyList<PlayerSetup> Players(MapTemplate template, int? players = null, IReadOnlyDictionary<int, string>? towns = null)
    {
        ArgumentNullException.ThrowIfNull(template);
        var count = players ?? template.MaxPlayers;
        if (count < 1 || count > template.MaxPlayers)
        {
            throw new ArgumentOutOfRangeException(nameof(players), count, $"{template.Name} has {template.MaxPlayers} start slots.");
        }

        foreach (var slot in towns?.Keys.Where(s => s < 0 || s >= count) ?? [])
        {
            throw new ArgumentException($"Start slot {slot} has no player.", nameof(towns));
        }

        var humans = template.MaxHumans ?? int.MaxValue;
        var setups = new List<PlayerSetup>();
        for (var slot = 0; slot < count; slot++)
        {
            var zone = template.Zones.FirstOrDefault(z => z.Owner == slot && z.Type is ZoneType.PlayerStart or ZoneType.AiStart);
            var human = zone?.Type == ZoneType.PlayerStart && humans-- > 0;
            setups.Add(new PlayerSetup(slot, slot < PlayerNames.Count ? PlayerNames[slot] : $"Player {slot + 1}", human,
                towns?.GetValueOrDefault(slot) ?? PlayerSetup.RandomTown));
        }

        return setups;
    }

    /// <summary>
    /// Generates the map. Refuses (with <see cref="ArgumentException"/>) a template with errors, a size the template does not support, and the
    /// player and town choices <see cref="Players"/> or <see cref="ScenarioAssembler"/> refuse. <paramref name="maxHeroes"/> overrides the template's
    /// hero limit (ADR-068).
    /// </summary>
    public static EditorMap Generate(
        MapTemplate template,
        int size,
        ulong seed,
        int? players = null,
        IReadOnlyDictionary<int, string>? towns = null,
        CreatureCatalog? creatures = null,
        HeroCatalog? heroRoster = null,
        BuildingCatalog? buildings = null,
        ArtifactCatalog? artifacts = null,
        int? maxHeroes = null)
    {
        ArgumentNullException.ThrowIfNull(template);
        if (TemplateValidator.Validate(template).FirstOrDefault(i => i.Severity == IssueSeverity.Error) is { } error)
        {
            throw new ArgumentException($"The template has errors: {error.Message}", nameof(template));
        }

        if (!template.Sizes.Contains(size))
        {
            throw new ArgumentOutOfRangeException(nameof(size), size, $"{template.Name} supports the sizes {string.Join(", ", template.Sizes)}.");
        }

        var (scenario, generation) = MapGenerator.Build(template, size, seed, Players(template, players, towns), new GameOptions(Seed: seed), creatures, heroRoster, artifacts, buildings, maxHeroes);
        scenario.Map.Description = Describe(template, size, seed, generation);
        return new EditorMap(scenario, generation, MapValidator.Validate(scenario));
    }

    /// <summary>The description a generated map starts with.</summary>
    public static string Describe(MapTemplate template, int size, ulong seed, GenerationResult generation) =>
        $"Generated from the template \"{template.Name}\" at {size}×{size}, seed {seed}" +
        (generation.Attempt == 0 ? "" : $" (attempt {generation.Attempt})") +
        $", generator version {MapGenerator.Version}." +
        (template.Description.Length == 0 ? "" : $" {template.Description}");
}
