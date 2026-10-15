using Godot;

namespace EW.Scripts.TechTree;

[GlobalClass]
public partial class TownBuildingLevelCost : Resource
{
    [Export(PropertyHint.Range, "1,3,1")]
    public int Level { get; set; } = 1;

    [Export]
    public Godot.Collections.Array<TownBuildingResourceCost> Costs { get; set; } = new();

    [Export]
    public Godot.Collections.Array<TownBuildingRequirement> Requirements { get; set; } = new();
}
