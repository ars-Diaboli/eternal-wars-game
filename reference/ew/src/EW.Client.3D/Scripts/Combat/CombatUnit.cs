using Godot;

namespace EW.Engine;

public partial class CombatUnit : Node3D
{
    public UnitData Data { get; private set; }
    
    // Dynamic stats that can change during combat via spells/buffs
    public int CurrentInitiative { get; set; }
    public bool HasTakenTurn { get; set; } = false;
    public bool IsDead { get; set; } = false;
    public bool IsEnemy { get; set; } = false;

    public void Initialize(UnitData data)
    {
        Data = data;
        CurrentInitiative = data.Initiative;
        
        // Instantiate the visual model inside this node
        var model = data.ModelScene.Instantiate();
        AddChild(model);
    }
}