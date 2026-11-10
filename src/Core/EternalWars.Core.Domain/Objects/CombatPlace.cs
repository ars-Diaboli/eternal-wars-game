using EternalWars.Core.Domain.Common;

namespace EternalWars.Core.Domain.Objects;

/// <summary>
/// A combat place of an arena map (ADR-097, ADR-100): the battle of day 2 is fought here. The defender stands on the entrance
/// (<see cref="MapObject.VisitPosition"/>), the attacker on the <see cref="AttackCell"/>, which is blocked so that no hero reaches it on day 1.
/// A hero standing on the entrance on day 2 makes its owner the defender (<see cref="Game.ArenaRules.Plan"/>); visiting it does nothing else, so
/// routes go through it.
/// </summary>
public sealed class CombatPlace(MapObjectId id, MapObjectDefinition definition, MapPosition position)
    : MapObject(id, definition, position, MapObjectKind.CombatPlace)
{
    /// <inheritdoc/>
    internal override string DigestState() => "";

    public override bool EndsRoute => false;

    /// <summary>Where the attacker is put on day 2: the footprint's first blocked cell.</summary>
    public MapPosition AttackCell => Definition.Footprint.OccupiedCells.FirstOrDefault(c => c.Cell == FootprintCell.Blocked) is var (dx, dy, cell) && cell == FootprintCell.Blocked
        ? Position.Offset(dx, dy)
        : VisitPosition;
}
