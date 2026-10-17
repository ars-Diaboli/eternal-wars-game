using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Editor;

/// <summary>Map name and description and whether the setup screen may change teams (guard modes are per monster, ADR-065).</summary>
public partial class MapPropertiesPanel : VBoxContainer
{
    private readonly LineEdit _name = new();
    private readonly TextEdit _description = new() { CustomMinimumSize = new Vector2(0, 90), WrapMode = TextEdit.LineWrappingMode.Boundary };
    private readonly CheckBox _teamsLocked = new() { Text = "Teams locked (setup cannot change them)" };
    private MapEditorSession _editor = null!;
    private bool _refreshing;

    public void Bind(MapEditorSession editor)
    {
        _editor = editor;
        AddChild(new Label { Text = "Name" });
        AddChild(_name);
        AddChild(new Label { Text = "Description" });
        AddChild(_description);
        var apply = new Button { Text = "Apply name and description" };
        apply.Pressed += () => _editor.SetMapProperties(_name.Text.Trim(), _description.Text);
        AddChild(apply);

        _teamsLocked.Toggled += locked =>
        {
            if (!_refreshing)
            {
                _editor.SetTeamsLocked(locked);
            }
        };
        AddChild(_teamsLocked);

        Refresh();
        this.SubscribeWhileInTree<UndoHistoryChanged>(editor.Events, _ => Refresh());
    }

    private void Refresh()
    {
        _refreshing = true;
        var scenario = _editor.Scenario;
        _name.Text = scenario.Map.Name;
        _description.Text = scenario.Map.Description;
        _teamsLocked.ButtonPressed = scenario.TeamsLocked;
        _refreshing = false;
    }
}
