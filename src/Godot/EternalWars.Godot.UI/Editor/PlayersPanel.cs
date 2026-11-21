using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.Shared.Events;
using EternalWars.Godot.Shared.Map;
using Godot;

namespace EternalWars.Godot.UI.Editor;

/// <summary>
/// The map settings window's Players tab: the player slots (name, human or computer by default, team, own hero limit; add a slot or remove
/// the last one), whether the setup screen may change teams, and the team counts it must keep to (ADR-117).
/// </summary>
public partial class PlayersPanel : VBoxContainer
{
    private readonly GridContainer _rows = new() { Columns = 4 };
    private readonly Label _status = new();
    private readonly CheckBox _teamsLocked = new() { Text = "Teams locked (setup cannot change them)" };
    private readonly SpinBox _minTeams = new() { MinValue = 0, MaxValue = 8, Step = 1, TooltipText = "The fewest teams a game of this map may have; 0 or 1 = no limit." };
    private readonly SpinBox _maxTeams = new() { MinValue = 0, MaxValue = 8, Step = 1, TooltipText = "The most teams a game of this map may have; 0 or 1 = no limit." };
    private MapEditorSession _editor = null!;

    public void Bind(MapEditorSession editor)
    {
        _editor = editor;
        AddChild(_rows);
        var buttons = new HBoxContainer();
        AddChild(buttons);
        var add = new Button { Text = "Add player" }.WithIcon(UiIcon.Plus);
        add.Pressed += () => Report(_editor.Players.Add($"Player {_editor.Scenario.Players.Count + 1}", isHuman: false, out _));
        buttons.AddChild(add);
        var remove = new Button { Text = "Remove last" }.WithIcon(UiIcon.Minus);
        remove.Pressed += () => Report(_editor.Players.RemoveLast());
        buttons.AddChild(remove);
        AddChild(_status);
        AddChild(new HSeparator());
        _teamsLocked.Toggled += locked => _editor.Players.SetTeamsLocked(locked);
        AddChild(_teamsLocked);

        // Team counts (ADR-117): 0 = no limit; a generated map starts with the template's, which this overrides.
        var counts = new HBoxContainer();
        counts.AddChild(new Label { Text = "Teams from" });
        counts.AddChild(_minTeams);
        counts.AddChild(new Label { Text = "to" });
        counts.AddChild(_maxTeams);
        counts.AddChild(new Label { Text = "(0 = any)" });
        _minTeams.ValueChanged += _ => ApplyTeamCounts();
        _maxTeams.ValueChanged += _ => ApplyTeamCounts();
        AddChild(counts);

        Refresh();
        this.SubscribeWhileInTree<PlayerSlotsChanged>(editor.Events, _ => Refresh());
        this.SubscribeWhileInTree<UndoHistoryChanged>(editor.Events, _ => Refresh());
    }

    private void Refresh()
    {
        var scenario = _editor.Scenario;
        _teamsLocked.SetPressedNoSignal(scenario.TeamsLocked);
        _minTeams.SetValueNoSignal(scenario.MinTeams ?? 0);
        _maxTeams.SetValueNoSignal(scenario.MaxTeams ?? 0);
        _rows.ClearChildren();

        _rows.AddChild(new Label { Text = "Name" });
        _rows.AddChild(new Label { Text = "Human" });
        _rows.AddChild(new Label { Text = "Team" });
        _rows.AddChild(new Label { Text = "Max heroes (0 = map's)" });
        foreach (var player in _editor.Scenario.Players)
        {
            var id = player.Id;
            var name = new LineEdit { Text = player.Name, CustomMinimumSize = new Vector2(150, 0) };
            name.AddThemeColorOverride("font_color", Palette.ForSlot(id).Lightened(0.3f));
            name.TextSubmitted += text => _editor.Players.Update(id, text.Trim(), _editor.Scenario.GetPlayer(id).IsHuman);
            _rows.AddChild(name);

            var human = new CheckBox { ButtonPressed = player.IsHuman };
            human.Toggled += on => _editor.Players.Update(id, _editor.Scenario.GetPlayer(id).Name, on);
            _rows.AddChild(human);

            var team = new SpinBox { MinValue = 0, MaxValue = GameState.MaxPlayers - 1, Value = player.Team.Value };
            team.ValueChanged += v => _editor.Players.SetTeam(id, new TeamId((int)v));
            _rows.AddChild(team);

            // ADR-068: a player's own hero limit for campaign-like maps; 0 = the map's limit.
            var heroes = new SpinBox { MinValue = 0, MaxValue = ScenarioRules.HeroLimit, Value = player.MaxHeroes ?? 0 };
            heroes.ValueChanged += v =>
            {
                Report(_editor.Players.SetHeroLimit(id, v == 0 ? null : (int)v));
                heroes.SetValueNoSignal(_editor.Scenario.GetPlayer(id).MaxHeroes ?? 0);
            };
            _rows.AddChild(heroes);
        }
    }

    private void ApplyTeamCounts()
    {
        int? Count(SpinBox box) => box.Value < 2 ? null : (int)box.Value;
        if (!_editor.Players.SetTeamCounts(Count(_minTeams), Count(_maxTeams)))
        {
            Refresh(); // the fewest above the most: back to what the map has
        }
    }

    private void Report(EditResult result) => _status.Text = result == EditResult.Ok ? "" : result.ToString();
}
