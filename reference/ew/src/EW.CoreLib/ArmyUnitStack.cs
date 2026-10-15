namespace EW.CoreLib.Types;

public class ArmyUnitStack
{
    public UnitData? Unit { get; set; }
    public uint Amount { get; set; } = 0;

    public bool IsEmpty => Unit == null || Amount == 0;
}

public class OwnedArmy
{
    public readonly ArmyUnitStack[] Units = [new(), new(), new(), new(), new(), new(), new()];
    
    public bool IsEmpty => Units.All(stack => stack.IsEmpty);
}