using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;

namespace EternalWars.Core.Domain.Visibility;

/// <summary>What a player last saw of a map object.</summary>
public sealed record KnownObject(MapObjectId Id, MapObjectDefinition Definition, MapPosition Position, PlayerId? Owner)
{
    public static KnownObject Of(MapObject mapObject) =>
        new(mapObject.Id, mapObject.Definition, mapObject.Position, (mapObject as IFlaggable)?.Owner);

    public IEnumerable<MapPosition> Cells() =>
        Definition.Footprint.OccupiedCells.Select(c => Position.Offset(c.Dx, c.Dy));
}

/// <summary>
/// Per-player knowledge of map objects (docs/project/decisions.md ADR-031). An object is re-read whenever any of
/// its cells is in the player's sight; out of sight, the player keeps seeing the last snapshot (old flag
/// colour, a chest someone already took). In <see cref="FogMode.PermanentReveal"/> "in sight" means
/// "explored", so memory simply tracks the live state of every explored object.
/// Views should draw objects from <see cref="Of"/> rather than from <see cref="GameState.Objects"/>.
/// </summary>
public sealed class ObjectMemory
{
    private readonly Dictionary<PlayerId, Dictionary<MapObjectId, KnownObject>> _known = [];

    public IReadOnlyCollection<KnownObject> Of(PlayerId player) =>
        _known.TryGetValue(player, out var known) ? known.Values : [];

    public KnownObject? Get(PlayerId player, MapObjectId id) =>
        _known.TryGetValue(player, out var known) ? known.GetValueOrDefault(id) : null;

    /// <summary>Updates everything <paramref name="player"/> can currently see; returns the ids that changed.</summary>
    internal IReadOnlyList<MapObjectId> Refresh(GameState state, PlayerId player)
    {
        if (!_known.TryGetValue(player, out var known))
        {
            known = [];
            _known[player] = known;
        }

        var changed = new List<MapObjectId>();
        foreach (var mapObject in state.Objects)
        {
            if (!mapObject.OccupiedCells().Any(c => state.Fog.IsInSight(player, c.Position)))
            {
                continue;
            }

            var snapshot = KnownObject.Of(mapObject);
            if (known.GetValueOrDefault(mapObject.Id) != snapshot)
            {
                known[mapObject.Id] = snapshot;
                changed.Add(mapObject.Id);
            }
        }

        // Remembered objects that are gone, noticed only when their place is in sight again.
        foreach (var remembered in known.Values.ToList())
        {
            var stillThere = state.Objects.Any(o => o.Id == remembered.Id);
            if (!stillThere && remembered.Cells().Any(p => state.Fog.IsInSight(player, p)))
            {
                known.Remove(remembered.Id);
                changed.Add(remembered.Id);
            }
        }

        return changed;
    }
}
