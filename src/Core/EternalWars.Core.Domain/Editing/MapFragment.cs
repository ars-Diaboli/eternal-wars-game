using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;

namespace EternalWars.Core.Domain.Editing;

/// <summary>What of a <see cref="MapFragment"/> a paste puts on the map.</summary>
[Flags]
public enum FragmentLayers
{
    None = 0,
    Terrain = 1,
    Heights = 2,
    Roads = 4,
    Rivers = 8,
    Objects = 16,
    All = Terrain | Heights | Roads | Rivers | Objects,
}

/// <summary>One tile of a fragment, <see cref="X"/> and <see cref="Y"/> counted from the fragment's top-left tile.</summary>
public readonly record struct FragmentTile(int X, int Y, TerrainType Terrain, RoadType Road, RiverType River);

/// <summary>One ground corner of a fragment and its height in eighths; corner (0, 0) is the top-left corner of the top-left tile.</summary>
public readonly record struct FragmentCorner(int X, int Y, int Height);

/// <summary>
/// Makes a copy of a map object with all its settings under another id at another place. The map format knows every kind's settings, so
/// the data layer implements this for the editor session (the core's rules do not copy objects).
/// </summary>
public interface IMapObjectCopier
{
    /// <summary>
    /// A copy of <paramref name="source"/> for the map <paramref name="target"/>. Links to other objects come along as they are (the
    /// caller remaps them). Throws <see cref="FormatException"/> when <paramref name="target"/>'s content does not have what the object uses.
    /// </summary>
    MapObject Copy(MapObject source, MapObjectId id, MapPosition position, GameState target);
}

/// <summary>
/// A piece of a map taken out of it (EDIT-4, ADR-250): the copied tiles' terrain, roads and rivers, the heights of their corners, and
/// objects with their settings. It belongs to no map, so it can be pasted into another one. Places are counted from the top-left tile of
/// the box round everything in it, on level 0. The objects keep the ids they had; a gate's link stays when its other end is in the
/// fragment too and is dropped otherwise. Heroes, start zones and event cells are not part of a fragment.
/// </summary>
public sealed class MapFragment
{
    public MapFragment(
        int width, int height, IReadOnlyList<FragmentTile> tiles, IReadOnlyList<FragmentCorner> corners, IReadOnlyList<MapObject> objects)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        (Width, Height, Tiles, Corners, Objects) = (width, height, tiles, corners, objects);
    }

    /// <summary>The box round the fragment's tiles and objects, in tiles.</summary>
    public int Width { get; }

    /// <inheritdoc cref="Width"/>
    public int Height { get; }

    /// <summary>The copied tiles; not every tile of the box need be among them.</summary>
    public IReadOnlyList<FragmentTile> Tiles { get; }

    /// <summary>The heights of the copied tiles' corners.</summary>
    public IReadOnlyList<FragmentCorner> Corners { get; }

    /// <summary>The copied objects, never on a map themselves: a paste copies them again.</summary>
    public IReadOnlyList<MapObject> Objects { get; }
}
