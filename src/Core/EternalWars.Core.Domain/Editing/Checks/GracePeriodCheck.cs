using EternalWars.Core.Domain.Common;
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

        var townless = scenario.Players.Where(p => scenario.HeroesOf(p.Id).Any() && !scenario.ObjectsOf<Town>().Any(t => t.Owner == p.Id)).ToList();
        foreach (var player in townless)
        {
            yield return new(IssueSeverity.Warning,
                $"Player {player.Name} starts without a town and loses after {scenario.Rules.GraceDays} days; for a map without towns set the grace period to 0 and add a 'lose a hero' condition.")
            {
                Fix = NearestTown(scenario, player.Id, townless.Select(p => p.Id)) is { } town
                    ? new OwnerFix($"Give {town.Name}, the nearest neutral town, to {player.Name}", town.Id, null, player.Id)
                    : null,
            };
        }
    }

    /// <summary>
    /// The neutral town nearest to <paramref name="player"/>'s heroes on their level (ADR-279), when no other neutral town is as near and no
    /// hero of the other players without a town is as near to it: then giving it to them is the clear answer.
    /// </summary>
    private static Town? NearestTown(GameState scenario, PlayerId player, IEnumerable<PlayerId> townless)
    {
        int Nearest(PlayerId who, Town town) =>
            scenario.HeroesOf(who).Where(h => h.Position.Level == town.Position.Level)
                .Select(h => HeroCheck.Distance(h.Position, town.VisitPosition)).DefaultIfEmpty(int.MaxValue).Min();

        var near = scenario.ObjectsOf<Town>().Where(t => t.Owner is null && Nearest(player, t) < int.MaxValue)
            .Select(t => (Town: t, Steps: Nearest(player, t))).OrderBy(t => t.Steps).Take(2).ToList();
        if (near.Count == 0 || (near.Count == 2 && near[1].Steps == near[0].Steps))
        {
            return null;
        }

        var (town, steps) = near[0];
        return townless.Where(p => p != player).All(p => Nearest(p, town) > steps) ? town : null;
    }
}
