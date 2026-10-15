using System.Collections.Generic;
using EW.Engine;
using EW.Scripts.Heroes;
using Godot;

namespace EW.Scripts;

public partial class OverworldEnemy : Area3D
{
    // Assign these in the Rider/Godot inspector for each enemy party
    [Export] public Godot.Collections.Array<UnitData> EnemyUnits { get; set; } = new();

    public override void _Ready()
    {
        // Connect the body entered signal to detect the player
        BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node3D body)
    {
        // Assuming your player node is named "Hero" or belongs to a "Player" group
        if (body.Name == "Hero" || body.IsInGroup("Player"))
        {
            GD.Print("Encounter triggered!");
            
            // Get player's army data (assuming Hero script holds it)
            var hero = (Hero)body;
            List<UnitData> playerArmy = [];//hero.GetArmy();

            // Convert Godot Array to standard C# List for the GameManager
            List<UnitData> enemyArmy = new List<UnitData>(EnemyUnits);

            // Trigger the transition via our Singleton
            GameManager.Instance.StartCombat(playerArmy, enemyArmy);
            
            // QueueFree this enemy so they aren't there when we return
            QueueFree(); 
        }
    }
}