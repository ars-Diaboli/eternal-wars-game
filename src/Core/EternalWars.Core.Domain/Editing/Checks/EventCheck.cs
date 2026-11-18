using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;

namespace EternalWars.Core.Domain.Editing.Checks;

/// <summary>
/// The map events (EVT-6 c): what stops the map loading (<see cref="MapEvents.MapEventRules"/>) as errors, then the warnings of
/// <see cref="MapEvents.MapEventChecks"/>; each at the event's first cell, quest object or town when it has one.
/// </summary>
internal sealed class EventCheck : IMapCheck
{
    public IEnumerable<MapIssue> Check(GameState scenario)
    {
        var set = scenario.MapEvents.Set;
        MapPosition? Where(MapEvents.MapEventProblem problem) => problem.Position ?? (problem.Event is not { } name ? null
            : set.Cells.FirstOrDefault(c => c.Event == name)?.Position
              ?? scenario.ObjectsOf<QuestSite>().FirstOrDefault(q => q.Quest == name)?.Position
              ?? set.Find(name)?.Towns.Select(t => scenario.FindObject(new MapObjectId(t))?.Position).FirstOrDefault(p => p is not null));
        foreach (var error in MapEvents.MapEventRules.Check(scenario, set))
        {
            yield return new(IssueSeverity.Error, error.Message, Where(error), error.Event);
        }

        foreach (var warning in MapEvents.MapEventChecks.Warnings(scenario, set))
        {
            yield return new(IssueSeverity.Warning, warning.Message, Where(warning), warning.Event);
        }
    }
}
