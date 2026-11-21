using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;

namespace EternalWars.Core.Generation;

/// <summary>What the editor's Scatter puts down (EDIT-6).</summary>
/// <param name="Density">How many of the free tiles get scenery, in percent (1 to 100).</param>
/// <param name="Kinds">The kinds of scenery to use (<see cref="SceneryPlacer.KindOf"/>: <c>tree</c>, <c>rock</c>…); null: every kind.</param>
/// <param name="KeepPathsOpen">Whether a piece is left out where it would close a way between the tiles round it, or to an entrance.</param>
public sealed record ScatterOptions(int Density = ScatterOptions.DefaultDensity, IReadOnlySet<string>? Kinds = null, bool KeepPathsOpen = true)
{
    public const int DefaultDensity = 35;
}

/// <summary>
/// The editor's Scatter (EDIT-6, ADR-252): scenery over an area, chosen as the generator chooses it (<see cref="SceneryPlacer"/>,
/// ADR-157): for each tile a kind drawn by its weight on the tile's terrain, then that kind's largest piece that fits.
/// <list type="bullet">
/// <item>A tile takes scenery when it is free: passable ground with no object, hero, road, river or event cell on it. Tiles of a
/// terrain no chosen scenery is made for stay free.</item>
/// <item><see cref="ScatterOptions.Density"/> of the free tiles are drawn to be covered; pieces of several tiles lie where drawn tiles
/// are side by side.</item>
/// <item>With <see cref="ScatterOptions.KeepPathsOpen"/>, a piece is left out where the walkable tiles round one of its cells would no
/// longer be joined to each other without that cell, or an entrance beside it would lose its way in. Whatever could be walked to
/// before can be walked to after.</item>
/// <item>The draws come from the scenario's random numbers, so the same map and seed give the same scatter. One call is one undo
/// step (inside a stroke it joins the stroke), and undoing it puts the random numbers back as well.</item>
/// </list>
/// </summary>
public static class ScatterEditor
{
    /// <summary>The kinds of scenery the catalog has, in name order.</summary>
    public static IReadOnlyList<string> Kinds(MapObjectCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return [.. catalog.OfKind(MapObjectKind.Scenery).Select(SceneryPlacer.KindOf).Distinct().Order(StringComparer.Ordinal)];
    }

    /// <summary>Scatters scenery over the selected tiles. Returns how many pieces were placed.</summary>
    public static int ScatterSelection(MapEditorSession session, ScatterOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        return Scatter(session, [.. session.Selection.Tiles], options);
    }

    /// <summary>Scatters scenery over <paramref name="tiles"/> (those off the map are passed over). Returns how many pieces were placed.</summary>
    public static int Scatter(MapEditorSession session, IEnumerable<MapPosition> tiles, ScatterOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(tiles);
        options ??= new ScatterOptions();
        if (options.Density is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.Density, "The density is 1 to 100 percent.");
        }

        var state = session.Scenario;
        var scenery = session.Catalog.OfKind(MapObjectKind.Scenery)
            .Where(d => options.Kinds is null || options.Kinds.Contains(SceneryPlacer.KindOf(d)))
            .OrderBy(d => d.Id, StringComparer.Ordinal)
            .ToList();
        var events = state.MapEvents.Set.Cells.Select(c => c.Position).ToHashSet();
        var free = tiles.Distinct()
            .Where(t => IsFree(state, t) && !events.Contains(t))
            .OrderBy(t => t.Level).ThenBy(t => t.Y).ThenBy(t => t.X)
            .ToList();
        if (scenery.Count == 0 || free.Count == 0)
        {
            return 0;
        }

        var placed = 0;
        session.AsOneStep("Scatter scenery", random =>
        {
            var wanted = free.Where(_ => random.Next(100) < options.Density).ToHashSet();
            foreach (var tile in free)
            {
                if (!wanted.Contains(tile))
                {
                    continue;
                }

                var terrain = state.Map.GetTerrain(tile);
                var fitting = scenery
                    .Where(d => d.Terrains.Contains(terrain)
                        && SceneryPlacer.Cells(d, SceneryPlacer.Anchor(d, tile)).ToList() is var cells
                        && cells.All(wanted.Contains)
                        && (!options.KeepPathsOpen || KeepsPathsOpen(state, cells)))
                    .ToList();
                wanted.Remove(tile);
                if (fitting.Count == 0)
                {
                    continue;
                }

                var definition = SceneryPlacer.Pick(fitting, terrain, random);
                var topLeft = SceneryPlacer.Anchor(definition, tile);
                if (session.Objects.Place(definition.Id, topLeft, out _) == EditResult.Ok)
                {
                    wanted.ExceptWith(SceneryPlacer.Cells(definition, topLeft));
                    placed++;
                }
            }
        });
        return placed;
    }

    private static bool IsFree(GameState state, MapPosition tile) =>
        state.Map.Contains(tile)
        && state.Terrains.IsPassable(state.Map.GetTerrain(tile))
        && state.ObjectCellAt(tile) is null
        && state.HeroAt(tile) is null
        && state.Map.GetRoad(tile) == RoadType.None
        && state.Map.GetRiver(tile) == RiverType.None;

    /// <summary>Whether blocking <paramref name="cells"/>, one after the other, closes no way (see <see cref="StaysOpen"/>).</summary>
    private static bool KeepsPathsOpen(GameState state, List<MapPosition> cells)
    {
        var blocked = new HashSet<MapPosition>();
        foreach (var cell in cells)
        {
            if (!StaysOpen(state, cell, blocked))
            {
                return false;
            }

            blocked.Add(cell);
        }

        return true;
    }

    /// <summary>
    /// Whether everything round <paramref name="cell"/> stays joined when it is blocked: the open ground among its eight neighbours is
    /// one piece without it (steps go to all eight sides), and every entrance beside it still has open ground of that piece beside it.
    /// A way through the cell then has a way round it, so nothing that could be reached is cut off.
    /// </summary>
    private static bool StaysOpen(GameState state, MapPosition cell, HashSet<MapPosition> blocked)
    {
        var ground = new List<MapPosition>();
        var entrances = new List<MapPosition>();
        foreach (var next in DirectionExtensions.All.Select(cell.Step))
        {
            if (!state.Map.Contains(next) || blocked.Contains(next) || !state.Terrains.IsPassable(state.Map.GetTerrain(next)))
            {
                continue;
            }

            switch (state.ObjectCellAt(next))
            {
                case null:
                    ground.Add(next);
                    break;
                case { Cell: FootprintCell.Visit }:
                    entrances.Add(next);
                    break;
            }
        }

        if (ground.Count == 0)
        {
            return entrances.Count == 0; // an entrance would lose the only tile it is entered from here
        }

        var reached = new HashSet<MapPosition> { ground[0] };
        var open = new Queue<MapPosition>([ground[0]]);
        while (open.TryDequeue(out var at))
        {
            foreach (var other in ground.Where(g => IsBeside(g, at) && reached.Add(g)))
            {
                open.Enqueue(other);
            }
        }

        return reached.Count == ground.Count && entrances.All(e => ground.Any(g => IsBeside(g, e)));
    }

    private static bool IsBeside(MapPosition a, MapPosition b) => a != b && Math.Abs(a.X - b.X) <= 1 && Math.Abs(a.Y - b.Y) <= 1;
}
