using EternalWars.Godot.Shared.Controls;
using System.Globalization;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Generation;
using EternalWars.Godot.Shared.Events;
using EternalWars.Godot.UI.Editor.Shell;
using EternalWars.Godot.UI.Hud;
using Godot;

namespace EternalWars.Godot.UI.Editor;

/// <summary>
/// What the app does for the template editor: the template's files (<see cref="TemplateFileService"/>), and generating a map from a template in
/// the map editor (returns null when the map is open, else why not).
/// </summary>
public sealed record TemplateEditorHost(TemplateFileService Files, Func<MapTemplate, int, ulong, string?> Generate);

/// <summary>
/// The template editor (ADR-070 RMG-10, ADR-117), like HotA's: the zone graph on the left (<see cref="TemplateGraphView"/>), on the right the
/// settings of the selected zone or connection, or of the template when nothing is selected, and under the graph the validator's issues, live,
/// and the template's undo history.
/// Every control applies at once through the <see cref="TemplateEditorSession"/>, one undo step each (EDIT-2, ADR-268); an undo, a redo or
/// another template shows every part again. The top row: the template's name (an asterisk over unsaved work), New, Open, a shipped template
/// (read-only), Save and Save as (the <see cref="TemplateFileService"/>, ADR-269), add zones, arrange the graph, and Generate (size and seed), which
/// opens the result in the map editor. The app places it (the editor's Template tab, EDIT-3, ADR-250) and shows the map once one was generated.
/// </summary>
public partial class TemplateEditorPanel : PanelContainer
{
    private readonly TemplateGraphView _graph = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
    private readonly VBoxContainer _inspector = new() { CustomMinimumSize = new Vector2(430, 0) };
    private readonly ItemList _issues = new()
    {
        Name = "Issues", TooltipText = "Errors make the template unusable; warnings point at what will likely play badly",
    };

    private readonly HistoryPanel _history = new() { Name = "History" };
    private readonly Label _title = new() { CustomMinimumSize = new Vector2(180, 0), ClipText = true };
    private readonly OptionButton _shipped = new() { TooltipText = "Open a shipped template, read-only: Save keeps a copy in your templates folder" };
    private readonly OptionButton _size = new();
    private readonly LineEdit _seed = new() { CustomMinimumSize = new Vector2(170, 0) };
    private readonly Label _message = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private TemplateEditorSession _session = null!;
    private TemplateEditorHost _host = null!;
    private RefreshScheduler _replaced = null!;

    /// <summary>The inspector is being rebuilt: a field that loses the focus as it goes does not apply its text.</summary>
    private bool _rebuilding;

    public TemplateEditorSession Session => _session;

    public void Bind(TemplateEditorSession session, TemplateEditorHost host)
    {
        _session = session;
        _host = host;
        this.UseOpaqueBackground();

        var rows = new VBoxContainer();
        AddChild(rows);
        var top = new HBoxContainer();
        rows.AddChild(top);
        top.AddChild(_title);
        Button(top, "New", () => _host.Files.New(), UiIcon.New);
        Button(top, "Open…", () => _host.Files.Open(), UiIcon.Load);
        _shipped.AddItem("Shipped…");
        foreach (var template in _host.Files.Shipped)
        {
            _shipped.AddItem(template.Name);
        }

        _shipped.ItemSelected += index =>
        {
            _shipped.Select(0);
            if (index > 0)
            {
                _host.Files.OpenShipped(_host.Files.Shipped[(int)index - 1]);
            }
        };
        top.AddChild(_shipped);
        Button(top, "Save", () => _host.Files.Save());
        Button(top, "Save as", () => _host.Files.SaveAs(), UiIcon.SaveAs);
        top.AddChild(new VSeparator());
        Button(top, "+ Start", () => Add(ZoneType.PlayerStart), ZoneIcon(ZoneType.PlayerStart));
        Button(top, "+ AI start", () => Add(ZoneType.AiStart), ZoneIcon(ZoneType.AiStart));
        Button(top, "+ Treasure", () => Add(ZoneType.Treasure), ZoneIcon(ZoneType.Treasure));
        Button(top, "+ Junction", () => Add(ZoneType.Junction), ZoneIcon(ZoneType.Junction));
        Button(top, "Arrange", () => _session.ArrangeGraph(all: true), UiIcon.Arrange);
        top.AddChild(new VSeparator());
        top.AddChild(_size);
        _seed.Text = ((ulong)System.Random.Shared.NextInt64()).ToString(CultureInfo.InvariantCulture);
        top.AddChild(_seed);
        Button(top, "Generate", Generate, UiIcon.Generate);

        var body = new HSplitContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        rows.AddChild(body);
        var left = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddChild(left);
        left.AddChild(_graph);
        // Under the graph: the live issues, and the template's undo history (EDIT-2: the bottom region's History is the map's).
        var below = new TabContainer { CustomMinimumSize = new Vector2(0, 160) };
        left.AddChild(below);
        below.AddChild(_issues);
        below.WithTabIcon(0, UiIcon.Error);
        below.AddChild(_history);
        below.WithTabIcon(1, UiIcon.History);
        _history.Bind(session, "template");
        left.AddChild(_message);
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(450, 0), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        body.AddChild(scroll);
        scroll.AddChild(_inspector);

        _graph.Bind(session);
        _graph.SelectionChanged += _ => ShowInspector();
        // An undo, a redo or another template: the inspector and the sizes show it once, at the end of the frame (the history may be moving
        // through several steps, and the change can come from a control of the inspector itself).
        _replaced = new RefreshScheduler(this, () =>
        {
            ShowInspector();
            ShowSizes();
        });
        this.SubscribeWhileInTree<TemplateChanged>(session.Events, e =>
        {
            _graph.Refresh();
            ShowIssues(e.Issues);
            if (e.Replaced)
            {
                _replaced.Request();
            }
        });
        // The service and this panel both live as long as the editor: plain events.
        host.Files.Changed += () => _title.Text = host.Files.Title;
        host.Files.Opened += Opened;

        _title.Text = host.Files.Title;
        ShowSizes();
        ShowIssues(session.Issues);
        ShowInspector();
    }

    /// <summary>A button; <paramref name="icon"/> stands before its text ("Save" shows only its icon).</summary>
    private static void Button(Container parent, string text, Action pressed, UiIcon? icon = null)
    {
        var button = new Button { Text = text };
        if (text == "Save")
        {
            button.WithIcon(UiIcon.Save);
        }
        else if (icon is { } shown)
        {
            button.WithIcon(shown, keepText: true);
        }

        button.Pressed += pressed;
        parent.AddChild(button);
    }

    private static UiIcon ZoneIcon(ZoneType type) => type switch
    {
        ZoneType.PlayerStart => UiIcon.ZoneStart,
        ZoneType.AiStart => UiIcon.ZoneAiStart,
        ZoneType.Treasure => UiIcon.ZoneTreasure,
        _ => UiIcon.ZoneJunction,
    };

    private static UiIcon KindIcon(ConnectionKind kind) => kind switch
    {
        ConnectionKind.Wide => UiIcon.ConnectionWide,
        ConnectionKind.Gate => UiIcon.ConnectionGate,
        ConnectionKind.Portal => UiIcon.ConnectionPortal,
        _ => UiIcon.ConnectionNormal,
    };

    private static UiIcon? GuardIcon(ConnectionGuard guard) => guard switch
    {
        ConnectionGuard.Monsters => UiIcon.ToolMonster,
        ConnectionGuard.Garrison => UiIcon.GuardGarrison,
        ConnectionGuard.BorderGuard => UiIcon.GuardBorder,
        ConnectionGuard.BorderGate => UiIcon.GuardBorderGate,
        _ => null,
    };

    private static UiIcon StrengthIcon(MonsterStrength strength) => strength switch
    {
        MonsterStrength.None => UiIcon.StrengthNone,
        MonsterStrength.Weak => UiIcon.StrengthWeak,
        MonsterStrength.Average => UiIcon.StrengthAverage,
        _ => UiIcon.StrengthStrong,
    };

    private static UiIcon? SymmetryIcon(TemplateSymmetry symmetry) => symmetry switch
    {
        TemplateSymmetry.Vertical => UiIcon.MirrorVertical,
        TemplateSymmetry.Horizontal => UiIcon.MirrorHorizontal,
        TemplateSymmetry.Diagonal => UiIcon.MirrorDiagonal,
        _ => null,
    };

    /// <summary>Another template is in the editor: nothing selected, the graph centred.</summary>
    private void Opened()
    {
        _graph.Select(GraphSelection.None);
        _graph.Refresh(centre: true);
        _message.Text = "";
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
        }
    }

    private void ShowIssues(IReadOnlyList<TemplateIssue> issues)
    {
        _issues.Clear();
        foreach (var issue in issues)
        {
            var index = _issues.AddItem($"{(issue.Severity == IssueSeverity.Error ? "Error" : "Warning")}: {issue.Message}", ProblemsPanel.SeverityIcon(issue.Severity));
            _issues.SetItemCustomFgColor(index, issue.Severity == IssueSeverity.Error ? UiTheme.IssueError : UiTheme.IssueWarning);
        }

        if (issues.Count == 0)
        {
            _issues.AddItem("No issues.");
        }
    }

    // Inspector ---------------------------------------------------------------------------------

    private void ShowInspector()
    {
        _rebuilding = true;
        try
        {
            _inspector.ClearChildren(detach: true);
        }
        finally
        {
            _rebuilding = false;
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

    private void Spin(string label, double value, double min, double max, Action<int> changed, double step = 1, string tooltip = "")
    {
        var spin = new SpinBox { MinValue = min, MaxValue = max, Step = step, Value = value, TooltipText = tooltip, CustomMinimumSize = new Vector2(110, 0) };
        spin.ValueChanged += v =>
        {
            if (!_rebuilding) // a SpinBox applies its typed text when it loses the focus, as the inspector is rebuilt too
            {
                changed((int)v);
            }
        };
        _inspector.AddRow(label, labelWidth: 150).AddChild(spin);
    }

    /// <summary>A labelled list of <paramref name="values"/>; <paramref name="icon"/> gives an item its icon (null: none).</summary>
    private void Choice<T>(
        string label, IReadOnlyList<T> values, T current, Action<T> changed, Func<T, string>? name = null, Func<T, UiIcon?>? icon = null)
    {
        var option = new OptionButton();
        foreach (var value in values)
        {
            var text = name?.Invoke(value) ?? value?.ToString() ?? "";
            if (icon?.Invoke(value) is { } own && UiIcons.Get(own) is { } texture)
            {
                option.AddIconItem(texture, text);
            }
            else
            {
                option.AddItem(text);
            }
        }

        option.Select(Math.Max(0, values.ToList().IndexOf(current)));
        option.ItemSelected += i => changed(values[(int)i]);
        _inspector.AddRow(label, labelWidth: 150).AddChild(option);
    }

    private void Check(string label, bool value, Action<bool> changed)
    {
        var box = new CheckBox { ButtonPressed = value };
        box.Toggled += on => changed(on);
        _inspector.AddRow(label, labelWidth: 150).AddChild(box);
    }

    private void Text(string label, string value, Action<string> submitted, bool multiline = false)
    {
        if (multiline)
        {
            var edit = new TextEdit { Text = value, CustomMinimumSize = new Vector2(270, 70), WrapMode = TextEdit.LineWrappingMode.Boundary };
            edit.FocusExited += () =>
            {
                if (!_rebuilding)
                {
                    submitted(edit.Text);
                }
            };
            _inspector.AddRow(label, labelWidth: 150).AddChild(edit);
            return;
        }

        var line = new LineEdit { Text = value, CustomMinimumSize = new Vector2(220, 0) };
        line.TextSubmitted += text => submitted(text);
        line.FocusExited += () =>
        {
            if (!_rebuilding)
            {
                submitted(line.Text);
            }
        };
        _inspector.AddRow(label, labelWidth: 150).AddChild(line);
    }

    private void Heading(string text) => _inspector.AddChild(new Label { Text = text, Modulate = UiTheme.TextCaution });

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
        Choice("Symmetry", Enum.GetValues<TemplateSymmetry>(), t.Symmetry, v => _session.UpdateTemplate(x => x with { Symmetry = v }), icon: SymmetryIcon);
        Choice("Connection guard", Enum.GetValues<ConnectionGuard>(), t.ConnectionGuard, v => _session.UpdateTemplate(x => x with { ConnectionGuard = v }),
            icon: g => GuardIcon(g));
        Check("Roads", t.Roads.Enabled, v => _session.UpdateTemplate(x => x with { Roads = x.Roads with { Enabled = v } }));
        Check("Week events", t.WeekEvents.Enabled, v => _session.UpdateTemplate(x => x with { WeekEvents = x.WeekEvents with { Enabled = v } }));
        var sizes = _inspector.AddRow("Sizes", labelWidth: 150);
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
        }, icon: t => ZoneIcon(t));
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
        Choice("Monsters", Enum.GetValues<MonsterStrength>(), z.Monsters, v => Zone(id, x => x with { Monsters = v }), icon: m => StrengthIcon(m));
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
        Spin("Obelisks (-1 = share of 25)", z.Obelisks ?? -1, -1, GrailRules.MaxObelisks, v => Zone(id, x => x with { Obelisks = v < 0 ? null : v }),
            tooltip: "How many obelisks the zone gets (ADR-140). -1: the zones left unset share the map's 25.");

        Heading("Treasure bands (count 0 = none)");
        for (var band = 0; band < 3; band++)
        {
            var at = band;
            var current = at < z.Treasure.Count ? z.Treasure[at] : new TreasureBand(500, 1500, 0);
            var row = _inspector.AddRow($"Band {at + 1}: value, count", labelWidth: 150);
            var low = new SpinBox { MinValue = 100, MaxValue = 50000, Step = 100, Value = current.MinValue };
            var high = new SpinBox { MinValue = 100, MaxValue = 50000, Step = 100, Value = current.MaxValue };
            var count = new SpinBox { MinValue = 0, MaxValue = 20, Value = at < z.Treasure.Count ? current.Count : 0 };
            void Apply()
            {
                if (_rebuilding)
                {
                    return;
                }

                Zone(id, x =>
                {
                    var bands = Enumerable.Range(0, 3).Select(i => i < x.Treasure.Count ? x.Treasure[i] : null).ToList();
                    bands[at] = count.Value < 1 ? null : new TreasureBand((int)low.Value, (int)Math.Max(low.Value, high.Value), (int)count.Value);
                    var treasure = bands.OfType<TreasureBand>().ToList();
                    return treasure.SequenceEqual(x.Treasure) ? x : x with { Treasure = treasure }; // a band left at count 0 changes nothing
                });
            }
            low.ValueChanged += _ => Apply();
            high.ValueChanged += _ => Apply();
            count.ValueChanged += _ => Apply();
            row.AddChild(low);
            row.AddChild(high);
            row.AddChild(count);
        }

        var town = _inspector.AddRow("Neutral town types", labelWidth: 150);
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
        }, icon: k => KindIcon(k));
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
            }, v => v?.ToString() ?? $"template's ({_session.Template.ConnectionGuard})", v => GuardIcon(v ?? _session.Template.ConnectionGuard));
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
