using Godot;

namespace EW.Scripts.DataTypes;

public static class ArmyUtils
{
    public const int ArmySize = 7;

    public static Godot.Collections.Array<ArmyUnitStack> CreateEmptyArmy()
    {
        Godot.Collections.Array<ArmyUnitStack> army = new();

        for (int i = 0; i < ArmySize; i++)
        {
            army.Add(CreateEmptySlot());
        }

        return army;
    }

    public static Godot.Collections.Array<ArmyUnitStack> Normalize(Godot.Collections.Array<ArmyUnitStack> army)
    {
        Godot.Collections.Array<ArmyUnitStack> normalizedArmy = new();

        if (army != null)
        {
            for (int i = 0; i < army.Count && normalizedArmy.Count < ArmySize; i++)
            {
                normalizedArmy.Add(army[i] ?? CreateEmptySlot());
            }
        }

        while (normalizedArmy.Count < ArmySize)
        {
            normalizedArmy.Add(CreateEmptySlot());
        }

        return normalizedArmy;
    }

    private static ArmyUnitStack CreateEmptySlot()
    {
        return new ArmyUnitStack();
    }
}
