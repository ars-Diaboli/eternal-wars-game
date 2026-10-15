using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Heroes;

public enum MoveResult
{
    Moved,
    NotYourTurn,
    OutOfBounds,
    Impassable,
    Occupied,
    NotEnoughMovementPoints,
}

public sealed class HeroMovementService(GameState state, IEventPublisher events)
{
    /// <summary>Diagonal steps cost ~√2 times more, as in HoMM.</summary>
    private const double DiagonalMultiplier = 1.41;

    public MoveResult TryStep(HeroId heroId, Direction direction)
    {
        var hero = state.GetHero(heroId);
        if (!state.IsStarted || hero.Owner != state.CurrentPlayer.Id)
        {
            return MoveResult.NotYourTurn;
        }

        var target = hero.Position.Step(direction);
        if (!state.Map.Contains(target))
        {
            return MoveResult.OutOfBounds;
        }

        var terrain = state.Map.GetTerrain(target);
        if (!terrain.IsPassableOnFoot())
        {
            return MoveResult.Impassable;
        }

        if (state.HeroAt(target) is not null)
        {
            return MoveResult.Occupied;
        }

        var cost = StepCost(terrain, direction);
        if (cost > hero.MovementPoints)
        {
            return MoveResult.NotEnoughMovementPoints;
        }

        var from = hero.Position;
        hero.Position = target;
        hero.MovementPoints -= cost;
        events.Publish(new HeroMoved(hero.Id, from, target, hero.MovementPoints));
        return MoveResult.Moved;
    }

    public static int StepCost(TerrainType destination, Direction direction)
    {
        var baseCost = destination.MovementCost();
        return direction.IsDiagonal() ? (int)Math.Round(baseCost * DiagonalMultiplier) : baseCost;
    }

    internal void RestoreMovement(PlayerId owner)
    {
        foreach (var hero in state.HeroesOf(owner))
        {
            hero.MovementPoints = hero.MaxMovementPoints;
            events.Publish(new HeroMovementRestored(hero.Id, hero.MovementPoints));
        }
    }
}
