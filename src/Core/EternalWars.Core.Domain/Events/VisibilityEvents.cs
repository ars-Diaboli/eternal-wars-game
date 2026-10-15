using EternalWars.Core.Domain.Common;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Events;

/// <summary>Tiles that became explored for <paramref name="Player"/>. Only newly revealed tiles are listed.</summary>
public sealed record TilesRevealed(PlayerId Player, IReadOnlyList<MapPosition> Positions) : IEvent;

/// <summary>
/// <c>ShroudWithRefog</c> only: tiles that came into or dropped out of <paramref name="Player"/>'s sight.
/// Views show or hide other players' units on these tiles.
/// </summary>
public sealed record SightChanged(PlayerId Player, IReadOnlyList<MapPosition> Entered, IReadOnlyList<MapPosition> Left) : IEvent;

/// <summary>
/// <paramref name="Player"/>'s knowledge of these objects changed (first seen, seen changed, or seen gone).
/// Views re-read <c>GameState.Memory.Get(player, id)</c>; a null result means "no longer there".
/// </summary>
public sealed record KnownObjectsChanged(PlayerId Player, IReadOnlyList<MapObjectId> Objects) : IEvent;
