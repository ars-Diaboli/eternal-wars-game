using EW.Engine;
using EW.Scripts.DataTypes.Enums;
using Godot;

namespace EW.Scripts.TechTree;

[GlobalClass]
public partial class TownDwellingDefinition : TownBuildingDefinition
{
    [Export] public string DisplayName { get; set; } = "Dwelling";
    [Export] public UnitTier UnitTier { get; set; } = UnitTier.Tier1;
    [Export] public UnitData ProducedUnit { get; set; }

    [Export(PropertyHint.Range, "0,999,1,or_greater")]
    public int WeeklyGrowthLevel1 { get; set; } = 1;

    [Export(PropertyHint.Range, "0,999,1,or_greater")]
    public int WeeklyGrowthLevel2 { get; set; } = 2;
}
