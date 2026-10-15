namespace EW.CoreLib.Types;

public static class MapObjectSizes
{
    #region Generic X by Y
    
    public static readonly byte[,] _1x1 = new byte[1, 1]
    {
        {1}
    };
    public static readonly byte[,] _1x2 = new byte[1, 2]
    {
        {1,1}
    };
    public static readonly byte[,] _1x3 = new byte[1, 3]
    {
        {1,1,1}
    };
    public static readonly byte[,] _2x2 = new byte[2, 2]
    {
        {1,1},
        {1,1},
    };
    public static readonly byte[,] _2x3 = new byte[2, 3]
    {
        {1,1,1},
        {1,1,1},
    };
    public static readonly byte[,] _2x4 = new byte[2, 4]
    {
        {1,1,1,1},
        {1,1,1,1},
    };
    public static readonly byte[,] _3x3 = new byte[3, 3]
    {
        {1,1,1},
        {1,1,1},
        {1,1,1},
    };
    public static readonly byte[,] _4x4 = new byte[4, 4]
    {
        {1,1,1,1},
        {1,1,1,1},
        {1,1,1,1},
        {1,1,1,1},
    };
    
    #endregion
    public static readonly byte[,] Town = new byte[7, 7]
    {
        {0,1,1,1,1,1,0},
        {1,1,1,1,1,1,1},
        {1,1,1,1,1,1,1},
        {1,1,1,1,1,1,1},
        {1,1,1,1,1,1,1},
        {1,1,1,1,1,1,1},
        {0,1,1,1,1,1,0},
    };
    public static readonly Position TownInteractionTile = new(6, 3);

    public static readonly byte[,] Hero = _1x1;
    public static readonly byte[,] RemovableObject = _1x1;

}