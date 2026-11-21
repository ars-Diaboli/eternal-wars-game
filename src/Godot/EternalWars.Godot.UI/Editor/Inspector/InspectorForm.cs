using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Magic;
using EternalWars.Core.Domain.MapEvents;
using EternalWars.Core.Domain.Objects;
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
/// form is being filled) and the rows several kinds share (a garrison's slots, a link to one of the map's events, jump links). Fields
/// bound to an <see cref="ObjectProperty{TObject, TValue}"/> edit every object shown at once and show "—" where they differ (ADR-255);
/// creatures, heroes, artifacts, spells and skills are chosen with <see cref="PicturePicker"/>s.
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

    /// <summary>The text that stands for values that differ between the objects edited together.</summary>
    public const string Differs = "—";

    /// <summary>The creatures by town and tier, as picker items (<paramref name="where"/> narrows them).</summary>
    public List<PickerItem> CreatureItems(Func<Core.Domain.Creatures.CreatureDefinition, bool>? where = null) => PickerItems.Creatures(Scenario, where);

    /// <summary>The roster's heroes by town, as picker items.</summary>
    public List<PickerItem> HeroItems() => PickerItems.Heroes(Scenario);

    /// <summary>The artifacts by slot (else by tier), as picker items.</summary>
    public List<PickerItem> ArtifactItems(bool bySlot = false) => PickerItems.Artifacts(Scenario, bySlot);

    /// <summary><paramref name="spells"/> by level and school, as picker items.</summary>
    public static List<PickerItem> SpellItems(IEnumerable<SpellDefinition> spells) => PickerItems.Spells(spells);

    /// <summary>The secondary skills by group, as picker items.</summary>
    public static List<PickerItem> SkillItems() => PickerItems.Skills();

    private static string Capital(string text) => PickerItems.Capital(text);

    /// <summary>The objects' one field as a drop-down of <paramref name="items"/>; "—" first while they differ. A pick sets it on all.</summary>
    public Choice<TValue> Choice<TObject, TValue>(
        Container row, string? label, IEnumerable<(string Text, TValue Value)> items, ObjectProperty<TObject, TValue> property, IReadOnlyList<TObject> objects)
        where TObject : MapObject
    {
        var shared = property.Shared(objects);
        var list = items.ToList();
        if (shared.Differs)
        {
            list.Insert(0, (Differs, shared.Value));
        }

        var choice = row.AddChoice(label, list, shared.Value);
        if (shared.Differs)
        {
            choice.Button.Selected = 0;
        }

        choice.Button.ItemSelected += index =>
        {
            if (!shared.Differs || index > 0)
            {
                Edit(() => property.Apply(editor, objects, choice.Value()));
            }
        };
        return choice;
    }

    /// <summary>The objects' owner (no owner first); "—" while they differ.</summary>
    public Choice<PlayerId?> Owner<TObject>(Container row, ObjectProperty<TObject, PlayerId?> property, IReadOnlyList<TObject> objects, string noOwner = "Unowned")
        where TObject : MapObject =>
        Choice(row, "Owner", new[] { (noOwner, (PlayerId?)null) }.Concat(Scenario.Players.Select(p => (p.Name, (PlayerId?)p.Id))), property, objects);

    /// <summary>
    /// The objects' one field as a <see cref="PicturePicker"/> of <paramref name="items"/>, keyed by <paramref name="key"/> and read back
    /// with <paramref name="value"/>; "—" while they differ. A pick sets it on all.
    /// </summary>
    public PicturePicker Picker<TObject, TValue>(
        Container row, string? label, IReadOnlyList<PickerItem> items, ObjectProperty<TObject, TValue> property, IReadOnlyList<TObject> objects,
        Func<TValue, string> key, Func<string, TValue> value)
        where TObject : MapObject
    {
        var shared = property.Shared(objects);
        var picker = PicturePicker.Add(row, label, items, shared.Differs ? null : key(shared.Value));
        picker.Picked += picked => Edit(() => property.Apply(editor, objects, value(picked)));
        return picker;
    }

    /// <summary>
    /// The objects' whole-number field (<paramref name="show"/> turns the value into the box's number, <paramref name="value"/> back);
    /// while they differ the box shows "—" until a number is set.
    /// </summary>
    public SpinBox Spin<TObject, TValue>(
        Container row, string? label, int min, int max, ObjectProperty<TObject, TValue> property, IReadOnlyList<TObject> objects,
        Func<TValue, int> show, Func<int, TValue> value)
        where TObject : MapObject
    {
        var shared = property.Shared(objects);
        var spin = row.AddSpin(label, show(shared.Value), min, max);
        OnNumber(spin, shared.Differs, number => Edit(() => property.Apply(editor, objects, value(number))));
        return spin;
    }

    /// <summary>
    /// Calls <paramref name="apply"/> with each number the box is set to. While <paramref name="differs"/> the box shows "—" and stands
    /// for no number: a number typed there is applied on Enter or when the box is left, even one equal to the first object's value that
    /// the box holds underneath (which a value change would not report); leaving it at "—" applies nothing.
    /// </summary>
    public static void OnNumber(SpinBox spin, bool differs, Action<int> apply)
    {
        spin.ValueChanged += number => apply((int)number);
        if (!differs)
        {
            return;
        }

        ShowDiffers(spin);
        var line = spin.GetLineEdit();
        var (min, max) = ((int)spin.MinValue, (int)spin.MaxValue);
        string? typed = null;
        line.TextChanged += text => typed = text; // only the user's typing, not the box's own text
        void Typed()
        {
            if (int.TryParse(typed?.Trim(), out var number))
            {
                typed = null;
                apply(Math.Clamp(number, min, max));
            }
        }

        line.TextSubmitted += _ => Typed();
        line.FocusExited += Typed;
    }

    /// <summary>The objects' yes-or-no field; while they differ its text ends in "(—)" and a click sets it on all.</summary>
    public CheckBox Check<TObject>(Container row, string text, ObjectProperty<TObject, bool> property, IReadOnlyList<TObject> objects)
        where TObject : MapObject
    {
        var shared = property.Shared(objects);
        var box = new CheckBox { Text = shared.Differs ? $"{text} ({Differs})" : text, ButtonPressed = shared.Value && !shared.Differs };
        box.Toggled += on => Edit(() => property.Apply(editor, objects, on));
        row.AddChild(box);
        return box;
    }

    /// <summary>The objects' text field, set when Enter is pressed or the field is left; empty with "—" as its hint while they differ.</summary>
    public LineEdit Text<TObject>(Container row, string? label, ObjectProperty<TObject, string> property, IReadOnlyList<TObject> objects, string hint, int width)
        where TObject : MapObject
    {
        if (label is not null)
        {
            row.AddChild(new Label { Text = label });
        }

        var shared = property.Shared(objects);
        var text = new LineEdit
        {
            Text = shared.Differs ? "" : shared.Value, PlaceholderText = shared.Differs ? Differs : hint, CustomMinimumSize = new Vector2(width, 0),
        };
        void Apply(string value)
        {
            if (!shared.Differs || value.Length > 0)
            {
                Edit(() => property.Apply(editor, objects, value));
            }
        }

        text.TextSubmitted += Apply;
        text.FocusExited += () => Apply(text.Text);
        row.AddChild(text);
        return text;
    }

    /// <summary>A number box that stands for values that differ: it shows "—" until a number is typed or stepped to.</summary>
    public static void ShowDiffers(SpinBox spin)
    {
        var line = spin.GetLineEdit();
        spin.Ready += () => Callable.From(() => line.Text = Differs).CallDeferred();
    }

    /// <summary>
    /// The one object a field for a single object stands for, or null when several are shown: then a note in the current part says the
    /// field is edited one object at a time.
    /// </summary>
    public T? Single<T>(IReadOnlyList<T> objects, string what)
        where T : MapObject
    {
        if (objects.Count == 1)
        {
            return objects[0];
        }

        Box.AddChild(new Label { Text = $"{Capital(what)}: select one to edit", Modulate = UiTheme.TextQuiet });
        return null;
    }

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
    public void EventChoice(string label, MapEventKind kind, string? current, Func<string?, EditResult> set) =>
        EventChoice(label, kind, new SharedValue<string?>(false, current), set);

    /// <summary><see cref="EventChoice(string, MapEventKind, string?, Func{string?, EditResult})"/> for several objects: "—" while they differ.</summary>
    public void EventChoice(string label, MapEventKind kind, SharedValue<string?> shared, Func<string?, EditResult> set)
    {
        var current = shared.Differs ? null : shared.Value;
        var names = Scenario.MapEvents.Set.Events.Where(e => e.Kind == kind).Select(e => e.Name).ToList();
        if (current is not null && !names.Contains(current))
        {
            names.Add(current); // a name the map does not have: shown so it can be cleared (the validator or the loader names it)
        }

        var row = new HBoxContainer();
        In(InspectorPart.Events).AddChild(row);
        var items = new[] { ("— none —", "") }.Concat(names.Select(n => (n, n))).ToList();
        if (shared.Differs)
        {
            items.Insert(0, (Differs, Differs));
        }

        var choice = row.AddChoice(label, items, shared.Differs ? Differs : current ?? "");
        choice.Button.ItemSelected += _ =>
        {
            if (choice.Value() != Differs)
            {
                Edit(() => set(choice.Value().Length == 0 ? null : choice.Value()));
            }
        };
        var create = new Button { Text = "New" }.WithIcon(UiIcon.Link, keepText: true);
        create.Pressed += () => Edit(() => NewEvent(kind, set));
        row.AddChild(create);
        if (current is not null)
        {
            var open = new Button { Text = "Edit" }.WithIcon(UiIcon.Link, keepText: true);
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
        var creatures = ArmyItems();
        for (var slot = 0; slot < Army.SlotCount; slot++)
        {
            var slotRow = new HBoxContainer();
            box.AddChild(slotRow);
            var stack = troops[slot];
            var creature = PicturePicker.Add(slotRow, $"{slot + 1}", creatures, stack?.Creature.Value ?? "");
            var count = slotRow.AddSpin("×", stack?.Count ?? 1, 1, 9999);
            var index = slot;
            void ApplySlot() => Edit(() => editor.Towns.SetGarrisonSlot(
                holder, index, creature.Value() is { Length: > 0 } id ? new CreatureStack(id, (int)count.Value) : null));
            creature.Picked += _ => ApplySlot();
            count.ValueChanged += _ => ApplySlot();
        }
    }

    /// <summary>The creatures for an army slot, "— empty —" first.</summary>
    public List<PickerItem> ArmyItems() => [PickerItem.None("— empty —"), .. CreatureItems()];

    /// <summary>
    /// Rows of creature stacks (a box's gifts or guard): one per stack and, while there are fewer than seven, an empty one to add another;
    /// "— none —" removes a stack. <paramref name="changed"/> is called after any change; read the rows with <see cref="Stacks"/>.
    /// </summary>
    public List<(PicturePicker Creature, SpinBox Count)> StackRows(VBoxContainer panel, string title, IReadOnlyList<CreatureStack> stacks, Action changed)
    {
        panel.AddChild(new Label { Text = title });
        List<PickerItem> items = [PickerItem.None("— none —"), .. CreatureItems()];
        var shown = stacks.Count < Army.SlotCount ? stacks.Append(null) : stacks.Cast<CreatureStack?>();
        return shown.Select(stack =>
        {
            var row = new HBoxContainer();
            panel.AddChild(row);
            var creature = PicturePicker.Add(row, null, items, stack?.Creature.Value ?? "");
            var count = row.AddSpin("×", stack?.Count ?? 1, 1, 99999);
            creature.Picked += _ => changed();
            count.ValueChanged += _ => changed();
            return (creature, count);
        }).ToList();
    }

    /// <summary>The stacks <see cref="StackRows"/> show.</summary>
    public static List<CreatureStack> Stacks(List<(PicturePicker Creature, SpinBox Count)> rows) =>
        [.. rows.Where(r => r.Creature.Value().Length > 0).Select(r => new CreatureStack(r.Creature.Value(), (int)r.Count.Value))];
}
