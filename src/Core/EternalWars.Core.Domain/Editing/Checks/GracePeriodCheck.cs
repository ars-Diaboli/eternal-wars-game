using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Editing.Checks;

/// <summary>
/// Campaign-like maps may start a player without a town (ADR-075): then the grace period should be off. An arena eliminates nobody (ADR-158).
/// </summary>
internal sealed class GracePeriodCheck : IMapCheck
{
    public IssueKind Kind => IssueKind.Rules;

    public IEnumerable<MapIssue> Check(GameState scenario)
    {
        if (!scenario.Rules.Elimination || scenario.Rules.GraceDays <= 0 || scenario.Rules.Arena)
        {
            yield break;
        }

        foreach (var player in scenario.Players.Where(p => scenario.HeroesOf(p.Id).Any() && !scenario.ObjectsOf<Town>().Any(t => t.Owner == p.Id)))
        {
            yield return new(IssueSeverity.Warning,
                $"Player {player.Name} starts without a town and loses after {scenario.Rules.GraceDays} days; for a map without towns set the grace period to 0 and add a 'lose a hero' condition.");
        }
    }
}
