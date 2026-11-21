using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.MapEvents;
using EternalWars.Godot.Shared.Controls;
using Godot;

namespace EternalWars.Godot.UI.Editor.Inspector;

/// <summary>The inspector's parts (EDIT-1, ADR-254), in the order they stand: each a heading that opens and closes, shown when it has rows.</summary>
internal enum InspectorPart
{
    /// <summary>What the thing is: its name, kind, owner.</summary>
    Identity,

    /// <summary>What it holds or gives: an army, artifacts, resources, spells, buildings.</summary>
    Contents,

    /// <summary>Who must be fought for it.</summary>
    Guard,

    /// <summary>The texts it shows.</summary>
    Messages,

    /// <summary>The things on the map it is tied to, as jump links.</summary>
    Links,

    /// <summary>The map events it runs.</summary>
    Events,
}

/// <summary>
/// What the inspector's forms (ADR-223) fill and edit through: the parts of the panel (ADR-254), the editor, the edits (skipped while the
/// form is being filled) and the rows several kinds share (a garrison's slots, a link to one of the map's events, jump links).
/// </summary>
internal sealed class InspectorForm(VBoxContainer panel, MapEditorSession editor, Action<string> openEvent, Action<MapLink> jump)
{
    private readonly Dictionary<InspectorPart, VBoxContainer> _parts = [];
    private InspectorPart _part;

    /// <summary>The parts that are closed; they stay so from one inspected thing to the next.</summary>
    public HashSet<InspectorPart> Closed { get; } = [];

    /// <summary>A part was opened or closed.</summary>
    public event Action? ClosedChanged;

    /// <summary>The part the rows go into now, top to bottom: the one <see cref="In"/> named last (Identity at first).</summary>
    public VBoxContainer Box => In(_part);

    /// <summary>The rows that follow go into <paramref name="part"/>; returns its box.</summary>
    public VBoxContainer In(InspectorPart part)
    {
        _part = part;
        if (!_parts.TryGetValue(part, out var body))
        {
            _parts[part] = body = new VBoxContainer { Name = part.ToString() };
        }

        return body;
    }

    /// <summary>A new thing is shown: the parts are empty again and the rows go into Identity.</summary>
    public void Begin()
    {
        _parts.Clear();
        _part = InspectorPart.Identity;
    }

    /// <summary>The form is filled: the parts that have rows go into the panel in their order, each under its heading.</summary>
    public void End()
    {
        foreach (var part in Enum.GetValues<InspectorPart>())
        {
            if (!_parts.TryGetValue(part, out var body))
            {
                continue;
            }

            if (body.GetChildCount() == 0)
            {
                body.Free();
                continue;
            }

            var header = new Button
            {
                Text = part.ToString(), Alignment = HorizontalAlignment.Left, ToggleMode = true, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                TooltipText = $"{part}: click to open or close",
            };
            void Show()
            {
                var open = !Closed.Contains(part);
                header.SetPressedNoSignal(open);
                header.WithIcon(open ? UiIcon.ArrowDown : UiIcon.ArrowRight, UiIcons.ButtonSize, keepText: true);
                body.Visible = open;
            }

            header.Pressed += () =>
            {
                _ = Closed.Remove(part) || Closed.Add(part);
                Show();
                ClosedChanged?.Invoke();
            };
            Show();
            panel.AddChild(header);
            var inset = new MarginContainer();
            inset.AddThemeConstantOverride("margin_left", UiSpacing.Padding);
            inset.AddChild(body);
            panel.AddChild(inset);
        }

        _parts.Clear();
    }

    /// <summary>The jump links (ADR-254), in the Links part: a click selects the linked thing and shows it on the map.</summary>
    public void Links(IReadOnlyList<MapLink> links)
    {
        var box = In(InspectorPart.Links);
        foreach (var link in links)
        {
            var go = new Button
            {
                Text = link.Text, Alignment = HorizontalAlignment.Left, ClipText = true, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                TooltipText = link.Object is null && link.Hero is null ? $"{link.Text}: shows the tile on the map" : $"{link.Text}: selects it and shows it on the map",
            };
            go.WithIcon(UiIcon.ArrowRight, UiIcons.ButtonSize, keepText: true);
            go.Pressed += () => jump(link);
            box.AddChild(go);
        }
    }

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
        In(InspectorPart.Events).AddChild(row);
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

    /// <summary>The seven garrison slots of a town or a map garrison (ADR-070), in the Guard part.</summary>
    public void GarrisonRows(MapObjectId holder, Army troops)
    {
        var box = In(InspectorPart.Guard);
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
