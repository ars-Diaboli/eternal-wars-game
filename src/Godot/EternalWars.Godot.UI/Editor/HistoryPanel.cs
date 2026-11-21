using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Editor;

/// <summary>
/// An undo history as a list (EDIT-1, EDIT-2, ADR-250): the state the history starts from, then every step, oldest first. The row of the
/// state the map (or the template) is in is selected and the undone steps after it are dim; a click on a row jumps there
/// (<see cref="IUndoSession.UndoTo"/>). The map's is a tab of the bottom region; the template editor shows the template's beside its issues.
/// Only a click of the left button jumps: the list takes no keys (no focus, no search by typing), so a key meant for the map or a
/// shortcut never moves the history.
/// </summary>
public partial class HistoryPanel : ItemList
{
    private IUndoSession _session = null!;
    private string _thing = "map";

    /// <summary>The history of <paramref name="session"/>; <paramref name="thing"/> names what it edits ("map", "template") in the tooltips.</summary>
    public void Bind(IUndoSession session, string thing = "map")
    {
        _session = session;
        _thing = thing;
        AllowSearch = false;
        FocusMode = FocusModeEnum.None;
        ItemClicked += (index, _, button) =>
        {
            if (button == (long)MouseButton.Left && (int)index != _session.History.Position)
            {
                _session.UndoTo((int)index);
            }
        };
        // At the end of the frame: a jump is made from this list's own signal, and the list is not rebuilt inside it.
        new RefreshScheduler(this, Refresh).On<UndoHistoryChanged>(session.Events);
        Refresh();
    }

    private void Refresh()
    {
        Clear();
        var history = _session.History;
        var steps = history.Steps;
        AddItem("Start");
        SetItemTooltip(0, $"The {_thing} before the first step the history keeps");
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
