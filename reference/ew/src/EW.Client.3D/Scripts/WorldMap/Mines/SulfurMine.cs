using EW.Scripts.DataTypes.Enums;

namespace EW.Scripts.WorldMap.Mines;

public partial class SulfurMine : ResourceMine
{
    public SulfurMine()
    {
        GeneratedResource = ResourceType.Sulfur;
        AmountPerTurn = 1;
    }
}
