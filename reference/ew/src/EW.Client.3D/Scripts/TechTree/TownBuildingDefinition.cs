using Godot;

namespace EW.Scripts.TechTree;

[GlobalClass]
public partial class TownBuildingDefinition : Resource
{
    [Export] public TownBuildingType BuildingType { get; set; } = TownBuildingType.None;

    [Export(PropertyHint.Range, "1,3,1")]
    public int MaxLevel { get; set; } = 1;

    [Export]
    public Godot.Collections.Array<TownBuildingLevelCost> LevelCosts { get; set; } = new();

    public TownBuildingLevelCost GetLevelCost(int level)
    {
        if (level <= 0 || LevelCosts == null)
        {
            return null;
        }

        foreach (TownBuildingLevelCost levelCost in LevelCosts)
        {
            if (levelCost != null && levelCost.Level == level)
            {
                return levelCost;
            }
        }

        return null;
    }
}
