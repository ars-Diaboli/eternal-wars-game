using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Pathfinding;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Heroes;

public sealed class HeroMovementService(GameState state, IEventPublisher events, MapObjectInteractionService interactions)
{
    /// <summary>Diagonal steps cost ~√2 times more, as in HoMM.</summary>
    public const double DiagonalMultiplier = 1.41;

    private readonly PathFinder _pathFinder = new(state);

    /// <summary>
    /// HoMM3 "last step" rule: any positive amount of movement points allows one more step,
    /// even if it costs more; the hero then has 0 left.
    /// </summary>
    public static bool CanAffordStep(int movementPointsLeft) => movementPointsLeft > 0;

    /// <summary>Plans a route through tiles the hero's owner has explored. Null when unreachable.</summary>
    public HeroPath? FindPath(HeroId heroId, MapPosition target) => _pathFinder.FindPath(heroId, target);

    /// <summary>Plans and follows a route as far as movement points allow (click-to-move).</summary>
    internal MovementOutcome MoveTo(HeroId heroId, MapPosition target)
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
    /// boundary), when a visit or meeting opens a decision, when a battle starts, or, in refog mode, when
    /// an enemy hero comes into sight. Every step is re-validated, so stale paths are safe.
    /// </summary>
    internal MovementOutcome MoveAlong(HeroPath path)
    {
        var hero = state.GetHero(path.Hero);
        var watchForEnemies = state.Options.FogMode == FogMode.ShroudWithRefog;
        var knownEnemies = watchForEnemies ? VisibleEnemies(hero.Owner) : [];
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

            if (watchForEnemies && !VisibleEnemies(hero.Owner).IsSubsetOf(knownEnemies))
            {
                return Outcome(MoveResult.EnemySpotted);
            }
        }

        return Outcome(MoveResult.Moved);

        MovementOutcome Outcome(MoveResult reason) => new(steps, reason, hero.Position == path.Destination);
    }

    /// <summary>
    /// One step. Stepping towards another hero is an encounter instead of a move: own hero → exchange
    /// (<see cref="MoveResult.Met"/>), enemy → battle (<see cref="MoveResult.BattleStarted"/>, an ambush if
    /// the enemy was unseen), ally → <see cref="MoveResult.Occupied"/>.
    /// </summary>
    internal MoveResult TryStep(HeroId heroId, Direction direction)
    {
        var hero = state.GetHero(heroId);
        var refusal = CheckCanAct(hero);
        if (refusal != MoveResult.Moved)
        {
            return refusal;
        }

        var target = hero.Position.Step(direction);
        var tile = MovementRules.CheckTile(state, target);
        if (tile != MoveResult.Moved)
        {
            return tile;
        }

        if (!CanAffordStep(hero.MovementPoints))
        {
            return MoveResult.NotEnoughMovementPoints;
        }

        if (state.HeroAt(target) is { } other)
        {
            switch (MovementRules.Classify(state, hero.Owner, other))
            {
                case HeroEncounter.Own:
                    interactions.Meet(hero, other);
                    return MoveResult.Met;
                case HeroEncounter.Enemy when hero.Army.IsEmpty:
                    return MoveResult.NoArmy;
                case HeroEncounter.Enemy:
                    interactions.StartBattle(hero, other, isAmbush: !state.CanSee(hero.Owner, other));
                    return MoveResult.BattleStarted;
                default:
                    return MoveResult.Occupied;
            }
        }

        var cost = MovementRules.StepCost(state, hero, hero.Position, target, direction);
        var entrance = state.ObjectCellAt(target) is { Cell: FootprintCell.Visit } cell ? cell.Object : null;

        // Monsters are attacked from where the hero stands; guards intercept anyone entering their area (ADR-040).
        if (entrance is MonsterGuard monster)
        {
            if (hero.Army.IsEmpty)
            {
                return MoveResult.NoArmy;
            }

            interactions.Visit(hero, monster);
            return MoveResult.GuardEncountered;
        }

        if (state.GuardsOf(target).ToList() is [var firstGuard, ..] guards)
        {
            var isPickup = entrance is ResourcePile or TreasureChest or ArtifactPickup or LootSite { Vanishes: true };
            if (isPickup && entrance is not null && guards.All(g => g.Mode == GuardMode.Classic))
            {
                // HoMM3: a pickup next to Classic monsters can be grabbed from outside their zone without a fight.
                hero.MovementPoints = Math.Max(0, hero.MovementPoints - cost);
                interactions.Visit(hero, entrance);
                return MoveResult.CollectedFromAdjacent;
            }

            // A pickup is defended by the Area stacks guarding it (ADR-065); anything else by the first guard.
            var guard = isPickup ? guards.First(g => g.Mode == GuardMode.Area) : firstGuard;

            if (hero.Army.IsEmpty)
            {
                return MoveResult.NoArmy;
            }

            interactions.Visit(hero, guard);
            return MoveResult.GuardEncountered;
        }

        var defended = entrance is Mine { Guard: not null } || (entrance is Town { Garrison.IsEmpty: false } town && IsHostile(hero, town));
        if (defended && hero.Army.IsEmpty)
        {
            return MoveResult.NoArmy;
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
            hero.MaxMovementPoints = MovementAllowance.Daily(state, hero);
            hero.MovementPoints = hero.MaxMovementPoints;
            events.Publish(new HeroMovementRestored(hero.Id, hero.MovementPoints));
        }
    }

    private bool IsHostile(Hero hero, Town town) => town.Owner is not { } owner || state.AreEnemies(hero.Owner, owner);

    private HashSet<HeroId> VisibleEnemies(PlayerId viewer) =>
        state.Heroes.Where(h => state.AreEnemies(viewer, h.Owner) && state.CanSee(viewer, h)).Select(h => h.Id).ToHashSet();

    private MoveResult CheckCanAct(Hero hero)
    {
        if (!state.IsStarted || hero.Owner != state.CurrentPlayer.Id)
        {
            return MoveResult.NotYourTurn;
        }

        return state.PendingDecision is null ? MoveResult.Moved : MoveResult.DecisionPending;
    }
}
