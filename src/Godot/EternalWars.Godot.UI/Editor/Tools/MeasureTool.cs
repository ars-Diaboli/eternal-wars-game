using EternalWars.Core.Domain.Analysis;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Heroes;
using Godot;

namespace EternalWars.Godot.UI.Editor.Tools;

/// <summary>
/// Measures the way between two tiles (U, ADR-283): the first click picks where a hero starts, and the route the game's pathfinding takes
/// follows the pointer from there; the second click keeps it. The route is drawn on the map as the game draws a hero's, by day; its steps,
/// cost and days for the movement points picked go to the status bar. A third click starts again. Nothing is edited.
/// </summary>
public sealed class MeasureTool(EditorToolContext context) : EditorTool(context)
{
    private int _points = MovementAllowance.NoArmyBase;
    private PlayerId _player = context.Editor.Scenario.Players[0].Id;
    private MapPosition? _from;
    private MapPosition? _to;

    /// <summary>Whether the second click kept the route: the pointer no longer moves its end.</summary>
    private bool _kept;

    public override string Name => "Measure";

    public override UiIcon? Icon => UiIcon.ToolMeasure;

    public override Key? Shortcut => Key.U;

    public override string ClickHint => "Measure: click where a hero starts, then where it goes: the route, its steps and days";

    /// <summary>The player whose hero walks: the picked one, or the first when that player has left the map.</summary>
    private PlayerId Player => Editor.Scenario.HasPlayer(_player) ? _player : Editor.Scenario.Players[0].Id;

    public override void BuildOptions(ToolOptions options)
    {
        var points = new SpinBox
        {
            MinValue = MapMeasure.MinPoints, MaxValue = MapMeasure.MaxPoints, Step = 50, Value = _points,
            TooltipText = $"The hero's movement points a day ({MovementAllowance.NoArmyBase} with no army; 2000 with the fastest)",
        };
        points.ValueChanged += v =>
        {
            _points = (int)v;
            Show(say: true);
        };
        options.Labelled("Points a day", points);
        options.Choice("Player", Editor.Scenario.Players.Select(p => (p.Name, p.Id)), Player, p =>
        {
            _player = p;
            Show(say: true);
        });
    }

    public override void Hover(MapPosition? tile)
    {
        if (_from is not null && !_kept && tile is { } to && to != _to)
        {
            _to = to;
            Show(say: true);
        }
    }

    public override void Press(MapPosition tile)
    {
        if (_from is null || _kept)
        {
            (_from, _to, _kept) = (tile, null, false);
            Show(say: false);
            Context.Say("Measure: now point at where the hero goes, and click to keep the route");
            return;
        }

        (_to, _kept) = (tile, true);
        Show(say: true);
    }

    public override void Refresh()
    {
        var map = Editor.Scenario.Map;
        if (_from is { } from && !map.Contains(from) || _to is { } to && !map.Contains(to))
        {
            Cancel(); // a resize or a crop left the measured tiles off the map
            return;
        }

        Show(say: _kept);
    }

    public override void Cancel()
    {
        (_from, _to, _kept) = (null, null, false);
        Context.Marks?.ShowBrush(null);
        Context.Route?.Clear();
    }

    /// <summary>Outlines the start and draws the route to the end, if there is one; with <paramref name="say"/> the status bar tells its numbers.</summary>
    private void Show(bool say)
    {
        Context.Marks?.ShowBrush(_from is { } start ? [start] : null);
        if (_from is not { } from || _to is not { } to)
        {
            Context.Route?.Clear();
            return;
        }

        var route = MapMeasure.Route(Editor.Scenario, from, to, Player, _points);
        if (route is null)
        {
            Context.Route?.Clear();
        }
        else
        {
            Context.Route?.Show(route.Path);
        }

        if (say)
        {
            Context.Say(Describe(from, to, route));
        }
    }

    /// <summary>"Measure 3, 4 → 15, 4: 12 steps, 1500 points, 1 day at 1500 a day".</summary>
    private static string Describe(MapPosition from, MapPosition to, MeasuredRoute? route) =>
        $"Measure {from.X}, {from.Y} → {to.X}, {to.Y}: " + (route is null
            ? "no route a hero can walk"
            : $"{route.Steps} step{(route.Steps == 1 ? "" : "s")}, {route.Cost} points, {route.Days} day{(route.Days == 1 ? "" : "s")} at {route.DailyPoints} a day");
}
