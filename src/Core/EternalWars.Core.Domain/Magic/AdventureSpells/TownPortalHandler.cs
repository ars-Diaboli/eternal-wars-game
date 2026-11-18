using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;

namespace EternalWars.Core.Domain.Magic.AdventureSpells;

/// <summary>
/// Town Portal: to an own town with a free entrance, at rank I the nearest; from rank II a picked one (without a pick, the nearest). Below rank III
/// it ends the day's movement.
/// </summary>
internal sealed class TownPortalHandler : AdventureSpellHandler
{
    public override string Spell => AdventureMagic.TownPortal;

    public override bool NeedsTarget(GameState state, Hero hero) => AdventureMagic.RankOf(state, hero, Spell) >= 2;

    public override string? Refusal(GameState state, Hero hero, int rank, MapPosition? target, bool aimed)
    {
        if (AdventureMagic.PortalTown(state, hero) is null)
        {
            return "There is no own town to return to.";
        }

        if (!aimed || target is null)
        {
            return null;
        }

        if (rank < 2)
        {
            return AdventureMagic.PortalTown(state, hero, target) == AdventureMagic.PortalTown(state, hero) ? null : "Town Portal goes only to the nearest own town at rank I.";
        }

        return AdventureMagic.PortalTown(state, hero, target) is null ? "Town Portal needs an own town with a free entrance." : null;
    }

    public override string Aim(int rank) => "Town Portal: click an own town.";

    public override void Cast(AdventureCast cast)
    {
        var town = AdventureMagic.PortalTown(cast.State, cast.Hero, cast.Rank >= 2 ? cast.Target : null)!;
        cast.Relocate(town.VisitPosition);
        cast.Towns.Enter(cast.Hero, town);
    }
}
