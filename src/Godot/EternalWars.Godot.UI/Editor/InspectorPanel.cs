using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.Shared.Events;
using EternalWars.Godot.UI.Editor.Inspector;
using EternalWars.Godot.UI.Editor.Shell;
using Godot;

namespace EternalWars.Godot.UI.Editor;

/// <summary>
/// Edits the selected hero (stats, Tactics, seven army slots) or object (mine owner and guard, chest values,
/// monster creature/count/disposition/guard mode, town owner, name, buildings, dwellings, guild spells and garrison, a Pandora's Box's contents, guard and
/// message; gate links are shown; links to the events a box runs, a quest object asks and a town has).
/// Every change is an undoable editor edit. The hero's fields are <see cref="HeroInspector"/>, an object's the section of its type
/// (<see cref="InspectorSections"/>, ADR-223); the panel fills again once a frame after edits, undo and redo.
/// The rows stand in parts that open and close (identity, contents, guard, messages, links, events; ADR-254); which are closed is
/// kept in the editor's settings. The Links part holds jump links (<see cref="MapLinks"/>): a click asks for the linked thing to be
/// selected and shown (<see cref="LinkRequested"/>). Several objects of one kind are edited together (<see cref="InspectObjects"/>,
/// ADR-255): a field set applies to all of them as one undo step, and a field whose values differ shows "—".
/// </summary>
public partial class InspectorPanel : VBoxContainer
{
    private const string SettingsSection = "inspector";
    private const string ClosedKey = "closed";

    private MapEditorSession _editor = null!;
    private InspectorForm _form = null!;
    private HeroId? _hero;
    private IReadOnlyList<MapObjectId> _objects = [];

    /// <summary>"Edit" on an event link: the editor opens that event in the "Events" tab.</summary>
    public event Action<string>? EventRequested;

    /// <summary>A jump link was clicked: the editor selects its object or hero and shows its tile.</summary>
    public event Action<MapLink>? LinkRequested;

    public void Bind(MapEditorSession editor, EditorSettingsFile? settings = null)
    {
        _editor = editor;
        _form = new InspectorForm(this, editor, name => EventRequested?.Invoke(name), link => LinkRequested?.Invoke(link));
        foreach (var name in settings?.Get(SettingsSection, ClosedKey, System.Array.Empty<string>()).AsStringArray() ?? [])
        {
            if (Enum.TryParse<InspectorPart>(name, out var part))
            {
                _form.Closed.Add(part);
            }
        }

        _form.ClosedChanged += () => settings?.Set(SettingsSection, ClosedKey, _form.Closed.Order().Select(p => p.ToString()).ToArray());
        Refresh();
        new RefreshScheduler(this, Refresh).On<UndoHistoryChanged>(editor.Events);
    }

    public void InspectHero(HeroId hero)
    {
        (_hero, _objects) = (hero, []);
        Refresh();
    }

    public void InspectObject(MapObjectId mapObject) => InspectObjects([mapObject]);

    /// <summary>Shows <paramref name="objects"/>: one object's form, or, for several of one kind, the fields they share.</summary>
    public void InspectObjects(IReadOnlyCollection<MapObjectId> objects)
    {
        (_hero, _objects) = (null, [.. objects]);
        Refresh();
    }

    private void Refresh()
    {
        _form.Filling = true;
        this.ClearChildren();
        _form.Begin();

        var scenario = _editor.Scenario;
        if (_hero is { } heroId && scenario.Heroes.FirstOrDefault(h => h.Id == heroId) is { } hero)
        {
            HeroInspector.Show(_form, hero);
            _form.Links(MapLinks.Of(scenario, hero));
        }
        else if (_objects.Where(id => scenario.FindObject(id) is not null).ToList() is { Count: > 0 } present)
        {
            ShowObjects(present);
        }
        else
        {
            AddChild(new Label { Text = "Use the Select tool and click a hero or object." });
        }

        _form.End();
        _form.Filling = false;
    }

    /// <summary>
    /// The object's name (ADR-257; a town's is in its own section): Enter or leaving the field sets it, blank takes it away. The map events, the
    /// conditions and the quest form show it, and the quest form takes it in place of the id.
    /// </summary>
    private void NameRow(Container identity, MapObject mapObject)
    {
        var row = new HBoxContainer();
        identity.AddChild(row);
        row.AddChild(new Label { Text = "Name" });
        var name = new LineEdit
        {
            Text = mapObject.Name ?? "",
            PlaceholderText = "none",
            MaxLength = MapObject.NameLength,
            CustomMinimumSize = new Vector2(UiTheme.InspectorNameWidth, 0),
            TooltipText = "A name of its own, unique on the map: the map events, the conditions and the quest form name the object by it.",
        };
        // Applies only a change typed over the name shown, while the object still has it: a field freed after an undo must not apply again.
        var shown = mapObject.Name ?? "";
        void Apply()
        {
            if (name.Text.Trim() != shown && (mapObject.Name ?? "") == shown)
            {
                shown = name.Text.Trim();
                _form.Edit(() => _editor.Objects.SetName(mapObject.Id, name.Text));
            }
        }

        name.TextSubmitted += _ => Apply();
        name.FocusExited += Apply;
        row.AddChild(name);
    }

    private void ShowObjects(IReadOnlyList<MapObjectId> ids)
    {
        var scenario = _editor.Scenario;
        var identity = _form.In(InspectorPart.Identity);
        if (_editor.ObjectsOfOneKind(ids) is not { } objects)
        {
            var kinds = ids.Select(id => scenario.FindObject(id)!.Definition.Name).GroupBy(n => n).OrderByDescending(g => g.Count()).ThenBy(g => g.Key);
            identity.AddChild(new Label
            {
                Text = $"{ids.Count} objects of different kinds: {string.Join(", ", kinds.Select(g => $"{g.Count()} × {g.Key}"))}. "
                       + "Select objects of one kind to edit them together.",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(UiTheme.InspectorNoteWidth, 0),
            });
            return;
        }

        var first = objects[0];
        var name = objects.All(o => o.Definition.Id == first.Definition.Id) ? first.Definition.Name : first.GetType().Name;
        identity.AddChild(new Label
        {
            Text = objects.Count == 1 ? $"{first.Definition.Name}{(first.GivenName is { } given ? $" \"{given}\"" : "")} #{first.Id.Value} at {first.Position}"
                : InspectorSections.Has(first.GetType()) ? $"{objects.Count} × {name}: a change applies to all of them"
                : $"{objects.Count} × {name}: nothing to edit",
        });
        if (objects.Count == 1 && first is not Town)
        {
            NameRow(identity, first);
        }

        InspectorSections.For(first)?.Show(_form, objects);
        if (objects.Count == 1)
        {
            _form.Links(MapLinks.Of(scenario, first));
        }
    }
}
