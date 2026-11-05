using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Objects;

/// <summary>
/// An obelisk (ADR-126, ADR-140): the first visit of a player's hero shows that player (and its allies) another share of the puzzle map around the
/// buried Grail. The visits start empty in every game; saves replay them.
/// </summary>
public sealed class Obelisk(MapObjectId id, MapObjectDefinition definition, MapPosition position)
    : MapObject(id, definition, position, MapObjectKind.Obelisk)
{
    private readonly SortedSet<int> _visitedBy = [];

    /// <summary>The players whose heroes have visited it, by id.</summary>
    public IEnumerable<PlayerId> VisitedBy => _visitedBy.Select(p => new PlayerId(p));

    public bool WasVisitedBy(PlayerId player) => _visitedBy.Contains(player.Value);

    /// <summary>Records the player's visit; false when it had visited already.</summary>
    internal bool Visit(PlayerId player) => _visitedBy.Add(player.Value);
}

/// <summary>
/// Where the Grail lies (ADR-140): <see cref="Position"/> is set by the map's author or drawn when the game starts (<see cref="GrailRules"/>); null
/// when the map has none. <see cref="IsFound"/> once a hero has dug it up; <see cref="Holes"/> are the cells dug so far, which stay.
/// </summary>
public sealed class GrailState
{
    private readonly HashSet<MapPosition> _holes = [];

    public MapPosition? Position { get; internal set; }

    public bool IsFound { get; internal set; }

    public IReadOnlyCollection<MapPosition> Holes => _holes;

    public bool HasHole(MapPosition position) => _holes.Contains(position);

    internal void AddHole(MapPosition position) => _holes.Add(position);
}

/// <summary>A piece of the puzzle map: its top-left cell in the puzzle (0 … <see cref="PuzzleMap.Size"/> − 1) and whether the player sees it.</summary>
public sealed record PuzzlePiece(int X, int Y, bool Revealed);

/// <summary>
/// The puzzle map of a player (ADR-140): a square of <see cref="Size"/> cells around the Grail, on the Grail's level, cut into pieces of
/// <see cref="GrailRules.PieceSize"/>; <see cref="Origin"/> is the map cell under its top-left corner (cells off the map show nothing). The
/// player has visited <see cref="Visited"/> of the map's <see cref="Obelisks"/> obelisks (allies' visits count); the outer pieces show first and the
/// middle one, over the Grail, last. <see cref="Grail"/> is given once every obelisk is visited: the spot is marked.
/// </summary>
public sealed record PuzzleMap(MapPosition Origin, int Size, IReadOnlyList<PuzzlePiece> Pieces, int Obelisks, int Visited, MapPosition? Grail)
{
    public int Revealed => Pieces.Count(p => p.Revealed);

    public bool IsComplete => Pieces.All(p => p.Revealed);

    /// <summary>Whether the map cell <paramref name="cell"/> lies on a revealed piece.</summary>
    public bool Shows(MapPosition cell)
    {
        var (dx, dy) = (cell.X - Origin.X, cell.Y - Origin.Y);
        return cell.Level == Origin.Level && dx >= 0 && dy >= 0 && dx < Size && dy < Size
               && Pieces.Any(p => p.Revealed && dx >= p.X && dy >= p.Y && dx < p.X + GrailRules.PieceSize && dy < p.Y + GrailRules.PieceSize);
    }
}

/// <summary>
/// The Grail, the obelisks, the puzzle map and digging (ADR-126, ADR-140, HoMM3):
/// <list type="bullet">
/// <item>The Grail lies on a cell where a hero could dig: passable ground, no road, no object. The author may bury it; otherwise, on a map with at
/// least one obelisk, it is drawn when the game starts, not within <see cref="TownDistance"/> cells of a town when there is such a cell.</item>
/// <item>A hero digs with a whole day's movement (it has not moved today) where it stands; the hole stays and is never dug again. Digging the Grail's
/// cell puts the Grail artifact into its backpack (a full backpack leaves it buried).</item>
/// <item>A hero carrying the Grail into one of its owner's towns without a Grail building raises it at once (not the day's building) and gives the
/// Grail up. The building: ADR-130.</item>
/// </list>
/// </summary>
public static class GrailRules
{
    /// <summary>How far from every town a drawn Grail lies at least (cells, on its level).</summary>
    public const int TownDistance = 10;

    /// <summary>The puzzle map's cells to each side of the Grail: the puzzle is 2 × 7 + 1 = 15 cells wide.</summary>
    public const int PuzzleRadius = 7;

    /// <summary>A puzzle piece's side in cells: 15 / 3 = 5 × 5 = 25 pieces.</summary>
    public const int PieceSize = 3;

    public const int PuzzleSize = (2 * PuzzleRadius) + 1;

    /// <summary>Whether a hero could dig at <paramref name="position"/> (ignoring its movement): passable ground, no road, no object cell, no hole.</summary>
    public static string? CheckGround(GameState state, MapPosition position) =>
        !state.Map.Contains(position) || !state.Terrains.IsPassable(state.Map.GetTerrain(position)) ? "There is nothing to dig here."
        : state.Map.GetRoad(position) != RoadType.None ? "Nobody digs up a road."
        : state.ObjectCellAt(position) is not null ? "Something stands here; dig on open ground."
        : state.Grail.HasHole(position) ? "This hole has been dug already."
        : null;

    /// <summary>Why <paramref name="hero"/> cannot dig where it stands, or null.</summary>
    public static string? CheckDig(GameState state, Hero hero) =>
        hero.MovementPoints < hero.MaxMovementPoints ? $"{hero.Name} needs a whole day's movement to dig." : CheckGround(state, hero.Position);

    /// <summary>Whether the hero carries the Grail.</summary>
    public static bool Carries(GameState state, Hero hero) =>
        state.Artifacts.Grail is { } grail && hero.Artifacts.Has(grail.Id);

    /// <summary>
    /// The puzzle map of <paramref name="player"/>, or null when there is no Grail. Revealed pieces: all of them once every obelisk is visited,
    /// else 25 × visited / obelisks, rounded down, outer ring first.
    /// </summary>
    public static PuzzleMap? Puzzle(GameState state, PlayerId player)
    {
        if (state.Grail.Position is not { } grail)
        {
            return null;
        }

        var obelisks = state.ObjectsOf<Obelisk>().ToList();
        var visited = obelisks.Count(o => o.VisitedBy.Any(p => p == player || state.AreAllies(p, player)));
        var pieces = PuzzleSize / PieceSize;
        var order = Enumerable.Range(0, pieces * pieces)
            .OrderByDescending(i => Math.Max(Math.Abs((i % pieces) - (pieces / 2)), Math.Abs((i / pieces) - (pieces / 2))))
            .ThenBy(i => i)
            .ToList();
        var shown = obelisks.Count == 0 ? 0 : visited >= obelisks.Count ? order.Count : order.Count * visited / obelisks.Count;
        var revealed = order.Take(shown).ToHashSet();
        var list = Enumerable.Range(0, pieces * pieces)
            .Select(i => new PuzzlePiece(i % pieces * PieceSize, i / pieces * PieceSize, revealed.Contains(i)))
            .ToList();
        return new PuzzleMap(grail.Offset(-PuzzleRadius, -PuzzleRadius), PuzzleSize, list, obelisks.Count, visited, shown == order.Count ? grail : null);
    }

    /// <summary>
    /// Buries the Grail when the game starts (from <see cref="GameState.Random"/>) on a map with an obelisk and no Grail set by its author: on a cell
    /// one could dig, away from the towns when such a cell exists. A map without obelisks keeps no Grail unless its author buried one.
    /// </summary>
    internal static void Bury(GameState state)
    {
        if (state.Grail.Position is not null || !state.ObjectsOf<Obelisk>().Any() || state.Artifacts.Grail is null)
        {
            return;
        }

        var cells = Enumerable.Range(0, state.Map.Levels).SelectMany(state.Map.Positions).Where(p => CheckGround(state, p) is null).ToList();
        var towns = state.ObjectsOf<Town>().Select(t => t.VisitPosition).ToList();
        var away = cells.Where(c => towns.All(t => t.Level != c.Level || Math.Max(Math.Abs(t.X - c.X), Math.Abs(t.Y - c.Y)) > TownDistance)).ToList();
        var pool = away.Count > 0 ? away : cells;
        if (pool.Count > 0)
        {
            state.Grail.Position = pool[state.Random.Next(pool.Count)];
        }
    }

    /// <summary>A hero visits an obelisk: its player's first visit shows it more of the puzzle map.</summary>
    internal static void Visit(IEventPublisher events, Hero hero, Obelisk obelisk)
    {
        if (obelisk.Visit(hero.Owner))
        {
            events.Publish(new ObeliskVisited(hero.Id, obelisk.Id, hero.Owner));
        }
        else
        {
            events.Publish(new MapObjectFoundEmpty(hero.Id, obelisk.Id));
        }
    }

    /// <summary>
    /// An own hero in a town without a Grail building raises it with the Grail it carries (ADR-130, ADR-140): the Grail is given up, the building
    /// stands at once and does not count as the town's building of the day.
    /// </summary>
    internal static void RaiseBuilding(GameState state, IEventPublisher events, Hero hero, Town town)
    {
        if (town.Owner != hero.Owner || town.Level(BuiltInBuildings.Grail) > 0 || !TownRules.Buildings(state, town).Contains(BuiltInBuildings.Grail)
            || state.Artifacts.Grail is not { } grail || !hero.Artifacts.Has(grail.Id))
        {
            return;
        }

        if (!hero.Artifacts.RemoveFromBackpack(grail.Id))
        {
            hero.Artifacts.RemoveWorn(grail.Id);
            state.RefreshArtifacts(hero);
        }

        events.Publish(new ArtifactLost(hero.Id, grail.Id));
        events.Publish(new HeroArtifactsChanged(hero.Id));
        town.SetLevel(BuiltInBuildings.Grail, 1);
        events.Publish(new TownBuilt(town.Id, BuiltInBuildings.Grail, 1));
        events.Publish(new GrailBuildingRaised(hero.Id, town.Id, TownRules.Buildings(state, town).Get(BuiltInBuildings.Grail, 1).Name));
    }
}
