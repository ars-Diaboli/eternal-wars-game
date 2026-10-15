using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Godot.Shared.Events;
using EternalWars.Godot.Shared.Map;
using Godot;

namespace EternalWars.Godot.UI.Editor;

/// <summary>Player slots: name, human/computer default, default team; add a slot or remove the last one.</summary>
public partial class PlayersPanel : VBoxContainer
{
    private readonly GridContainer _rows = new() { Columns = 3 };
    private readonly Label _status = new();
    private MapEditorSession _editor = null!;

    public void Bind(MapEditorSession editor)
    {
        _editor = editor;
        AddChild(_rows);
        var buttons = new HBoxContainer();
        AddChild(buttons);
        var add = new Button { Text = "Add player" };
        add.Pressed += () => Report(_editor.AddPlayer($"Player {_editor.Scenario.Players.Count + 1}", isHuman: false, out _));
        buttons.AddChild(add);
        var remove = new Button { Text = "Remove last" };
        remove.Pressed += () => Report(_editor.RemoveLastPlayer());
        buttons.AddChild(remove);
        AddChild(_status);

        Refresh();
        this.SubscribeWhileInTree<PlayerSlotsChanged>(editor.Events, _ => Refresh());
        this.SubscribeWhileInTree<UndoHistoryChanged>(editor.Events, _ => Refresh());
    }

    private void Refresh()
    {
        foreach (var child in _rows.GetChildren())
        {
            child.QueueFree();
        }

        _rows.AddChild(new Label { Text = "Name" });
        _rows.AddChild(new Label { Text = "Human" });
        _rows.AddChild(new Label { Text = "Team" });
        foreach (var player in _editor.Scenario.Players)
        {
            var id = player.Id;
            var name = new LineEdit { Text = player.Name, CustomMinimumSize = new Vector2(150, 0) };
            name.AddThemeColorOverride("font_color", Palette.ForPlayer(id).Lightened(0.3f));
            name.TextSubmitted += text => _editor.UpdatePlayer(id, text.Trim(), _editor.Scenario.GetPlayer(id).IsHuman);
            _rows.AddChild(name);

            var human = new CheckBox { ButtonPressed = player.IsHuman };
            human.Toggled += on => _editor.UpdatePlayer(id, _editor.Scenario.GetPlayer(id).Name, on);
            _rows.AddChild(human);

            var team = new SpinBox { MinValue = 0, MaxValue = GameState.MaxPlayers - 1, Value = player.Team.Value };
            team.ValueChanged += v => _editor.SetPlayerTeam(id, new TeamId((int)v));
            _rows.AddChild(team);
        }
    }

    private void Report(EditResult result) => _status.Text = result == EditResult.Ok ? "" : result.ToString();
}
