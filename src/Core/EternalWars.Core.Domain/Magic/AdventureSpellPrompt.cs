using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;

namespace EternalWars.Core.Domain.Magic;

/// <summary>What the status line asks for while an adventure spell waits for its cell (<see cref="AdventureMagic.NeedsTarget"/>).</summary>
public static class AdventureSpellPrompt
{
    /// <summary>
    /// The prompt for <paramref name="spell"/> armed by <paramref name="hero"/>: Dimension Door (with its range at the hero's rank), Distant
    /// Vision, Eavesdrop (a town in sight below rank II), else Town Portal.
    /// </summary>
    public static string Aim(GameState state, HeroId hero, string spell)
    {
        var rank = AdventureMagic.RankOf(state, state.GetHero(hero), spell);
        return spell switch
        {
            AdventureMagic.DimensionDoor => $"Dimension Door: click a tile up to {AdventureMagic.DimensionDoorRange(rank)} tiles away in a straight line.",
            AdventureMagic.DistantVision => "Distant Vision: click a cell in sight.",
            AdventureMagic.Eavesdrop => rank >= 2 ? "Eavesdrop: click an enemy town." : "Eavesdrop: click an enemy town in sight.",
            _ => "Town Portal: click an own town.",
        };
    }
}
