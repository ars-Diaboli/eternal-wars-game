using System.Collections.Generic;
using EW.Engine;
using EW.Scripts.Combat;
using EW.Scripts.Messaging;
using Godot;
using CombatTurnManager = EW.Scripts.Combat.CombatTurnManager;

namespace EW;

public partial class CombatManager : Node, IHandleEvent<TurnChanged>
{
    [Export] public Marker3D PlayerSpawnRoot { get; set; }
    [Export] public Marker3D EnemySpawnRoot { get; set; }
    [Export] public PackedScene CombatUnitBaseScene { get; set; } // A scene containing a Node3D + CombatUnit.cs
    
    private CombatTurnManager _turnManager;
    private List<CombatUnit> _spawnedUnits = new();

    public override void _Ready()
    {
        _turnManager = GetNode<CombatTurnManager>("CombatTurnManager");

        SpawnArmies();
        
        // Hand the units over to the Turn Manager to kickoff combat
        _turnManager.Initialize(_spawnedUnits);
    }
    
    private void SpawnArmies()
    {
        // Setup Player units
        var playerArmy = GameManager.Instance.PlayerArmy;
        for (int i = 0; i < playerArmy.Count; i++)
        {
            var pos = PlayerSpawnRoot.GlobalPosition + new Vector3(i * 2, 0, 0);
            SpawnUnit(playerArmy[i], pos);
        }

        // Setup Enemy units
        var enemyArmy = GameManager.Instance.EnemyArmy;
        for (int i = 0; i < enemyArmy.Count; i++)
        {
            var pos = EnemySpawnRoot.GlobalPosition + new Vector3(i * -2, 0, 0);
            SpawnUnit(enemyArmy[i], pos);
        }
    }

    private void SpawnUnit(UnitData data, Vector3 position)
    {
        // Instantiate our generic logical wrapper
        CombatUnit unitNode = CombatUnitBaseScene.Instantiate<CombatUnit>();
        AddChild(unitNode);
        unitNode.GlobalPosition = position;
        
        // Pass the resource data to visually construct and stat the unit
        unitNode.Initialize(data);
        
        _spawnedUnits.Add(unitNode);
    }

    public void Handle(TurnChanged eventData)
    {
        // 1. Highlight the active unit on screen
        // 2. Enable UI controls if it's a player unit, or trigger AI behavior if it's an enemy
        if (eventData.ActiveUnit.IsEnemy)
        {
            ExecuteEnemyAI(eventData.ActiveUnit);
        }
        else
        {
            EnablePlayerControls(eventData.ActiveUnit);
        }
    }

    private void ExecuteEnemyAI(CombatUnit enemy)
    {
        GD.Print($"AI processing for {enemy.Data.UnitName}...");
        // Code for pathfinding to nearest player, attacking, etc.
        
        // For testing, just end turn after a short delay
        GetTree().CreateTimer(1.5f).Timeout += () => _turnManager.AdvanceTurn();
    }

    private void EnablePlayerControls(CombatUnit playerUnit)
    {
        GD.Print("Waiting for Player Input...");
        // Here, your mouse-click/grid script would look at playerUnit.Data.MovementRange 
        // to highlight reachable tiles!
    }
    
    // Call this when all enemies are dead
    public void OnVictory()
    {
        GameManager.Instance.EndCombat(playerWon: true);
    }
}