using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;

namespace EternalWars.Core.Domain.Editing.Checks;

/// <summary>
/// The Grail (ADR-140): where the author buried it a hero must be able to dig (an object or road put there later makes it unreachable); a buried
/// Grail without obelisks can only be found by chance; it must lie <see cref="GrailRules.EdgeDistance"/> cells from the edge, so a map smaller than
/// <see cref="GrailRules.MinMapSize"/> has none and its obelisks are pointless; a map holds at most <see cref="GrailRules.MaxObelisks"/> obelisks.
/// </summary>
internal sealed class GrailCheck : IMapCheck
{
    public IssueKind Kind => IssueKind.Objects;

    public IEnumerable<MapIssue> Check(GameState scenario)
    {
        var obelisks = scenario.ObjectsOf<Obelisk>().ToList();
        if (obelisks.Count > GrailRules.MaxObelisks)
        {
            yield return new(IssueSeverity.Error, $"The map has {obelisks.Count} obelisks; at most {GrailRules.MaxObelisks} are allowed.", obelisks[GrailRules.MaxObelisks].Position);
        }

        if (obelisks.Count > 0 && !GrailRules.HoldsGrail(scenario.Map))
        {
            yield return new(IssueSeverity.Warning,
                $"The map is under {GrailRules.MinMapSize} tiles wide or high, so it has no Grail and its obelisks show nothing.", obelisks[0].Position);
        }

        if (scenario.Grail.Position is not { } grail)
        {
            yield break;
        }

        if (!GrailRules.FarFromEdge(scenario.Map, grail))
        {
            yield return new(IssueSeverity.Error, $"The Grail lies nearer the map's edge than {GrailRules.EdgeDistance} cells.", grail);
        }

        if (GrailRules.CheckGround(scenario, grail) is { } problem)
        {
            yield return new(IssueSeverity.Error, $"Nobody can dig up the Grail: {problem}", grail);
        }

        if (!scenario.ObjectsOf<Obelisk>().Any())
        {
            yield return new(IssueSeverity.Warning, "The Grail is buried but the map has no obelisks: players find it only by digging at random.", grail);
        }
    }
}
