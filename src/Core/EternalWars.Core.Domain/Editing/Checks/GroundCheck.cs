using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Map;

namespace EternalWars.Core.Domain.Editing.Checks;

/// <summary>Roads and rivers on impassable terrain.</summary>
internal sealed class GroundCheck : IMapCheck
{
    public IEnumerable<MapIssue> Check(GameState scenario)
    {
        var map = scenario.Map;
        for (var level = 0; level < map.Levels; level++)
        {
            foreach (var position in map.Positions(level))
            {
                if (map.GetRoad(position) != RoadType.None && !scenario.Terrains.IsPassable(map.GetTerrain(position)))
                {
                    yield return new(IssueSeverity.Warning, "Road on impassable terrain.", position);
                }

                if (map.GetRiver(position) != RiverType.None && !scenario.Terrains.IsPassable(map.GetTerrain(position)))
                {
                    yield return new(IssueSeverity.Warning, "River on impassable terrain.", position);
                }
            }
        }
    }
}
