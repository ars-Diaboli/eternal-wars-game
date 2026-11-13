namespace EternalWars.Core.Domain.Map;

/// <summary>
/// The relief's limits (ADR-075, ADR-182 h), in eighths of a cell width. A corner's range follows the tiles that share it:
/// walkable ground 0–4 cell widths, water below 0, impassable rock above 4. Where water meets walkable ground (or rock) the
/// corner is the shoreline, 0; where rock meets walkable ground it is the cliff's foot, 4. Nothing more is enforced.
/// </summary>
public static class ReliefLimits
{
    /// <summary>Heights are stored in steps of 1/8 cell width.</summary>
    public const int StepsPerCell = 8;

    /// <summary>The top of walkable ground: 4 cell widths.</summary>
    public const int WalkableMax = 4 * StepsPerCell;

    /// <summary>The lowest stored height: −2 cell widths.</summary>
    public const int StoredMin = -2 * StepsPerCell;

    /// <summary>The highest stored height: just under 8 cell widths.</summary>
    public const int StoredMax = (8 * StepsPerCell) - 1;

    /// <summary>The range a corner of <paramref name="map"/> may take, from the tiles around it.</summary>
    public static (int Min, int Max) RangeAt(AdventureMap map, int level, int cx, int cy)
    {
        bool walkable = false, water = false, rock = false;
        for (var y = cy - 1; y <= cy; y++)
        {
            for (var x = cx - 1; x <= cx; x++)
            {
                if (x < 0 || y < 0 || x >= map.Width || y >= map.Height)
                {
                    continue;
                }

                switch (map.GetTerrain(new(x, y, level)))
                {
                    case TerrainType.Water:
                        water = true;
                        break;
                    case TerrainType.Rock:
                        rock = true;
                        break;
                    default:
                        walkable = true;
                        break;
                }
            }
        }

        return (walkable, water, rock) switch
        {
            (_, true, _) when walkable || rock => (0, 0),
            (true, _, true) => (WalkableMax, WalkableMax),
            (true, _, _) => (0, WalkableMax),
            (_, true, _) => (StoredMin, -1),
            _ => (WalkableMax + 1, StoredMax),
        };
    }

    /// <summary>Clamps <paramref name="eighths"/> to the range of corner (<paramref name="cx"/>, <paramref name="cy"/>).</summary>
    public static int Clamp(AdventureMap map, int level, int cx, int cy, int eighths)
    {
        var (min, max) = RangeAt(map, level, cx, cy);
        return Math.Clamp(eighths, min, max);
    }
}
