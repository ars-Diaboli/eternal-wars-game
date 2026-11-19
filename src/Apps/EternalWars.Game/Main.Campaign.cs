using EternalWars.Core.Data.Campaigns;
using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.Campaigns;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Godot.Shared;
using EternalWars.Godot.UI.Hud;
using EternalWars.Godot.UI.Setup;
using Godot;

namespace EternalWars.Game;

/// <summary>
/// Campaigns (ADR-241): the "Campaign" screen, the story before and after each map, the next map started with what the last one carried, and a
/// loaded campaign save going on with its campaign.
/// </summary>
public partial class Main
{
    /// <summary>How a shipped campaign is named in saves: this prefix and its name; any other source is a campaign file's path.</summary>
    private const string BuiltInCampaign = "builtin:";

    /// <summary>The game's own folder of campaign files; the map editor's maps folder is read too, so a campaign can sit next to its maps.</summary>
    private const string CampaignsFolder = "user://campaigns";

    /// <summary>The campaign the game being played is a map of; null for a single game.</summary>
    private ActiveCampaign? _campaign;

    /// <summary>A campaign being played: where it comes from, the campaign, and where it stood when the current map began.</summary>
    private sealed record ActiveCampaign(string Source, Campaign Campaign, CampaignProgress Progress)
    {
        public CampaignRecord Record => new(Source, Progress);

        public CampaignMap Map => Campaign.Maps[Math.Min(Progress.Map, Campaign.Maps.Count - 1)];
    }

    /// <summary>The shipped campaign, then the campaign files of the game's folder and of the map editor's maps folder (by file name).</summary>
    private static List<CampaignChoice> CampaignChoices()
    {
        var choices = new List<CampaignChoice> { new(BuiltInCampaign + BorderWatchCampaign.Name, BorderWatchCampaign.Campaign) };
        foreach (var folder in new[] { ProjectSettings.GlobalizePath(CampaignsFolder), Path.Combine(Path.GetDirectoryName(OS.GetUserDataDir())!, EditorMapsFolder) })
        {
            choices.AddRange(CampaignSerializer.LoadFolder(folder, GD.PushWarning).Select(c => new CampaignChoice(c.Path, c.Campaign)));
        }

        return choices;
    }

    /// <summary>The map editor's maps folder, beside the game's user data folder (as on the new-game screen).</summary>
    private const string EditorMapsFolder = "Eternal Wars Map Editor/maps";

    private void ShowCampaigns()
    {
        var screen = new CampaignScreen();
        ShowSetup(screen, back: ShowMainMenu);
        screen.Bind(CampaignChoices());
        screen.BackRequested += ShowMainMenu;
        screen.Started += (choice, difficulty) =>
        {
            _campaign = new ActiveCampaign(choice.Source, choice.Campaign, CampaignRules.Start(difficulty));
            ShowCampaignIntro();
        };
    }

    /// <summary>The next map's title and intro; going on starts it.</summary>
    private void ShowCampaignIntro(string? problem = null)
    {
        if (_campaign is not { } campaign)
        {
            return;
        }

        LeaveGame();
        var map = campaign.Map;
        var text = map.Intro.Length > 0 ? map.Intro : "The next map awaits.";
        var story = new CampaignStoryScreen(
            $"{campaign.Campaign.Name}: {campaign.Progress.Map + 1}. {map.Title}", problem is null ? text : $"{text}\n\n{problem}", "Start the map");
        ShowSetup(story, back: LeaveCampaign);
        story.ContinueRequested += PlayCampaignMap;
        story.MainMenuRequested += LeaveCampaign;
    }

    private void LeaveCampaign()
    {
        _campaign = null;
        ShowMainMenu();
    }

    /// <summary>Starts the campaign's next map with its difficulty, the variables and heroes carried in, and a new seed.</summary>
    private void PlayCampaignMap()
    {
        if (_campaign is not { } campaign)
        {
            return;
        }

        GameState state;
        try
        {
            var seed = LaunchArguments.Seed ?? (ulong)System.Random.Shared.NextInt64(); // a new game's seed, as the new-game screen draws one
            state = MapConverter.ToGameState(CampaignMapDocument(campaign), GameContent.All, CampaignRules.Options(campaign.Progress, seed));
        }
        catch (Exception e) when (e is FormatException or IOException or InvalidOperationException or ArgumentException or System.Text.Json.JsonException)
        {
            ShowCampaignIntro($"The map does not load: {e.Message}");
            return;
        }

        CloseSetup();
        _generated = null;
        StartGame(state);
        _campaign = campaign; // StartGame begins a single game's way; this one is the campaign's
    }

    /// <summary>The map document of the campaign's current map: a shipped campaign builds it, a campaign file names a map file beside it.</summary>
    private static MapDocument CampaignMapDocument(ActiveCampaign campaign) =>
        campaign.Source.StartsWith(BuiltInCampaign, StringComparison.Ordinal)
            ? MapConverter.ToDocument(BorderWatchCampaign.CreateMap(campaign.Map.Map, GameContent.All))
            : MapSerializer.Load(Path.Combine(Path.GetDirectoryName(campaign.Source)!, campaign.Map.Map));

    /// <summary>The campaign a loaded save is a map of, when it can still be found; null for a single game or a campaign file that is gone.</summary>
    private static ActiveCampaign? ResumeCampaign(CampaignRecord? record)
    {
        if (record is null)
        {
            return null;
        }

        if (record.Source == BuiltInCampaign + BorderWatchCampaign.Name)
        {
            return new ActiveCampaign(record.Source, BorderWatchCampaign.Campaign, record.Progress);
        }

        try
        {
            return new ActiveCampaign(record.Source, CampaignSerializer.Load(record.Source), record.Progress);
        }
        catch (Exception e) when (e is FormatException or IOException or UnauthorizedAccessException)
        {
            GD.PushWarning($"The save's campaign {record.Source} does not load ({e.Message}); the map plays on its own.");
            return null;
        }
    }

    /// <summary>What the result screen offers for a campaign map: the next map when it was won, trying again when not; null for a single game.</summary>
    private CampaignResult? CampaignResultOf(GameState state)
    {
        if (_campaign is not { } campaign)
        {
            return null;
        }

        return CampaignRules.Finish(campaign.Campaign, campaign.Progress, state) is { } next
            ? new CampaignResult(Won: true, next.IsWon(campaign.Campaign) ? null : campaign.Campaign.Maps[next.Map].Title)
            : new CampaignResult(Won: false, null);
    }

    /// <summary>A won map's "Next map": its outro, then the next map's intro, or the campaign's end after the last map.</summary>
    private void ContinueCampaign()
    {
        if (_campaign is not { } campaign || CampaignRules.Finish(campaign.Campaign, campaign.Progress, _session.State) is not { } next)
        {
            return;
        }

        var won = campaign.Map;
        _campaign = campaign with { Progress = next };
        LeaveGame();
        var last = next.IsWon(campaign.Campaign);
        var outro = won.Outro.Length > 0 ? won.Outro : $"{won.Title} is won.";
        var story = new CampaignStoryScreen(won.Title, outro, last ? "Finish" : "Go on");
        ShowSetup(story, back: LeaveCampaign);
        story.MainMenuRequested += LeaveCampaign;
        story.ContinueRequested += last ? ShowCampaignEnd : () => ShowCampaignIntro();
    }

    /// <summary>The campaign is won: the days each map took.</summary>
    private void ShowCampaignEnd()
    {
        if (_campaign is not { } campaign)
        {
            return;
        }

        var days = campaign.Campaign.Maps.Zip(campaign.Progress.Days, (map, day) => $"{map.Title}: day {day}");
        var story = new CampaignStoryScreen($"{campaign.Campaign.Name}: won", string.Join('\n', days), "Main menu");
        ShowSetup(story, back: LeaveCampaign);
        story.ContinueRequested += LeaveCampaign;
        story.MainMenuRequested += LeaveCampaign;
    }
}
