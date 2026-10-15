using System.Collections.Generic;
using System.Linq;
using EW.Engine;
using EW.Scripts.Messaging;
using Godot;

namespace EW.Scripts.Combat;

public class TurnChanged : IGameEvent
{
    public string Name => "combat.turn_changed";
    public CombatUnit ActiveUnit { get; init; }
}
public class RoundStarted : IGameEvent
{
    public string Name => "combat.round_started";
    public int CurrentRound { get; init; }
}

public partial class CombatTurnManager : Node
{
    private List<CombatUnit> _allUnits = new();
    private Queue<CombatUnit> _turnQueue = new();
    
    public int CurrentRound { get; private set; } = 0;
    public CombatUnit ActiveUnit { get; private set; }
    
    private CombatTurnState _currentState;
    
    public void Initialize(List<CombatUnit> combatants)
    {
        _allUnits = combatants;
        StartNewRound();
    }
    
    public void StartNewRound()
    {
        CurrentRound++;
        Hub.Instance?.Publish(new RoundStarted { CurrentRound = CurrentRound });
        GD.Print($"--- Round {CurrentRound} Started ---");

        // 1. Reset turn flags for living units
        foreach (var unit in _allUnits.Where(u => !u.IsDead))
        {
            unit.HasTakenTurn = false;
        }

        // 2. Sort by Initiative (Highest first). 
        // Tie-breaker: You can add secondary logic here if initiatives match.
        var sortedUnits = _allUnits
            .Where(u => !u.IsDead)
            .OrderByDescending(u => u.CurrentInitiative)
            .ToList();

        // 3. Populate the queue
        _turnQueue.Clear();
        foreach (var unit in sortedUnits)
        {
            _turnQueue.Enqueue(unit);
        }

        AdvanceTurn();
    }
    
    public void AdvanceTurn()
    {
        // Remove any units that might have died before getting their turn
        while (_turnQueue.Count > 0 && _turnQueue.Peek().IsDead)
        {
            _turnQueue.Dequeue();
        }

        // If the queue is empty, the round is over!
        if (_turnQueue.Count == 0)
        {
            StartNewRound();
            return;
        }

        // Set the next unit active
        ActiveUnit = _turnQueue.Dequeue();
        ActiveUnit.HasTakenTurn = true;

        GD.Print($"It is now {ActiveUnit.Data.UnitName}'s turn!");
        Hub.Instance?.Publish(new TurnChanged { ActiveUnit = ActiveUnit });
    }

    public void HandleUnitDeath(CombatUnit deadUnit)
    {
        deadUnit.IsDead = true;
        // The AdvanceTurn method will automatically prune them when their turn comes up
    }
}
