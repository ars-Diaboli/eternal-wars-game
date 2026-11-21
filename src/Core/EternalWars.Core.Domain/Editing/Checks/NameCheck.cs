using EternalWars.Core.Domain.Game;

namespace EternalWars.Core.Domain.Editing.Checks;

/// <summary>
/// Object names are unique (ADR-257), ignoring case, so the map events, the conditions, the editor's lists and the quest form name one object.
/// A name the author gave counts against every other object's name, a town's default one too; two towns named as their faction are not reported.
/// </summary>
internal sealed class NameCheck : IMapCheck
{
    public IssueKind Kind => IssueKind.Objects;

    public IEnumerable<MapIssue> Check(GameState scenario) =>
        scenario.Objects.Where(o => o.Name is not null)
            .GroupBy(o => o.Name!, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1 && g.Any(o => o.GivenName is not null))
            .Select(g => g.OrderBy(o => o.Id.Value).ToList())
            .Select(same => new MapIssue(
                IssueSeverity.Error,
                $"{same.Count} objects are named \"{same[0].Name}\" ({string.Join(", ", same.Select(o => $"the {o.Definition.Name} at {o.VisitPosition.X}, {o.VisitPosition.Y}"))}); an object's name must be its own.",
                same[1].VisitPosition));
}
