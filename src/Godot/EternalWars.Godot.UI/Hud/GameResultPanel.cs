using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// The end of the game (ADR-057): victory, defeat or a draw, why, and who won. Shown over everything by the app once
/// the game is over (and a battle screen, if one is open, has been closed). Commands are refused from then on, so
/// what is left is to look at the map or go back to the main menu.
/// A won game on a map that is not campaign-like also shows each human winner's score and rank title (ADR-101, <see cref="ScoreRules"/>): the
/// winners type their names, and "Enter the ranking" (or leaving the panel) puts the game into the ranking table, which is then shown with the
/// new rows highlighted. Every end screen offers "Save replay".
/// </summary>
public partial class GameResultPanel : CenterContainer
{
    private readonly Label _title = new() { HorizontalAlignment = HorizontalAlignment.Center };
    private readonly Label _text = new() { HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(420, 0) };
    private readonly VBoxContainer _scores = new();
    private readonly Button _enter = new() { Text = "Enter the ranking" };
    private readonly RankingTableView _table = new();
    private readonly LineEdit _replayName = new() { PlaceholderText = "Name of the replay", CustomMinimumSize = new Vector2(260, 0) };
    private readonly Label _message = new() { HorizontalAlignment = HorizontalAlignment.Center };
    private readonly Dictionary<PlayerId, LineEdit> _names = [];
    private Func<IReadOnlyDictionary<PlayerId, string>, RankingView?> _enterRanking = _ => null;
    private Func<string, string> _saveReplay = _ => string.Empty;
    private bool _entered = true;

    /// <summary>The player asked to go back to the main menu (the game is entered into the ranking first).</summary>
    public event Action? MainMenuRequested;

    public GameResultPanel()
    {
        AnchorRight = 1;
        AnchorBottom = 1;
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;

        var panel = new PanelContainer();
        AddChild(panel);
        var rows = new VBoxContainer();
        rows.AddThemeConstantOverride("separation", 12);
        panel.AddChild(rows);
        _title.AddThemeFontSizeOverride("font_size", 32);
        rows.AddChild(_title);
        rows.AddChild(_text);
        rows.AddChild(_scores);
        _enter.Pressed += EnterRanking;
        rows.AddChild(_enter);
        rows.AddChild(_table);

        var replay = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        replay.AddChild(_replayName);
        var save = new Button { Text = "Save replay" };
        save.Pressed += () => _message.Text = _saveReplay(_replayName.Text);
        replay.AddChild(save);
        rows.AddChild(replay);
        rows.AddChild(_message);

        var leave = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        var close = new Button { Text = "Look at the map" };
        close.Pressed += () =>
        {
            EnterRanking();
            Visible = false;
        };
        leave.AddChild(close);
        var mainMenu = new Button { Text = "Main menu" };
        mainMenu.Pressed += () =>
        {
            EnterRanking();
            Visible = false;
            MainMenuRequested?.Invoke();
        };
        leave.AddChild(mainMenu);
        rows.AddChild(leave);
    }

    /// <summary>
    /// <paramref name="enterRanking"/> puts the game's scores into the ranking under the names typed (by player) and returns the table to show,
    /// or null when no row made it; <paramref name="saveReplay"/> writes the game's replay under a name and returns what to tell the player.
    /// </summary>
    public void Bind(Func<IReadOnlyDictionary<PlayerId, string>, RankingView?> enterRanking, Func<string, string> saveReplay)
    {
        _enterRanking = enterRanking;
        _saveReplay = saveReplay;
    }

    /// <summary>The headline: Victory when a human player won, Defeat when only computers did, Draw when nobody won.</summary>
    public static string Headline(GameState state, GameOutcome outcome) =>
        !outcome.HasWinner ? "Draw"
        : state.Players.Any(p => p.IsHuman && outcome.Won(p.Id)) ? "Victory"
        : state.Players.Any(p => p.IsHuman) ? "Defeat"
        : "Game over";

    /// <summary>A score line: the points, the rank title and what they come from.</summary>
    public static string Describe(GameState state, GameScore score)
    {
        var title = state.Creatures.TryGet(score.Title, out var creature) ? creature.Name : score.Title;
        var timer = score.TurnTimerMinutes is { } minutes ? $", turn timer {minutes} min" : string.Empty;
        return $"{state.GetPlayer(score.Player).Name}: {score.Score} points, rank {title} (day {score.Days}, {score.Difficulty}{timer})";
    }

    public void ShowOutcome(GameState state, GameOutcome outcome)
    {
        var lines = new List<string> { outcome.Reason };
        if (outcome.HasWinner)
        {
            lines.Add($"Winners: {string.Join(", ", outcome.Winners.Select(w => state.GetPlayer(w).Name))}");
        }

        lines.Add($"Day {outcome.Date.AbsoluteDay} ({outcome.Date})");
        var scores = ScoreRules.Scores(state);
        if (outcome.HasWinner && scores.Count == 0 && state.Players.Any(p => p.IsHuman && outcome.Won(p.Id)))
        {
            lines.Add("This map is not ranked.");
        }

        _title.Text = Headline(state, outcome);
        _text.Text = string.Join('\n', lines);
        ShowScores(state, scores);
        _table.Display(null);
        _replayName.Text = string.Empty;
        _message.Text = string.Empty;
        Visible = true;
    }

    private void ShowScores(GameState state, IReadOnlyList<GameScore> scores)
    {
        foreach (var child in _scores.GetChildren())
        {
            child.QueueFree();
        }

        _names.Clear();
        foreach (var score in scores)
        {
            _scores.AddChild(new Label { Text = Describe(state, score), HorizontalAlignment = HorizontalAlignment.Center });
            var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            row.AddChild(new Label { Text = "Name for the ranking:" });
            var name = new LineEdit { Text = state.GetPlayer(score.Player).Name, CustomMinimumSize = new Vector2(220, 0), MaxLength = 32 };
            row.AddChild(name);
            _scores.AddChild(row);
            _names[score.Player] = name;
        }

        _entered = scores.Count == 0;
        _enter.Visible = !_entered;
    }

    /// <summary>Enters the game once; the table is shown when a row made it.</summary>
    private void EnterRanking()
    {
        if (_entered)
        {
            return;
        }

        _entered = true;
        _enter.Visible = false;
        foreach (var name in _names.Values)
        {
            name.Editable = false;
        }

        var names = _names.ToDictionary(n => n.Key, n => string.IsNullOrWhiteSpace(n.Value.Text) ? "Player" : n.Value.Text.Trim());
        var table = _enterRanking(names);
        _table.Display(table);
        if (table is null)
        {
            _message.Text = "The score did not make the ranking.";
        }
    }
}
