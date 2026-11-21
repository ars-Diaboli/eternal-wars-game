using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Editor;

/// <summary>
/// The map settings window's Rumours tab (ADR-257): the map's rumours, one a line, which the game's taverns tell one a week in turn. "Apply
/// rumours" sets them as one undoable rules edit; blank lines are left out.
/// </summary>
public partial class RumoursPanel : VBoxContainer
{
    private readonly TextEdit _text = new()
    {
        PlaceholderText = "One rumour a line, for example:\nThey say the old bridge is guarded by something that does not sleep.",
        WrapMode = TextEdit.LineWrappingMode.Boundary,
        CustomMinimumSize = new Vector2(0, 320),
        SizeFlagsVertical = SizeFlags.ExpandFill,
    };

    private readonly Label _status = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private MapEditorSession _editor = null!;

    /// <summary>The text last shown from the map: a refresh keeps lines typed over it and not applied yet.</summary>
    private string _shown = "";

    public void Bind(MapEditorSession editor)
    {
        _editor = editor;
        AddChild(new Label
        {
            Text = $"The taverns tell one rumour a week, in this order, starting over after the last (at most {ScenarioRules.MaxRumours}, each up to {ScenarioRules.RumourLength} characters).",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        AddChild(_text);
        var apply = new Button { Text = "Apply rumours", SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        apply.Pressed += Apply;
        AddChild(apply);
        AddChild(_status);
        Refresh();
        this.SubscribeWhileInTree<ScenarioRulesChanged>(editor.Events, _ => Refresh());
        this.SubscribeWhileInTree<UndoHistoryChanged>(editor.Events, _ => Refresh());
    }

    /// <summary>The rumours the text holds: its lines, trimmed, without the blank ones.</summary>
    public static IReadOnlyList<string> Lines(string text) =>
        [.. text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0)];

    private void Apply()
    {
        var rumours = Lines(_text.Text);
        if (rumours.SequenceEqual(_editor.Scenario.Rules.Rumours))
        {
            _text.Text = _shown; // only blank lines or spaces changed
            return;
        }

        var before = _shown;
        _shown = _text.Text; // the refresh after the edit then shows the applied lines
        var result = _editor.Map.SetRules(_editor.Scenario.Rules with { Rumours = rumours });
        if (result == EditResult.Ok)
        {
            return; // the refresh shows the count
        }

        _shown = before; // refused: the typed lines stay until applied

        _status.Text = result == EditResult.InvalidValue
            ? $"Keep to {ScenarioRules.MaxRumours} rumours of at most {ScenarioRules.RumourLength} characters each."
            : GeneralSettingsPanel.RulesRefusal(result);
    }

    private void Refresh()
    {
        var rumours = _editor.Scenario.Rules.Rumours;
        var text = string.Join('\n', rumours);
        if (!_text.HasFocus() && _text.Text == _shown)
        {
            _text.Text = text;
        }

        _shown = text;

        _status.Text = rumours.Count switch
        {
            0 => "The map has no rumours.",
            1 => "1 rumour.",
            _ => $"{rumours.Count} rumours.",
        };
    }
}
