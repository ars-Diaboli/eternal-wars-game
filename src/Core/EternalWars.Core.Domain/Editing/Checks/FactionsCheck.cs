using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Editing.Checks;

/// <summary>
/// A player's factions (ADR-257) only narrow what it may choose: a town of another faction the map gives it is still its own, which the author
/// may not mean.
/// </summary>
internal sealed class FactionsCheck : IMapCheck
{
    public IssueKind Kind => IssueKind.Players;

    public IEnumerable<MapIssue> Check(GameState scenario)
    {
        foreach (var player in scenario.Players.Where(p => p.Factions is not null))
        {
            var allowed = PlayerFactions.Allowed(player);
            foreach (var town in scenario.ObjectsOf<Town>().Where(t => t.Owner == player.Id && !t.IsRandom && !allowed.Contains(t.TownType)).OrderBy(t => t.Id.Value))
            {
                yield return new(
                    IssueSeverity.Warning,
                    $"Player {player.Name} may take only {string.Join(", ", allowed.Select(BuiltInMapObjects.TownName))}, but starts with {town.Name}, a {BuiltInMapObjects.TownName(town.TownType)} town.",
                    town.VisitPosition);
            }
        }
    }
}
