using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;

namespace EternalWars.Core.Domain.MapEvents.Actions;

/// <summary>A primary stat of the hero (gains only).</summary>
internal sealed class PrimaryStatHandler : HeroActionHandler<PrimaryStatAction>
{
    protected override void ApplyTo(MapEventActionContext context, Hero hero, PrimaryStatAction action)
    {
        if (context.Evaluate(action.Amount) is > 0 and var raised)
        {
            context.Service.Progression.RaiseStat(hero, action.Stat, raised);
        }
    }

    protected override EventAction Fix(MapEventActionContext context, PrimaryStatAction action) =>
        action with { Amount = new NumberExpr(context.Evaluate(action.Amount)) };

    protected override string Describe(GameState state, PrimaryStatAction action) => $"{Signed(Amount(action.Amount))} {action.Stat}";
}
