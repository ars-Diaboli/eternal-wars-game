using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Editing.Checks;

/// <summary>A player without a starting hero or town.</summary>
internal sealed class StartCheck : IMapCheck
{
    public IEnumerable<MapIssue> Check(GameState scenario) =>
        scenario.Players.Where(p => !scenario.HeroesOf(p.Id).Any() && !scenario.ObjectsOf<Town>().Any(t => t.Owner == p.Id))
            .Select(player => new MapIssue(IssueSeverity.Warning, $"Player {player.Name} has no starting hero or town."));
}
