using EternalWars.Core.Domain.Common;

namespace EternalWars.Core.Domain.Pathfinding;

/// <summary>How a route marker is shaded by the day it is reached (ADR-309): today's, then the later days alternating two shades.</summary>
public enum RouteShade
{
    Today,
    LaterOdd,
    LaterEven,
}

/// <summary>A "Day N" tag on a route: at step <see cref="Step"/>, the first one reached on <see cref="Date"/> (<see cref="PathStep.Day"/> days from today).</summary>
public sealed record RouteDayTag(int Step, int Day, GameDate Date)
{
    public string Text => $"Day {Date.AbsoluteDay}";
}

/// <summary>
/// The days along a route, for its markers and tags (ADR-309): each step's day is <see cref="PathStep.Day"/>, as the pathfinder plans it
/// with the hero's movement left today and a full day's after. A tag stands at the start of the first two days and of the last.
/// </summary>
public static class RouteDays
{
    /// <summary>The shade of a marker reached <paramref name="day"/> days from today.</summary>
    public static RouteShade ShadeOf(int day) => day <= 0 ? RouteShade.Today : day % 2 == 1 ? RouteShade.LaterOdd : RouteShade.LaterEven;

    /// <summary>
    /// The tags of <paramref name="path"/> planned on <paramref name="today"/>: on the first step of the route's first two days and of its last,
    /// each once, in route order (a hero with no movement left today starts tomorrow). A route that ends on its first day has one tag.
    /// </summary>
    public static IReadOnlyList<RouteDayTag> Tags(HeroPath path, GameDate today)
    {
        ArgumentNullException.ThrowIfNull(path);
        var lastDay = path.Steps[^1].Day;
        var tags = new List<RouteDayTag>();
        var daysStarted = 0;
        for (var i = 0; i < path.Steps.Count; i++)
        {
            var day = path.Steps[i].Day;
            if (i > 0 && path.Steps[i - 1].Day == day)
            {
                continue;
            }

            if (++daysStarted <= 2 || day == lastDay)
            {
                tags.Add(new RouteDayTag(i, day, new GameDate(today.AbsoluteDay + day)));
            }
        }

        return tags;
    }
}
