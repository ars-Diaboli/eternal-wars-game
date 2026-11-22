using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Pathfinding;

namespace EternalWars.Core.Domain.Analysis;

/// <summary>
/// The route <see cref="MapMeasure.Route"/> found: its steps, each with the day it is walked on (0 the first), for a hero with
/// <see cref="DailyPoints"/> movement points a day.
/// </summary>
public sealed record MeasuredRoute(HeroPath Path, int DailyPoints)
{
    /// <summary>The steps walked (a gate's jump is no step).</summary>
    public int Steps => Path.Steps.Count(s => s.Kind == PathStepKind.Walk);

    /// <summary>The route's movement cost.</summary>
    public int Cost => Path.TotalCost;

    /// <summary>The days it takes, the first counted: a hero that starts the day fresh arrives on this day.</summary>
    public int Days => Path.Turns;
}

/// <summary>
/// The editor's Measure tool (U, ADR-283): the route the game's pathfinding takes between two tiles, and its steps and days for a hero with
/// the movement points the user picks.
/// </summary>
/// <remarks>
/// The hero is a stand-in of the player picked (its keys, garrisons and allies count), with no army, skill or artifact, so at home on no
/// terrain: the step costs are those of the analysis's scout with no town (<see cref="MapAnalysis.Reach"/>). Unlike the scout it walks as a
/// hero in the game does (<see cref="PathFinder.FindPathFor"/>): it goes round guarded tiles and visits, ends on a monster, a hero (an
/// allied one is no end) or a barring quest object, and takes gates; fog is ignored. A day ends when a step leaves no movement point (the game
/// lets the day's last step run over what is left), and each new day starts with <c>dailyPoints</c>.
/// </remarks>
public static class MapMeasure
{
    /// <summary>The fewest movement points a day the tool offers.</summary>
    public const int MinPoints = 100;

    /// <summary>The most.</summary>
    public const int MaxPoints = 5000;

    /// <summary>
    /// The route from <paramref name="from"/> to <paramref name="to"/> for a hero of <paramref name="player"/> with
    /// <paramref name="dailyPoints"/> movement points a day (<see cref="MinPoints"/> … <see cref="MaxPoints"/>); null when no route goes
    /// there, a hero cannot stand on <paramref name="from"/>, or the two are one tile.
    /// </summary>
    public static MeasuredRoute? Route(GameState state, MapPosition from, MapPosition to, PlayerId player, int dailyPoints = MovementAllowance.NoArmyBase)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentOutOfRangeException.ThrowIfLessThan(dailyPoints, MinPoints);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(dailyPoints, MaxPoints);
        if (!state.Map.Contains(from) || !state.Map.Contains(to) || from == to || MovementRules.CheckTile(state, from) != MoveResult.Moved)
        {
            return null;
        }

        var hero = new Hero(new HeroId(-1), "Measure", player, from);
        if (new PathFinder(state).FindPathFor(hero, to, respectFog: false) is not { } path)
        {
            return null;
        }

        // The days again with the points picked: the finder counts them from the stand-in's own (as BuildPath does).
        var steps = new List<PathStep>(path.Steps.Count);
        var left = dailyPoints;
        var day = 0;
        foreach (var step in path.Steps)
        {
            if (step.Kind == PathStepKind.Walk && !HeroMovementService.CanAffordStep(left))
            {
                day++;
                left = dailyPoints;
            }

            left = Math.Max(0, left - step.Cost);
            steps.Add(step with { Day = day });
        }

        return new MeasuredRoute(path with { Steps = steps }, dailyPoints);
    }
}
