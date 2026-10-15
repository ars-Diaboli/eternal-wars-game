using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Pathfinding;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Heroes;

public sealed class HeroMovementService(GameState state, IEventPublisher events, MapObjectInteractionService interactions)
{
    /// <summary>Diagonal steps cost ~√2 times more, as in HoMM.</summary>
    private const double DiagonalMultiplier = 1.41;

    private readonly PathFinder _pathFinder = new(state);

    /// <summary>
    /// HoMM3 rule: a step costs the terrain of the tile being <b>left</b>, ×1.41 when diagonal.
    /// </summary>
    public static int StepCost(TerrainType origin, Direction direction)
    {
        var baseCost = origin.MovementCost();
        return direction.IsDiagonal() ? (int)Math.Round(baseCost * DiagonalMultiplier) : baseCost;
    }

    /// <summary>
    /// HoMM3 "last step" rule: any positive amount of movement points allows one more step,
    /// even if it costs more; the hero then has 0 left.
    /// </summary>
    public static bool CanAffordStep(int movementPointsLeft) => movementPointsLeft > 0;

    /// <summary>Plans a route through tiles the hero's owner has explored. Null when unreachable.</summary>
    public HeroPath? FindPath(HeroId heroId, MapPosition target) => _pathFinder.FindPath(heroId, target);

    /// <summary>Plans and follows a route as far as movement points allow (click-to-move).</summary>
    public MovementOutcome MoveTo(HeroId heroId, MapPosition target)
    {
        var refusal = CheckCanAct(state.GetHero(heroId));
        if (refusal != MoveResult.Moved)
        {
            return new MovementOutcome(0, refusal, Arrived: false);
        }

        return FindPath(heroId, target) is { } path
            ? MoveAlong(path)
            : new MovementOutcome(0, MoveResult.NoPath, Arrived: false);
    }

    /// <summary>
    /// Follows a planned path step by step, publishing one <see cref="HeroMoved"/> per step. Stops at the
    /// first step that cannot be made (usually <see cref="MoveResult.NotEnoughMovementPoints"/> at a day
    /// boundary) or when a visit opens a decision. Every step is re-validated, so stale paths are safe.
    /// </summary>
    public MovementOutcome MoveAlong(HeroPath path)
    {
        var hero = state.GetHero(path.Hero);
        var steps = 0;
        foreach (var step in path.Steps)
        {
            if (step.Kind == PathStepKind.Teleport)
            {
                // The gate visit on the previous step already moved the hero.
                if (hero.Position != step.Position)
                {
                    return Outcome(MoveResult.Interrupted);
                }

                continue;
            }

            var direction = step.Position.Level == hero.Position.Level
                ? DirectionExtensions.FromOffset(step.Position.X - hero.Position.X, step.Position.Y - hero.Position.Y)
                : null;
            if (direction is null)
            {
                return Outcome(MoveResult.Interrupted);
            }

            var result = TryStep(hero.Id, direction.Value);
            if (result != MoveResult.Moved)
            {
                return Outcome(result);
            }

            steps++;
            if (state.PendingDecision is not null)
            {
                return Outcome(MoveResult.DecisionPending);
            }
        }

        return Outcome(MoveResult.Moved);

        MovementOutcome Outcome(MoveResult reason) => new(steps, reason, hero.Position == path.Destination);
    }

    public MoveResult TryStep(HeroId heroId, Direction direction)
    {
        var hero = state.GetHero(heroId);
        var refusal = CheckCanAct(hero);
        if (refusal != MoveResult.Moved)
        {
            return refusal;
        }

        var target = hero.Position.Step(direction);
        var enter = MovementRules.CheckEnter(state, target);
        if (enter != MoveResult.Moved)
        {
            return enter;
        }

        var cost = StepCost(state.Map.GetTerrain(hero.Position), direction);
        if (!CanAffordStep(hero.MovementPoints))
        {
            return MoveResult.NotEnoughMovementPoints;
        }

        var from = hero.Position;
        hero.Position = target;
        hero.MovementPoints = Math.Max(0, hero.MovementPoints - cost);
        events.Publish(new HeroMoved(hero.Id, from, target, hero.MovementPoints));

        if (state.ObjectCellAt(target) is { Cell: FootprintCell.Visit } visit)
        {
            interactions.Visit(hero, visit.Object);
        }

        return MoveResult.Moved;
    }

    internal void RestoreMovement(PlayerId owner)
    {
        foreach (var hero in state.HeroesOf(owner))
        {
            hero.MaxMovementPoints = MovementAllowance.Daily(hero);
            hero.MovementPoints = hero.MaxMovementPoints;
            events.Publish(new HeroMovementRestored(hero.Id, hero.MovementPoints));
        }
    }

    private MoveResult CheckCanAct(Hero hero)
    {
        if (!state.IsStarted || hero.Owner != state.CurrentPlayer.Id)
        {
            return MoveResult.NotYourTurn;
        }

        return state.PendingDecision is null ? MoveResult.Moved : MoveResult.DecisionPending;
    }
}
