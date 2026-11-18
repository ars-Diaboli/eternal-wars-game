using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Heroes;

namespace EternalWars.Core.Domain.Magic.AdventureSpells;

/// <summary>
/// Grave Whisper: a Necromancy share for the week, until the hero raises undead; rank III also shows the numbers of the neutral stacks around it.
/// </summary>
internal sealed class GraveWhisperHandler : AdventureSpellHandler
{
    public override string Spell => AdventureMagic.GraveWhisper;

    public override void Cast(AdventureCast cast)
    {
        var hero = cast.Hero;
        var percent = AdventureMagic.GraveWhisperPercent(cast.Rank);
        cast.Mark(new HeroStatus(cast.Spell, HeroStatusKind.Necromancy, percent, StatusExpiry.EndOfWeek));
        cast.Events.Publish(new GravesMarked(hero.Id, hero.Owner, percent,
            cast.Rank >= 3 ? AdventureIntel.MonstersAround(cast.State, hero.Owner, hero.Position, AdventureMagic.GraveWhisperRadius) : []));
    }
}
