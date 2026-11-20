using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;

namespace EternalWars.Core.Data.Maps;

/// <summary>
/// The editor session's object copier (EDIT-4, ADR-250): an object goes through the map format's own codec (<see cref="MapConverter"/>),
/// so a copy has exactly what a saved and reopened object has.
/// </summary>
public sealed class MapObjectCopier : IMapObjectCopier
{
    public static MapObjectCopier Instance { get; } = new();

    public MapObject Copy(MapObject source, MapObjectId id, MapPosition position, GameState target) => MapConverter.Copy(source, id, position, target);
}
