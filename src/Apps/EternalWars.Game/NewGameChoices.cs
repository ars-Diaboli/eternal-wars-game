using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Data.Templates;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Core.Generation;
using EternalWars.Godot.Shared;
using EternalWars.Godot.UI.Setup;
using Godot;

namespace EternalWars.Game;

/// <summary>What the new-game screen offers (composition): the hand-made scenarios and the random-map templates, from the game and the editor's folders.</summary>
internal static class NewGameChoices
{
    private const string EditorMapsFolder = "Eternal Wars Map Editor/maps";

    /// <summary>Where the editor's template editor saves (its <c>user://templates</c>, ADR-070).</summary>
    private const string EditorTemplatesFolder = "Eternal Wars Map Editor/templates";

    /// <summary>
    /// The map given with --map, the shipped maps (the demo, Two Towns, Ember March, the three arenas), and every map saved by the editor, each
    /// with the new-game screen's tab it is listed on (ADR-201): the shipped ones by hand, a file by <see cref="MapKinds.Of"/>.
    /// </summary>
    public static List<ScenarioChoice> Scenarios()
    {
        var choices = new List<ScenarioChoice>();
        if (LaunchArguments.MapPath is { } path)
        {
            choices.Add(FromFile(path));
        }

        choices.Add(Built("Demo Valley", content => DemoScenario.Create(content: content), MapKind.Scenario));
        choices.Add(Built("Two Towns", TownsScenario.Create, MapKind.Scenario));
        choices.Add(Built(EmberMarchScenario.Name, EmberMarchScenario.Create, MapKind.Scenario));
        choices.AddRange(ArenaScenarios.All.Select(arena => Built(arena.Name, content => arena.Create(content), MapKind.Arena)));
        var editorMaps = Path.Combine(Path.GetDirectoryName(OS.GetUserDataDir())!, EditorMapsFolder);
        if (Directory.Exists(editorMaps))
        {
            choices.AddRange(Directory.GetFiles(editorMaps, "*" + MapSerializer.FileExtension).Order().Select(FromFile));
        }

        return choices;

        static ScenarioChoice FromFile(string file)
        {
            var (kind, width, height) = Describe(file);
            return new(
                Path.GetFileName(file), options => MapConverter.ToGameState(MapSerializer.Load(file), GameContent.All, options), kind, width, height);
        }

        // A built-in scenario goes through the map format like a file, so it starts the way a saved copy of it would. Its size is read once, for
        // sorting and filtering the list (ADR-204).
        static ScenarioChoice Built(string name, Func<ContentCatalogs, GameState> create, MapKind kind)
        {
            var map = create(GameContent.All).Map;
            return new(
                name, options => MapConverter.ToGameState(MapConverter.ToDocument(create(GameContent.All)), GameContent.All, options), kind, map.Width,
                map.Height);
        }
    }

    /// <summary>
    /// The tab and size of a map file; one that does not load is listed with the scenarios, of unknown size, where choosing it says why.
    /// </summary>
    private static (MapKind Kind, int Width, int Height) Describe(string file)
    {
        try
        {
            var state = MapConverter.ToGameState(MapSerializer.Load(file), GameContent.All, GameOptions.Default);
            return (MapKinds.Of(state), state.Map.Width, state.Map.Height);
        }
        catch (Exception e) when (e is FormatException or IOException or InvalidOperationException or System.Text.Json.JsonException)
        {
            return (MapKind.Scenario, 0, 0);
        }
    }

    /// <summary>
    /// The shipped templates (ADR-086), then the valid ones the editor's template editor saved (ADR-117): generated on "Start" for
    /// the players set up, the map seed being the game seed. Each map generated is handed to <paramref name="generated"/> with how it was made.
    /// </summary>
    public static List<RandomMapChoice> RandomMaps(Action<GameState, GenerationInfo> generated) =>
    [
        .. BuiltInTemplates.All.Select(template => RandomMap(template, template.Name, generated)),
        .. TemplateFolder.LoadValid(Path.Combine(Path.GetDirectoryName(OS.GetUserDataDir())!, EditorTemplatesFolder), GD.PushWarning)
            .Select(u => RandomMap(u.Template, $"{u.Template.Name} ({Path.GetFileName(u.Path)})", generated)),
    ];

    private static RandomMapChoice RandomMap(MapTemplate template, string label, Action<GameState, GenerationInfo> generated) => new(
        label,
        template.Description,
        template.Sizes,
        template.MinPlayers,
        template.TeamsLocked,
        StartSlots.Of(template).Select(s => new RandomMapSlot(s.Name, s.HumanAllowed, s.HumanByDefault, s.Team, s.PicksTier1Dwelling)).ToList(),
        request =>
        {
            var slots = StartSlots.Of(template);
            var players = request.Players
                .Select(p => new PlayerSetup(p.Slot, slots[p.Slot].Name, p.IsHuman, p.Town ?? PlayerSetup.RandomTown, p.Hero, p.Team, p.Tier1Dwelling))
                .ToList();
            var (state, generation) = MapGenerator.Build(
                template, request.Size, request.Options.Seed, players, request.Options, GameContent.All, request.MaxHeroes);
            generated(state, GenerationInfo.Of(template.Name, request.Size, request.Options.Seed, generation.Attempt));
            return new RandomMapResult(state, generation.Problems);
        },
        template.MinTeams,
        template.MaxTeams,
        template.MaxHeroes);
}
