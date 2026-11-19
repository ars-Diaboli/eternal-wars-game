using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Magic.AdventureSpells;

namespace EternalWars.Core.Domain.Magic;

/// <summary>What the status line asks for while an adventure spell waits for its cell (<see cref="AdventureMagic.NeedsTarget"/>).</summary>
public static class AdventureSpellPrompt
{
    /// <summary>
    /// The prompt for <paramref name="spell"/> armed by <paramref name="hero"/>, its handler's (<see cref="IAdventureSpellHandler.Aim"/>): Dimension
    /// Door (with its range at the hero's rank), Distant Vision, Eavesdrop (a town in sight below rank II), else Town Portal.
    /// </summary>
    public static string Aim(GameState state, HeroId hero, Common.SpellId spell)
    {
        var rank = AdventureMagic.RankOf(state, state.GetHero(hero), spell);
        return AdventureSpellHandlers.Find(spell)?.Aim(rank) ?? "Town Portal: click an own town.";
    }
}
