using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;

namespace EternalWars.Core.Domain.MapEvents.Actions;

/// <summary>Changes the hero's mana: up to twice its maximum, down to 0.</summary>
internal sealed class ManaHandler : HeroActionHandler<ManaAction>
{
    protected override void ApplyTo(MapEventActionContext context, Hero hero, ManaAction action)
    {
        var amount = context.Evaluate(action.Amount);
        var before = hero.Mana;
        if (amount > 0 && Math.Min(2 * hero.MaxMana, before + amount) is var raised && raised > before)
        {
            hero.Mana = raised;
            context.Events.Publish(new HeroManaRestored(hero.Id, hero.Mana, hero.MaxMana));
        }
        else if (amount < 0 && before > 0)
        {
            hero.Mana = before + amount;
            context.Events.Publish(new HeroManaSpent(hero.Id, before - hero.Mana, hero.Mana, hero.MaxMana));
        }
    }

    protected override EventAction Fix(MapEventActionContext context, ManaAction action) =>
        action with { Amount = new NumberExpr(context.Evaluate(action.Amount)) };

    protected override string Describe(GameState state, ManaAction action) => $"{Signed(Amount(action.Amount))} mana";

    protected override IEnumerable<EventExpr> Expressions(ManaAction action) => [action.Amount];

    protected override string Line(ManaAction action, GameState? state) => Gain(action.Amount, "mana");

    protected override EventWorth Worth(IMapEventValuer valuer, ManaAction action) =>
        valuer.Hero is { } hero ? EventWorth.Gain(2 * Math.Max(-hero.Mana, valuer.Average(action.Amount))) : EventWorth.Nothing;
}
