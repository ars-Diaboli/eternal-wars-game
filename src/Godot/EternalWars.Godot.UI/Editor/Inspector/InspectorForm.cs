using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.MapEvents;
using EternalWars.Godot.Shared.Controls;
using Godot;

namespace EternalWars.Godot.UI.Editor.Inspector;

/// <summary>
/// What the inspector's sections (ADR-223) fill and edit through: the panel's box, the editor, the edits (skipped while the form is being filled)
/// and the rows several kinds share (a garrison's slots, a link to one of the map's events).
/// </summary>
internal sealed class InspectorForm(VBoxContainer box, MapEditorSession editor, Action<string> openEvent)
{
    /// <summary>The panel the rows go into, top to bottom.</summary>
    public VBoxContainer Box => box;

    public MapEditorSession Editor => editor;

    public GameState Scenario => editor.Scenario;

    /// <summary>True while the form is being filled: setting the fields' values then edits nothing.</summary>
    public bool Filling { get; set; }

    /// <summary>The creatures by town and tier, as drop-down items.</summary>
    public List<(string Text, string Id)> Creatures() =>
        [.. Scenario.Creatures.All.OrderBy(c => c.Town ?? "~").ThenBy(c => c.Tier).Select(c => ($"{c.Name} (T{c.Tier})", c.Id.Value))];

    /// <summary>Applies an edit unless the form is just filling its fields.</summary>
    public void Edit(Func<EditResult> edit)
    {
        if (!Filling && edit() is not EditResult.Ok and var result)
        {
            GD.PushWarning($"Edit refused: {result}");
        }
    }

    /// <summary>Opens the event in the editor's "Events" tab.</summary>
    public void OpenEvent(string name) => openEvent(name);

    /// <summary>
    /// A row that picks one of the map's events of a kind by name (a box's hero event, a quest object's quest), or none; "New" writes a blank event
    /// of the kind and picks it (one undo step), "Edit" opens the picked one in the "Events" tab.
    /// </summary>
    public void EventChoice(string label, MapEventKind kind, string? current, Func<string?, EditResult> set)
    {
        var names = Scenario.MapEvents.Set.Events.Where(e => e.Kind == kind).Select(e => e.Name).ToList();
        if (current is not null && !names.Contains(current))
        {
            names.Add(current); // a name the map does not have: shown so it can be cleared (the validator or the loader names it)
        }

        var row = new HBoxContainer();
        box.AddChild(row);
        var choice = row.AddChoice(label, new[] { ("— none —", "") }.Concat(names.Select(n => (n, n))), current ?? "");
        choice.Button.ItemSelected += _ => Edit(() => set(choice.Value().Length == 0 ? null : choice.Value()));
        var create = new Button { Text = "New" };
        create.Pressed += () => Edit(() => NewEvent(kind, set));
        row.AddChild(create);
        if (current is not null)
        {
            var open = new Button { Text = "Edit" };
            open.Pressed += () => OpenEvent(current);
            row.AddChild(open);
        }
    }

    /// <summary>Adds a blank event of the kind and hands its name to <paramref name="use"/>, as one undo step.</summary>
    public EditResult NewEvent(MapEventKind kind, Func<string?, EditResult> use, IReadOnlyList<int>? towns = null)
    {
        var name = editor.MapEvents.UnusedName(kind.ToString().ToLowerInvariant());
        editor.BeginStroke($"New {kind.ToString().ToLowerInvariant()} event");
        try
        {
            var result = editor.MapEvents.Add(MapEventTemplates.Blank(name, kind, towns));
            return result == EditResult.Ok ? use(name) : result;
        }
        finally
        {
            editor.EndStroke();
        }
    }

    /// <summary>The seven garrison slots of a town or a map garrison (ADR-070).</summary>
    public void GarrisonRows(MapObjectId holder, Army troops)
    {
        box.AddHeading("Garrison");
        var creatures = new[] { ("— empty —", "") }.Concat(Creatures()).ToList();
        for (var slot = 0; slot < Army.SlotCount; slot++)
        {
            var slotRow = new HBoxContainer();
            box.AddChild(slotRow);
            var stack = troops[slot];
            var creature = slotRow.AddChoice($"{slot + 1}", creatures, stack?.Creature.Value ?? "");
            var count = slotRow.AddSpin("×", stack?.Count ?? 1, 1, 9999);
            var index = slot;
            void ApplySlot() => Edit(() => editor.Towns.SetGarrisonSlot(
                holder, index, creature.Value() is { Length: > 0 } id ? new CreatureStack(id, (int)count.Value) : null));
            creature.Button.ItemSelected += _ => ApplySlot();
            count.ValueChanged += _ => ApplySlot();
        }
    }
}
