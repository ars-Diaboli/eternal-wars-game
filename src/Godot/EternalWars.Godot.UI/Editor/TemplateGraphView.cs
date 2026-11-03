using EternalWars.Core.Domain.Common;
using EternalWars.Core.Generation;
using EternalWars.Godot.Shared.Map;
using Godot;

namespace EternalWars.Godot.UI.Editor;

/// <summary>What is selected in the zone graph: a zone (by id), a connection (by index) or nothing.</summary>
public sealed record GraphSelection(string? Zone, int? Connection)
{
    public static GraphSelection None { get; } = new(null, null);
}

/// <summary>
/// The zone graph of the template editor (ADR-117), drawn in code: a circle per zone (its size by weight, its colour by type, a start zone in its
/// player's colour), a line per connection (wide: thick, gate: purple, portal: cyan, its guard value written on it). Left click selects, left drag
/// moves a zone, right drag from one zone to another joins them, a drag on empty ground pans, the wheel zooms, Delete removes the selection.
/// Changes go to the <see cref="TemplateEditorSession"/>; the view redraws on <see cref="TemplateChanged"/> (bound by the panel).
/// </summary>
public partial class TemplateGraphView : Control
{
    private const float NodeRadius = 16;

    private TemplateEditorSession _session = null!;
    private Vector2 _pan;
    private float _scale = 70;
    private string? _dragging;
    private string? _connectFrom;
    private bool _panning;
    private Vector2 _mouse;

    public GraphSelection Selection { get; private set; } = GraphSelection.None;

    public event Action<GraphSelection>? SelectionChanged;

    public void Bind(TemplateEditorSession session)
    {
        _session = session;
        ClipContents = true;
        FocusMode = FocusModeEnum.All;
        MouseFilter = MouseFilterEnum.Stop;
        Resized += QueueRedraw;
    }

    /// <summary>Selects a zone or connection (or nothing) as if clicked, and tells the panel.</summary>
    public void Select(GraphSelection selection)
    {
        Selection = selection;
        SelectionChanged?.Invoke(selection);
        QueueRedraw();
    }

    /// <summary>Centres the graph and drops a selection whose zone or connection is gone.</summary>
    public void Refresh(bool centre = false)
    {
        if ((Selection.Zone is { } zone && _session.Template.Zone(zone) is null) || (Selection.Connection is { } c && c >= _session.Template.Connections.Count))
        {
            Select(GraphSelection.None);
        }

        if (centre)
        {
            _pan = Vector2.Zero;
        }

        QueueRedraw();
    }

    private Vector2 ToScreen(TemplateZone zone) => (Size / 2) + _pan + (new Vector2((float)(zone.GraphX ?? 0), (float)(zone.GraphY ?? 0)) * _scale);

    private (double X, double Y) ToGraph(Vector2 screen)
    {
        var graph = (screen - (Size / 2) - _pan) / _scale;
        return (Math.Round(graph.X, 2), Math.Round(graph.Y, 2));
    }

    private float Radius(TemplateZone zone) => NodeRadius + (zone.Size * 5);

    private TemplateZone? ZoneAt(Vector2 screen) =>
        _session.Template.Zones.LastOrDefault(z => ToScreen(z).DistanceTo(screen) <= Radius(z));

    private int? ConnectionAt(Vector2 screen)
    {
        var template = _session.Template;
        for (var i = template.Connections.Count - 1; i >= 0; i--)
        {
            if (template.Zone(template.Connections[i].From) is { } a && template.Zone(template.Connections[i].To) is { } b
                && Geometry2D.GetClosestPointToSegment(screen, ToScreen(a), ToScreen(b)).DistanceTo(screen) <= 6)
            {
                return i;
            }
        }

        return null;
    }

    public override void _GuiInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } press:
                GrabFocus();
                if (ZoneAt(press.Position) is { } zone)
                {
                    _dragging = zone.Id;
                    Select(new GraphSelection(zone.Id, null));
                }
                else if (ConnectionAt(press.Position) is { } connection)
                {
                    Select(new GraphSelection(null, connection));
                }
                else
                {
                    _panning = true;
                    Select(GraphSelection.None);
                }

                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false }:
                _dragging = null;
                _panning = false;
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true } press:
                _connectFrom = ZoneAt(press.Position)?.Id;
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: false } release:
                if (_connectFrom is { } from && ZoneAt(release.Position) is { } to && to.Id != from && _session.Connect(from, to.Id))
                {
                    Select(new GraphSelection(null, _session.Template.Connections.Count - 1));
                }

                _connectFrom = null;
                QueueRedraw();
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
                _mouse = motion.Position;
                if (_dragging is { } id)
                {
                    var (x, y) = ToGraph(motion.Position);
                    _session.MoveZone(id, x, y);
                }
                else if (_panning)
                {
                    _pan += motion.Relative;
                    QueueRedraw();
                }
                else if (_connectFrom is not null)
                {
                    QueueRedraw();
                }

                break;
            case InputEventKey { Pressed: true, Keycode: Key.Delete }:
                if (Selection.Zone is { } selected)
                {
                    _session.RemoveZone(selected);
                }
                else if (Selection.Connection is { } index)
                {
                    _session.Disconnect(index);
                }

                Select(GraphSelection.None);
                break;
            default:
                return;
        }

        AcceptEvent();
    }

    /// <summary>Where a new zone goes: the middle of the view, a little off any zone already there.</summary>
    public (double X, double Y) FreeSpot()
    {
        var (x, y) = ToGraph(Size / 2);
        while (_session.Template.Zones.Any(z => Math.Abs((z.GraphX ?? 0) - x) < 0.6 && Math.Abs((z.GraphY ?? 0) - y) < 0.6))
        {
            x += 0.8;
            y += 0.5;
        }

        return (x, y);
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.09f, 0.1f, 0.12f));
        var font = ThemeDB.FallbackFont;
        var template = _session.Template;
        for (var i = 0; i < template.Connections.Count; i++)
        {
            var c = template.Connections[i];
            if (template.Zone(c.From) is not { } a || template.Zone(c.To) is not { } b)
            {
                continue;
            }

            var (from, to) = (ToScreen(a), ToScreen(b));
            var colour = c.Kind switch
            {
                ConnectionKind.Wide => new Color(0.45f, 0.75f, 0.45f),
                ConnectionKind.Gate => new Color(0.7f, 0.45f, 0.9f),
                ConnectionKind.Portal => new Color(0.35f, 0.8f, 0.9f),
                _ => new Color(0.7f, 0.7f, 0.7f),
            };
            if (Selection.Connection == i)
            {
                colour = Colors.Yellow;
            }

            if (c.Kind is ConnectionKind.Gate or ConnectionKind.Portal)
            {
                DrawDashedLine(from, to, colour, 2, 8);
            }
            else
            {
                DrawLine(from, to, colour, c.Kind == ConnectionKind.Wide ? 6 : 2, antialiased: true);
            }

            var guard = template.GuardOf(c);
            var label = c.Kind == ConnectionKind.Wide ? "wide" : $"{c.GuardValue}{(guard == ConnectionGuard.Monsters ? "" : $" {guard}")}{(c.Chance < 100 ? $" {c.Chance}%" : "")}{(c.Passages > 1 ? $" ×{c.Passages}" : "")}";
            DrawString(font, ((from + to) / 2) + new Vector2(-20, -4), label, fontSize: 12, modulate: colour);
        }

        if (_connectFrom is { } start && template.Zone(start) is { } origin)
        {
            DrawDashedLine(ToScreen(origin), _mouse, Colors.Yellow, 2, 6);
        }

        foreach (var zone in template.Zones)
        {
            var at = ToScreen(zone);
            var radius = Radius(zone);
            var fill = zone.Type switch
            {
                ZoneType.PlayerStart when zone.Owner is { } owner => Palette.ForPlayer(new PlayerId(owner)),
                ZoneType.AiStart when zone.Owner is { } owner => Palette.ForPlayer(new PlayerId(owner)).Darkened(0.4f),
                ZoneType.Treasure => new Color(0.75f, 0.6f, 0.2f),
                _ => new Color(0.4f, 0.42f, 0.45f),
            };
            DrawCircle(at, radius, fill);
            DrawArc(at, radius, 0, Mathf.Tau, 32, Selection.Zone == zone.Id ? Colors.Yellow : zone.Level == 1 ? new Color(0.6f, 0.3f, 0.8f) : Colors.Black, Selection.Zone == zone.Id ? 3 : 2);
            var title = zone.Id + (zone.Level == 1 ? " (U)" : "");
            DrawString(font, at + new Vector2(-radius, radius + 14), title, fontSize: 13, modulate: Colors.White);
            var detail = zone.Type switch
            {
                ZoneType.PlayerStart or ZoneType.AiStart => $"P{zone.Owner + 1}{(zone.Team is { } team ? $" T{team + 1}" : "")}",
                _ => zone.Monsters.ToString(),
            };
            DrawString(font, at + new Vector2(-radius + 4, 4), detail, fontSize: 11, modulate: Colors.Black);
        }

        DrawString(font, new Vector2(8, Size.Y - 8), "LMB select / drag · RMB drag zone→zone: connect · drag empty: pan · wheel: zoom · Del: remove", fontSize: 12, modulate: new Color(0.7f, 0.7f, 0.7f));
    }
}
