namespace EW.Scripts.TechTree;

public sealed class TownTechTreeBuildingState
{
    public TownBuildingType BuildingType { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public bool Visible { get; init; }
    public string ButtonText { get; init; } = string.Empty;
    public string TooltipText { get; init; } = string.Empty;
    public bool Disabled { get; init; }
    public TownTechTreeMarkerState MarkerState { get; init; } = TownTechTreeMarkerState.Hidden;
    public TownDwellingDefinition DwellingDefinition { get; init; }
}
