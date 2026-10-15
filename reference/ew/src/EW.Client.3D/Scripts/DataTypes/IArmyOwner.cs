namespace EW.Scripts.DataTypes;

public interface IArmyOwner
{
    Godot.Collections.Array<ArmyUnitStack> Army { get; set; }
}
