using EternalWars.Core.Domain.Events;

namespace EternalWars.Core.Domain.Magic.AdventureSpells;

/// <summary>View Terrain: the area around the hero is explored for good (ADR-120).</summary>
internal sealed class ViewTerrainHandler : AdventureSpellHandler
{
    public override Common.SpellId Spell => AdventureMagic.ViewTerrain;

    public override void Cast(AdventureCast cast)
    {
        var hero = cast.Hero;
        cast.Fog.RevealArea(hero.Owner, hero.Position, AdventureMagic.ViewTerrainRadius(cast.Rank));
        cast.Events.Publish(new AreaRevealed(hero.Owner, hero.Position, AdventureMagic.ViewTerrainRadius(cast.Rank)));
    }
}
