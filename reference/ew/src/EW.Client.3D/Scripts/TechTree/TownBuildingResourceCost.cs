using EW.Scripts.DataTypes.Enums;
using Godot;

namespace EW.Scripts.TechTree;

[GlobalClass]
public partial class TownBuildingResourceCost : Resource
{
    [Export] public ResourceType ResourceType { get; set; } = ResourceType.Gold;

    [Export(PropertyHint.Range, "0,99999,1,or_greater")]
    public int Amount { get; set; }
}
