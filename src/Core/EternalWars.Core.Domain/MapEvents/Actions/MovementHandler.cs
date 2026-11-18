using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;

namespace EternalWars.Core.Domain.MapEvents.Actions;

/// <summary>Changes the hero's movement points for today, down to 0.</summary>
internal sealed class MovementHandler : HeroActionHandler<MovementAction>
{
    protected override void ApplyTo(MapEventActionContext context, Hero hero, MovementAction action)
    {
        if (context.Evaluate(action.Amount) is not 0 and var points)
        {
            var before = hero.MovementPoints;
            hero.MovementPoints = Math.Max(0, before + points);
            context.Events.Publish(new HeroMovementGained(hero.Id, hero.MovementPoints - before, hero.MovementPoints));
        }
    }

    protected override EventAction Fix(MapEventActionContext context, MovementAction action) =>
        action with { Amount = new NumberExpr(context.Evaluate(action.Amount)) };

    protected override string Describe(GameState state, MovementAction action) => $"{Signed(Amount(action.Amount))} movement";
}
