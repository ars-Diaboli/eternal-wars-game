using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Map;

namespace EternalWars.Core.Domain.Visibility;

public enum MinimapMarker
{
    None,
    Object,
    Hero,
}

/// <summary>What one tile looks like on the minimap. Colours are chosen by the front-end.</summary>
public readonly record struct MinimapCell(bool Explored, TerrainType Terrain, MinimapMarker Marker, PlayerId? Owner);

/// <summary>A small picture drawn over the minimap's cells (ADR-309): a town or a hero.</summary>
public enum MinimapPictogramKind
{
    Town,
    Hero,
}

/// <summary>A pictogram's place, the middle of what it stands for in tiles (a tile's centre at its whole numbers), its kind and owner.</summary>
public readonly record struct MinimapPictogram(double X, double Y, MinimapPictogramKind Kind, PlayerId? Owner);

public static class Minimap
{
    /// <summary>
    /// Builds a row-major (<c>y * width + x</c>) snapshot of one level as seen by <paramref name="viewer"/>
    /// (null = omniscient, e.g. the editor). Objects come from the viewer's <see cref="ObjectMemory"/>,
    /// so under refog they show their last known state.
    /// </summary>
    public static MinimapCell[] Build(GameState state, PlayerId? viewer, int level)
    {
        var map = state.Map;
        var cells = new MinimapCell[map.Width * map.Height];
        foreach (var position in map.Positions(level))
        {
            var explored = viewer is not { } player || state.Fog.IsExplored(player, position);
            cells[(position.Y * map.Width) + position.X] = new MinimapCell(explored, map.GetTerrain(position), MinimapMarker.None, null);
        }

        var objects = viewer is { } who ? state.Memory.Of(who) : state.Objects.Select(KnownObject.Of);
        foreach (var known in objects)
        {
            foreach (var position in known.Cells().Where(p => p.Level == level))
            {
                var index = (position.Y * map.Width) + position.X;
                cells[index] = cells[index] with { Marker = MinimapMarker.Object, Owner = known.Owner };
            }
        }

        foreach (var hero in state.Heroes.Where(h => h.Position.Level == level))
        {
            var index = (hero.Position.Y * map.Width) + hero.Position.X;
            if (viewer is not { } player || state.CanSee(player, hero))
            {
                cells[index] = cells[index] with { Marker = MinimapMarker.Hero, Owner = hero.Owner };
            }
        }

        return cells;
    }

    /// <summary>
    /// The towns and heroes on one level as <paramref name="viewer"/> knows them (null = omniscient), as <see cref="Build"/> marks their cells:
    /// a town from the viewer's memory, in the middle of its cells; a hero the viewer sees, on its cell. Towns first, so heroes are drawn over them.
    /// </summary>
    public static IReadOnlyList<MinimapPictogram> Pictograms(GameState state, PlayerId? viewer, int level)
    {
        ArgumentNullException.ThrowIfNull(state);
        var objects = viewer is { } who ? state.Memory.Of(who) : state.Objects.Select(KnownObject.Of);
        var pictograms = new List<MinimapPictogram>();
        foreach (var town in objects.Where(o => o.Definition.Kind == Objects.MapObjectKind.Town && o.Position.Level == level))
        {
            var cells = town.Cells().ToList();
            pictograms.Add(new MinimapPictogram(cells.Average(c => c.X), cells.Average(c => c.Y), MinimapPictogramKind.Town, town.Owner));
        }

        foreach (var hero in state.Heroes.Where(h => h.Position.Level == level && (viewer is not { } player || state.CanSee(player, h))))
        {
            pictograms.Add(new MinimapPictogram(hero.Position.X, hero.Position.Y, MinimapPictogramKind.Hero, hero.Owner));
        }

        return pictograms;
    }
}
