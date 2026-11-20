using EternalWars.Core.Domain.Game;

namespace EternalWars.Core.Domain.Editing.Checks;

/// <summary>A player with more heroes than it may have: the editor refuses this; a map made elsewhere loses the surplus when the game starts (ADR-068).</summary>
internal sealed class HeroLimitCheck : IMapCheck
{
    public IssueKind Kind => IssueKind.Rules;

    public IEnumerable<MapIssue> Check(GameState scenario) =>
        scenario.Players.Where(p => scenario.HeroesOf(p.Id).Count() > scenario.HeroLimit(p.Id))
            .Select(player => new MapIssue(IssueSeverity.Warning,
                $"Player {player.Name} starts with more heroes than it may have ({scenario.HeroLimit(player.Id)}); the newest are removed when the game starts."));
}
