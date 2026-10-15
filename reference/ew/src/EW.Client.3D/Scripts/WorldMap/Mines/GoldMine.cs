using EW.Scripts.DataTypes.Enums;

namespace EW.Scripts.WorldMap.Mines;

public partial class GoldMine : ResourceMine
{
    public GoldMine()
    {
        GeneratedResource = ResourceType.Gold;
        AmountPerTurn = 1000;
    }
}
