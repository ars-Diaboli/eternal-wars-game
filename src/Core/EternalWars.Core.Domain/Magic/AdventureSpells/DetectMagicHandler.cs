using EternalWars.Core.Domain.Events;

namespace EternalWars.Core.Domain.Magic.AdventureSpells;

/// <summary>Detect Magic: the magic objects around the hero; rank III also shows creature banks' rewards.</summary>
internal sealed class DetectMagicHandler : AdventureSpellHandler
{
    public override Common.SpellId Spell => AdventureMagic.DetectMagic;

    public override void Cast(AdventureCast cast)
    {
        var hero = cast.Hero;
        var radius = AdventureMagic.DetectMagicRadius(cast.Rank);
        cast.Events.Publish(new MagicDetected(hero.Id, hero.Owner, hero.Position, radius, AdventureIntel.ObjectsAround(cast.State, hero.Owner, hero.Position, radius, bankRewards: cast.Rank >= 3)));
    }
}
