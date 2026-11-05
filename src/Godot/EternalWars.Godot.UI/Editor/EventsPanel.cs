using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.MapEvents;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Editor;

/// <summary>
/// The editor's "Events" tab (ADR-115, EVT-6): the map's events in run order (new, from a simple form, up, down, delete), the chosen event's name,
/// whether it starts enabled, its towns or cells, and its tree. A node of the tree is edited as the text the map file writes
/// (<see cref="IMapEventNodeText"/>); actions come from a menu of templates (<see cref="MapEventTemplates"/>). Below: the map variables and the
/// validator's findings for the event. Every change is an undoable <see cref="MapEditorSession"/> edit.
/// </summary>
public partial class EventsPanel : ScrollContainer
{
    private static readonly Color ErrorColour = new(1f, 0.45f, 0.4f);
    private static readonly Color WarningColour = new(1f, 0.85f, 0.4f);

    private readonly VBoxContainer _box = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private readonly Dictionary<ulong, Node> _nodes = [];
    private MapEditorSession _editor = null!;
    private IMapEventNodeText _nodeText = null!;
    private string? _selected;
    private Node? _node;
    private string? _form;
    private TextEdit? _text;
    private Label? _textError;

    /// <summary>A node of the tree: list <see cref="List"/> of the action at <see cref="Parent"/> (empty: the event), and an action in it, or the list itself.</summary>
    private sealed record Node(IReadOnlyList<EventStep> Parent, int List, int? Index)
    {
        public IReadOnlyList<EventStep>? Path => Index is { } index ? [.. Parent, new EventStep(List, index)] : null;
    }

    /// <summary>The hero event whose cells a click on the map places or removes, or null when clicks use the tools.</summary>
    public string? PlacingCells { get; private set; }

    /// <summary>The selected event changed (the map highlights its cells).</summary>
    public event Action<string?>? SelectionChanged;

    /// <summary>"Show" on a cell or object: the camera goes there.</summary>
    public event Action<MapPosition>? ShowRequested;

    public void Bind(MapEditorSession editor, IMapEventNodeText nodeText)
    {
        (_editor, _nodeText) = (editor, nodeText);
        HorizontalScrollMode = ScrollMode.Disabled;
        AddChild(_box);
        Refresh();
        this.SubscribeWhileInTree<UndoHistoryChanged>(editor.Events, _ => Later());
    }

    /// <summary>Opens an event (the inspector's and the validator's links).</summary>
    public void Select(string name)
    {
        (_selected, _node, _form) = (name, null, null);
        SelectionChanged?.Invoke(name);
        Later();
    }

    /// <summary>A click on the map in cell mode: places a cell of the event there, or removes the cell of that event that is there.</summary>
    public EditResult ClickCell(MapPosition tile)
    {
        if (PlacingCells is not { } name)
        {
            return EditResult.InvalidTarget;
        }

        var existing = _editor.Scenario.MapEvents.Set.Cells.FirstOrDefault(c => c.Position == tile);
        return _editor.SetEventCell(tile, existing?.Event == name ? null : name);
    }

    private void Later() => Callable.From(Refresh).CallDeferred();

    private void Refresh()
    {
        foreach (var child in _box.GetChildren())
        {
            _box.RemoveChild(child);
            child.QueueFree();
        }

        _nodes.Clear();
        (_text, _textError) = (null, null);
        var set = _editor.Scenario.MapEvents.Set;
        if (_selected is not null && set.Find(_selected) is null)
        {
            (_selected, _node) = (null, null);
            SelectionChanged?.Invoke(null);
        }

        if (PlacingCells is not null && set.Find(PlacingCells) is not { Kind: MapEventKind.Hero })
        {
            PlacingCells = null;
        }

        ShowList(set);
        if (_form is not null)
        {
            ShowForm(_form);
        }
        else if (_selected is not null && set.Find(_selected) is { } definition)
        {
            ShowEvent(definition);
        }

        ShowVariables(set);
    }

    // The list of events ------------------------------------------------------------------------------------------------

    private void ShowList(MapEventSet set)
    {
        Heading("Events (they run in this order)");
        var issues = MapValidator.Validate(_editor.Scenario).Where(i => i.Event is not null).ToList();
        var list = new ItemList { CustomMinimumSize = new Vector2(0, 120), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _box.AddChild(list);
        foreach (var definition in set.Events)
        {
            var mine = issues.Where(i => i.Event == definition.Name).ToList();
            var index = list.AddItem(MapEventText.Describe(definition) + (mine.Count == 0 ? "" : $"  ⚠ {mine.Count}"));
            if (mine.Count > 0)
            {
                list.SetItemCustomFgColor(index, mine.Any(i => i.Severity == IssueSeverity.Error) ? ErrorColour : WarningColour);
            }

            if (definition.Name == _selected)
            {
                list.Select(index);
            }
        }

        if (set.Events.Count == 0)
        {
            list.AddItem("No events yet: add one with New.");
        }

        list.ItemSelected += index =>
        {
            if (index < set.Events.Count)
            {
                Select(set.Events[(int)index].Name);
            }
        };

        var row = Row();
        var add = Menu(row, "New ▾",
        [
            "Hero event (cells, boxes)", "Player event (turn start)", "Town event (turn start)", "Quest (quest objects)", null,
            "Timed event (form)…", "Event cell (form)…", "Town event (form)…", "Quest (form)…",
        ]);
        add.IdPressed += id =>
        {
            if (id < 4)
            {
                var kind = (MapEventKind[])[MapEventKind.Hero, MapEventKind.Player, MapEventKind.Town, MapEventKind.Quest];
                var name = _editor.UnusedEventName(kind[id].ToString().ToLowerInvariant());
                if (_editor.AddMapEvent(MapEventTemplates.Blank(name, kind[id])) == EditResult.Ok)
                {
                    Select(name);
                }
            }
            else
            {
                _form = (string[])["timed", "cell", "town", "quest"] is var forms ? forms[id - 5] : null;
                Later();
            }
        };
        if (_selected is { } selected)
        {
            Button(row, "Up", () => _editor.MoveMapEvent(selected, -1));
            Button(row, "Down", () => _editor.MoveMapEvent(selected, 1));
            Button(row, "Delete", () => _editor.RemoveMapEvent(selected));
        }
    }

    // One event -----------------------------------------------------------------------------------------------------

    private void ShowEvent(MapEventDefinition definition)
    {
        var name = definition.Name;
        Heading($"{definition.Kind} event '{name}'");
        var row = Row();
        row.AddChild(new Label { Text = "Name" });
        var rename = new LineEdit { Text = name, CustomMinimumSize = new Vector2(180, 0) };
        rename.TextSubmitted += text =>
        {
            text = text.Trim();
            if (text != name && _editor.ReplaceMapEvent(name, definition with { Name = text }) == EditResult.Ok)
            {
                Select(text);
            }
        };
        row.AddChild(rename);
        var enabled = new CheckBox { Text = "Enabled at the start", ButtonPressed = definition.Enabled };
        enabled.Toggled += on => _editor.ReplaceMapEvent(name, definition with { Enabled = on });
        row.AddChild(enabled);

        switch (definition.Kind)
        {
            case MapEventKind.Hero:
                ShowCells(definition);
                break;
            case MapEventKind.Town:
                ShowTowns(definition);
                break;
            case MapEventKind.Quest:
                ShowQuestObjects(definition);
                break;
            default:
                _box.AddChild(new Label { Text = "Runs at the start of every player's turn; who it is for is a condition (an If)." });
                break;
        }

        ShowTree(definition);
        ShowNodeEditor(definition);

        foreach (var issue in MapValidator.Validate(_editor.Scenario).Where(i => i.Event == name))
        {
            _box.AddChild(new Label
            {
                Text = $"{issue.Severity}: {issue.Message}",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                Modulate = issue.Severity == IssueSeverity.Error ? ErrorColour : WarningColour,
            });
        }
    }

    private void ShowCells(MapEventDefinition definition)
    {
        var name = definition.Name;
        var cells = _editor.Scenario.MapEvents.Set.Cells.Where(c => c.Event == name).ToList();
        var boxes = _editor.Scenario.ObjectsOf<PandorasBox>().Where(b => b.Event == name).ToList();
        var row = Row();
        row.AddChild(new Label { Text = $"Cells: {cells.Count}{(boxes.Count == 0 ? "" : $"; run by {boxes.Count} Pandora's box(es)")}" });
        var place = new CheckButton { Text = "Place cells (click the map)", ButtonPressed = PlacingCells == name };
        place.Toggled += on =>
        {
            PlacingCells = on ? name : null;
            Later();
        };
        row.AddChild(place);
        if (cells.Count > 0)
        {
            var next = 0;
            Button(row, "Show a cell", () => ShowRequested?.Invoke(cells[next++ % cells.Count].Position));
        }
    }

    private void ShowTowns(MapEventDefinition definition)
    {
        _box.AddChild(new Label { Text = "Runs at the start of the owner's turn in these towns (neutral towns: at the start of the day):" });
        var flow = new HFlowContainer();
        _box.AddChild(flow);
        var towns = _editor.Scenario.ObjectsOf<Town>().OrderBy(t => t.Id.Value).ToList();
        foreach (var town in towns)
        {
            var id = town.Id.Value;
            var box = new CheckBox { Text = $"{town.Name} {town.VisitPosition}", ButtonPressed = definition.Towns.Contains(id) };
            box.Toggled += on => _editor.ReplaceMapEvent(definition.Name, definition with
            {
                Towns = on ? [.. definition.Towns, id] : [.. definition.Towns.Where(t => t != id)],
            });
            flow.AddChild(box);
        }

        if (towns.Count == 0)
        {
            flow.AddChild(new Label { Text = "(the map has no towns)" });
        }
    }

    private void ShowQuestObjects(MapEventDefinition definition)
    {
        var sites = _editor.Scenario.ObjectsOf<QuestSite>().Where(q => q.Quest == definition.Name).ToList();
        var row = Row();
        row.AddChild(new Label
        {
            Text = sites.Count == 0 ? "No quest object runs it: pick it in a Seer's Hut's, Quest Guard's or Quest Gate's inspector." : $"The quest of the {sites[0].Definition.Name} at {sites[0].VisitPosition}",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        });
        if (sites.Count > 0)
        {
            Button(row, "Show", () => ShowRequested?.Invoke(sites[0].VisitPosition));
        }

        Button(row, "Add stage", () => _editor.ReplaceMapEvent(definition.Name, definition with
        {
            Stages = [.. definition.Stages, QuestForms.Level(definition.Stages.Count + 2, $"Come back when you have reached level {definition.Stages.Count + 2}.")],
        }));
    }

    // The tree ------------------------------------------------------------------------------------------------------

    private void ShowTree(MapEventDefinition definition)
    {
        var tree = new Tree { HideRoot = true, CustomMinimumSize = new Vector2(0, 220), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _box.AddChild(tree);
        var root = tree.CreateItem();
        var names = MapEventTreeEdit.ListNames(definition);
        var lists = MapEventTreeEdit.Lists(definition);
        TreeItem? selected = null;
        for (var i = 0; i < lists.Count; i++)
        {
            var header = Item(tree, root, names[i] + (definition.Kind == MapEventKind.Quest ? $": {MapEventText.Describe(definition.Stages[i].Requirement, _editor.Scenario)}" : ""), new Node([], i, null), ref selected);
            header.SetCustomColor(0, new Color(0.7f, 0.85f, 1f));
            AddActions(tree, header, [], i, lists[i], ref selected);
        }

        if (lists.Count == 0)
        {
            Item(tree, root, "(no stages: add one)", new Node([], -1, null), ref selected);
        }

        selected?.Select(0);
        tree.ItemSelected += () =>
        {
            if (tree.GetSelected() is { } item && _nodes.TryGetValue(item.GetInstanceId(), out var node) && node != _node)
            {
                _node = node;
                FillText(definition);
            }
        };

        var row = Row();
        var actions = MapEventTemplates.Actions(_editor.Scenario, _editor.Scenario.MapEvents.Set.Events.FirstOrDefault(e => e.Name != definition.Name && e.Kind != MapEventKind.Quest)?.Name,
            _editor.Scenario.MapEvents.Set.Variables.Keys.Order(StringComparer.Ordinal).FirstOrDefault());
        var add = Menu(row, "Add action ▾", [.. actions.Select(a => a.Label)]);
        add.IdPressed += id => AddAction(definition, actions[(int)id].Action);
        Button(row, "Up", () => MoveNode(definition, -1));
        Button(row, "Down", () => MoveNode(definition, 1));
        Button(row, "Delete", () => DeleteNode(definition));
    }

    private void AddActions(Tree tree, TreeItem parent, IReadOnlyList<EventStep> at, int list, IReadOnlyList<EventAction> actions, ref TreeItem? selected)
    {
        for (var i = 0; i < actions.Count; i++)
        {
            var action = actions[i];
            var item = Item(tree, parent, MapEventText.Describe(action, _editor.Scenario), new Node(at, list, i), ref selected);
            var inner = MapEventTreeEdit.Lists(action);
            var names = MapEventTreeEdit.ListNames(action);
            IReadOnlyList<EventStep> path = [.. at, new EventStep(list, i)];
            for (var l = 0; l < inner.Count; l++)
            {
                var header = Item(tree, item, names[l], new Node(path, l, null), ref selected);
                header.SetCustomColor(0, new Color(0.7f, 0.85f, 1f));
                AddActions(tree, header, path, l, inner[l], ref selected);
            }
        }
    }

    private TreeItem Item(Tree tree, TreeItem parent, string text, Node node, ref TreeItem? selected)
    {
        var item = tree.CreateItem(parent);
        item.SetText(0, text);
        _nodes[item.GetInstanceId()] = node;
        if (node == _node)
        {
            selected = item;
        }

        return item;
    }

    private void AddAction(MapEventDefinition definition, EventAction action)
    {
        var lists = MapEventTreeEdit.Lists(definition);
        if (lists.Count == 0)
        {
            return;
        }

        // Into the chosen list (at its end), after the chosen action, else at the end of the event's first list.
        var (parent, list, index) = _node switch
        {
            { Index: { } i } node => (node.Parent, node.List, i + 1),
            { List: >= 0 } node => (node.Parent, node.List, Count(definition, node.Parent, node.List)),
            _ => ([], 0, lists[0].Count),
        };
        if (_editor.ReplaceMapEvent(definition.Name, MapEventTreeEdit.Insert(definition, parent, list, index, action)) == EditResult.Ok)
        {
            _node = new Node(parent, list, index);
        }
    }

    private static int Count(MapEventDefinition definition, IReadOnlyList<EventStep> parent, int list) =>
        (parent.Count == 0 ? MapEventTreeEdit.Lists(definition) : MapEventTreeEdit.Lists(MapEventTreeEdit.Get(definition, parent)!))[list].Count;

    private void MoveNode(MapEventDefinition definition, int offset)
    {
        if (_node?.Path is { } path && _editor.ReplaceMapEvent(definition.Name, MapEventTreeEdit.Move(definition, path, offset)) == EditResult.Ok)
        {
            _node = _node with { Index = MapEventTreeEdit.Moved(definition, path, offset)[^1].Index };
        }
    }

    private void DeleteNode(MapEventDefinition definition)
    {
        if (_node?.Path is { } path)
        {
            _editor.ReplaceMapEvent(definition.Name, MapEventTreeEdit.Remove(definition, path));
            _node = null;
        }
        else if (definition.Kind == MapEventKind.Quest && _node is { Parent.Count: 0, List: >= 0 } stage && stage.List < definition.Stages.Count)
        {
            _editor.ReplaceMapEvent(definition.Name, MapEventTreeEdit.SetStage(definition, stage.List, null));
            _node = null;
        }
    }

    // Editing a node as text ---------------------------------------------------------------------------------------

    private void ShowNodeEditor(MapEventDefinition definition)
    {
        _text = new TextEdit { CustomMinimumSize = new Vector2(0, 170), WrapMode = TextEdit.LineWrappingMode.Boundary, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _box.AddChild(_text);
        var row = Row();
        Button(row, "Apply", () => ApplyText(definition));
        var conditions = MapEventTemplates.Conditions(_editor.Scenario.MapEvents.Set.Variables.Keys.Order(StringComparer.Ordinal).FirstOrDefault());
        var insert = Menu(row, "Insert condition ▾", [.. conditions.Select(c => c.Label)]);
        insert.IdPressed += id => _text?.InsertTextAtCaret(_nodeText.Write(conditions[(int)id].Condition));
        _textError = new Label { Modulate = ErrorColour, AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddChild(_textError);
        FillText(definition);
    }

    /// <summary>The chosen action (or quest stage, without its actions) as the map file writes it.</summary>
    private void FillText(MapEventDefinition definition)
    {
        if (_text is null || _textError is null)
        {
            return;
        }

        _textError.Text = "";
        (_text.Text, _text.Editable) = _node switch
        {
            { Path: { } path } when MapEventTreeEdit.Get(definition, path) is { } action => (_nodeText.Write(action), true),
            { Parent.Count: 0, List: >= 0 } stage when definition.Kind == MapEventKind.Quest && stage.List < definition.Stages.Count =>
                (_nodeText.Write(definition.Stages[stage.List]), true),
            _ => ("Choose an action in the tree to edit it here (a quest's stage: its requirement and texts), or add one with \"Add action\".", false),
        };
    }

    private void ApplyText(MapEventDefinition definition)
    {
        if (_text is not { Editable: true } text || _textError is null)
        {
            return;
        }

        try
        {
            var edited = _node switch
            {
                { Path: { } path } => MapEventTreeEdit.Replace(definition, path, _nodeText.ReadAction(text.Text)),
                { List: var stage } => MapEventTreeEdit.SetStage(definition, stage, _nodeText.ReadStage(text.Text, definition.Stages[stage].Actions)),
                _ => definition,
            };
            _editor.ReplaceMapEvent(definition.Name, edited);
        }
        catch (FormatException e)
        {
            _textError.Text = e.Message;
        }
    }

    // Variables -----------------------------------------------------------------------------------------------------

    private void ShowVariables(MapEventSet set)
    {
        Heading("Map variables (whole numbers, with their starting values)");
        foreach (var (name, value) in set.Variables.OrderBy(v => v.Key, StringComparer.Ordinal))
        {
            var row = Row();
            row.AddChild(new Label { Text = name, CustomMinimumSize = new Vector2(160, 0) });
            var spin = Spin(row, value, int.MinValue / 2, int.MaxValue / 2);
            spin.ValueChanged += v => _editor.SetMapVariable(name, (int)v);
            Button(row, "Remove", () => _editor.RemoveMapVariable(name));
        }

        var add = Row();
        var newName = new LineEdit { PlaceholderText = "new variable", CustomMinimumSize = new Vector2(160, 0) };
        add.AddChild(newName);
        var start = Spin(add, 0, int.MinValue / 2, int.MaxValue / 2);
        Button(add, "Add", () => _editor.SetMapVariable(newName.Text, (int)start.Value));
    }

    // Simple event forms --------------------------------------------------------------------------------------------

    /// <summary>A form that writes a new event (HoMM3's timed events, event cells, town events and quests, ADR-115); the result is an ordinary tree.</summary>
    private void ShowForm(string form)
    {
        var scenario = _editor.Scenario;
        Heading(form switch
        {
            "timed" => "New timed event (a player event)",
            "cell" => "New event cell (a hero event; click the map to place its cells afterwards)",
            "town" => "New town event",
            _ => "New quest (give it to a quest object in that object's inspector)",
        });
        var row = Row();
        row.AddChild(new Label { Text = "Name" });
        var name = new LineEdit { Text = _editor.UnusedEventName(form), CustomMinimumSize = new Vector2(180, 0) };
        row.AddChild(name);

        var message = new TextEdit
        {
            PlaceholderText = form == "quest" ? "What the quest asks (its proposal)" : "Message (empty: none; the rewards are given anyway)",
            CustomMinimumSize = new Vector2(0, 60),
            WrapMode = TextEdit.LineWrappingMode.Boundary,
        };
        _box.AddChild(message);

        SpinBox? first = null, every = null;
        if (form is "timed" or "town")
        {
            var days = Row();
            days.AddChild(new Label { Text = "First day" });
            first = Spin(days, 1, 1, 9999);
            days.AddChild(new Label { Text = "then every (days, 0 = once)" });
            every = Spin(days, 0, 0, 999);
        }

        List<CheckBox> players = [];
        CheckBox? people = null, computers = null, once = null;
        if (form != "quest")
        {
            var who = new HFlowContainer();
            _box.AddChild(who);
            who.AddChild(new Label { Text = "For (none ticked: every player)" });
            players = [.. scenario.Players.Select(p => new CheckBox { Text = p.Name })];
            players.ForEach(p => who.AddChild(p));
            people = new CheckBox { Text = "people", ButtonPressed = true };
            computers = new CheckBox { Text = "computers", ButtonPressed = form == "town" };
            who.AddChild(people);
            who.AddChild(computers);
            if (form == "cell")
            {
                once = new CheckBox { Text = "only once", ButtonPressed = true };
                who.AddChild(once);
            }
        }

        var towns = new List<(CheckBox Box, int Id)>();
        LineEdit? buildings = null;
        OptionButton? line = null;
        SpinBox? growth = null;
        var lines = scenario.Creatures.All.Where(c => !scenario.Creatures.IsUpgrade(c.Id)).OrderBy(c => c.Town).ThenBy(c => c.Tier).ToList();
        if (form == "town")
        {
            var flow = new HFlowContainer();
            _box.AddChild(flow);
            flow.AddChild(new Label { Text = "Towns" });
            foreach (var town in scenario.ObjectsOf<Town>().OrderBy(t => t.Id.Value))
            {
                var box = new CheckBox { Text = town.Name };
                flow.AddChild(box);
                towns.Add((box, town.Id.Value));
            }

            var build = Row();
            build.AddChild(new Label { Text = "Build (ids, comma-separated)" });
            buildings = new LineEdit { PlaceholderText = "tavern, marketplace", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            build.AddChild(buildings);
            var hire = Row();
            hire.AddChild(new Label { Text = "More to hire" });
            line = Options(hire, [.. lines.Select(c => $"{c.Name} ({c.Town})")]);
            growth = Spin(hire, 0, -999, 999);
        }

        OptionButton? questKind = null;
        SpinBox? amount = null;
        LineEdit? questId = null;
        var questKinds = (string[])["Reach a level", "Bring gold", "Bring creatures (id, count)", "Bring an artifact (id)", "Beat a monster (object id)",
            "Beat a hero (hero id)", "Be the roster hero (id)", "Be a player (slot 1…)"];
        if (form == "quest")
        {
            var ask = Row();
            ask.AddChild(new Label { Text = "Asks" });
            questKind = Options(ask, questKinds);
            amount = Spin(ask, 5, 0, 999_999);
            questId = new LineEdit { PlaceholderText = "id", CustomMinimumSize = new Vector2(150, 0) };
            ask.AddChild(questId);
        }

        _box.AddChild(new Label { Text = form == "quest" ? "Rewards for completing it" : "Rewards (negative resources and creatures take)" });
        var rewards = new GridContainer { Columns = 6 };
        _box.AddChild(rewards);
        var resources = Enum.GetValues<ResourceType>().Select(r => (r, Spin(rewards, 0, -99_999, 99_999, r.ToString()))).ToList();
        var experience = Spin(rewards, 0, 0, 999_999, "Experience");
        var attack = Spin(rewards, 0, 0, 99, "Attack");
        var defense = Spin(rewards, 0, 0, 99, "Defense");
        var power = Spin(rewards, 0, 0, 99, "Power");
        var knowledge = Spin(rewards, 0, 0, 99, "Knowledge");
        var morale = Spin(rewards, 0, -3, 3, "Morale");
        var luck = Spin(rewards, 0, -3, 3, "Luck");
        var creatureRow = Row();
        creatureRow.AddChild(new Label { Text = "Creatures" });
        var creatures = scenario.Creatures.All.OrderBy(c => c.Town).ThenBy(c => c.Tier).ThenBy(c => c.Id, StringComparer.Ordinal).ToList();
        var creature = Options(creatureRow, [.. creatures.Select(c => $"{c.Name} ({c.Town})")]);
        var count = Spin(creatureRow, 0, -9999, 9999);
        creatureRow.AddChild(new Label { Text = "Artifact" });
        var artifacts = scenario.Artifacts.All.OrderBy(a => a.Name).ToList();
        var artifact = Options(creatureRow, ["— none —", .. artifacts.Select(a => a.Name)]);

        var buttons = Row();
        var problem = new Label { Modulate = ErrorColour };
        Button(buttons, "Create", () =>
        {
            var reward = new SimpleRewards
            {
                Resources = resources.Where(r => r.Item2.Value != 0).ToDictionary(r => r.r, r => (int)r.Item2.Value),
                Experience = (int)experience.Value,
                Attack = (int)attack.Value,
                Defense = (int)defense.Value,
                SpellPower = (int)power.Value,
                Knowledge = (int)knowledge.Value,
                Morale = (int)morale.Value,
                Luck = (int)luck.Value,
                Creatures = count.Value == 0 || creatures.Count == 0 ? [] : [new CreatureStack(creatures[creature.Selected].Id, (int)count.Value)],
                Artifacts = artifact.Selected <= 0 ? [] : [artifacts[artifact.Selected - 1].Id],
            };
            var who = new EventTriggers([.. players.Select((p, i) => (p, i)).Where(p => p.p.ButtonPressed).Select(p => p.i)],
                people?.ButtonPressed ?? true, computers?.ButtonPressed ?? false);
            var eventName = name.Text.Trim();
            MapEventDefinition definition;
            try
            {
                definition = form switch
                {
                    "timed" => SimpleEventForms.Timed(eventName, message.Text, (int)first!.Value, (int)every!.Value, who, reward),
                    "cell" => SimpleEventForms.Cell(eventName, message.Text, who, once!.ButtonPressed, reward),
                    "town" => SimpleEventForms.Town(eventName, [.. towns.Where(t => t.Box.ButtonPressed).Select(t => t.Id)], message.Text, (int)first!.Value,
                        (int)every!.Value, who, reward,
                        [.. buildings!.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)],
                        growth!.Value == 0 || lines.Count == 0 ? new Dictionary<string, int>() : new Dictionary<string, int> { [lines[line!.Selected].Id] = (int)growth.Value }),
                    _ => new MapEventDefinition(eventName, MapEventKind.Quest, [])
                    {
                        Stages = [Quest(questKind!.Selected, (int)amount!.Value, questId!.Text.Trim(), message.Text, reward)],
                    },
                };
            }
            catch (FormatException e)
            {
                problem.Text = e.Message;
                return;
            }

            if (_editor.AddMapEvent(definition) is not EditResult.Ok)
            {
                problem.Text = "The name is empty or another event has it.";
                return;
            }

            _form = null;
            PlacingCells = form == "cell" ? eventName : null;
            Select(eventName);
        });
        Button(buttons, "Cancel", () =>
        {
            _form = null;
            Later();
        });
        buttons.AddChild(problem);
    }

    /// <summary>The quest form's stage (<see cref="QuestForms"/>): what it asks, the proposal and the rewards.</summary>
    private static QuestStage Quest(int kind, int amount, string id, string proposal, SimpleRewards rewards)
    {
        proposal = string.IsNullOrWhiteSpace(proposal) ? "Help me, traveller." : proposal;
        var given = rewards.Actions().ToArray();
        int Number() => int.TryParse(id, out var n) ? n : throw new FormatException($"'{id}' is not a number.");
        string Id() => id.Length > 0 ? id : throw new FormatException("Give the id.");
        return kind switch
        {
            0 => QuestForms.Level(Math.Max(1, amount), proposal, given),
            1 => QuestForms.Resources(new ResourceSet(Gold: amount), proposal, given),
            2 => QuestForms.Creatures([new CreatureStack(Id(), Math.Max(1, amount))], proposal, given),
            3 => QuestForms.Artifacts([Id()], proposal, given),
            4 => QuestForms.DefeatMonster(Number(), proposal, given),
            5 => QuestForms.DefeatHero(Number(), proposal, given),
            6 => QuestForms.BeHero(Id(), proposal, given),
            _ => QuestForms.BePlayer(Math.Max(1, amount) - 1, proposal, given),
        };
    }

    // Controls ------------------------------------------------------------------------------------------------------

    private void Heading(string text)
    {
        _box.AddChild(new HSeparator());
        _box.AddChild(new Label { Text = text, Modulate = new Color(0.95f, 0.85f, 0.6f) });
    }

    private HBoxContainer Row()
    {
        var row = new HBoxContainer();
        _box.AddChild(row);
        return row;
    }

    private static void Button(Container row, string text, Func<EditResult> edit) => Button(row, text, () =>
    {
        if (edit() is not EditResult.Ok and var result)
        {
            GD.PushWarning($"Edit refused: {result}");
        }
    });

    private static void Button(Container row, string text, Action pressed)
    {
        var button = new global::Godot.Button { Text = text };
        button.Pressed += pressed;
        row.AddChild(button);
    }

    /// <summary>A drop-down menu; a null entry is a separator. Item ids are the entries' indexes.</summary>
    private static PopupMenu Menu(Container row, string text, IReadOnlyList<string?> items)
    {
        var menu = new MenuButton { Text = text, Flat = false };
        row.AddChild(menu);
        var popup = menu.GetPopup();
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i] is { } item)
            {
                popup.AddItem(item, i);
            }
            else
            {
                popup.AddSeparator();
            }
        }

        return popup;
    }

    private static OptionButton Options(Container row, IReadOnlyList<string> items)
    {
        var button = new OptionButton { FitToLongestItem = false, CustomMinimumSize = new Vector2(150, 0) };
        foreach (var item in items)
        {
            button.AddItem(item);
        }

        row.AddChild(button);
        return button;
    }

    private static SpinBox Spin(Container row, int value, int min, int max, string? label = null)
    {
        if (label is not null)
        {
            row.AddChild(new Label { Text = label });
        }

        var spin = new SpinBox { MinValue = min, MaxValue = max, Value = value, AllowGreater = false };
        row.AddChild(spin);
        return spin;
    }
}
