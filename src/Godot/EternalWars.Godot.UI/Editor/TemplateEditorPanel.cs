using EternalWars.Godot.Shared.Controls;
using System.Globalization;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Domain.Turns;
using EternalWars.Core.Generation;
using EternalWars.Godot.Shared.Events;
using EternalWars.Godot.UI.Editor.Shell;
using EternalWars.Godot.UI.Hud;
using Godot;

namespace EternalWars.Godot.UI.Editor;

/// <summary>
/// What the app does for the template editor: the template's files (<see cref="TemplateFileService"/>), the content tables maps are generated
/// with, opening a generated map (template, size, seed, map) in the map editor as a new map, and the user's zone presets (ADR-273; none kept
/// when left out).
/// </summary>
public sealed record TemplateEditorHost(
    TemplateFileService Files, ContentCatalogs Content, Action<MapTemplate, int, ulong, EditorMap> Open, IZonePresetFiles? Presets = null);

/// <summary>The user's zone presets (ADR-273), kept by the app (the editor's <c>user://template-presets/</c>).</summary>
public interface IZonePresetFiles
{
    /// <summary>The presets kept, by name; a file that cannot be read is left out.</summary>
    IReadOnlyList<ZonePreset> Load();

    /// <summary>Whether a preset of that name is kept (Save would replace it).</summary>
    bool Exists(string name);

    /// <summary>Keeps the preset, over one of the same name. Throws <see cref="IOException"/> or <see cref="UnauthorizedAccessException"/> when it cannot.</summary>
    void Save(ZonePreset preset);

    /// <summary>Forgets the preset of that name; false when there is none.</summary>
    bool Delete(string name);
}

/// <summary>
/// The template editor (ADR-070 RMG-10, ADR-117), like HotA's: the zone graph on the left (<see cref="TemplateGraphView"/>), on the right the
/// settings of the selected zone or connection, or of the template when nothing is selected, and under the graph the validator's issues, live,
/// and the template's undo history.
/// Every control applies at once through the <see cref="TemplateEditorSession"/>, one undo step each (EDIT-2, ADR-268); an undo, a redo or
/// another template shows every part again. The top row: the template's name (an asterisk over unsaved work), New, Open, a shipped template
/// (read-only), Save and Save as (the <see cref="TemplateFileService"/>, ADR-269), add zones, and Generate (size and seed, ADR-274), whose
/// map shows in the Preview tab beside the graph; the Seeds tab generates many seeds at that size. The app places it (the editor's Template tab, EDIT-3, ADR-250)
/// and shows a map once one is opened in the map editor.
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
    private readonly CheckBox _snap = new()
    {
        Text = "Snap to grid", ButtonPressed = true, TooltipText = "Dragged zones and aligned rows land on a grid of half a graph unit",
    };

    private Button _alignRow = null!;
    private Button _alignColumn = null!;
    private Button _spreadRow = null!;
    private Button _spreadColumn = null!;
    private Button _mirror = null!;
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
        ThemeTypeVariation = UiTheme.PagePanel;

        var rows = new VBoxContainer();
        AddChild(rows);
        var top = new HFlowContainer(); // wraps on a narrow window or at a large UI scale (ADR-277)
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
        top.AddChild(PresetMenu("+ Preset", "Add a zone with a preset's settings", AddPreset, userOnly: false));
        top.AddChild(new VSeparator());
        top.AddChild(_size);
        _seed.Text = ((ulong)System.Random.Shared.NextInt64()).ToString(CultureInfo.InvariantCulture);
        top.AddChild(_seed);
        Button(top, "Generate", Generate, UiIcon.Generate);

        var body = new HSplitContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        rows.AddChild(body);
        var left = new VSplitContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddChild(left);
        // The graph with its tools over it (ADR-270, for the selected zones), beside the preview and the batch's seeds (ADR-274).
        var graph = new VBoxContainer();
        left.AddChild(Views(graph));
        var tools = new HFlowContainer();
        graph.AddChild(tools);
        _snap.Toggled += on => _graph.SnapToGrid = on;
        tools.AddChild(_snap);
        tools.AddChild(new VSeparator());
        _alignRow = ToolButton(tools, "Align in a row", "The selected zones at one height", () => Align(GraphLine.Row));
        _alignColumn = ToolButton(tools, "Align in a column", "The selected zones one above the other", () => Align(GraphLine.Column));
        _spreadRow = ToolButton(tools, "Spread across", "The selected zones evenly from left to right, the outer two staying", () => Spread(GraphLine.Row));
        _spreadColumn = ToolButton(tools, "Spread down", "The selected zones evenly from top to bottom, the outer two staying", () => Spread(GraphLine.Column));
        _mirror = ToolButton(tools, "Mirror", "", () => _message.Text = Mirror(_graph.Selection.Zones));
        _mirror.WithIcon(UiIcon.MirrorVertical, keepText: true);
        tools.AddChild(new VSeparator());
        Button(tools, "Arrange", () =>
        {
            _graph.EndDrag();
            _session.ArrangeGraph(all: true);
        }, UiIcon.Arrange);
        graph.AddChild(_graph);
        // Under the graph: the live issues, and the template's undo history (EDIT-2: the bottom region's History is the map's).
        var under = new VBoxContainer();
        left.AddChild(under);
        var below = new TabContainer { CustomMinimumSize = new Vector2(0, 120), SizeFlagsVertical = SizeFlags.ExpandFill };
        under.AddChild(below);
        below.AddChild(_issues);
        below.WithTabIcon(0, UiIcon.Error);
        below.AddChild(_history);
        below.WithTabIcon(1, UiIcon.History);
        _history.Bind(session, "template");
        under.AddChild(_message);
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(450, 0), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        body.AddChild(scroll);
        scroll.AddChild(_inspector);

        _graph.Bind(session);
        _graph.SelectionChanged += _ =>
        {
            ShowInspector();
            ShowTools();
        };
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
            ShowTools(); // the symmetry or a mirror may have changed
            ShowPreviewInfo(); // the preview and the batch say when the template changed since
            ShowBatchInfo();
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
        ShowTools();
    }

    private static Button ToolButton(Container parent, string text, string tooltip, Action pressed)
    {
        var button = new Button { Text = text, TooltipText = tooltip };
        button.Pressed += pressed;
        parent.AddChild(button);
        return button;
    }

    /// <summary>Align takes two selected zones, Spread three, Mirror zones not mirrored yet in a template with a symmetry.</summary>
    private void ShowTools()
    {
        var count = _graph.Selection.Zones.Count;
        _alignRow.Disabled = _alignColumn.Disabled = count < 2;
        _spreadRow.Disabled = _spreadColumn.Disabled = count < 3;
        var refusal = _session.MirrorRefusal(_graph.Selection.Zones);
        _mirror.Disabled = refusal is not null;
        _mirror.TooltipText = refusal ?? $"Add the selected zones' images under the template's symmetry ({_session.Template.Symmetry}); an edit of one carries to its images";
    }

    /// <summary>The selected zones get their images (ADR-272), which join the selection. Returns what happened.</summary>
    private string Mirror(IReadOnlyList<string> ids)
    {
        _graph.EndDrag();
        if (_session.MirrorRefusal(ids) is { } refusal)
        {
            return refusal;
        }

        var images = _session.MakeMirror(ids)!;
        _graph.Select(GraphSelection.Of([.. ids, .. images]));
        return $"Mirrored {Zones(ids.Count)} as {string.Join(", ", images)}; an edit of one carries to its images";
    }

    /// <summary>The zone's mirror cycle is unlinked: its zones are edited apart from now on.</summary>
    private string BreakMirror(string id)
    {
        _graph.EndDrag();
        return _session.BreakMirror(id) ? $"The mirror of {id} is broken: its zones are edited apart" : $"Zone {id} has no mirror";
    }

    private void Align(GraphLine line)
    {
        _graph.EndDrag();
        _session.AlignZones(_graph.Selection.Zones, line, _graph.Grid);
    }

    private void Spread(GraphLine line)
    {
        _graph.EndDrag();
        _session.SpreadZones(_graph.Selection.Zones, line);
    }

    // The selection and the clipboard (ADR-271): the editor's Edit commands call these on the Template tab ---------------------------

    /// <summary>
    /// The zones last copied or cut, with the connections between them; kept while another template is opened (the panel lives as long as the
    /// editor), so zones go from one template to another.
    /// </summary>
    public TemplateFragment? Clipboard { get; private set; }

    /// <summary>Why Delete cannot run now; null when a zone or a connection is selected.</summary>
    public string? WhyNothingSelected() =>
        _graph.Selection.Zones.Count > 0 || _graph.Selection.Connection is not null ? null : "No zone and no connection is selected";

    /// <summary>Why Copy, Cut and Duplicate cannot run: they take zones.</summary>
    public string? WhyNoZones() => _graph.Selection.Zones.Count > 0 ? null : "No zone is selected";

    public void SelectAll() => _graph.SelectAll();

    public void Deselect() => _graph.Select(GraphSelection.None);

    /// <summary>The selected zones and the connections between them go to <see cref="Clipboard"/>; Cut also removes them. Returns what happened.</summary>
    public string Copy(bool cut)
    {
        _graph.EndDrag();
        if (_session.Copy(_graph.Selection.Zones) is not { } fragment)
        {
            return "No zone is selected";
        }

        Clipboard = fragment;
        if (cut)
        {
            _session.RemoveZones(fragment.Zones.Select(z => z.Id));
            _graph.Select(GraphSelection.None);
        }

        return $"{(cut ? "Cut" : "Copied")} {Zones(fragment.Zones.Count)} and {fragment.Connections.Count} connection(s) between them; Ctrl+V pastes";
    }

    /// <summary>The clipboard's zones come in under the pointer (or in the middle of the view) with new ids, and are selected.</summary>
    public string Paste()
    {
        _graph.EndDrag();
        if (Clipboard is not { } fragment)
        {
            return "Nothing was copied";
        }

        var (x, y) = _graph.PasteSpot(fragment);
        return Placed(_session.Paste(fragment, x, y), fragment, "Pasted");
    }

    /// <summary>A copy of the selected zones beside them, selected.</summary>
    public string Duplicate()
    {
        _graph.EndDrag();
        if (_session.Copy(_graph.Selection.Zones) is not { } fragment)
        {
            return "No zone is selected";
        }

        return Placed(_session.Duplicate(fragment.Zones.Select(z => z.Id), 1, 1.5), fragment, "Duplicated"); // below and right, clear of the cards
    }

    private string Placed(IReadOnlyList<string>? ids, TemplateFragment fragment, string verb)
    {
        if (ids is null)
        {
            return $"No room: {fragment.StartZones} start zone(s) need a free player slot each, of 8";
        }

        _graph.Select(GraphSelection.Of(ids));
        return $"{verb} {Zones(ids.Count)}";
    }

    /// <summary>Removes the selected zones (one step) or the selected connection.</summary>
    public string Delete()
    {
        _graph.EndDrag(); // a removal is a step of its own, not part of a drag
        var selection = _graph.Selection;
        _graph.Select(GraphSelection.None);
        if (selection.Connection is { } index && _session.Disconnect(index))
        {
            return "Removed the connection";
        }

        var removed = _session.RemoveZones(selection.Zones);
        return removed == 0 ? "Nothing is selected" : $"Removed {Zones(removed)}";
    }

    private static string Zones(int count) => count == 1 ? "1 zone" : $"{count} zones";

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
            _graph.Select(GraphSelection.Of(id));
        }
        else
        {
            _message.Text = "Every player slot (8) has a start zone already.";
        }
    }

    // Zone presets (ADR-273) ------------------------------------------------------------------------

    /// <summary>The built-in presets, then the user's.</summary>
    private IReadOnlyList<ZonePreset> Presets() => [.. ZonePresets.BuiltIn, .. UserPresets()];

    private IReadOnlyList<ZonePreset> UserPresets() => _host.Presets?.Load() ?? [];

    /// <summary>A drop-down button listing the presets (only the user's with <paramref name="userOnly"/>), read anew each time it opens.</summary>
    private MenuButton PresetMenu(string text, string tooltip, Action<ZonePreset> picked, bool userOnly)
    {
        var menu = new MenuButton { Text = text, TooltipText = tooltip, Flat = false };
        var shown = new List<ZonePreset>();
        menu.AboutToPopup += () =>
        {
            var popup = menu.GetPopup();
            popup.Clear();
            shown.Clear();
            shown.AddRange(userOnly ? UserPresets() : Presets());
            for (var i = 0; i < shown.Count; i++)
            {
                if (i > 0 && shown[i].BuiltIn != shown[i - 1].BuiltIn)
                {
                    popup.AddSeparator();
                }

                popup.AddItem(shown[i].BuiltIn ? shown[i].Name : $"{shown[i].Name} (yours)", i);
            }

            if (shown.Count == 0)
            {
                popup.AddItem("(no presets of yours yet: Save as preset in a zone's inspector)", int.MaxValue); // not -1: Godot numbers that by index
                popup.SetItemDisabled(0, true);
            }
        };
        menu.GetPopup().IdPressed += id =>
        {
            if (id >= 0 && id < shown.Count)
            {
                picked(shown[(int)id]);
            }
        };
        return menu;
    }

    private void AddPreset(ZonePreset preset)
    {
        _graph.EndDrag();
        var (x, y) = _graph.FreeSpot();
        if (_session.AddZone(preset, x, y) is { } id)
        {
            _graph.Select(GraphSelection.Of(id));
            _message.Text = $"Added {id} as {preset.Name}";
        }
        else
        {
            _message.Text = "Every player slot (8) has a start zone already.";
        }
    }

    private void ApplyPreset(string id, ZonePreset preset)
    {
        _graph.EndDrag();
        _message.Text = _session.ApplyPreset(id, preset)
            ? $"Zone {id} has the settings of {preset.Name}"
            : $"No room: {preset.Name} makes start zones of {id} and its mirror images, and too few of the 8 player slots are free";
        ShowInspector();
    }

    /// <summary>Asks for a name and keeps the zone's settings as a preset of the user's.</summary>
    private void SaveAsPreset(string id)
    {
        if (_host.Presets is not { } files || _session.Template.Zone(id) is not { } zone)
        {
            return;
        }

        var box = new NameBox { Name = "PresetName" };
        AddChild(box);
        var taken = UserPresets().Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var proposal = Enumerable.Range(1, taken.Count + 1).Select(n => $"My zone {n}").First(n => !taken.Contains(n));
        box.Bind($"Save the settings of zone {id} as a preset", proposal, name =>
        {
            try
            {
                var replaced = files.Exists(name);
                files.Save(ZonePresets.Of(name, zone));
                _message.Text = $"{(replaced ? "Replaced" : "Saved")} preset {name}; + Preset and Apply preset list it";
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                _message.Text = $"Could not save the preset: {e.Message}";
            }
        });
    }

    private void DeletePreset(ZonePreset preset)
    {
        try
        {
            _message.Text = _host.Presets?.Delete(preset.Name) == true ? $"Deleted preset {preset.Name}" : $"No preset {preset.Name} to delete";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _message.Text = $"Could not delete the preset: {e.Message}";
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
        else if (_graph.Selection.Zones.Count > 1)
        {
            ShowZones(_graph.Selection.Zones);
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

    /// <summary>
    /// A labelled grid of tick boxes, one per value; a tick or an untick gives the ticked values, in the order of <paramref name="values"/>
    /// (the pickers of terrains, town types, factions and road mines).
    /// </summary>
    private void Ticks<T>(string label, IReadOnlyList<T> values, IEnumerable<T> ticked, Action<IReadOnlyList<T>> changed, Func<T, string>? name = null, int columns = 3)
    {
        var on = ticked.ToHashSet();
        var grid = new GridContainer { Columns = columns };
        var boxes = new List<(T Value, CheckBox Box)>();
        foreach (var value in values)
        {
            var box = new CheckBox { Text = name?.Invoke(value) ?? value?.ToString() ?? "", ButtonPressed = on.Contains(value) };
            if (value is string townType)
            {
                box.WithCrest(townType); // a faction with its crest (ADR-281); no crest for anything else
            }

            box.Toggled += _ => changed([.. boxes.Where(b => b.Box.ButtonPressed).Select(b => b.Value)]);
            boxes.Add((value, box));
            grid.AddChild(box);
        }

        _inspector.AddRow(label, labelWidth: 150).AddChild(grid);
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
        Choice("Road type", Enum.GetValues<RoadType>(), t.Roads.Type,
            v => _session.UpdateTemplate(x => x with { Roads = x.Roads with { Type = v } }));
        Ticks("Roads to mines", Enum.GetValues<ResourceType>(), t.Roads.Mines,
            v => _session.UpdateTemplate(x => x with { Roads = x.Roads with { Mines = v } }));
        Check("Roads to gates", t.Roads.Gates, v => _session.UpdateTemplate(x => x with { Roads = x.Roads with { Gates = v } }));
        Check("Week events", t.WeekEvents.Enabled, v => _session.UpdateTemplate(x => x with { WeekEvents = x.WeekEvents with { Enabled = v } }));
        Spin("Weeks before events", t.WeekEvents.SkipWeeks, 0, WeekEventSettings.MaxSkipWeeks,
            v => _session.UpdateTemplate(x => x with { WeekEvents = x.WeekEvents with { SkipWeeks = v } }));
        Ticks("Weekly factions", WeekEventSettings.Factions, t.WeekEvents.ToSettings().Weekly,
            v => _session.UpdateTemplate(x => x with { WeekEvents = x.WeekEvents with { WeeklyFactions = Factions(v) } }), FactionName);
        Ticks("Monthly factions", WeekEventSettings.Factions, t.WeekEvents.ToSettings().Monthly,
            v => _session.UpdateTemplate(x => x with { WeekEvents = x.WeekEvents with { MonthlyFactions = Factions(v) } }), FactionName);
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

    /// <summary>Week event factions as kept: null when they are the playable towns (the default).</summary>
    private static IReadOnlyList<string>? Factions(IReadOnlyList<string> ticked) =>
        ticked.Order(StringComparer.Ordinal).SequenceEqual(BuiltInMapObjects.PlayableTowns.Order(StringComparer.Ordinal)) ? null : ticked;

    private static string FactionName(string faction) => faction == WeekEventSettings.Neutral ? "Neutral" : BuiltInMapObjects.TownName(faction);

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
                _graph.Select(GraphSelection.Of(v.Trim()));
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
        var mirrorRow = _inspector.AddRow("", labelWidth: 150);
        if (TemplateMirrors.Cycle(_session.Template, id) is { } cycle)
        {
            mirrorRow.AddChild(new Label { Text = $"In sync with {string.Join(", ", cycle.Skip(1))}" });
        }

        if (!TemplateMirrors.IsOwnImage(z) || TemplateMirrors.IsNamed(_session.Template, id))
        {
            Button(mirrorRow, "Break mirror", () => _message.Text = BreakMirror(id));
        }
        else
        {
            var button = new Button { Text = "Make mirror image", Disabled = _session.MirrorRefusal([id]) is not null, TooltipText = _session.MirrorRefusal([id]) ?? "" };
            button.Pressed += () => _message.Text = Mirror([id]);
            mirrorRow.AddChild(button);
        }

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

        Heading("Terrains and towns (none ticked: any)");
        var passable = Enum.GetValues<TerrainType>().Where(_session.Content.Terrains.IsPassable).ToList();
        Ticks("Terrains", passable, z.Terrains ?? [], v => Zone(id, x => x with { Terrains = v.Count == 0 ? null : v }));
        Ticks("Neutral town types", BuiltInMapObjects.PlayableTowns, z.TownTypes ?? [],
            v => Zone(id, x => x with { TownTypes = v.Count == 0 ? null : v }), BuiltInMapObjects.TownName);
        if (z.Type is ZoneType.PlayerStart or ZoneType.AiStart)
        {
            ShowStartBuildings(id, z);
        }

        Heading("Presets");
        var presets = new HBoxContainer();
        _inspector.AddChild(presets);
        presets.AddChild(PresetMenu("Apply preset", "Give this zone a preset's settings (its id, slot, team, mirror, level and place stay)", p => ApplyPreset(id, p), userOnly: false));
        if (_host.Presets is not null)
        {
            Button(presets, "Save as preset…", () => SaveAsPreset(id));
            presets.AddChild(PresetMenu("Delete preset", "Forget one of your presets", DeletePreset, userOnly: true));
        }

        Button(_inspector, "Remove zone (Del)", () => _message.Text = Delete());
    }

    /// <summary>A start zone's buildings besides the default start (each at a level, 0: not built) and its dwellings by tier (ADR-030).</summary>
    private void ShowStartBuildings(string id, TemplateZone z)
    {
        Heading("Start buildings besides the default (0: not built)");
        var catalog = _session.Content.Buildings;
        var grid = new GridContainer { Columns = 4 };
        _inspector.AddChild(grid);
        foreach (var building in catalog.TownBuildings.Where(b => b.Value != BuiltInBuildings.Grail).OrderBy(b => b.Value, StringComparer.Ordinal))
        {
            var key = building.Value;
            grid.AddChild(new Label { Text = catalog.Get(building, 1).Name, CustomMinimumSize = new Vector2(120, 0), ClipText = true, TooltipText = key });
            var spin = new SpinBox { MinValue = 0, MaxValue = catalog.MaxLevel(building), Value = z.StartBuildings?.GetValueOrDefault(key) ?? 0 };
            spin.ValueChanged += v =>
            {
                if (_rebuilding)
                {
                    return;
                }

                Zone(id, x =>
                {
                    var buildings = (x.StartBuildings ?? new Dictionary<string, int>()).ToDictionary(p => p.Key, p => p.Value);
                    if (v < 1)
                    {
                        buildings.Remove(key);
                    }
                    else
                    {
                        buildings[key] = (int)v;
                    }

                    return x with { StartBuildings = buildings.Count == 0 ? null : buildings };
                });
            };
            grid.AddChild(spin);
        }

        Heading("Start dwellings by tier (without tier 1, the player picks one)");
        var tiers = new HBoxContainer();
        _inspector.AddChild(tiers);
        for (var tier = 1; tier <= 5; tier++)
        {
            var at = tier;
            tiers.AddChild(new Label { Text = $"T{at}" });
            var spin = new SpinBox { MinValue = 0, MaxValue = at == 5 ? 1 : 2, Value = z.StartDwellings?.Count(t => t == at) ?? 0, TooltipText = $"Tier-{at} dwellings built at the start" };
            spin.ValueChanged += v =>
            {
                if (_rebuilding)
                {
                    return;
                }

                Zone(id, x =>
                {
                    var dwellings = (x.StartDwellings ?? []).Where(t => t != at).Concat(Enumerable.Repeat(at, (int)v)).Order().ToList();
                    return x with { StartDwellings = dwellings.Count == 0 ? null : dwellings };
                });
            };
            tiers.AddChild(spin);
        }
    }

    /// <summary>Several zones selected (ADR-271): which, and what can be done with all of them.</summary>
    private void ShowZones(IReadOnlyList<string> ids)
    {
        Heading($"{ids.Count} zones selected");
        _inspector.AddChild(new Label { Text = string.Join(", ", ids), AutowrapMode = TextServer.AutowrapMode.WordSmart });
        var inner = _session.Copy(ids)?.Connections.Count ?? 0;
        _inspector.AddChild(new Label { Text = $"{inner} connection(s) between them go with a copy." });
        var align = new HBoxContainer();
        _inspector.AddChild(align);
        Button(align, "Align in a row", () => Align(GraphLine.Row));
        Button(align, "Align in a column", () => Align(GraphLine.Column));
        if (ids.Count >= 3)
        {
            Button(align, "Spread across", () => Spread(GraphLine.Row));
            Button(align, "Spread down", () => Spread(GraphLine.Column));
        }

        var edit = new HBoxContainer();
        _inspector.AddChild(edit);
        Button(edit, "Copy (Ctrl+C)", () => _message.Text = Copy(cut: false), UiIcon.Copy);
        Button(edit, "Duplicate (Ctrl+D)", () => _message.Text = Duplicate(), UiIcon.Duplicate);
        Button(edit, "Remove zones (Del)", () => _message.Text = Delete(), UiIcon.Delete);
        var refusal = _session.MirrorRefusal(ids);
        var mirror = new Button { Text = "Make mirror images", Disabled = refusal is not null, TooltipText = refusal ?? "" };
        mirror.Pressed += () => _message.Text = Mirror(ids);
        _inspector.AddChild(mirror);
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

        Button(_inspector, "Remove connection (Del)", () => _message.Text = Delete());
    }
}
