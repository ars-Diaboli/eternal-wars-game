using EW.Scripts.DataTypes.Enums;

namespace EW.Scripts.WorldMap.Mines;

public partial class GemMine : ResourceMine
{
    public GemMine()
    {
        GeneratedResource = ResourceType.Gems;
        AmountPerTurn = 1;
    }
}
