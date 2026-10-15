namespace EW.Engine;

using Godot;
using System.Collections.Generic;

public partial class GameManager : Node
{
    // Singleton instance
    public static GameManager Instance { get; private set; }

    // Data to transfer between scenes
    public List<UnitData> PlayerArmy { get; set; } = new();
    public List<UnitData> EnemyArmy { get; set; } = new();
    
    // Store the overworld scene path or state to return to it later
    private string _overworldScenePath = "res://Scenes/Overworld.tscn";
    private PackedScene _combatScene;

    public override void _Ready()
    {
        Instance = this;
        // Preload combat scene for faster transitions
        _combatScene = GD.Load<PackedScene>("res://Scenes/CombatMap.tscn");
    }

    public void StartCombat(List<UnitData> playerUnits, List<UnitData> enemyUnits)
    {
        PlayerArmy = playerUnits;
        EnemyArmy = enemyUnits;

        // Optional: Play a transition animation here (fade to black, screen swirl)
        
        // Change to the combat scene
        GetTree().ChangeSceneToPacked(_combatScene);
    }

    public void EndCombat(bool playerWon)
    {
        if (playerWon)
        {
            GD.Print("Player won! Apply rewards, remove enemy from overworld.");
        }
        else
        {
            GD.Print("Player lost! Game Over sequence.");
        }

        // Return to exploration map
        GetTree().ChangeSceneToFile(_overworldScenePath);
    }
}