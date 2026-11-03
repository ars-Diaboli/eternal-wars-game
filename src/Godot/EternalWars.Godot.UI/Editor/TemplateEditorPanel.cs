using System.Globalization;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Generation;
using EternalWars.Godot.Shared.Events;
using EternalWars.Godot.UI.Hud;
using Godot;

namespace EternalWars.Godot.UI.Editor;

/// <summary>A template the editor can open: its name and how to read it (a shipped one or a file in the user's templates folder).</summary>
public sealed record TemplateSource(string Name, Func<MapTemplate> Open);

/// <summary>
/// What the app does for the template editor: the templates it can open, saving one (returns a message), and generating a map from one in the map
/// editor (returns null when the map is open, else why not).
/// </summary>
public sealed record TemplateEditorHost(
    Func<IReadOnlyList<TemplateSource>> Templates,
    Func<MapTemplate, string> Save,
    Func<MapTemplate, int, ulong, string?> Generate);

/// <summary>
/// The template editor (ADR-070 RMG-10, ADR-117), like HotA's: the zone graph on the left (<see cref="TemplateGraphView"/>), on the right the
/// settings of the selected zone or connection, or of the template when nothing is selected, and under the graph the validator's issues, live.
/// Every control applies at once through the <see cref="TemplateEditorSession"/> (no undo, the owner's answer). The top row: New, open a template,
/// Save (the user's templates folder), add zones, arrange the graph, and Generate (size and seed), which opens the result in the map editor.
/// Escape or Close hides it.
/// </summary>
public partial class TemplateEditorPanel : PanelContainer
{
    private readonly TemplateGraphView _graph = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
    private readonly VBoxContainer _inspector = new() { CustomMinimumSize = new Vector2(430, 0) };
    private readonly ItemList _issues = new() { CustomMinimumSize = new Vector2(0, 130) };
    private readonly OptionButton _open = new();
    private readonly OptionButton _size = new();
    private readonly LineEdit _seed = new() { CustomMinimumSize = new Vector2(170, 0) };
    private readonly Label _message = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private TemplateEditorSession _session = null!;
    private TemplateEditorHost _host = null!;
    private IReadOnlyList<TemplateSource> _sources = [];

    public event Action? Closed;

    public TemplateEditorSession Session => _session;

    public void Bind(TemplateEditorSession session, TemplateEditorHost host)
    {
        _session = session;
        _host = host;
        this.UseOpaqueBackground();
        SetAnchorsPreset(LayoutPreset.FullRect);
        OffsetLeft = OffsetTop = 8;
        OffsetRight = OffsetBottom = -8;

        var rows = new VBoxContainer();
        AddChild(rows);
        var top = new HBoxContainer();
        rows.AddChild(top);
        top.AddChild(new Label { Text = "Template editor" });
        Button(top, "New", () => Load(TemplateEditorSession.Blank()));
        _open.ItemSelected += index =>
        {
            if (index > 0 && index - 1 < _sources.Count)
            {
                Load(_sources[(int)index - 1].Open());
            }
        };
        top.AddChild(_open);
        Button(top, "Save", () => _message.Text = _host.Save(_session.Template));
        top.AddChild(new VSeparator());
        Button(top, "+ Start", () => Add(ZoneType.PlayerStart));
        Button(top, "+ AI start", () => Add(ZoneType.AiStart));
        Button(top, "+ Treasure", () => Add(ZoneType.Treasure));
        Button(top, "+ Junction", () => Add(ZoneType.Junction));
        Button(top, "Arrange", () => _session.ArrangeGraph(all: true));
        top.AddChild(new VSeparator());
        top.AddChild(_size);
        _seed.Text = ((ulong)System.Random.Shared.NextInt64()).ToString(CultureInfo.InvariantCulture);
        top.AddChild(_seed);
        Button(top, "Generate", Generate);
        top.AddChild(new VSeparator());
        Button(top, "Close (Esc)", () =>
        {
            Visible = false;
            Closed?.Invoke();
        });

        var body = new HSplitContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        rows.AddChild(body);
        var left = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddChild(left);
        left.AddChild(_graph);
        left.AddChild(new Label { Text = "Issues (errors make the template unusable, warnings point at what will likely play badly):" });
        left.AddChild(_issues);
        left.AddChild(_message);
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(450, 0), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        body.AddChild(scroll);
        scroll.AddChild(_inspector);

        _graph.Bind(session);
        _graph.SelectionChanged += _ => ShowInspector();
        this.SubscribeWhileInTree<TemplateChanged>(session.Events, e =>
        {
            _graph.Refresh();
            ShowIssues(e.Issues);
        });

        RefreshSources();
        ShowSizes();
        ShowIssues(session.Issues);
        ShowInspector();
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (Visible && @event is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            Visible = false;
            Closed?.Invoke();
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>Shows the editor (the template list is read again, a file may have been saved meanwhile).</summary>
    public void Open()
    {
        RefreshSources();
        Visible = true;
    }

    private static void Button(Container parent, string text, Action pressed)
    {
        var button = new Button { Text = text };
        button.Pressed += pressed;
        parent.AddChild(button);
    }

    private void RefreshSources()
    {
        _sources = _host.Templates();
        _open.Clear();
        _open.AddItem("Open…");
        foreach (var source in _sources)
        {
            _open.AddItem(source.Name);
        }

        _open.Select(0);
    }

    private void Load(MapTemplate template)
    {
        _session.Load(template);
        if (template.Zones.Any(z => z.GraphX is null || z.GraphY is null))
        {
            _session.ArrangeGraph();
        }

        _graph.Select(GraphSelection.None);
        _graph.Refresh(centre: true);
        ShowSizes();
        _message.Text = $"Opened {template.Name}.";
        _open.Select(0);
    }

    private void Add(ZoneType type)
    {
        var (x, y) = _graph.FreeSpot();
        if (_session.AddZone(type, x, y) is { } id)
        {
            _graph.Select(new GraphSelection(id, null));
        }
        else
        {
            _message.Text = "Every player slot (8) has a start zone already.";
        }
    }

    private void ShowSizes()
    {
        _size.Clear();
        foreach (var size in _session.Template.Sizes.Count > 0 ? _session.Template.Sizes : MapSizes.Standard)
        {
            _size.AddItem($"{size}×{size}", size);
        }

        _size.Select(_size.ItemCount / 2);
    }

    private void Generate()
    {
        if (!ulong.TryParse(_seed.Text.Trim(), CultureInfo.InvariantCulture, out var seed))
        {
            _message.Text = "Enter a numeric seed.";
            return;
        }

        if (!TemplateValidator.IsValid(_session.Template))
        {
            _message.Text = "Fix the errors first.";
            return;
        }

        if (_host.Generate(_session.Template, _size.GetSelectedId(), seed) is { } refused)
        {
            _message.Text = refused;
            return;
        }

        Visible = false;
        Closed?.Invoke();
    }

    private void ShowIssues(IReadOnlyList<TemplateIssue> issues)
    {
        _issues.Clear();
        foreach (var issue in issues)
        {
            var index = _issues.AddItem($"{(issue.Severity == IssueSeverity.Error ? "Error" : "Warning")}: {issue.Message}");
            _issues.SetItemCustomFgColor(index, issue.Severity == IssueSeverity.Error ? new Color(1, 0.45f, 0.4f) : new Color(1, 0.85f, 0.4f));
        }

        if (issues.Count == 0)
        {
            _issues.AddItem("No issues.");
        }
    }

    // Inspector ---------------------------------------------------------------------------------

    private void ShowInspector()
    {
        foreach (var child in _inspector.GetChildren())
        {
            _inspector.RemoveChild(child);
            child.QueueFree();
        }

        if (_graph.Selection.Zone is { } zone && _session.Template.Zone(zone) is not null)
        {
            ShowZone(zone);
        }
        else if (_graph.Selection.Connection is { } connection && connection < _session.Template.Connections.Count)
        {
            ShowConnection(connection);
        }
        else
        {
            ShowTemplate();
        }
    }

    private HBoxContainer Row(string label)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(150, 0) });
        _inspector.AddChild(row);
        return row;
    }

    private void Spin(string label, double value, double min, double max, Action<int> changed, double step = 1, string tooltip = "")
    {
        var spin = new SpinBox { MinValue = min, MaxValue = max, Step = step, Value = value, TooltipText = tooltip, CustomMinimumSize = new Vector2(110, 0) };
        spin.ValueChanged += v => changed((int)v);
        Row(label).AddChild(spin);
    }

    private void Choice<T>(string label, IReadOnlyList<T> values, T current, Action<T> changed, Func<T, string>? name = null)
    {
        var option = new OptionButton();
        foreach (var value in values)
        {
            option.AddItem(name?.Invoke(value) ?? value?.ToString() ?? "");
        }

        option.Select(Math.Max(0, values.ToList().IndexOf(current)));
        option.ItemSelected += i => changed(values[(int)i]);
        Row(label).AddChild(option);
    }

    private void Check(string label, bool value, Action<bool> changed)
    {
        var box = new CheckBox { ButtonPressed = value };
        box.Toggled += on => changed(on);
        Row(label).AddChild(box);
    }

    private void Text(string label, string value, Action<string> submitted, bool multiline = false)
    {
        if (multiline)
        {
            var edit = new TextEdit { Text = value, CustomMinimumSize = new Vector2(270, 70), WrapMode = TextEdit.LineWrappingMode.Boundary };
            edit.FocusExited += () => submitted(edit.Text);
            Row(label).AddChild(edit);
            return;
        }

        var line = new LineEdit { Text = value, CustomMinimumSize = new Vector2(220, 0) };
        line.TextSubmitted += text => submitted(text);
        line.FocusExited += () => submitted(line.Text);
        Row(label).AddChild(line);
    }

    private void Heading(string text) => _inspector.AddChild(new Label { Text = text, Modulate = new Color(1, 0.85f, 0.5f) });

    private void ShowTemplate()
    {
        var t = _session.Template;
        Heading("Template (select a zone or a connection to edit it)");
        Text("Name", t.Name, v => _session.UpdateTemplate(x => x with { Name = v.Trim() }));
        Text("Description", t.Description, v => _session.UpdateTemplate(x => x with { Description = v }), multiline: true);
        _inspector.AddChild(new Label { Text = $"Players: up to {t.MaxPlayers} (one per start zone)" });
        Spin("Fewest players", t.MinPlayers, 2, 8, v => _session.UpdateTemplate(x => TemplateEditorSession.SyncPlayers(x with { MinPlayers = v })));
        Spin("Humans at most", t.MaxHumans ?? 0, 0, 8, v => _session.UpdateTemplate(x => x with { MaxHumans = v == 0 ? null : v }), tooltip: "0 = no limit");
        Spin("Teams from", t.MinTeams ?? 0, 0, 8, v => _session.UpdateTemplate(x => x with { MinTeams = v < 2 ? null : v }), tooltip: "0 = no limit");
        Spin("Teams to", t.MaxTeams ?? 0, 0, 8, v => _session.UpdateTemplate(x => x with { MaxTeams = v < 2 ? null : v }), tooltip: "0 = no limit");
        Check("Teams locked", t.TeamsLocked, v => _session.UpdateTemplate(x => x with { TeamsLocked = v }));
        Spin("Hero limit", t.MaxHeroes, 1, 8, v => _session.UpdateTemplate(x => x with { MaxHeroes = v }));
        Check("Underground", t.Underground, v => _session.UpdateTemplate(x => x with { Underground = v }));
        Choice("Symmetry", Enum.GetValues<TemplateSymmetry>(), t.Symmetry, v => _session.UpdateTemplate(x => x with { Symmetry = v }));
        Choice("Connection guard", Enum.GetValues<ConnectionGuard>(), t.ConnectionGuard, v => _session.UpdateTemplate(x => x with { ConnectionGuard = v }));
        Check("Roads", t.Roads.Enabled, v => _session.UpdateTemplate(x => x with { Roads = x.Roads with { Enabled = v } }));
        Check("Week events", t.WeekEvents.Enabled, v => _session.UpdateTemplate(x => x with { WeekEvents = x.WeekEvents with { Enabled = v } }));
        var sizes = Row("Sizes");
        var grid = new GridContainer { Columns = 4 };
        sizes.AddChild(grid);
        foreach (var size in MapSizes.Standard)
        {
            var box = new CheckBox { Text = $"{size}", ButtonPressed = t.Sizes.Contains(size) };
            box.Toggled += on =>
            {
                _session.UpdateTemplate(x => x with { Sizes = on ? [.. x.Sizes.Append(size).Distinct().Order()] : [.. x.Sizes.Where(s => s != size)] });
                ShowSizes();
            };
            grid.AddChild(box);
        }
    }

    private void Zone(string id, Func<TemplateZone, TemplateZone> change)
    {
        if (_session.Template.Zone(id) is { } zone)
        {
            _session.UpdateZone(change(zone));
        }
    }

    private void ShowZone(string id)
    {
        var z = _session.Template.Zone(id)!;
        Heading($"Zone '{z.Id}'");
        Text("Id", z.Id, v =>
        {
            if (v.Trim() != id && _session.RenameZone(id, v))
            {
                _graph.Select(new GraphSelection(v.Trim(), null));
            }
        });
        Choice("Type", Enum.GetValues<ZoneType>(), z.Type, v =>
        {
            Zone(id, x => v is ZoneType.PlayerStart or ZoneType.AiStart
                ? x with { Type = v, Owner = x.Owner ?? FreeSlot(), PlayerTowns = x.PlayerTowns.Min < 1 ? CountRange.Exactly(1) : x.PlayerTowns }
                : x with { Type = v, Owner = null, Team = null, PlayerTowns = CountRange.None, StartBuildings = null, StartDwellings = null });
            ShowInspector();
        });
        if (z.Type is ZoneType.PlayerStart or ZoneType.AiStart)
        {
            Spin("Player slot", (z.Owner ?? 0) + 1, 1, 8, v => Zone(id, x => x with { Owner = v - 1 }));
            Spin("Team", (z.Team ?? -1) + 1, 0, 8, v => Zone(id, x => x with { Team = v == 0 ? null : v - 1 }), tooltip: "0 = a team of its own");
            Spin("Player towns from", z.PlayerTowns.Min, 1, 4, v => Zone(id, x => x with { PlayerTowns = new CountRange(v, Math.Max(v, x.PlayerTowns.Max)) }));
            Spin("Player towns to", z.PlayerTowns.Max, 1, 4, v => Zone(id, x => x with { PlayerTowns = new CountRange(Math.Min(v, x.PlayerTowns.Min), v) }));
        }

        Spin("Size (weight)", z.Size, 1, 20, v => Zone(id, x => x with { Size = v }));
        Spin("Level", z.Level, 0, 1, v => Zone(id, x => x with { Level = v }), tooltip: "1 = underground (the template needs it on)");
        Spin("Neutral towns from", z.NeutralTowns.Min, 0, 4, v => Zone(id, x => x with { NeutralTowns = new CountRange(v, Math.Max(v, x.NeutralTowns.Max)) }));
        Spin("Neutral towns to", z.NeutralTowns.Max, 0, 4, v => Zone(id, x => x with { NeutralTowns = new CountRange(Math.Min(v, x.NeutralTowns.Min), v) }));
        Choice("Monsters", Enum.GetValues<MonsterStrength>(), z.Monsters, v => Zone(id, x => x with { Monsters = v }));
        Choice("Mine guards", Enum.GetValues<MineGuarding>(), z.MineGuards, v => Zone(id, x => x with { MineGuards = v }));
        Choice("Obstacles", Enum.GetValues<ObstacleDensity>(), z.Obstacles, v => Zone(id, x => x with { Obstacles = v }));
        var mirrors = new List<string?> { null };
        mirrors.AddRange(_session.Template.Zones.Select(o => (string?)o.Id));
        Choice("Mirror", mirrors, z.Mirror, v => Zone(id, x => x with { Mirror = v }), v => v ?? "(its own image)");

        Heading("Mines (at least)");
        foreach (var resource in Enum.GetValues<ResourceType>())
        {
            Spin(resource.ToString(), z.Mines.GetValueOrDefault(resource), 0, 6, v => Zone(id, x =>
            {
                var mines = x.Mines.ToDictionary(m => m.Key, m => m.Value);
                if (v == 0)
                {
                    mines.Remove(resource);
                }
                else
                {
                    mines[resource] = v;
                }

                return x with { Mines = mines };
            }));
        }

        Spin("Resource piles", z.ExtraPiles, 0, 20, v => Zone(id, x => x with { ExtraPiles = v }));

        Heading("Treasure bands (count 0 = none)");
        for (var band = 0; band < 3; band++)
        {
            var at = band;
            var current = at < z.Treasure.Count ? z.Treasure[at] : new TreasureBand(500, 1500, 0);
            var row = Row($"Band {at + 1}: value, count");
            var low = new SpinBox { MinValue = 100, MaxValue = 50000, Step = 100, Value = current.MinValue };
            var high = new SpinBox { MinValue = 100, MaxValue = 50000, Step = 100, Value = current.MaxValue };
            var count = new SpinBox { MinValue = 0, MaxValue = 20, Value = at < z.Treasure.Count ? current.Count : 0 };
            void Apply() => Zone(id, x =>
            {
                var bands = Enumerable.Range(0, 3).Select(i => i < x.Treasure.Count ? x.Treasure[i] : null).ToList();
                bands[at] = count.Value < 1 ? null : new TreasureBand((int)low.Value, (int)Math.Max(low.Value, high.Value), (int)count.Value);
                return x with { Treasure = [.. bands.OfType<TreasureBand>()] };
            });
            low.ValueChanged += _ => Apply();
            high.ValueChanged += _ => Apply();
            count.ValueChanged += _ => Apply();
            row.AddChild(low);
            row.AddChild(high);
            row.AddChild(count);
        }

        var town = Row("Neutral town types");
        town.AddChild(new Label { Text = z.TownTypes is { Count: > 0 } types ? string.Join(", ", types) : "any playable town (edit the file to limit)" });
        Button(_inspector, "Remove zone (Del)", () =>
        {
            _session.RemoveZone(id);
            _graph.Select(GraphSelection.None);
        });
    }

    private int FreeSlot()
    {
        var used = _session.Template.Zones.Select(z => z.Owner).OfType<int>().ToHashSet();
        return Enumerable.Range(0, 8).FirstOrDefault(s => !used.Contains(s));
    }

    private void Connection(int index, Func<TemplateConnection, TemplateConnection> change)
    {
        if (index < _session.Template.Connections.Count)
        {
            _session.UpdateConnection(index, change(_session.Template.Connections[index]));
        }
    }

    private void ShowConnection(int index)
    {
        var c = _session.Template.Connections[index];
        Heading($"Connection {c.From} – {c.To}");
        Choice("Kind", Enum.GetValues<ConnectionKind>(), c.Kind, v =>
        {
            Connection(index, x => x with { Kind = v });
            ShowInspector();
        });
        if (c.Kind != ConnectionKind.Wide)
        {
            Spin("Guard value", c.GuardValue, 0, 100000, v => Connection(index, x => x with { GuardValue = v }), step: 100, tooltip: "Neutral army power (creature value × count); 0 = unguarded");
        }

        Spin("Passages", c.Passages, 1, TemplateValidator.MaxPassages, v => Connection(index, x => x with { Passages = v }));
        Spin("Chance %", c.Chance, 1, 100, v => Connection(index, x => x with { Chance = v }), tooltip: "How often the connection is made; one the zones need is always made");
        if (c.Kind == ConnectionKind.Normal)
        {
            var guards = new List<ConnectionGuard?> { null };
            guards.AddRange(Enum.GetValues<ConnectionGuard>().Select(g => (ConnectionGuard?)g));
            Choice("Guard", guards, c.Guard, v =>
            {
                Connection(index, x => x with { Guard = v });
                ShowInspector();
            }, v => v?.ToString() ?? $"template's ({_session.Template.ConnectionGuard})");
            if (_session.Template.GuardOf(c) is ConnectionGuard.BorderGuard or ConnectionGuard.BorderGate)
            {
                var colours = new List<ObjectColour?> { null };
                colours.AddRange(Enum.GetValues<ObjectColour>().Select(o => (ObjectColour?)o));
                Choice("Keymaster colour", colours, c.Colour, v => Connection(index, x => x with { Colour = v }), v => v?.ToString() ?? "drawn from the seed");
            }
        }

        Button(_inspector, "Remove connection (Del)", () =>
        {
            _session.Disconnect(index);
            _graph.Select(GraphSelection.None);
        });
    }
}
