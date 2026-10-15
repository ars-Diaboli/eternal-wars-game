using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Editor;

/// <summary>Live <see cref="MapValidator"/> results after every edit; selecting an issue reports its tile.</summary>
public partial class ValidationPanel : ItemList
{
    private IReadOnlyList<MapIssue> _issues = [];
    private MapEditorSession _editor = null!;

    public event Action<MapPosition>? IssueSelected;

    public void Bind(MapEditorSession editor)
    {
        _editor = editor;
        CustomMinimumSize = new Vector2(0, 200);
        ItemSelected += index =>
        {
            if (_issues[(int)index].Position is { } position)
            {
                IssueSelected?.Invoke(position);
            }
        };
        Refresh();
        this.SubscribeWhileInTree<UndoHistoryChanged>(editor.Events, _ => Refresh());
    }

    public void Refresh()
    {
        Clear();
        _issues = MapValidator.Validate(_editor.Scenario);
        if (_issues.Count == 0)
        {
            AddItem("No problems found.");
            return;
        }

        foreach (var issue in _issues)
        {
            var index = AddItem($"{issue.Severity}: {issue.Message}{(issue.Position is { } p ? $" {p}" : "")}");
            SetItemCustomFgColor(index, issue.Severity == IssueSeverity.Error ? new Color(1f, 0.45f, 0.4f) : new Color(1f, 0.85f, 0.4f));
        }
    }
}
