using EW.CoreLib.Tests.aStar;
using EW.CoreLib.Tests.WorldMap;
using EW.CoreLib.Types;
using NUnit.Framework;

namespace EW.CoreLib.Tests;

[TestFixture]
public class HeroMovementTests : GameMapTestsBase
{
   [Test]
   public void ProcessAnimation_WhenProgressDoesNotReachWaypoint_KeepsHeroInPlace()
   {
      RedHero.Path.MovementWaypoints.Enqueue(new PathWaypoint(new Position(0, 1), 100, 1f, true, true));
      RedHero.Path.StartMovement();

      RedHero.ProcessAnimation(0.15);

      Assert.That(RedHero.Position, Is.EqualTo(new Position(0, 0)));
      Assert.That(RedHero.Path.MovementWaypoints.Count, Is.EqualTo(1));
      Assert.That(RedHero.Path.IsMoving, Is.True);
   }

   [Test]
   public void ProcessAnimation_WhenProgressReachesWaypoint_MovesHeroToNextWaypoint()
   {
      RedHero.Path.MovementWaypoints.Enqueue(new PathWaypoint(new Position(0, 1), 100, 1f, true, true));
      RedHero.Path.MovementWaypoints.Enqueue(new PathWaypoint(new Position(0, 2), 100, 1f, true, true));
      RedHero.Path.StartMovement();

      RedHero.ProcessAnimation(0.2);

      Assert.That(RedHero.Position, Is.EqualTo(new Position(0, 1)));
      Assert.That(RedHero.Path.MovementWaypoints.Count, Is.EqualTo(1));
      Assert.That(RedHero.Path.MovementWaypoints.Peek().Position, Is.EqualTo(new Position(0, 2)));
      Assert.That(RedHero.Path.IsMoving, Is.True);
   }

   [Test]
   public void ProcessAnimation_WhenAllWaypointsAreConsumed_StopsMovementAtFinalPosition()
   {
      RedHero.Path.MovementWaypoints.Enqueue(new PathWaypoint(new Position(0, 1), 100, 1f, true, true));
      RedHero.Path.MovementWaypoints.Enqueue(new PathWaypoint(new Position(0, 2), 100, 1f, true, true));
      RedHero.Path.StartMovement();

      RedHero.ProcessAnimation(0.25);
      RedHero.ProcessAnimation(0.25);

      Assert.That(RedHero.Position, Is.EqualTo(new Position(0, 2)));
      Assert.That(RedHero.Path.MovementWaypoints, Is.Empty);
      Assert.That(RedHero.Path.IsMoving, Is.False);
   }

   [Test]
   public void ProcessAnimation_WhenStopAtNextWaypointIsRequested_StopsAtNearestWaypoint()
   {
      RedHero.Path.MovementWaypoints.Enqueue(new PathWaypoint(new Position(0, 1), 100, 1f, true, true));
      RedHero.Path.MovementWaypoints.Enqueue(new PathWaypoint(new Position(0, 2), 100, 1f, true, true));
      RedHero.Path.StartMovement();
      RedHero.Path.RequestStopAtNearestWaypoint();

      RedHero.ProcessAnimation(0.25);

      Assert.That(RedHero.Position, Is.EqualTo(new Position(0, 1)));
      Assert.That(RedHero.Path.MovementWaypoints.Count, Is.EqualTo(1));
      Assert.That(RedHero.Path.MovementWaypoints.Peek().Position, Is.EqualTo(new Position(0, 2)));
      Assert.That(RedHero.Path.IsMoving, Is.False);
      Assert.That(RedHero.Path.StopAtNextWaypoint, Is.False);
   }

   [Test]
   public void TryGetCurrentTraversal_WhenHeroIsMoving_ReturnsInterpolatedSegment()
   {
      RedHero.Path.MovementWaypoints.Enqueue(new PathWaypoint(new Position(0, 1), 100, 1f, true, true));
      RedHero.Path.StartMovement();

      RedHero.ProcessAnimation(0.1);

      var movementProgress = RedHero.Path.GetMovementProgress();

      Assert.That(movementProgress.From, Is.EqualTo(Position.Zero));
      Assert.That(movementProgress.To, Is.EqualTo(new Position(0, 1)));
      Assert.That(movementProgress.Progress, Is.EqualTo(0.5f).Within(0.001f));
   }

   [Test]
   public void ProcessAnimation_WhenWaypointIsReached_DeductsWaypointMovementCost()
   {
      RedHero.Path.CurrentMovementPoints = 250;
      RedHero.Path.MovementWaypoints.Enqueue(new PathWaypoint(new Position(0, 1), 100, 1f, true, true));
      RedHero.Path.StartMovement();

      RedHero.ProcessAnimation(0.2);

      Assert.That(RedHero.Path.CurrentMovementPoints, Is.EqualTo(150));
   }
}
