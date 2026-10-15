using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;

namespace EternalWars.Core.Domain.Editing;

public enum IssueSeverity
{
    Warning,
    Error,
}

public sealed record MapIssue(IssueSeverity Severity, string Message, MapPosition? Position = null);

/// <summary>
/// Checks a scenario for problems that individual edits cannot prevent (e.g. painting water under a
/// hero). The editor shows these; errors should block "play this map".
/// </summary>
public static class MapValidator
{
    public static IReadOnlyList<MapIssue> Validate(GameState scenario)
    {
        var issues = new List<MapIssue>();
        var map = scenario.Map;

        foreach (var hero in scenario.Heroes)
        {
            if (!scenario.HasPlayer(hero.Owner))
            {
                issues.Add(new(IssueSeverity.Error, $"Hero {hero.Name} belongs to a missing player.", hero.Position));
            }

            if (!map.Contains(hero.Position) || !map.GetTerrain(hero.Position).IsPassableOnFoot())
            {
                issues.Add(new(IssueSeverity.Error, $"Hero {hero.Name} stands on impassable terrain.", hero.Position));
            }
            else if (scenario.ObjectCellAt(hero.Position) is not null)
            {
                issues.Add(new(IssueSeverity.Error, $"Hero {hero.Name} stands inside an object.", hero.Position));
            }
        }

        foreach (var mapObject in scenario.Objects)
        {
            if (!map.GetTerrain(mapObject.VisitPosition).IsPassableOnFoot())
            {
                issues.Add(new(IssueSeverity.Error, $"{mapObject.Definition.Name} has an impassable entrance.", mapObject.VisitPosition));
            }

            switch (mapObject)
            {
                case Mine { Owner: { } owner } mine when !scenario.HasPlayer(owner):
                    issues.Add(new(IssueSeverity.Error, $"{mine.Definition.Name} belongs to a missing player.", mine.Position));
                    break;
                case SubterraneanGate { LinkedGate: null } gate:
                    issues.Add(new(IssueSeverity.Warning, "Subterranean gate is not linked.", gate.Position));
                    break;
                case SubterraneanGate { LinkedGate: { } link } gate
                    when scenario.Objects.FirstOrDefault(o => o.Id == link) is not SubterraneanGate partner || partner.LinkedGate != gate.Id:
                    issues.Add(new(IssueSeverity.Error, "Subterranean gate link is not mutual.", gate.Position));
                    break;
            }
        }

        foreach (var player in scenario.Players.Where(p => !scenario.HeroesOf(p.Id).Any()))
        {
            issues.Add(new(IssueSeverity.Warning, $"Player {player.Name} has no starting hero."));
        }

        return issues;
    }
}
