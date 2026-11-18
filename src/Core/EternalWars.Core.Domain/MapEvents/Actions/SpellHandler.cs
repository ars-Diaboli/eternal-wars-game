using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;

namespace EternalWars.Core.Domain.MapEvents.Actions;

/// <summary>Teaches the hero a spell; it needs a spell book.</summary>
internal sealed class SpellHandler : HeroActionHandler<SpellAction>
{
    protected override void ApplyTo(MapEventActionContext context, Hero hero, SpellAction action)
    {
        if (hero.HasSpellBook && context.State.Spells.Contains(action.Spell) && hero.Learn(action.Spell))
        {
            context.Events.Publish(new MapEventSpellLearned(hero.Id, action.Spell));
        }
    }

    protected override string Describe(GameState state, SpellAction action) =>
        $"+ {(state.Spells.TryGet(action.Spell, out var spell) ? spell.Name : action.Spell)}";
}
