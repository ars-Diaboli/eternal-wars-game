using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;

namespace EternalWars.Core.Domain.Magic.AdventureSpells;

/// <summary>Distant Vision: the armies around a cell in sight; rank III also sees under the fog.</summary>
internal sealed class DistantVisionHandler : AdventureSpellHandler
{
    public override Common.SpellId Spell => AdventureMagic.DistantVision;

    public override bool NeedsTarget(GameState state, Hero hero) => true;

    public override string? Refusal(GameState state, Hero hero, int rank, MapPosition? target, bool aimed) =>
        !aimed ? null
        : target is { } cell && state.Map.Contains(cell) && state.Fog.IsInSight(hero.Owner, cell) ? null : "Distant Vision looks at a cell in sight.";

    public override string Aim(int rank) => "Distant Vision: click a cell in sight.";

    public override void Cast(AdventureCast cast)
    {
        var hero = cast.Hero;
        var center = cast.Target!.Value;
        var radius = AdventureMagic.DistantVisionRadius(cast.Rank);
        cast.Events.Publish(new ArmiesSighted(hero.Id, hero.Owner, center, radius, AdventureIntel.ArmiesAround(cast.State, hero.Owner, center, radius, underFog: cast.Rank >= 3)));
    }
}
