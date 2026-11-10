using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Turns;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Heroes;

/// <summary>
/// At the start of its owner's turn every hero regains its daily mana (ADR-063), or all of it with the Wizard's Well (ADR-121); nothing happens,
/// and nothing is published, when the pool is full.
/// </summary>
internal sealed class ManaRestoration(GameState state, IEventPublisher events) : ITurnPhaseParticipant
{
    void ITurnPhaseParticipant.PrepareTurn(PlayerId player)
    {
        foreach (var hero in state.AllHeroesOf(player).OrderBy(h => h.Id.Value))
        {
            var max = hero.MaxMana;
            if (hero.Mana < max)
            {
                hero.Mana = hero.Bonus.FullMana ? max : Math.Min(max, hero.Mana + ManaRules.DailyMana(hero));
                events.Publish(new HeroManaRestored(hero.Id, hero.Mana, max));
            }
        }
    }
}
