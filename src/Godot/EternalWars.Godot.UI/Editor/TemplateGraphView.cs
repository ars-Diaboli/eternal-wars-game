using System.Globalization;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Generation;
using EternalWars.Godot.Shared.Map;
using Godot;

namespace EternalWars.Godot.UI.Editor;

/// <summary>What is selected in the zone graph: zones (by id, ADR-271), a connection (by index) or nothing.</summary>
public sealed record GraphSelection
{
    public static GraphSelection None { get; } = new();

    public IReadOnlyList<string> Zones { get; private init; } = [];

    public int? Connection { get; private init; }

    /// <summary>The one selected zone; null when none or several are selected.</summary>
    public string? Zone => Zones.Count == 1 ? Zones[0] : null;

    public static GraphSelection Of(string zone) => new() { Zones = [zone] };

    public static GraphSelection Of(IEnumerable<string> zones) => new() { Zones = [.. zones.Distinct()] };

    public static GraphSelection OfConnection(int connection) => new() { Connection = connection };

    public bool Contains(string zone) => Zones.Contains(zone);
}

/// <summary>
/// The zone graph of the template editor (ADR-117, ADR-270), drawn in code. Each zone is a card: a header in its type's colour (a start zone in
/// its player's), its id, type, owner and team, size, monster strength, towns and mines; zoomed far out, only the header. Each connection is a
/// line (wide: thick, gate and portal: dashed) labelled with its kind, guard, guard value, passages and chance.
/// Left click selects a zone or a connection, Ctrl or Shift + click adds or drops a zone, a left drag on empty ground draws a marquee (ADR-271);
/// a left drag on a zone moves it, or the whole selection when it is part of it, on the grid while <see cref="SnapToGrid"/> is on. A right drag
/// from one zone to another joins them; a middle drag, or a right drag on empty ground, pans; the wheel zooms.
/// Changes go to the <see cref="TemplateEditorSession"/>; the view redraws on <see cref="TemplateChanged"/> (bound by the panel). Delete, copy,
/// paste and the rest are the editor's commands, which act on <see cref="Selection"/>.
/// </summary>
public partial class TemplateGraphView : Control
{
    /// <summary>A card's size in pixels, and the header-only card's when zoomed out below <see cref="CompactScale"/>.</summary>
    private static readonly Vector2 CardSize = new(150, 84);

    private static readonly Vector2 CompactSize = new(110, 22);
    private const float CompactScale = 50;
    private const float HeaderHeight = 20;

    private static readonly Color Background = new(0.09f, 0.1f, 0.12f);
    private static readonly Color GridLine = new(0.14f, 0.15f, 0.18f);
    private static readonly Color CardBody = new(0.15f, 0.16f, 0.19f);
    private static readonly Color CardText = new(0.85f, 0.86f, 0.88f);
    private static readonly Color Selected = Colors.Yellow;
    private static readonly Color MirrorLine = new(0.35f, 0.75f, 0.85f, 0.7f);

    private TemplateEditorSession _session = null!;
    private Vector2 _pan;
    private float _scale = 70;
    private Vector2 _mouse;
    private bool _hovering;

    /// <summary>A zone drag: the zone held, where the press was in the graph, and every moving zone's place at the press.</summary>
    private (string Zone, (double X, double Y) Press, Dictionary<string, (double X, double Y)> Start)? _drag;

    /// <summary>Where the zone was pressed on the screen; the zones move only once the pointer is <see cref="DragThreshold"/> away (a click moves nothing).</summary>
    private Vector2 _pressedAt;

    private bool _dragMoved;
    private const float DragThreshold = 4;

    /// <summary>The press on a zone of a multi-selection without moving: the release selects just that zone.</summary>
    private string? _narrowOnRelease;

    private string? _connectFrom;
    private bool _panning;

    /// <summary>A marquee: where it began, and the zones selected before it when it adds to them (Ctrl or Shift).</summary>
    private (Vector2 From, IReadOnlyList<string> Kept)? _marquee;

    public GraphSelection Selection { get; private set; } = GraphSelection.None;

    public event Action<GraphSelection>? SelectionChanged;

    /// <summary>Dragged zones and aligned rows land on the grid of <see cref="TemplateGraph.Grid"/> units, which the view draws (ADR-270).</summary>
    public bool SnapToGrid
    {
        get => _snap;
        set
        {
            _snap = value;
            QueueRedraw();
        }
    }

    private bool _snap = true;

    /// <summary>The grid aligned zones land on: <see cref="TemplateGraph.Grid"/> with <see cref="SnapToGrid"/>, else none.</summary>
    public double Grid => SnapToGrid ? TemplateGraph.Grid : 0;

    public void Bind(TemplateEditorSession session)
    {
        _session = session;
        ClipContents = true;
        FocusMode = FocusModeEnum.All;
        MouseFilter = MouseFilterEnum.Stop;
        Resized += QueueRedraw;
        MouseEntered += () => _hovering = true;
        MouseExited += () => _hovering = false;
    }

    /// <summary>Selects zones, a connection or nothing as if clicked, and tells the panel.</summary>
    public void Select(GraphSelection selection)
    {
        Selection = selection;
        SelectionChanged?.Invoke(selection);
        QueueRedraw();
    }

    /// <summary>Every zone (Ctrl+A on the Template tab).</summary>
    public void SelectAll() => Select(GraphSelection.Of(_session.Template.Zones.Select(z => z.Id)));

    /// <summary>Centres the graph and drops the selected zones and connection that are gone.</summary>
    public void Refresh(bool centre = false)
    {
        var template = _session.Template;
        if (Selection.Connection is { } c && c >= template.Connections.Count)
        {
            Select(GraphSelection.None);
        }
        else if (Selection.Zones.Any(z => template.Zone(z) is null))
        {
            Select(GraphSelection.Of(Selection.Zones.Where(z => template.Zone(z) is not null)));
        }

        if (centre)
        {
            _pan = Vector2.Zero;
        }

        QueueRedraw();
    }

    private Vector2 ToScreen((double X, double Y) place) => (Size / 2) + _pan + (new Vector2((float)place.X, (float)place.Y) * _scale);

    private Vector2 ToScreen(TemplateZone zone) => ToScreen(TemplateGraph.Place(zone));

    private (double X, double Y) ToGraph(Vector2 screen)
    {
        var graph = (screen - (Size / 2) - _pan) / _scale;
        return (graph.X, graph.Y);
    }

    private bool Compact => _scale < CompactScale;

    private Rect2 Card(TemplateZone zone)
    {
        var size = Compact ? CompactSize : CardSize;
        return new Rect2(ToScreen(zone) - (size / 2), size);
    }

    private TemplateZone? ZoneAt(Vector2 screen) => _session.Template.Zones.LastOrDefault(z => Card(z).HasPoint(screen));

    private int? ConnectionAt(Vector2 screen)
    {
        var template = _session.Template;
        for (var i = template.Connections.Count - 1; i >= 0; i--)
        {
            if (Ends(template, template.Connections[i]) is { } ends
                && (Geometry2D.GetClosestPointToSegment(screen, ends.From, ends.To).DistanceTo(screen) <= 6
                    || LabelBox(template, template.Connections[i], ends).HasPoint(screen)))
            {
                return i;
            }
        }

        return null;
    }

    private (Vector2 From, Vector2 To)? Ends(MapTemplate template, TemplateConnection connection) =>
        template.Zone(connection.From) is { } a && template.Zone(connection.To) is { } b ? (ToScreen(a), ToScreen(b)) : null;

    /// <summary>Where a new zone goes: the middle of the view, a little off any zone already there.</summary>
    public (double X, double Y) FreeSpot()
    {
        var (x, y) = ToGraph(Size / 2);
        (x, y) = (TemplateGraph.Snap(x, Grid), TemplateGraph.Snap(y, Grid));
        while (TemplateGraph.Crowded((x, y), _session.Template.Zones.Select(TemplateGraph.Place)))
        {
            x += 1;
            y += 1.5;
        }

        return (x, y);
    }

    /// <summary>
    /// Where Paste puts the middle of <paramref name="fragment"/>: under the pointer while it is over the graph, else the middle of the view;
    /// while snapping, moved by whole grid steps from where the zones were copied, so zones on the grid stay on it.
    /// </summary>
    public (double X, double Y) PasteSpot(TemplateFragment fragment)
    {
        ArgumentNullException.ThrowIfNull(fragment);
        var (x, y) = ToGraph(_hovering ? _mouse : Size / 2);
        var (cx, cy) = fragment.Centre;
        return (cx + TemplateGraph.Snap(x - cx, Grid), cy + TemplateGraph.Snap(y - cy, Grid));
    }

    /// <summary>A drag ends when the graph is hidden (another tab shows) as well as on the release.</summary>
    public override void _Notification(int what)
    {
        if (what == NotificationVisibilityChanged && !IsVisibleInTree())
        {
            EndDrag();
            _marquee = null;
            _panning = false;
            _connectFrom = null;
        }
    }

    /// <summary>Ends a zone drag (one undo step); the editor calls it before a command changes the template (Delete, paste, align).</summary>
    public void EndDrag()
    {
        _session.EndStep();
        _drag = null;
        _dragMoved = false;
        _narrowOnRelease = null;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouse mouse)
        {
            _mouse = mouse.Position; // a click moves no pointer, yet Paste goes where it was
        }

        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } press:
                GrabFocus();
                Press(press.Position, press.CtrlPressed || press.ShiftPressed);
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } release:
                if (_marquee is { } marquee)
                {
                    var box = new Rect2(marquee.From, Vector2.Zero).Expand(release.Position);
                    var inside = _session.Template.Zones.Where(z => box.Intersects(Card(z))).Select(z => z.Id);
                    _marquee = null;
                    Select(GraphSelection.Of([.. marquee.Kept, .. inside]));
                }

                var narrow = _narrowOnRelease;
                EndDrag();
                if (narrow is not null)
                {
                    Select(GraphSelection.Of(narrow)); // a click on one zone of several, without a drag
                }

                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true } press:
                _connectFrom = ZoneAt(press.Position)?.Id;
                _panning = _connectFrom is null;
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: false } release:
                _panning = false;
                EndDrag(); // a connection is a step of its own, not part of a drag
                if (_connectFrom is { } from && ZoneAt(release.Position) is { } to && to.Id != from && _session.Connect(from, to.Id))
                {
                    Select(GraphSelection.OfConnection(_session.Template.Connections.Count - 1));
                }

                _connectFrom = null;
                QueueRedraw();
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Middle } middle:
                _panning = middle.Pressed;
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelUp, Pressed: true }:
                _scale = Math.Min(200, _scale * 1.1f);
                QueueRedraw();
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelDown, Pressed: true }:
                _scale = Math.Max(20, _scale / 1.1f);
                QueueRedraw();
                break;
            case InputEventMouseMotion motion:
                if (_drag is not null)
                {
                    DragTo(motion.Position);
                }
                else if (_panning)
                {
                    _pan += motion.Relative;
                    QueueRedraw();
                }
                else if (_connectFrom is not null || _marquee is not null)
                {
                    QueueRedraw();
                }

                break;
            default:
                return;
        }

        AcceptEvent();
    }

    /// <summary>A left press: on a zone it selects (or, <paramref name="adding"/>, adds or drops it) and starts a drag; on empty ground a marquee.</summary>
    private void Press(Vector2 at, bool adding)
    {
        EndDrag(); // a drag whose release never came ends here
        if (ZoneAt(at) is { } zone)
        {
            if (adding)
            {
                Select(Selection.Contains(zone.Id)
                    ? GraphSelection.Of(Selection.Zones.Where(z => z != zone.Id))
                    : GraphSelection.Of([.. Selection.Zones, zone.Id]));
                if (!Selection.Contains(zone.Id))
                {
                    return; // dropped from the selection: nothing to drag
                }
            }
            else if (Selection.Contains(zone.Id) && Selection.Zones.Count > 1)
            {
                _narrowOnRelease = zone.Id; // the whole selection drags; a click without a drag keeps only this one
            }
            else
            {
                Select(GraphSelection.Of(zone.Id));
            }

            var moving = Selection.Zones.Select(id => _session.Template.Zone(id)).OfType<TemplateZone>().ToDictionary(z => z.Id, TemplateGraph.Place);
            _drag = (zone.Id, ToGraph(at), moving);
            _pressedAt = at;
            _session.BeginStep(moving.Count == 1 ? $"Move zone {zone.Id}" : $"Move {moving.Count} zones"); // the whole drag is one undo step (ADR-268)
        }
        else if (ConnectionAt(at) is { } connection)
        {
            Select(GraphSelection.OfConnection(connection));
        }
        else
        {
            var kept = adding ? Selection.Zones : [];
            _marquee = (at, kept);
            Select(GraphSelection.Of(kept));
        }
    }

    /// <summary>The held zone follows the pointer (on the grid while snapping), and the other moving zones keep their places beside it.</summary>
    private void DragTo(Vector2 at)
    {
        if (!_session.IsStepOpen)
        {
            _drag = null; // an undo or another template ended the drag's step: the rest of the drag moves nothing
            _narrowOnRelease = null;
            return;
        }

        if (!_dragMoved)
        {
            if (at.DistanceTo(_pressedAt) < DragThreshold)
            {
                return; // a click's jitter: a zone off the grid does not jump onto it
            }

            _dragMoved = true;
            _narrowOnRelease = null; // it is a drag: the selection stays as it is
        }

        var (held, press, start) = _drag!.Value;
        var pointer = ToGraph(at);
        var (hx, hy) = start[held];
        var dx = TemplateGraph.Snap(hx + pointer.X - press.X, Grid) - hx;
        var dy = TemplateGraph.Snap(hy + pointer.Y - press.Y, Grid) - hy;

        _session.MoveZones(start.ToDictionary(p => p.Key, p => (Math.Round(p.Value.X + dx, 2), Math.Round(p.Value.Y + dy, 2))));
    }

    // Drawing ------------------------------------------------------------------------------------

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), Background);
        if (SnapToGrid)
        {
            DrawGrid();
        }

        var template = _session.Template;
        for (var i = 0; i < template.Connections.Count; i++)
        {
            DrawConnection(template, i);
        }

        // A zone and its mirror (ADR-117, ADR-272): a thin dotted line, under the cards.
        foreach (var zone in template.Zones)
        {
            if (zone.Mirror is { } mirror && mirror != zone.Id && template.Zone(mirror) is { } image)
            {
                DrawDashedLine(ToScreen(zone), ToScreen(image), MirrorLine, 1, 3);
            }
        }

        if (_connectFrom is { } start && template.Zone(start) is { } origin)
        {
            DrawDashedLine(ToScreen(origin), _mouse, Selected, 2, 6);
        }

        foreach (var zone in template.Zones)
        {
            DrawCard(zone);
        }

        for (var i = 0; i < template.Connections.Count; i++)
        {
            DrawConnectionLabel(template, i);
        }

        if (_marquee is { } marquee)
        {
            var box = new Rect2(marquee.From, Vector2.Zero).Expand(_mouse);
            DrawRect(box, new Color(Selected, 0.12f));
            DrawRect(box, Selected, filled: false, width: 1);
        }

        DrawString(ThemeDB.FallbackFont, new Vector2(8, Size.Y - 8),
            "LMB select / drag · Ctrl+click: add · drag empty: marquee · RMB zone→zone: connect · MMB or RMB empty: pan · wheel: zoom",
            fontSize: 12, modulate: UiTheme.TextQuiet);
    }

    /// <summary>The snapping grid: a line every grid step, a brighter one every graph unit; too dense when zoomed far out, only the units.</summary>
    private void DrawGrid()
    {
        var step = (float)TemplateGraph.Grid * _scale;
        var origin = (Size / 2) + _pan;
        var every = step < 12 ? 2 : 1;
        for (var axis = 0; axis < 2; axis++)
        {
            var length = axis == 0 ? Size.X : Size.Y;
            var o = axis == 0 ? origin.X : origin.Y;
            var first = (int)Math.Floor(-o / step);
            for (var n = first; (n * step) + o <= length; n++)
            {
                if (n % every != 0)
                {
                    continue;
                }

                var at = (n * step) + o;
                var colour = n % 2 == 0 ? GridLine.Lightened(0.08f) : GridLine;
                if (axis == 0)
                {
                    DrawLine(new Vector2(at, 0), new Vector2(at, Size.Y), colour);
                }
                else
                {
                    DrawLine(new Vector2(0, at), new Vector2(Size.X, at), colour);
                }
            }
        }
    }

    private static Color KindColour(ConnectionKind kind) => kind switch
    {
        ConnectionKind.Wide => new Color(0.45f, 0.75f, 0.45f),
        ConnectionKind.Gate => new Color(0.7f, 0.45f, 0.9f),
        ConnectionKind.Portal => new Color(0.35f, 0.8f, 0.9f),
        _ => new Color(0.7f, 0.7f, 0.7f),
    };

    /// <summary>A connection's label: its kind, its guard (a normal one's, when not monsters), the guard value, passages above one and a chance below 100.</summary>
    public static string Label(MapTemplate template, TemplateConnection connection)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(connection);
        var parts = new List<string> { connection.Kind.ToString() };
        if (connection.Kind != ConnectionKind.Wide)
        {
            var guard = template.GuardOf(connection);
            var value = connection.GuardValue == 0 ? "unguarded" : connection.GuardValue.ToString("N0", CultureInfo.InvariantCulture);
            parts.Add(guard == ConnectionGuard.Monsters ? value : $"{Words(guard.ToString())} {value}");
        }

        if (connection.Passages > 1)
        {
            parts.Add($"×{connection.Passages}");
        }

        if (connection.Chance < 100)
        {
            parts.Add($"{connection.Chance}%");
        }

        return string.Join(" · ", parts);
    }

    /// <summary>A name in words: <c>BorderGuard</c> → <c>border guard</c>.</summary>
    private static string Words(string name) =>
        string.Concat(name.Select((c, i) => char.IsUpper(c) && i > 0 ? $" {char.ToLowerInvariant(c)}" : char.ToLowerInvariant(c).ToString()));

    private Rect2 LabelBox(MapTemplate template, TemplateConnection connection, (Vector2 From, Vector2 To) ends)
    {
        var size = ThemeDB.FallbackFont.GetStringSize(Label(template, connection), fontSize: 11) + new Vector2(8, 4);
        return new Rect2(((ends.From + ends.To) / 2) - (size / 2), size); // the middle of the line is the middle of its part between the cards
    }

    private void DrawConnection(MapTemplate template, int index)
    {
        var connection = template.Connections[index];
        if (Ends(template, connection) is not { } ends)
        {
            return;
        }

        var colour = Selection.Connection == index ? Selected : KindColour(connection.Kind);
        if (connection.Kind is ConnectionKind.Gate or ConnectionKind.Portal)
        {
            DrawDashedLine(ends.From, ends.To, colour, 2, 8);
        }
        else
        {
            DrawLine(ends.From, ends.To, colour, connection.Kind == ConnectionKind.Wide ? 6 : 2, antialiased: true);
        }
    }

    /// <summary>A connection's label, over the cards so that it always reads.</summary>
    private void DrawConnectionLabel(MapTemplate template, int index)
    {
        var connection = template.Connections[index];
        if (Ends(template, connection) is not { } ends)
        {
            return;
        }

        var colour = Selection.Connection == index ? Selected : KindColour(connection.Kind);
        var label = Label(template, connection);
        var box = LabelBox(template, connection, ends);
        DrawRect(box, Background.Lightened(0.05f));
        DrawRect(box, colour, filled: false, width: 1);
        DrawString(ThemeDB.FallbackFont, box.Position + new Vector2(4, box.Size.Y - 5), label, fontSize: 11, modulate: colour);
    }

    /// <summary>A zone type's colour: a start zone in its player's (darker for the computer's), treasure gold, a junction grey.</summary>
    private static Color TypeColour(TemplateZone zone) => zone.Type switch
    {
        ZoneType.PlayerStart when zone.Owner is { } owner => Palette.ForSlot(new PlayerId(owner)),
        ZoneType.AiStart when zone.Owner is { } owner => Palette.ForSlot(new PlayerId(owner)).Darkened(0.4f),
        ZoneType.Treasure => new Color(0.75f, 0.6f, 0.2f),
        _ => new Color(0.45f, 0.47f, 0.5f),
    };

    private static UiIcon TypeIcon(ZoneType type) => type switch
    {
        ZoneType.PlayerStart => UiIcon.ZoneStart,
        ZoneType.AiStart => UiIcon.ZoneAiStart,
        ZoneType.Treasure => UiIcon.ZoneTreasure,
        _ => UiIcon.ZoneJunction,
    };

    private void DrawCard(TemplateZone zone)
    {
        var font = ThemeDB.FallbackFont;
        var card = Card(zone);
        var header = new Rect2(card.Position, new Vector2(card.Size.X, Math.Min(HeaderHeight, card.Size.Y)));
        var colour = TypeColour(zone);
        DrawRect(card, CardBody);
        DrawRect(header, colour);
        var text = colour.Luminance > 0.55f ? Colors.Black : Colors.White;
        var x = header.Position.X + 4;
        if (UiIcons.Get(TypeIcon(zone.Type), 16) is { } icon)
        {
            DrawTextureRect(icon, new Rect2(x, header.Position.Y + 2, 16, 16), tile: false);
            x += 19;
        }

        DrawString(font, new Vector2(x, header.End.Y - 5), zone.Id + (zone.Level == 1 ? " (U)" : ""), HorizontalAlignment.Left,
            card.End.X - x - 4, 13, text);
        if (!Compact)
        {
            var y = header.End.Y + 13;
            foreach (var line in CardLines(zone))
            {
                DrawString(font, new Vector2(card.Position.X + 5, y), line, HorizontalAlignment.Left, card.Size.X - 8, 11, CardText);
                y += 15;
            }
        }

        var selected = Selection.Contains(zone.Id);
        DrawRect(card, selected ? Selected : zone.Level == 1 ? new Color(0.6f, 0.3f, 0.8f) : Colors.Black, filled: false, width: selected ? 3 : 1);
    }

    /// <summary>A card's lines under its header: the type with its owner and team and the size, the monsters, the towns, the mines.</summary>
    public static IReadOnlyList<string> CardLines(TemplateZone zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        var type = zone.Type switch
        {
            ZoneType.PlayerStart => $"Start P{zone.Owner + 1}",
            ZoneType.AiStart => $"AI start P{zone.Owner + 1}",
            _ => zone.Type.ToString(),
        };
        if (zone.Team is { } team)
        {
            type += $" T{team + 1}";
        }

        string Count(CountRange range) => range.Min == range.Max ? $"{range.Min}" : $"{range.Min}–{range.Max}";
        var towns = new List<string>();
        if (zone.PlayerTowns.Max > 0)
        {
            towns.Add($"{Count(zone.PlayerTowns)} own");
        }

        if (zone.NeutralTowns.Max > 0)
        {
            towns.Add($"{Count(zone.NeutralTowns)} neutral");
        }

        var mines = zone.Mines.Where(m => m.Value > 0).OrderBy(m => m.Key).Select(m => $"{Letter(m.Key)}{m.Value}").ToList();
        return
        [
            $"{type} · size {zone.Size}",
            $"Monsters: {zone.Monsters.ToString().ToLowerInvariant()}",
            towns.Count == 0 ? "No towns" : $"Towns: {string.Join(", ", towns)}",
            mines.Count == 0 ? "No mines" : $"Mines: {string.Join(" ", mines)}",
        ];
    }

    /// <summary>A resource's letter on a card: G(old), W(ood, lumber), O(re), C(rystal), M(ercury), S(ulfur).</summary>
    private static string Letter(ResourceType resource) => resource switch
    {
        ResourceType.Lumber => "W",
        _ => resource.ToString()[..1],
    };
}
