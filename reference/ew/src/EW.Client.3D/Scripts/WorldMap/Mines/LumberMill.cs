using EW.Scripts.DataTypes.Enums;

namespace EW.Scripts.WorldMap.Mines;

public partial class LumberMill : ResourceMine
{
    public LumberMill()
    {
        GeneratedResource = ResourceType.Wood;
        AmountPerTurn = 2;
    }
}
