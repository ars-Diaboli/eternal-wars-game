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

    /// <summary>The demo, the map given with --map, and every map saved by the editor.</summary>
    public static List<ScenarioChoice> Scenarios()
    {
        var choices = new List<ScenarioChoice>();
        if (LaunchArguments.MapPath is { } path)
        {
            choices.Add(FromFile(path));
        }

        choices.Add(Built("Demo Valley", content => DemoScenario.Create(content: content)));
        choices.Add(Built("Two Towns", TownsScenario.Create));
        choices.Add(Built(EmberMarchScenario.Name, EmberMarchScenario.Create));
        var editorMaps = Path.Combine(Path.GetDirectoryName(OS.GetUserDataDir())!, EditorMapsFolder);
        if (Directory.Exists(editorMaps))
        {
            choices.AddRange(Directory.GetFiles(editorMaps, "*" + MapSerializer.FileExtension).Order().Select(FromFile));
        }

        return choices;

        static ScenarioChoice FromFile(string file) =>
            new(Path.GetFileName(file), options => MapConverter.ToGameState(MapSerializer.Load(file), GameContent.All, options));

        // A built-in scenario goes through the map format like a file, so it starts the way a saved copy of it would.
        static ScenarioChoice Built(string name, Func<ContentCatalogs, GameState> create) =>
            new(name, options => MapConverter.ToGameState(MapConverter.ToDocument(create(GameContent.All)), GameContent.All, options));
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
                template, request.Size, request.Options.Seed, players, request.Options, GameContent.Creatures, GameContent.Heroes, GameContent.Artifacts,
                GameContent.Buildings, request.MaxHeroes);
            generated(state, GenerationInfo.Of(template.Name, request.Size, request.Options.Seed, generation.Attempt));
            return new RandomMapResult(state, generation.Problems);
        },
        template.MinTeams,
        template.MaxTeams,
        template.MaxHeroes);
}
