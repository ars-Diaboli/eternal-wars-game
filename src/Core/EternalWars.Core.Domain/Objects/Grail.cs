using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Objects;

/// <summary>
/// An obelisk (ADR-126, ADR-140): the first visit of a player's hero opens that player's next pieces of the puzzle map around the buried Grail
/// (<see cref="GrailService.VisitObelisk"/>); each player has its own puzzle. The visits start empty in every game; saves replay them.
/// </summary>
public sealed class Obelisk(MapObjectId id, MapObjectDefinition definition, MapPosition position)
    : MapObject(id, definition, position, MapObjectKind.Obelisk)
{
    /// <inheritdoc/>
    internal override string DigestState() => $"visited {string.Join(',', VisitedBy.Select(v => v.Value))}";

    private readonly SortedSet<int> _visitedBy = [];

    /// <summary>The players whose heroes have visited it, by id.</summary>
    public IEnumerable<PlayerId> VisitedBy => _visitedBy.Select(p => new PlayerId(p));

    public bool WasVisitedBy(PlayerId player) => _visitedBy.Contains(player.Value);

    /// <summary>Records the player's visit; false when it had visited already.</summary>
    internal bool Visit(PlayerId player) => _visitedBy.Add(player.Value);
}

/// <summary>
/// Where the Grail lies (ADR-140): <see cref="Position"/> is set by the map's author or drawn when the game starts (<see cref="GrailRules"/>); null
/// when the map has none. <see cref="PuzzleOrigin"/> is the map cell under the puzzle's top-left corner, drawn at game start so the Grail falls in
/// the puzzle's centre 4×4 cells. Each player's opened pieces (indices 0 … 99, row by row) are kept per player. <see cref="IsFound"/> once a hero
/// has dug it up; <see cref="Holes"/> are the cells dug so far, which stay.
/// </summary>
public sealed class GrailState
{
    private readonly HashSet<MapPosition> _holes = [];
    private readonly SortedDictionary<int, SortedSet<int>> _pieces = [];

    public MapPosition? Position { get; internal set; }

    public MapPosition? PuzzleOrigin { get; internal set; }

    public bool IsFound { get; internal set; }

    public IReadOnlyCollection<MapPosition> Holes => _holes;

    public bool HasHole(MapPosition position) => _holes.Contains(position);

    /// <summary>The puzzle pieces <paramref name="player"/> has opened, by index (row × 10 + column).</summary>
    public IReadOnlyCollection<int> PiecesOf(PlayerId player) => _pieces.TryGetValue(player.Value, out var set) ? set : [];

    /// <summary>Every player's opened pieces, by player id (for the digest).</summary>
    public IEnumerable<(PlayerId Player, IReadOnlyCollection<int> Pieces)> AllPieces =>
        _pieces.Select(p => (new PlayerId(p.Key), (IReadOnlyCollection<int>)p.Value));

    internal void AddHole(MapPosition position) => _holes.Add(position);

    internal void Open(PlayerId player, IEnumerable<int> pieces)
    {
        if (!_pieces.TryGetValue(player.Value, out var set))
        {
            _pieces[player.Value] = set = [];
        }

        set.UnionWith(pieces);
    }
}

/// <summary>Where a puzzle piece lies: the outer ring, the mid ring (the rest of the middle 6×6 pieces) or the centre (the middle 2×2) (ADR-140).</summary>
public enum PuzzleRing
{
    Outer,
    Mid,
    Centre,
}

/// <summary>A piece of the puzzle map: its top-left cell in the puzzle (0 … <see cref="PuzzleMap.Size"/> − 1) and whether the player sees it.</summary>
public sealed record PuzzlePiece(int X, int Y, bool Revealed);

/// <summary>
/// The puzzle map of a player (ADR-140): <see cref="Size"/> × <see cref="Size"/> cells on the Grail's level cut into pieces of
/// <see cref="GrailRules.PieceSize"/>, row by row; <see cref="Origin"/> is the map cell under its top-left corner. The player has visited
/// <see cref="Visited"/> of the map's <see cref="Obelisks"/> obelisks (its own visits only). <see cref="Grail"/> is given once the centre is open:
/// the spot is marked.
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
               && Pieces[(dy / GrailRules.PieceSize * GrailRules.PiecesPerSide) + (dx / GrailRules.PieceSize)].Revealed;
    }
}

/// <summary>
/// The Grail, the obelisks, the puzzle map and digging (ADR-126, ADR-140, MAP-36, HoMM3):
/// <list type="bullet">
/// <item>The Grail lies on a cell where a hero could dig (passable ground, no road, no object), at least <see cref="EdgeDistance"/> cells from the
/// map's edges, so maps smaller than <see cref="MinMapSize"/> have none. The author may bury it; otherwise, on a map with at least one obelisk, it
/// is drawn when the game starts, not within <see cref="TownDistance"/> cells of a town when there is such a cell.</item>
/// <item>The puzzle map is <see cref="PuzzleSize"/> × <see cref="PuzzleSize"/> cells in 100 pieces of <see cref="PieceSize"/> × <see cref="PieceSize"/>;
/// its place is drawn at game start so the Grail falls in its centre 4×4 cells. It shows the terrain and the objects that stay
/// (<see cref="ShowsOnPuzzle"/>), never their owners.</item>
/// <item>Each player has its own puzzle. The k-th obelisk a player visits (of n on the map) opens <see cref="Share"/> pieces drawn from
/// <see cref="GameState.Random"/>: the 100 pieces split as evenly as can be, the first visits getting the smaller shares. The first n / 2 (rounded
/// down) visits draw from the outer ring, the next ones but the last from the outer and mid rings, the last opens the centre and everything left.</item>
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

    /// <summary>How far the Grail lies from each edge of the map at least (cells between it and the edge).</summary>
    public const int EdgeDistance = 10;

    /// <summary>The smallest map side that can hold a Grail: the edge distance on both sides and the puzzle's 4-cell centre.</summary>
    public const int MinMapSize = (2 * EdgeDistance) + CentreCells;

    /// <summary>The most obelisks a map may hold.</summary>
    public const int MaxObelisks = 25;

    /// <summary>The puzzle map's side in cells.</summary>
    public const int PuzzleSize = 20;

    /// <summary>A puzzle piece's side in cells.</summary>
    public const int PieceSize = 2;

    /// <summary>The puzzle's side in pieces: 10 × 10 = <see cref="PieceCount"/> pieces.</summary>
    public const int PiecesPerSide = PuzzleSize / PieceSize;

    public const int PieceCount = PiecesPerSide * PiecesPerSide;

    /// <summary>The centre's side in cells (2 × 2 pieces), where the Grail lies.</summary>
    internal const int CentreCells = 4;

    /// <summary>The ring of the piece at column <paramref name="x"/>, row <paramref name="y"/> (0 … 9).</summary>
    public static PuzzleRing RingOf(int x, int y)
    {
        var distance = Math.Max(Math.Max(4 - x, x - 5), Math.Max(4 - y, y - 5));
        return distance <= 0 ? PuzzleRing.Centre : distance <= 2 ? PuzzleRing.Mid : PuzzleRing.Outer;
    }

    /// <summary>
    /// How many pieces the <paramref name="visit"/>-th obelisk (1-based) of <paramref name="obelisks"/> opens: the 100 pieces split as evenly as can be,
    /// the larger shares at the end (11 obelisks: ten 9s, then 10).
    /// </summary>
    public static int Share(int obelisks, int visit)
    {
        var (each, rest) = (PieceCount / obelisks, PieceCount % obelisks);
        return each + (visit > obelisks - rest ? 1 : 0);
    }

    /// <summary>Whether the map is large enough for a Grail (<see cref="MinMapSize"/> each way).</summary>
    public static bool HoldsGrail(AdventureMap map) => map.Width >= MinMapSize && map.Height >= MinMapSize;

    /// <summary>Whether <paramref name="position"/> lies <see cref="EdgeDistance"/> cells from every edge.</summary>
    public static bool FarFromEdge(AdventureMap map, MapPosition position) =>
        position.X >= EdgeDistance && position.Y >= EdgeDistance && position.X < map.Width - EdgeDistance && position.Y < map.Height - EdgeDistance;

    /// <summary>
    /// Whether the puzzle map shows the object: everything that stays where it is. Not shown: what is picked up or beaten away (piles, chests,
    /// artifacts, loot, Pandora's Boxes, monsters, prisons, quest guards).
    /// </summary>
    public static bool ShowsOnPuzzle(MapObject mapObject) => mapObject.Kind is not (MapObjectKind.ResourcePile or MapObjectKind.TreasureChest
        or MapObjectKind.Artifact or MapObjectKind.Loot or MapObjectKind.PandorasBox or MapObjectKind.Monster or MapObjectKind.Prison
        or MapObjectKind.QuestGuard);

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
    /// The puzzle map of <paramref name="player"/>, or null when there is no Grail: its opened pieces, and the Grail's cell once the centre is open.
    /// </summary>
    public static PuzzleMap? Puzzle(GameState state, PlayerId player)
    {
        if (state.Grail.Position is not { } grail || state.Grail.PuzzleOrigin is not { } origin)
        {
            return null;
        }

        var obelisks = state.ObjectsOf<Obelisk>().ToList();
        var opened = state.Grail.PiecesOf(player);
        var pieces = Enumerable.Range(0, PieceCount)
            .Select(i => new PuzzlePiece(i % PiecesPerSide * PieceSize, i / PiecesPerSide * PieceSize, opened.Contains(i)))
            .ToList();
        var centreOpen = Enumerable.Range(0, PieceCount).Where(i => RingOf(i % PiecesPerSide, i / PiecesPerSide) == PuzzleRing.Centre).All(opened.Contains);
        return new PuzzleMap(origin, PuzzleSize, pieces, obelisks.Count, obelisks.Count(o => o.WasVisitedBy(player)), centreOpen ? grail : null);
    }
}
