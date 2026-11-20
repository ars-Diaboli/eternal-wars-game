using EternalWars.Core.Domain.Game;

namespace EternalWars.Core.Domain.Editing.Checks;

/// <summary>
/// One of the validator's checks (<see cref="MapValidator.Validate"/>): the issues of one part of a scenario, in the order the editor lists them.
/// The checks run in <see cref="MapChecks.All"/>'s order.
/// </summary>
internal interface IMapCheck
{
    /// <summary>The part of the map this check's issues are about: the validator marks them with it.</summary>
    IssueKind Kind { get; }

    /// <summary>The scenario's issues this check finds, in order.</summary>
    IEnumerable<MapIssue> Check(GameState scenario);
}
