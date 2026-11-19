using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;

namespace EternalWars.Core.Domain.MapEvents.Actions;

/// <summary>Gives the hero a spell book.</summary>
internal sealed class SpellBookHandler : HeroActionHandler<SpellBookAction>
{
    protected override void ApplyTo(MapEventActionContext context, Hero hero, SpellBookAction action)
    {
        if (!hero.HasSpellBook)
        {
            hero.HasSpellBook = true;
            context.Events.Publish(new MapEventSpellBookGained(hero.Id));
        }
    }

    protected override string Describe(GameState state, SpellBookAction action) => "+ spell book";

    protected override string Line(SpellBookAction action, GameState? state) => "Give a spell book";

    protected override EventWorth Worth(IMapEventValuer valuer, SpellBookAction action) =>
        valuer.Hero is { HasSpellBook: false } ? EventWorth.Gain(500) : EventWorth.Nothing;
}
