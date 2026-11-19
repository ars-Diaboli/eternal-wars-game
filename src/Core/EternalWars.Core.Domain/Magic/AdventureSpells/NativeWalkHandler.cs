using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;

namespace EternalWars.Core.Domain.Magic.AdventureSpells;

/// <summary>
/// Pathfinder's Call and Ashwalk: <see cref="Terrains"/> (every terrain from rank II) are native to the hero's army for the rest of the day; rank III
/// adds <see cref="AdventureMagic.NativeWalkMovementPercent"/> % of the day's movement allowance.
/// </summary>
internal sealed class NativeWalkHandler(Common.SpellId spell, IReadOnlyList<TerrainType> terrains) : AdventureSpellHandler
{
    public override Common.SpellId Spell { get; } = spell;

    /// <summary>The terrains the spell makes native at rank I.</summary>
    public IReadOnlyList<TerrainType> Terrains { get; } = terrains;

    public override void Cast(AdventureCast cast)
    {
        var hero = cast.Hero;
        cast.Mark(new HeroStatus(
            cast.Spell.Value, HeroStatusKind.NativeTerrain, AdventureMagic.TerrainMask(AdventureMagic.NativeTerrains(cast.Spell, cast.Rank)), StatusExpiry.EndOfDay));
        if (cast.Rank >= 3 && hero.MaxMovementPoints * AdventureMagic.NativeWalkMovementPercent / 100 is var extra and > 0)
        {
            hero.MovementPoints += extra;
            cast.Events.Publish(new HeroMovementGained(hero.Id, extra, hero.MovementPoints));
        }
    }
}
