using EW.Scripts.DataTypes.Enums;

namespace EW.Scripts.WorldMap.Mines;

public partial class OreMine : ResourceMine
{
    public OreMine()
    {
        GeneratedResource = ResourceType.Ore;
        AmountPerTurn = 2;
    }
}
