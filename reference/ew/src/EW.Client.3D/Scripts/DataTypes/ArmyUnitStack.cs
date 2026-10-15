using EW.Engine;
using Godot;

namespace EW.Scripts.DataTypes;

[GlobalClass]
public partial class ArmyUnitStack : Resource
{
    [Export] public UnitData Unit { get; set; }

    [Export(PropertyHint.Range, "0,99999,1,or_greater")]
    public int Amount { get; set; }

    public bool IsEmpty => Unit == null || Amount <= 0;
}
