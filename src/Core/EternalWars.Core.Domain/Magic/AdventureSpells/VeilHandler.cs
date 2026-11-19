using EternalWars.Core.Domain.Heroes;

namespace EternalWars.Core.Domain.Magic.AdventureSpells;

/// <summary>Veil: the hero is veiled at its rank for the week (rank III hides it on the map, <see cref="AdventureMagic.HidesFrom"/>).</summary>
internal sealed class VeilHandler : AdventureSpellHandler
{
    public override Common.SpellId Spell => AdventureMagic.Veil;

    public override void Cast(AdventureCast cast) => cast.Mark(new HeroStatus(cast.Spell.Value, HeroStatusKind.Veil, cast.Rank, StatusExpiry.EndOfWeek));
}
