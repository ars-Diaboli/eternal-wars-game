using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Editor;

/// <summary>
/// The undo history as a list (EDIT-1, ADR-250): the state the history starts from, then every step, oldest first. The row of the state the
/// map is in is selected and the undone steps after it are dim; a click on a row jumps there (<see cref="MapEditorSession.UndoTo"/>).
/// Only a click of the left button jumps: the list takes no keys (no focus, no search by typing), so a key meant for the map or a
/// shortcut never moves the history.
/// </summary>
public partial class HistoryPanel : ItemList
{
    private MapEditorSession _editor = null!;

    public void Bind(MapEditorSession editor)
    {
        _editor = editor;
        AllowSearch = false;
        FocusMode = FocusModeEnum.None;
        ItemClicked += (index, _, button) =>
        {
            if (button == (long)MouseButton.Left && (int)index != _editor.History.Position)
            {
                _editor.UndoTo((int)index);
            }
        };
        // At the end of the frame: a jump is made from this list's own signal, and the list is not rebuilt inside it.
        new RefreshScheduler(this, Refresh).On<UndoHistoryChanged>(editor.Events);
        Refresh();
    }

    private void Refresh()
    {
        Clear();
        var history = _editor.History;
        var steps = history.Steps;
        AddItem("Start");
        SetItemTooltip(0, "The map before the first step the history keeps");
        for (var i = 0; i < steps.Count; i++)
        {
            var index = AddItem($"{i + 1}. {steps[i]}");
            if (i >= history.Position)
            {
                SetItemCustomFgColor(index, UiTheme.TextDone);
                SetItemTooltip(index, "Undone: a click redoes up to here");
            }
        }

        Select(history.Position);
        EnsureCurrentIsVisible();
    }
}
