using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;

namespace EternalWars.Core.Domain.Visibility;

/// <summary>What a player last saw of a map object.</summary>
public sealed record KnownObject(MapObjectId Id, MapObjectDefinition Definition, MapPosition Position, PlayerId? Owner)
{
    public static KnownObject Of(MapObject mapObject) =>
        new(mapObject.Id, mapObject.Definition, mapObject.Position, (mapObject as IFlaggable)?.Owner)
        {
            Orientation = (mapObject as Scenery)?.Orientation ?? default,
        };

    /// <summary>How a piece of scenery lay (ADR-252); none for everything else.</summary>
    public Orientation Orientation { get; init; }

    /// <summary>The object's shape as it lay on the map.</summary>
    public Footprint Footprint => Definition.Footprint.Oriented(Orientation);

    public IEnumerable<MapPosition> Cells() =>
        Footprint.OccupiedCells.Select(c => Position.Offset(c.Dx, c.Dy));
}

/// <summary>
/// Per-player knowledge of map objects (ADR-031). An object is re-read whenever any of
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

    /// <summary>A LAN client (ADR-193): what the host says the player last saw of an object.</summary>
    internal void Set(PlayerId player, KnownObject known)
    {
        if (!_known.TryGetValue(player, out var map))
        {
            map = [];
            _known[player] = map;
        }

        map[known.Id] = known;
    }

    /// <summary>A LAN client (ADR-193): the player knows the object is gone.</summary>
    internal void Forget(PlayerId player, MapObjectId id)
    {
        if (_known.TryGetValue(player, out var map))
        {
            map.Remove(id);
        }
    }

    /// <summary>Updates everything <paramref name="player"/> can currently see; returns the ids that changed.</summary>
    internal IReadOnlyList<MapObjectId> Refresh(GameState state, PlayerId player)
    {
        var known = KnownOf(player);
        List<MapObjectId>? changed = null;
        foreach (var mapObject in state.Objects)
        {
            if (IsInSight(state, player, mapObject.Position, mapObject.Footprint))
            {
                Notice(known, mapObject, ref changed);
            }
        }

        ForgetGone(state, player, known, ref changed);
        return changed ?? [];
    }

    /// <summary>
    /// Updates what came into <paramref name="player"/>'s sight on <paramref name="revealed"/> and <paramref name="entered"/> tiles (a hero moved or
    /// looked around); returns the ids that changed. Objects already in sight are not looked at: whatever changes a remembered part of an object
    /// (its owner or definition, its removal) publishes an event that refreshes everything (<see cref="VisibilityService"/>).
    /// </summary>
    internal IReadOnlyList<MapObjectId> Refresh(GameState state, PlayerId player, IReadOnlyList<MapPosition> revealed, IReadOnlyList<MapPosition> entered)
    {
        if (revealed.Count == 0 && entered.Count == 0)
        {
            return [];
        }

        var known = KnownOf(player);
        List<MapObjectId>? changed = null;
        NoticeOn(revealed);
        NoticeOn(entered);
        ForgetGone(state, player, known, ref changed);
        return changed ?? [];

        void NoticeOn(IReadOnlyList<MapPosition> tiles)
        {
            foreach (var tile in tiles)
            {
                if (state.ObjectCellAt(tile) is { Object: var mapObject } && state.Fog.IsInSight(player, tile) &&
                    (changed is null || !changed.Contains(mapObject.Id)))
                {
                    Notice(known, mapObject, ref changed);
                }
            }
        }
    }

    private Dictionary<MapObjectId, KnownObject> KnownOf(PlayerId player)
    {
        if (!_known.TryGetValue(player, out var known))
        {
            known = [];
            _known[player] = known;
        }

        return known;
    }

    /// <summary>Remembers the object as it is now, if that differs from what was remembered (a snapshot is made only then).</summary>
    private static void Notice(Dictionary<MapObjectId, KnownObject> known, MapObject mapObject, ref List<MapObjectId>? changed)
    {
        var owner = (mapObject as IFlaggable)?.Owner;
        var orientation = (mapObject as Scenery)?.Orientation ?? default;
        if (known.TryGetValue(mapObject.Id, out var remembered) && remembered.Position == mapObject.Position && remembered.Owner == owner &&
            remembered.Orientation == orientation && Equals(remembered.Definition, mapObject.Definition))
        {
            return;
        }

        known[mapObject.Id] = KnownObject.Of(mapObject);
        (changed ??= []).Add(mapObject.Id);
    }

    /// <summary>Remembered objects that are gone, noticed only when their place is in sight again.</summary>
    private static void ForgetGone(GameState state, PlayerId player, Dictionary<MapObjectId, KnownObject> known, ref List<MapObjectId>? changed)
    {
        List<MapObjectId>? gone = null;
        foreach (var remembered in known.Values)
        {
            if (state.FindObject(remembered.Id) is null && IsInSight(state, player, remembered.Position, remembered.Footprint))
            {
                (gone ??= []).Add(remembered.Id);
            }
        }

        foreach (var id in gone ?? [])
        {
            known.Remove(id);
            (changed ??= []).Add(id);
        }
    }

    private static bool IsInSight(GameState state, PlayerId player, MapPosition position, Footprint footprint)
    {
        foreach (var (dx, dy, _) in footprint.OccupiedCells)
        {
            if (state.Fog.IsInSight(player, position.Offset(dx, dy)))
            {
                return true;
            }
        }

        return false;
    }
}
