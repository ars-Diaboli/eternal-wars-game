using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;

namespace EternalWars.Core.Domain.MapEvents.Actions;

/// <summary>Experience for the hero (gains only).</summary>
internal sealed class ExperienceHandler : HeroActionHandler<ExperienceAction>
{
    protected override void ApplyTo(MapEventActionContext context, Hero hero, ExperienceAction action)
    {
        if (context.Evaluate(action.Amount) is > 0 and var gained)
        {
            context.Service.Progression.GrantExperience(hero, gained);
        }
    }

    protected override EventAction Fix(MapEventActionContext context, ExperienceAction action) =>
        action with { Amount = new NumberExpr(context.Evaluate(action.Amount)) };

    protected override string Describe(GameState state, ExperienceAction action) => $"{Signed(Amount(action.Amount))} experience";
}
