namespace EW.Scripts.TechTree;

public static class TownBuildingTypeExtensions
{
    public static bool IsDwelling(this TownBuildingType buildingType)
    {
        return buildingType >= TownBuildingType.Dwelling1 && buildingType <= TownBuildingType.Dwelling9;
    }
}
