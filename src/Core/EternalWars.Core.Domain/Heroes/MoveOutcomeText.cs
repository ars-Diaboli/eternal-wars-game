using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Pathfinding;

namespace EternalWars.Core.Domain.Heroes;

/// <summary>A hero's move, step or planned route in words, for the adventure map's status line.</summary>
public static class MoveOutcomeText
{
    /// <summary>What a map command did: its outcome described, or the refusal.</summary>
    public static string Of(CommandResult result) => result.IsAccepted ? Describe(result.Outcome) : $"Refused: {result.Error}";

    /// <summary>A step's or a move's result (a <see cref="MovementOutcome"/> or a <see cref="MoveResult"/>); empty for none.</summary>
    public static string Describe(object? outcome) => outcome switch
    {
        MovementOutcome move => move.Arrived ? $"Arrived ({move.StepsTaken} steps)." : $"{Describe(move.StopReason)} ({move.StepsTaken} steps).",
        MoveResult reason => reason switch
        {
            MoveResult.Moved => "Moved.",
            MoveResult.NotYourTurn => "Not your turn.",
            MoveResult.DecisionPending => "Answer the open question first.",
            MoveResult.OutOfBounds => "The edge of the map.",
            MoveResult.Impassable => "Impassable ground.",
            MoveResult.Blocked => "The way is blocked.",
            MoveResult.Occupied => "An allied hero stands there.",
            MoveResult.NotEnoughMovementPoints => "No movement left today.",
            MoveResult.NoPath => "No path there.",
            MoveResult.Interrupted => "The way changed: stopped.",
            MoveResult.Met => "Meeting.",
            MoveResult.BattleStarted => "Battle!",
            MoveResult.EnemySpotted => "An enemy hero comes into sight: stopped.",
            MoveResult.GuardEncountered => "Guards bar the way.",
            MoveResult.CollectedFromAdjacent => "Picked up from beside the guards.",
            MoveResult.NoArmy => "A hero without an army cannot attack.",
            MoveResult.QuestVisited => "A quest.",
            _ => reason.ToString(),
        },
        null => "",
        _ => outcome.ToString() ?? "",
    };

    /// <summary>A previewed route: its length and the days it takes.</summary>
    public static string Route(HeroPath path) => $"{path.Steps.Count} steps, {path.Turns} day(s). Click again to go.";
}
