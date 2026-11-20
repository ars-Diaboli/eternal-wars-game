using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;

namespace EternalWars.Core.Domain.Editing.Checks;

/// <summary>
/// The map events (EVT-6 c): what stops the map loading (<see cref="MapEvents.MapEventRules"/>) as errors, then the warnings of
/// <see cref="MapEvents.MapEventChecks"/>; each at the event's first cell, quest object or town when it has one. A place an action names
/// (reveal, give a hero) that is off the map is a warning: a resize or a typed number can leave one there.
/// </summary>
internal sealed class EventCheck : IMapCheck
{
    public IssueKind Kind => IssueKind.Events;

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

        foreach (var definition in set.Events)
        {
            var places = new List<(string What, MapPosition Place)>();
            MapEvents.MapEventTreeEdit.Rewrite(definition, action =>
            {
                switch (action)
                {
                    case MapEvents.RevealAction reveal:
                        places.Add(("reveals the map round", new MapPosition(reveal.X, reveal.Y, reveal.Level)));
                        break;
                    case MapEvents.GiveHeroAction { X: { } x, Y: { } y } give:
                        places.Add(("gives a hero at", new MapPosition(x, y, give.Level)));
                        break;
                }

                return action;
            });
            foreach (var (what, place) in places.Where(p => !scenario.Map.Contains(p.Place)).Distinct())
            {
                yield return new(
                    IssueSeverity.Warning, $"Event '{definition.Name}' {what} {place.X}, {place.Y} (level {place.Level}), which is off the map.",
                    Where(new MapEvents.MapEventProblem("", definition.Name)), definition.Name);
            }
        }
    }
}
