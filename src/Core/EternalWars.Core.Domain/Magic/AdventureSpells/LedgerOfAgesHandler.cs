using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Heroes;

namespace EternalWars.Core.Domain.Magic.AdventureSpells;

/// <summary>Ledger of Ages: the players' ledger; from rank II with their towns, and at rank III it stays open for the week.</summary>
internal sealed class LedgerOfAgesHandler : AdventureSpellHandler
{
    public override Common.SpellId Spell => AdventureMagic.LedgerOfAges;

    public override void Cast(AdventureCast cast)
    {
        var hero = cast.Hero;
        if (cast.Rank >= 3)
        {
            cast.Mark(new HeroStatus(cast.Spell.Value, HeroStatusKind.Cast, 1, StatusExpiry.EndOfWeek));
        }

        cast.Events.Publish(new LedgerRead(hero.Id, hero.Owner, AdventureIntel.Ledger(cast.State, hero.Owner, inTowns: cast.Rank >= 2)));
    }
}
