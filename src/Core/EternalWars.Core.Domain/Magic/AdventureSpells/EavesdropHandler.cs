using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;

namespace EternalWars.Core.Domain.Magic.AdventureSpells;

/// <summary>
/// Eavesdrop: what a town of another player, not an ally, or a neutral one shows, in sight (rank I) or explored (from rank II); at rank III the
/// hero keeps listening to it for the week.
/// </summary>
internal sealed class EavesdropHandler : AdventureSpellHandler
{
    public override Common.SpellId Spell => AdventureMagic.Eavesdrop;

    public override bool NeedsTarget(GameState state, Hero hero) => true;

    public override string? Refusal(GameState state, Hero hero, int rank, MapPosition? target, bool aimed)
    {
        if (!aimed)
        {
            return null;
        }

        if (AdventureMagic.EavesdropTown(state, target) is not { } town)
        {
            return "Eavesdrop needs a town to listen to.";
        }

        if (town.Owner is { } owner && (owner == hero.Owner || state.AreAllies(owner, hero.Owner)))
        {
            return "Eavesdrop listens only to enemy towns.";
        }

        return rank >= 2
            ? AdventureIntel.Explored(state, hero.Owner, town) ? null : "Eavesdrop needs a town the player has explored."
            : AdventureIntel.InSight(state, hero.Owner, town) ? null : "Eavesdrop at rank I needs a town in sight.";
    }

    public override string Aim(int rank) => rank >= 2 ? "Eavesdrop: click an enemy town." : "Eavesdrop: click an enemy town in sight.";

    public override void Cast(AdventureCast cast)
    {
        var hero = cast.Hero;
        var listened = AdventureMagic.EavesdropTown(cast.State, cast.Target)!;
        if (cast.Rank >= 3)
        {
            cast.Mark(new HeroStatus(cast.Spell.Value, HeroStatusKind.Cast, listened.Id.Value, StatusExpiry.EndOfWeek));
        }

        cast.Events.Publish(new TownOverheard(hero.Id, hero.Owner, AdventureIntel.Town(cast.State, listened, hero.Owner)));
    }
}
