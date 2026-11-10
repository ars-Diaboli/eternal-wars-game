using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Towns;

/// <summary>
/// A building or dwelling level going up, whoever builds it: the owner's building of the day (<see cref="TownService"/>) or a map event's free
/// build (<see cref="MapEvents.MapEventService"/>). The callers check and pay.
/// </summary>
internal sealed class TownConstruction(GameState state, IEventPublisher events, TownVisitService visits)
{
    /// <summary>
    /// The building stands at <paramref name="level"/>. One that teaches or gives the heroes in the town something (the mage guild, the war lodge, a
    /// school, a unique building) lets them take it at once (<see cref="TownVisitService.VisitBy"/>).
    /// </summary>
    public void Raise(Town town, string building, int level)
    {
        town.SetLevel(building, level);
        events.Publish(new TownBuilt(town.Id, building, level));
        if (building is BuiltInBuildings.MageGuild or BuiltInBuildings.WarLodge || BuiltInBuildings.SchoolOf(building) is not null
            || UniqueBuildings.Get(building) is not null)
        {
            visits.VisitBy(town);
        }
    }

    /// <summary>The line's dwelling stands at <paramref name="level"/>; a new one opens with its first week of creatures, fort bonus included (HoMM3, ADR-066).</summary>
    public void RaiseDwelling(Town town, CreatureDefinition line, int level)
    {
        town.SetDwelling(line.Id, level);
        if (level == 1)
        {
            town.SetAvailable(line.Id, town.AvailableOf(line.Id) + TownRules.WeeklyGrowth(state, town, line));
        }

        events.Publish(new DwellingBuilt(town.Id, line.Id, level, town.AvailableOf(line.Id)));
    }
}
