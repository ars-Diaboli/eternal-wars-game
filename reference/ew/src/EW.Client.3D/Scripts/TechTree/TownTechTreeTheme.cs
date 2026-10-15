using System.Collections.Generic;
using EW.Engine;
using EW.Scripts.DataTypes.Enums;
using Godot;

namespace EW.Scripts.TechTree;

[GlobalClass]
public partial class TownTechTreeTheme : TownTechTreeBase
{
    private static readonly Dictionary<Faction, TownTechTreeTheme> DefaultThemes = new();

    [Export] public Faction Faction { get; set; } = Faction.Neutral;
    [Export] public Texture2D BackgroundImage { get; set; }
    [Export] public Godot.Collections.Array<TownDwellingDefinition> Dwellings { get; set; } = new();

    public override TownBuildingDefinition GetBuildingDefinition(TownBuildingType buildingType)
    {
        TownBuildingDefinition baseDefinition = base.GetBuildingDefinition(buildingType);
        if (baseDefinition != null)
        {
            return baseDefinition;
        }

        if (buildingType == TownBuildingType.None || Dwellings == null)
        {
            return null;
        }

        foreach (TownDwellingDefinition dwelling in Dwellings)
        {
            if (dwelling != null && dwelling.BuildingType == buildingType)
            {
                return dwelling;
            }
        }

        return null;
    }

    public IReadOnlyList<TownDwellingDefinition> GetDwellingDefinitions()
    {
        return Dwellings ?? new Godot.Collections.Array<TownDwellingDefinition>();
    }

    public TownDwellingDefinition GetDwellingDefinition(TownBuildingType buildingType)
    {
        return GetBuildingDefinition(buildingType) as TownDwellingDefinition;
    }

    public static TownTechTreeTheme GetDefault(Faction faction)
    {
        if (faction == Faction.Neutral)
        {
            return null;
        }

        if (!DefaultThemes.TryGetValue(faction, out TownTechTreeTheme theme))
        {
            theme = CreateDefaultTheme(faction);
            DefaultThemes[faction] = theme;
        }

        return theme;
    }

    private static TownTechTreeTheme CreateDefaultTheme(Faction faction)
    {
        TownTechTreeTheme theme = new TownTechTreeTheme
        {
            Faction = faction
        };

        theme.Dwellings =
        [
            CreateDefaultDwelling(faction, TownBuildingType.Dwelling1, "Dwelling I", UnitTier.Tier1, 12, 18),
            CreateDefaultDwelling(faction, TownBuildingType.Dwelling2, "Dwelling II", UnitTier.Tier1, 10, 15),
            CreateDefaultDwelling(faction, TownBuildingType.Dwelling3, "Dwelling III", UnitTier.Tier2, 8, 12),
            CreateDefaultDwelling(faction, TownBuildingType.Dwelling4, "Dwelling IV", UnitTier.Tier2, 7, 10),
            CreateDefaultDwelling(faction, TownBuildingType.Dwelling5, "Dwelling V", UnitTier.Tier3, 5, 8),
            CreateDefaultDwelling(faction, TownBuildingType.Dwelling6, "Dwelling VI", UnitTier.Tier3, 4, 6),
            CreateDefaultDwelling(faction, TownBuildingType.Dwelling7, "Dwelling VII", UnitTier.Tier4, 3, 5),
            CreateDefaultDwelling(faction, TownBuildingType.Dwelling8, "Dwelling VIII", UnitTier.Tier4, 2, 4),
            CreateDefaultDwelling(faction, TownBuildingType.Dwelling9, "Dwelling IX", UnitTier.Tier5, 1, 2)
        ];

        return theme;
    }

    private static TownDwellingDefinition CreateDefaultDwelling(
        Faction faction,
        TownBuildingType buildingType,
        string displayName,
        UnitTier unitTier,
        int growthLevel1,
        int growthLevel2)
    {
        return new TownDwellingDefinition
        {
            BuildingType = buildingType,
            DisplayName = $"{faction} {displayName}",
            UnitTier = unitTier,
            ProducedUnit = CreateDefaultUnit(faction, displayName, unitTier),
            MaxLevel = 2,
            WeeklyGrowthLevel1 = growthLevel1,
            WeeklyGrowthLevel2 = growthLevel2,
            LevelCosts =
            [
                CreateDwellingLevelCost(1, unitTier),
                CreateDwellingLevelCost(2, unitTier)
            ]
        };
    }

    private static UnitData CreateDefaultUnit(Faction faction, string displayName, UnitTier unitTier)
    {
        int tier = (int)unitTier;
        return new UnitData
        {
            UnitName = $"{faction} {displayName} Unit",
            Tier = unitTier,
            MaxHealth = 10 * tier,
            Attack = 2 * tier,
            Defense = 2 * tier,
            Speed = 3 + tier,
            Initiative = 5 + tier
        };
    }

    private static TownBuildingLevelCost CreateDwellingLevelCost(int level, UnitTier unitTier)
    {
        int tier = (int)unitTier;
        TownBuildingLevelCost levelCost = new TownBuildingLevelCost
        {
            Level = level
        };

        int goldCost = level == 1
            ? 500 * tier
            : 1000 * tier;
        levelCost.Costs.Add(new TownBuildingResourceCost
        {
            ResourceType = ResourceType.Gold,
            Amount = goldCost
        });

        if (tier >= 2)
        {
            levelCost.Costs.Add(new TownBuildingResourceCost
            {
                ResourceType = ResourceType.Wood,
                Amount = level * tier
            });
        }

        if (tier >= 3)
        {
            levelCost.Costs.Add(new TownBuildingResourceCost
            {
                ResourceType = ResourceType.Ore,
                Amount = level * tier
            });
        }

        if (tier >= 4)
        {
            levelCost.Costs.Add(new TownBuildingResourceCost
            {
                ResourceType = ResourceType.Gems,
                Amount = level
            });
        }

        if (tier >= 5)
        {
            levelCost.Costs.Add(new TownBuildingResourceCost
            {
                ResourceType = ResourceType.Crystals,
                Amount = level
            });
        }

        return levelCost;
    }
}
