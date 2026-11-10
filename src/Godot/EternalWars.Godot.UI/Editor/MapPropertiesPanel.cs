using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Editor;

/// <summary>
/// Map name and description, whether the setup screen may change teams and the team counts it must keep to (ADR-117; guard modes are per
/// monster, ADR-065).
/// </summary>
public partial class MapPropertiesPanel : VBoxContainer
{
    private readonly LineEdit _name = new();
    private readonly TextEdit _description = new() { CustomMinimumSize = new Vector2(0, 90), WrapMode = TextEdit.LineWrappingMode.Boundary };
    private readonly CheckBox _teamsLocked = new() { Text = "Teams locked (setup cannot change them)" };
    private readonly SpinBox _minTeams = new() { MinValue = 0, MaxValue = 8, Step = 1, TooltipText = "The fewest teams a game of this map may have; 0 or 1 = no limit." };
    private readonly SpinBox _maxTeams = new() { MinValue = 0, MaxValue = 8, Step = 1, TooltipText = "The most teams a game of this map may have; 0 or 1 = no limit." };
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
        apply.Pressed += () => _editor.Map.SetProperties(_name.Text.Trim(), _description.Text);
        AddChild(apply);

        _teamsLocked.Toggled += locked =>
        {
            if (!_refreshing)
            {
                _editor.Players.SetTeamsLocked(locked);
            }
        };
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
        this.SubscribeWhileInTree<UndoHistoryChanged>(editor.Events, _ => Refresh());
    }

    private void Refresh()
    {
        _refreshing = true;
        var scenario = _editor.Scenario;
        _name.Text = scenario.Map.Name;
        _description.Text = scenario.Map.Description;
        _teamsLocked.ButtonPressed = scenario.TeamsLocked;
        _minTeams.Value = scenario.MinTeams ?? 0;
        _maxTeams.Value = scenario.MaxTeams ?? 0;
        _refreshing = false;
    }

    private void ApplyTeamCounts()
    {
        if (_refreshing)
        {
            return;
        }

        int? Count(SpinBox box) => box.Value < 2 ? null : (int)box.Value;
        if (!_editor.Players.SetTeamCounts(Count(_minTeams), Count(_maxTeams)))
        {
            Refresh(); // the fewest above the most: back to what the map has
        }
    }
}
