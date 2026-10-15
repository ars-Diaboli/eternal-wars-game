using Godot;

namespace EW.Scripts.TechTree;

[GlobalClass]
public partial class TownBuildingRequirement : Resource
{
    [Export] public TownBuildingType BuildingType { get; set; } = TownBuildingType.None;

    [Export(PropertyHint.Range, "1,3,1")]
    public int MinLevel { get; set; } = 1;
}
