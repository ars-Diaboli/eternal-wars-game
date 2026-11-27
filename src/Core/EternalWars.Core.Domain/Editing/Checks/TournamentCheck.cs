using EternalWars.Core.Domain.Game;

namespace EternalWars.Core.Domain.Editing.Checks;

/// <summary>
/// A tournament map (ADR-366): errors for what keeps it from being one (<see cref="TournamentRules.MapProblems"/>), since such a game does not
/// start: an arena too, not exactly two players, the two allied, or a player allowed other than one hero.
/// </summary>
internal sealed class TournamentCheck : IMapCheck
{
    public IssueKind Kind => IssueKind.Rules;

    public IEnumerable<MapIssue> Check(GameState scenario) =>
        TournamentRules.MapProblems(scenario).Select(problem => new MapIssue(IssueSeverity.Error, problem));
}
