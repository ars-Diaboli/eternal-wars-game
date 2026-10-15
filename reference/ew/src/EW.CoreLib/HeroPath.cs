using EW.CoreLib.Interfaces;
using EW.CoreLib.Types;

namespace EW.CoreLib;

public class HeroPath : IPersist<HeroPathStateDto>
{
   public Position Position { get; private set; }
   public int CurrentMovementPoints { get; set; } = 2500;
   public bool IsMoving { get; private set; } = false;
   public bool StopAtNextWaypoint { get; private set; }
   public Queue<PathWaypoint> MovementWaypoints { get; } = new();
   public double MovementProgress { get; private set; }
   
   private IInteractableMapObject? PendingInteractionTarget { get; set; }
   
   public bool IsMovementDestination(Position position) => MovementWaypoints.Last().Position == position;

   public bool IsNextToDestination() => MovementWaypoints.Count == 1 && MovementWaypoints.Peek().CanReachWithCurrentBudget;

   public bool ProgressedToNextWaypoint() => MovementProgress >= MovementWaypoints.Peek().Distance;
   public bool IsNextWaypointReachable() => MovementWaypoints.Count > 0 && MovementWaypoints.Peek().CanReachWithCurrentBudget;

   internal void SetPosition(Position position)
   {
      Position = position;
      MovementProgress = 0;
      IsMoving = false;
      StopAtNextWaypoint = false;
      MovementWaypoints.Clear();
   }
   
   public void ProgressToNextWaypoint()
   {
      var nextWaypoint = MovementWaypoints.Dequeue();
      if (CurrentMovementPoints < nextWaypoint.MovementCost) throw new InvalidOperationException("Not enough movement points to progress to next waypoint");
      CurrentMovementPoints -= nextWaypoint.MovementCost;
      MovementProgress -= nextWaypoint.Distance;
      Position = nextWaypoint.Position;
   }
   
   public bool IsCloseToInteractionTarget() => PendingInteractionTarget != null && IsNextToDestination();

   public void InteractWithTarget(Hero hero)
   {
      PendingInteractionTarget?.OnPlayerInteract(hero);
      PendingInteractionTarget = null;
   }
   
   public void MovementProgressed(double progress) => MovementProgress += progress;

   public (Position From, Position? To, float Progress) GetMovementProgress()
   {
      return !IsMoving || MovementWaypoints.Count == 0
         ? (Position, null, 0f)
         : (
            Position,
            MovementWaypoints.Peek().Position,
            (float)Math.Clamp(MovementProgress / MovementWaypoints.Peek().Distance, 0d, 1d)
         );
   }

   public void StartMovement() => IsMoving = true;

   public void StopMovement()
   {
      IsMoving = false;
      StopAtNextWaypoint = false;
      MovementProgress = 0;
   }

   public void RequestStopAtNearestWaypoint()
   {
      if (!IsMoving || !IsNextWaypointReachable())
      {
         StopMovement();
         return;
      }

      StopAtNextWaypoint = true;
   }

   public void AssignNewPath(PathWaypoint[] path)
   {
      ClearPendingInteractionTarget();
      MovementWaypoints.Clear();
      MovementProgress = 0;

      foreach (var waypoint in path)
      {
         MovementWaypoints.Enqueue(waypoint);
      }
   }
   
   public void SetPendingInteractionTarget(IRemovableMapObject target) => PendingInteractionTarget = target;

   public void ClearPendingInteractionTarget() => PendingInteractionTarget = null;

   public HeroPathStateDto ToState() => new()
   {
      CurrentMovementPoints = CurrentMovementPoints,
      MovementWaypoints = MovementWaypoints.ToArray()
   };

   // Populates this path in place, preserving the Position already set by the owning Hero's
   // constructor (Position is not carried on HeroPathStateDto), then restores movement state.
   public void FromState(HeroPathStateDto dto)
   {
      CurrentMovementPoints = dto.CurrentMovementPoints;

      foreach (var waypoint in dto.MovementWaypoints)
         MovementWaypoints.Enqueue(waypoint);
   }
}
