using Godot;

namespace EW.Scripts.TechTree;

[GlobalClass]
public partial class TownBuildingLevel : Resource
{
    [Export] public TownBuildingType BuildingType { get; set; } = TownBuildingType.None;

    [Export(PropertyHint.Range, "0,3,1")]
    public int Level { get; set; }
}
