using EW.Scripts.DataTypes.Enums;
using Godot;

namespace EW.Scripts.TechTree;

[GlobalClass]
public partial class TownTechTreeBase : Resource
{
    [Export]
    public Godot.Collections.Array<TownBuildingDefinition> CommonBuildings { get; set; } =
    [
        CreateTownHallDefinition(),
        CreateFortDefinition(),
        CreateBlacksmithDefinition(),
        CreateMarketplaceDefinition(),
        CreateMagesGuildDefinition(),
        CreateTavernDefinition()
    ];

    public virtual bool SupportsBuilding(TownBuildingType buildingType)
    {
        return GetBuildingDefinition(buildingType) != null;
    }

    public virtual int GetMaxLevel(TownBuildingType buildingType)
    {
        return GetBuildingDefinition(buildingType)?.MaxLevel ?? 0;
    }

    public virtual TownBuildingLevelCost GetLevelCost(TownBuildingType buildingType, int level)
    {
        return GetBuildingDefinition(buildingType)?.GetLevelCost(level);
    }

    public virtual TownBuildingDefinition GetBuildingDefinition(TownBuildingType buildingType)
    {
        if (buildingType == TownBuildingType.None || CommonBuildings == null)
        {
            return null;
        }

        foreach (TownBuildingDefinition definition in CommonBuildings)
        {
            if (definition != null && definition.BuildingType == buildingType)
            {
                return definition;
            }
        }

        return null;
    }

    private static TownBuildingDefinition CreateTownHallDefinition()
    {
        return new TownBuildingDefinition
        {
            BuildingType = TownBuildingType.TownHall,
            MaxLevel = 3,
            LevelCosts =
            [
                CreateLevelCost(1, (ResourceType.Gold, 2500), (ResourceType.Wood, 5), (ResourceType.Ore, 5)),
                CreateLevelCost(
                    2,
                    [(TownBuildingType.Blacksmith, 1), (TownBuildingType.Marketplace, 1), (TownBuildingType.MagesGuild, 1)],
                    (ResourceType.Gold, 5000), (ResourceType.Wood, 10), (ResourceType.Ore, 10)),
                CreateLevelCost(
                    3,
                    [(TownBuildingType.Fort, 2)],
                    (ResourceType.Gold, 10000), (ResourceType.Wood, 20), (ResourceType.Ore, 20))
            ]
        };
    }

    private static TownBuildingDefinition CreateFortDefinition()
    {
        return new TownBuildingDefinition
        {
            BuildingType = TownBuildingType.Fort,
            MaxLevel = 3,
            LevelCosts =
            [
                CreateLevelCost(
                    1,
                    [(TownBuildingType.Tavern, 1)],
                    (ResourceType.Gold, 1500), (ResourceType.Wood, 5), (ResourceType.Ore, 5)),
                CreateLevelCost(2, (ResourceType.Gold, 3000), (ResourceType.Wood, 10), (ResourceType.Ore, 10)),
                CreateLevelCost(3, (ResourceType.Gold, 6000), (ResourceType.Wood, 15), (ResourceType.Ore, 15))
            ]
        };
    }

    private static TownBuildingDefinition CreateBlacksmithDefinition()
    {
        return new TownBuildingDefinition
        {
            BuildingType = TownBuildingType.Blacksmith,
            MaxLevel = 1,
            LevelCosts =
            [
                CreateLevelCost(1, (ResourceType.Gold, 1000), (ResourceType.Wood, 5), (ResourceType.Ore, 5))
            ]
        };
    }

    private static TownBuildingDefinition CreateMarketplaceDefinition()
    {
        return new TownBuildingDefinition
        {
            BuildingType = TownBuildingType.Marketplace,
            MaxLevel = 2,
            LevelCosts =
            [
                CreateLevelCost(1, (ResourceType.Gold, 1250), (ResourceType.Wood, 5)),
                CreateLevelCost(2, (ResourceType.Gold, 2500), (ResourceType.Wood, 5), (ResourceType.Ore, 5))
            ]
        };
    }

    private static TownBuildingDefinition CreateMagesGuildDefinition()
    {
        return new TownBuildingDefinition
        {
            BuildingType = TownBuildingType.MagesGuild,
            MaxLevel = 3,
            LevelCosts =
            [
                CreateLevelCost(1, (ResourceType.Gold, 2000), (ResourceType.Wood, 5), (ResourceType.Gems, 2)),
                CreateLevelCost(2, (ResourceType.Gold, 3500), (ResourceType.Crystals, 3), (ResourceType.Gems, 3)),
                CreateLevelCost(3, (ResourceType.Gold, 5000), (ResourceType.Crystals, 5), (ResourceType.Sulfur, 5))
            ]
        };
    }

    private static TownBuildingDefinition CreateTavernDefinition()
    {
        return new TownBuildingDefinition
        {
            BuildingType = TownBuildingType.Tavern,
            MaxLevel = 1,
            LevelCosts =
            [
                CreateLevelCost(1, (ResourceType.Gold, 750), (ResourceType.Wood, 5))
            ]
        };
    }

    private static TownBuildingLevelCost CreateLevelCost(int level, params (ResourceType ResourceType, int Amount)[] costs)
    {
        return CreateLevelCost(level, [], costs);
    }

    private static TownBuildingLevelCost CreateLevelCost(
        int level,
        (TownBuildingType BuildingType, int MinLevel)[] requirements,
        params (ResourceType ResourceType, int Amount)[] costs)
    {
        TownBuildingLevelCost levelCost = new TownBuildingLevelCost
        {
            Level = level
        };

        foreach ((TownBuildingType buildingType, int minLevel) in requirements)
        {
            levelCost.Requirements.Add(new TownBuildingRequirement
            {
                BuildingType = buildingType,
                MinLevel = minLevel
            });
        }

        foreach ((ResourceType resourceType, int amount) in costs)
        {
            levelCost.Costs.Add(new TownBuildingResourceCost
            {
                ResourceType = resourceType,
                Amount = amount
            });
        }

        return levelCost;
    }
}
