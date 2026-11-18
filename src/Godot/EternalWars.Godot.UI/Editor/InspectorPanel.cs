using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.Shared.Events;
using EternalWars.Godot.UI.Editor.Inspector;
using Godot;

namespace EternalWars.Godot.UI.Editor;

/// <summary>
/// Edits the selected hero (stats, Tactics, seven army slots) or object (mine owner and guard, chest values,
/// monster creature/count/disposition/guard mode, town owner, name, buildings, dwellings, guild spells and garrison, a Pandora's Box's contents, guard and
/// message; gate links are shown; links to the events a box runs, a quest object asks and a town has).
/// Every change is an undoable editor edit. The hero's fields are <see cref="HeroInspector"/>, an object's the section of its type
/// (<see cref="InspectorSections"/>, ADR-223); the panel fills again once a frame after edits, undo and redo.
/// </summary>
public partial class InspectorPanel : VBoxContainer
{
    private MapEditorSession _editor = null!;
    private InspectorForm _form = null!;
    private HeroId? _hero;
    private MapObjectId? _object;

    /// <summary>"Edit" on an event link: the editor opens that event in the "Events" tab.</summary>
    public event Action<string>? EventRequested;

    public void Bind(MapEditorSession editor)
    {
        _editor = editor;
        _form = new InspectorForm(this, editor, name => EventRequested?.Invoke(name));
        Refresh();
        new RefreshScheduler(this, Refresh).On<UndoHistoryChanged>(editor.Events);
    }

    public void InspectHero(HeroId hero)
    {
        (_hero, _object) = (hero, null);
        Refresh();
    }

    public void InspectObject(MapObjectId mapObject)
    {
        (_hero, _object) = (null, mapObject);
        Refresh();
    }

    private void Refresh()
    {
        _form.Filling = true;
        this.ClearChildren();

        var scenario = _editor.Scenario;
        if (_hero is { } heroId && scenario.Heroes.FirstOrDefault(h => h.Id == heroId) is { } hero)
        {
            HeroInspector.Show(_form, hero);
        }
        else if (_object is { } objectId && scenario.FindObject(objectId) is { } mapObject)
        {
            AddChild(new Label { Text = $"{mapObject.Definition.Name} #{mapObject.Id.Value} at {mapObject.Position}" });
            InspectorSections.For(mapObject)?.Show(_form, mapObject);
        }
        else
        {
            AddChild(new Label { Text = "Use the Select tool and click a hero or object." });
        }

        _form.Filling = false;
    }
}
