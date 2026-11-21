using EternalWars.Core.Domain.Analysis;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Generation;
using EternalWars.Godot.Shared.Events;
using EternalWars.Godot.Shared.Map;
using Godot;

namespace EternalWars.Godot.UI.Editor;

/// <summary>
/// The editor's overlays (ADR-264), a tab of the right region: one at a time, or none. Reach is drawn from a chosen start: a player's (its
/// first town, else its first hero) or the first selected cell, for the first player. The overlay is computed again in the core
/// (<see cref="MapOverlays"/>) after every edit and when the level changes, and drawn by the map's <see cref="OverlayView"/>; its legend
/// (<see cref="OverlayLegend"/>) lies over the map view's lower left corner while one is shown.
/// </summary>
public partial class OverlaysPanel : VBoxContainer
{
    private static readonly (string Text, OverlayKind? Kind, string Tip)[] Choices =
    [
        ("None", null, "No overlay"),
        ("Passability", OverlayKind.Passability, "Blocked cells, entrances, and the cells around a monster"),
        ("Owners", OverlayKind.Owners, "Every owned or ownable thing in its owner's colour"),
        ("Guard strength", OverlayKind.GuardStrength, "The army power guarding each cell, as a heat map"),
        ("Reach", OverlayKind.Reach, "How many days a hero needs from a chosen start to every tile"),
        ("Zones", OverlayKind.Zones, "The zones of a generated map, else the start zones"),
        ("Height contours", OverlayKind.Height, "Contour lines of the ground every half a cell of height"),
    ];

    /// <summary>The choices by name, None first (the View menu lists them).</summary>
    public static IEnumerable<(string Text, OverlayKind? Kind)> Names => Choices.Select(c => (c.Text, c.Kind));

    private readonly Dictionary<OverlayKind, CheckBox> _boxes = [];
    private readonly CheckBox _none = new() { Name = "OverlayNone" };
    private readonly VBoxContainer _from = new() { Name = "ReachFrom", Visible = false };
    private readonly OverlayLegend _legend = new() { Name = "OverlayLegend", Visible = false };
    private MapEditorSession _editor = null!;
    private Func<GameState, ZoneReading> _zones = null!;
    private MapViewContext? _context;
    private OverlayView? _view;
    private bool _queued;

    /// <summary>The zones read last, and the map they were read for (the layout runs again only when its description or size changes).</summary>
    private (string Key, ZoneReading Reading)? _zoneCache;

    /// <summary>
    /// How far the map's content moved in this session (resizes, crops and shifts, and their undos, add up): the generated zones move with it.
    /// A move in an earlier session, saved, cannot be told.
    /// </summary>
    private (int X, int Y) _moved;

    /// <summary>The overlay shown; null for none.</summary>
    public OverlayKind? Kind { get; private set; }

    /// <summary>Whose start reach is drawn from; null with <see cref="FromTile"/> for a chosen cell.</summary>
    private PlayerId? _fromPlayer;

    /// <summary>The cell reach is drawn from when it was chosen from the selection.</summary>
    private MapPosition? FromTile { get; set; }

    /// <summary>
    /// The session's map; <paramref name="zones"/> reads a map's generated zones (the app gives it the templates); the legend goes over
    /// <paramref name="mapView"/>.
    /// </summary>
    public void Bind(MapEditorSession editor, Func<GameState, ZoneReading> zones, Control mapView)
    {
        _editor = editor;
        _zones = zones;
        AddChild(new Label { Text = "One overlay at a time over the map, with its legend.", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        var group = new ButtonGroup();
        foreach (var (text, kind, tip) in Choices)
        {
            var box = kind is null ? _none : new CheckBox { Name = $"Overlay{kind}" };
            box.Text = text;
            box.TooltipText = tip;
            box.ButtonGroup = group;
            box.Toggled += on =>
            {
                if (on)
                {
                    Choose(kind);
                }
            };
            AddChild(box);
            if (kind is { } k)
            {
                _boxes[k] = box;
            }
        }

        _none.SetPressedNoSignal(true);
        AddChild(_from);
        mapView.AddChild(_legend);
        _legend.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomLeft);
        _legend.GrowVertical = GrowDirection.Begin;
        _legend.OffsetLeft = 12;
        _legend.OffsetBottom = -12;
        TreeExiting += () =>
        {
            _legend.QueueFree(); // a panel of its own map: it leaves with the panel
            if (_context is not null)
            {
                _context.LevelChanged -= Queue;
            }
        };

        // Every edit may change what an overlay shows.
        this.SubscribeWhileInTree<UndoHistoryChanged>(editor.Events, _ => Queue());
        this.SubscribeWhileInTree<MapResized>(editor.Events, e => _moved = (_moved.X + e.Dx, _moved.Y + e.Dy));
    }

    /// <summary>The map's views, made again for a new size of the map: the overlay is drawn there from now on.</summary>
    public void Attach(MapViewContext context, OverlayView view)
    {
        if (_context is not null)
        {
            _context.LevelChanged -= Queue;
        }

        _context = context;
        _view = view;
        context.LevelChanged += Queue;
        view.Bind(context);
        Queue();
    }

    /// <summary>Shows <paramref name="kind"/> (null: none), as if its box were clicked.</summary>
    public void Choose(OverlayKind? kind)
    {
        Kind = kind;
        var chosen = kind is { } k ? _boxes[k] : _none;
        foreach (var box in _boxes.Values.Append(_none))
        {
            box.SetPressedNoSignal(box == chosen); // without the signal the group does not let go of the box checked before
        }

        Queue();
    }

    /// <summary>The overlay is computed again once this frame's edits are done.</summary>
    private void Queue()
    {
        if (!_queued)
        {
            _queued = true;
            Callable.From(Refresh).CallDeferred();
        }
    }

    private void Refresh()
    {
        _queued = false;
        if (!IsInsideTree() || _context is null || _view is null)
        {
            return;
        }

        var state = _editor.Scenario;
        var level = _context.Level;
        var starts = Kind == OverlayKind.Reach ? new MapAnalysis(state).Starts() : [];
        var from = Kind == OverlayKind.Reach ? From(starts) : null;
        ShowStarts(starts);
        var overlay = Kind switch
        {
            OverlayKind.Passability => MapOverlays.Passability(state, level),
            OverlayKind.Owners => MapOverlays.Owners(state, level),
            OverlayKind.GuardStrength => MapOverlays.GuardStrength(state, level),
            OverlayKind.Reach => from is not null ? MapOverlays.Reach(state, from, level) : null,
            OverlayKind.Zones => ZonesOverlay(state, level),
            OverlayKind.Height => MapOverlays.Height(state, level),
            _ => null,
        };
        _view.Show(overlay);
        var title = Choices.First(c => c.Kind == Kind).Text;
        _legend.Show(overlay, title, Kind == OverlayKind.Reach && overlay is null ? "No start: no player has a town or a hero on the map. Select a cell and choose \"The selected cell\"." : null);
    }

    private MapOverlay ZonesOverlay(GameState state, int level)
    {
        var key = $"{state.Map.Description}|{state.Map.Width}×{state.Map.Height}×{state.Map.Levels}";
        if (_zoneCache?.Key != key)
        {
            _zoneCache = (key, _zones(state));
        }

        var reading = _zoneCache.Value.Reading;
        if (reading.Zones is not { } zones || _moved == (0, 0))
        {
            return MapOverlays.Zones(state, level, reading.Zones, reading.Note);
        }

        var (dx, dy) = _moved;
        var moved = zones with { ZoneAt = t => zones.ZoneAt(new MapPosition(t.X - dx, t.Y - dy, t.Level)) };
        return MapOverlays.Zones(state, level, moved, $"{reading.Note} The map's content moved by {dx}, {dy} since: the zones moved with it.");
    }

    /// <summary>
    /// Where reach is drawn from now: the chosen cell (for the first player), else the chosen player's start, else the first start (the
    /// choice follows it); none when there is neither.
    /// </summary>
    private MapStart? From(IReadOnlyList<MapStart> starts)
    {
        if (FromTile is { } tile && _editor.Scenario.Map.Contains(tile) && _editor.Scenario.Players.Any())
        {
            return new MapStart(_editor.Scenario.Players.First().Id, tile, null);
        }

        var start = starts.FirstOrDefault(s => s.Player == _fromPlayer) ?? starts.FirstOrDefault();
        _fromPlayer = start?.Player;
        return start;
    }

    /// <summary>The reach overlay's choice of start: a button per player's start and one for the selection, under the overlays.</summary>
    private void ShowStarts(IReadOnlyList<MapStart> starts)
    {
        _from.Visible = Kind == OverlayKind.Reach;
        if (!_from.Visible)
        {
            return;
        }

        foreach (var child in _from.GetChildren())
        {
            _from.RemoveChild(child);
            child.QueueFree();
        }

        _from.AddChild(new Label { Text = "Reach from:" });
        var group = new ButtonGroup();
        var state = _editor.Scenario;
        foreach (var start in starts)
        {
            var name = state.GetPlayer(start.Player).Name;
            AddFrom($"{name}'s start ({start.Tile.X}, {start.Tile.Y})", start.Player == _fromPlayer && FromTile is null, () => (_fromPlayer, FromTile) = (start.Player, null));
        }

        var selected = _editor.Selection.Cells(state).FirstOrDefault();
        var tileText = FromTile is { } tile && _fromPlayer is null ? $"The chosen cell ({tile.X}, {tile.Y})" : "The selected cell";
        AddFrom(tileText, _fromPlayer is null && FromTile is not null, () =>
        {
            if (_editor.Selection.Cells(_editor.Scenario).Count > 0)
            {
                (_fromPlayer, FromTile) = (null, _editor.Selection.Cells(_editor.Scenario)[0]);
            }
        }, tooltip: selected is { } s ? $"From ({s.X}, {s.Y}), the first selected cell, for the first player" : "Select a cell first");

        void AddFrom(string text, bool pressed, Action choose, string? tooltip = null)
        {
            var box = new CheckBox { Text = text, ButtonGroup = group, ButtonPressed = pressed, TooltipText = tooltip ?? "" };
            box.Toggled += on =>
            {
                if (on)
                {
                    choose();
                    Queue();
                }
            };
            _from.AddChild(box);
        }
    }
}

/// <summary>An overlay's legend (ADR-264), over the map view: its name, a swatch and a text per band, and its note.</summary>
public partial class OverlayLegend : PanelContainer
{
    private readonly VBoxContainer _rows = new() { MouseFilter = MouseFilterEnum.Ignore }; // the map under the legend takes its clicks

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_rows);
    }

    /// <summary>Shows <paramref name="overlay"/>'s legend under <paramref name="title"/>, or <paramref name="empty"/> when there is no overlay to show.</summary>
    public void Show(MapOverlay? overlay, string title, string? empty)
    {
        foreach (var child in _rows.GetChildren())
        {
            _rows.RemoveChild(child);
            child.QueueFree();
        }

        Visible = overlay is not null || empty is not null;
        if (!Visible)
        {
            return;
        }

        _rows.AddChild(new Label { Text = title, ThemeTypeVariation = "HeaderSmall", MouseFilter = MouseFilterEnum.Ignore });
        foreach (var band in overlay?.Legend ?? [])
        {
            var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            row.AddChild(new ColorRect { Color = Palette.ForTint(band.Tint) with { A = 1 }, CustomMinimumSize = new Vector2(14, 14), SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore });
            row.AddChild(new Label { Text = band.Text, MouseFilter = MouseFilterEnum.Ignore });
            _rows.AddChild(row);
        }

        var note = overlay?.Note is { Length: > 0 } text ? text : empty;
        if (note is not null)
        {
            _rows.AddChild(new Label
            {
                Text = note, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(260, 0), MouseFilter = MouseFilterEnum.Ignore,
            });
        }
    }
}
