using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Editing.Checks;

/// <summary>
/// A player without a starting hero or town. When one neutral town, and only one, stands in the player's start zone (its entrance does), the
/// fix gives it to the player (ADR-265).
/// </summary>
internal sealed class StartCheck : IMapCheck
{
    public IssueKind Kind => IssueKind.Players;

    public IEnumerable<MapIssue> Check(GameState scenario) =>
        scenario.Players.Where(p => !scenario.HeroesOf(p.Id).Any() && !scenario.ObjectsOf<Town>().Any(t => t.Owner == p.Id))
            .Select(player => scenario.ObjectsOf<Town>()
                    .Where(t => t.Owner is null && scenario.Map.Contains(t.VisitPosition) && scenario.Map.GetStartZone(t.VisitPosition) == player.Id)
                    .ToList() is [var town]
                ? new MapIssue(IssueSeverity.Warning, $"Player {player.Name} has no starting hero or town.", town.VisitPosition)
                {
                    Fix = new OwnerFix($"Give {town.Name}, the neutral town in the start zone, to {player.Name}", town.Id, null, player.Id),
                }
                : new MapIssue(IssueSeverity.Warning, $"Player {player.Name} has no starting hero or town."));
}
