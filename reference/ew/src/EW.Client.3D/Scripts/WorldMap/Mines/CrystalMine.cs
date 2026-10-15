using EW.Scripts.DataTypes.Enums;

namespace EW.Scripts.WorldMap.Mines;

public partial class CrystalMine : ResourceMine
{
    public CrystalMine()
    {
        GeneratedResource = ResourceType.Crystals;
        AmountPerTurn = 1;
    }
}
