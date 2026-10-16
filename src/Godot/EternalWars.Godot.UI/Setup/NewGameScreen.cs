using System.Globalization;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Godot.Shared.Map;
using Godot;

namespace EternalWars.Godot.UI.Setup;

/// <summary>A map the player can start: a name and a factory building its initial state for the chosen options.</summary>
public sealed record ScenarioChoice(string Name, Func<GameOptions, GameState> Create);

/// <summary>
/// New-game setup: map, teams (unless the map locks them, ADR-032), fog mode and refog margin, seed.
/// Raises <see cref="Started"/> with the ready state; the app then builds the session and the world.
/// </summary>
public partial class NewGameScreen : PanelContainer
{
    private readonly OptionButton _maps = new();
    private readonly OptionButton _fog = new();
    private readonly OptionButton _complexity = new();
    private readonly SpinBox _margin = new() { MinValue = 0, MaxValue = 6, Value = GameOptions.Default.RefogMargin };
    private readonly LineEdit _seed = new() { CustomMinimumSize = new Vector2(220, 0) };
    private readonly GridContainer _players = new() { Columns = 3 };
    private readonly Label _status = new();
    private readonly Dictionary<PlayerId, SpinBox> _teams = [];
    private IReadOnlyList<ScenarioChoice> _choices = [];

    public event Action<GameState>? Started;

    public void Bind(IReadOnlyList<ScenarioChoice> choices)
    {
        _choices = choices;
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(520, 0) };
        AddChild(box);
        box.AddChild(new Label { Text = "New game" });

        foreach (var choice in choices)
        {
            _maps.AddItem(choice.Name);
        }

        _maps.ItemSelected += _ => ShowPlayers();
        Row(box, "Map", _maps);

        _fog.AddItem("Permanent reveal (HoMM3)", (int)FogMode.PermanentReveal);
        _fog.AddItem("Shroud + refog for units", (int)FogMode.ShroudWithRefog);
        _fog.ItemSelected += _ => _margin.Editable = SelectedFog == FogMode.ShroudWithRefog;
        Row(box, "Fog of war", _fog);
        _margin.Editable = false;
        Row(box, "Refog margin", _margin);

        _complexity.AddItem("Map default", -1);
        foreach (var level in Enum.GetValues<Complexity>())
        {
            _complexity.AddItem($"{level} ({StartingResources.For(level).Gold} gold)", (int)level);
        }

        Row(box, "Complexity", _complexity);

        _seed.Text = ((ulong)Random.Shared.NextInt64()).ToString(CultureInfo.InvariantCulture);
        Row(box, "Seed", _seed);

        box.AddChild(_players);
        var start = new Button { Text = "Start" };
        start.Pressed += Start;
        box.AddChild(start);
        box.AddChild(_status);
        ShowPlayers();
    }

    private FogMode SelectedFog => (FogMode)_fog.GetSelectedId();

    private ScenarioChoice? SelectedMap => _maps.Selected >= 0 && _maps.Selected < _choices.Count ? _choices[_maps.Selected] : null;

    private static void Row(Container parent, string label, Control control)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(140, 0) });
        row.AddChild(control);
        parent.AddChild(row);
    }

    /// <summary>Lists the selected map's player slots; team boxes are disabled when the map locks its teams.</summary>
    private void ShowPlayers()
    {
        foreach (var child in _players.GetChildren())
        {
            child.QueueFree();
        }

        _teams.Clear();
        if (SelectedMap is not { } map)
        {
            return;
        }

        GameState preview;
        try
        {
            preview = map.Create(GameOptions.Default);
        }
        catch (Exception e) when (e is FormatException or IOException)
        {
            _status.Text = $"Cannot load {map.Name}: {e.Message}";
            return;
        }

        _players.AddChild(new Label { Text = "Player" });
        _players.AddChild(new Label { Text = "Controlled by" });
        _players.AddChild(new Label { Text = preview.TeamsLocked ? "Team (locked by the map)" : "Team" });
        foreach (var player in preview.Players)
        {
            var name = new Label { Text = player.Name };
            name.AddThemeColorOverride("font_color", Palette.ForPlayer(player.Id).Lightened(0.3f));
            _players.AddChild(name);
            _players.AddChild(new Label { Text = player.IsHuman ? "Human" : "Computer" });
            var team = new SpinBox { MinValue = 0, MaxValue = GameState.MaxPlayers - 1, Value = player.Team.Value, Editable = !preview.TeamsLocked };
            _players.AddChild(team);
            _teams[player.Id] = team;
        }
    }

    private void Start()
    {
        if (SelectedMap is not { } map || !ulong.TryParse(_seed.Text, CultureInfo.InvariantCulture, out var seed))
        {
            _status.Text = "Choose a map and enter a numeric seed.";
            return;
        }

        try
        {
            var state = map.Create(new GameOptions(SelectedFog, (int)_margin.Value, seed, _complexity.GetSelectedId() < 0 ? null : (Complexity)_complexity.GetSelectedId()));
            if (!state.TeamsLocked)
            {
                state.AssignTeams(_teams.ToDictionary(t => t.Key, t => new TeamId((int)t.Value.Value)));
            }

            Started?.Invoke(state);
        }
        catch (Exception e) when (e is FormatException or IOException or InvalidOperationException)
        {
            _status.Text = e.Message;
        }
    }
}
