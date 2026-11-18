using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing.Checks;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Editing;

public enum IssueSeverity
{
    Warning,
    Error,
}

/// <summary>A problem the validator found: how bad, what, where on the map and, for the map events, which event (the Events tab opens it).</summary>
public sealed record MapIssue(IssueSeverity Severity, string Message, MapPosition? Position = null, string? Event = null);

/// <summary>
/// Checks a scenario for problems that individual edits cannot prevent (e.g. painting water under a
/// hero). The editor shows these; errors should block "play this map".
/// </summary>
public static class MapValidator
{
    /// <summary>Runs every check (<see cref="MapChecks.All"/>) in order and lists their issues.</summary>
    public static IReadOnlyList<MapIssue> Validate(GameState scenario) => MapChecks.All.SelectMany(c => c.Check(scenario)).ToList();

    /// <summary>A hero may start in the entrance of its own town, visiting it (HoMM3; generated maps put the start hero there).</summary>
    internal static bool IsOwnTownEntrance((MapObject Object, FootprintCell Cell) cell, PlayerId owner) =>
        cell is { Object: Town town, Cell: FootprintCell.Visit } && town.Owner == owner;
}
