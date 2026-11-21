using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Players;

namespace EternalWars.Core.Domain.Analysis;

/// <summary>The map editor's overlays (ADR-264), one shown at a time.</summary>
public enum OverlayKind
{
    /// <summary>Blocked cells, object entrances and the cells around a monster.</summary>
    Passability,

    /// <summary>Every owned or ownable thing in its owner's colour.</summary>
    Owners,

    /// <summary>The army power guarding every cell, as a heat map.</summary>
    GuardStrength,

    /// <summary>The days from a chosen start to every tile.</summary>
    Reach,

    /// <summary>The zones of a generated map, else the start zones.</summary>
    Zones,

    /// <summary>Contour lines of the ground's height.</summary>
    Height,
}

/// <summary>What a band of an overlay means for its colour; the views pick the colour.</summary>
public enum TintKind
{
    Blocked,
    Entrance,
    Guarded,

    /// <summary>A player's colour (<see cref="OverlayTint.Value"/> is the colour index).</summary>
    Player,

    /// <summary>Owned by no one.</summary>
    Neutral,

    /// <summary>A heat from 0 (weakest) to 100 (strongest).</summary>
    Heat,

    /// <summary>A day of a route (<see cref="OverlayTint.Value"/> is the day, 1 … ).</summary>
    Day,

    /// <summary>One of several zones (<see cref="OverlayTint.Value"/> is the zone's index).</summary>
    Zone,

    /// <summary>A contour line (<see cref="OverlayTint.Value"/>: 1 for a whole cell of height, 0 between).</summary>
    Contour,
}

/// <summary>The colour meaning of an overlay band.</summary>
public readonly record struct OverlayTint(TintKind Kind, int Value = 0);

/// <summary>A line of an overlay's legend: what its colour means.</summary>
public sealed record OverlayBand(string Text, OverlayTint Tint);

/// <summary>A tinted tile; <see cref="Band"/> is an index into the overlay's legend.</summary>
public readonly record struct OverlayCell(MapPosition Tile, int Band);

/// <summary>
/// A line of an overlay in corner coordinates: corner (cx, cy) is the top-left corner of tile (cx, cy), so tile (x, y)'s centre is at
/// (x + 0.5, y + 0.5). <see cref="Band"/> is an index into the legend.
/// </summary>
public readonly record struct OverlayLine(float X1, float Y1, float X2, float Y2, int Band);

/// <summary>
/// One overlay of one level (ADR-264): the tinted tiles or the lines, the legend that says what each band means, and a note the legend shows
/// under its lines (empty: none).
/// </summary>
public sealed record MapOverlay(
    OverlayKind Kind, int Level, IReadOnlyList<OverlayBand> Legend, IReadOnlyList<OverlayCell> Cells, IReadOnlyList<OverlayLine> Lines, string Note);

/// <summary>
/// The zones of a generated map (read from its description by the generator, ADR-264): each zone's name and which zone a tile is in (null:
/// none), and where they come from.
/// </summary>
public sealed record ZoneMap(IReadOnlyList<string> Names, Func<MapPosition, int?> ZoneAt, string Source);

/// <summary>
/// The overlays' numbers (ADR-264), computed over a <see cref="GameState"/> for one level, for the editor to draw: passability as the game's
/// walking sees it, owners, guard strength and reach from <see cref="MapAnalysis"/>, zones, and the ground's contour lines.
/// </summary>
public static class MapOverlays
{
    /// <summary>The days the reach overlay tells apart; a farther tile is in one band "more".</summary>
    public const int ReachDays = MapAnalysis.FarDays;

    /// <summary>How many heat bands the guard strength overlay has.</summary>
    public const int GuardBands = 5;

    /// <summary>The height between two contour lines, in eighths of a cell (half a cell).</summary>
    public const int ContourStep = ReliefLimits.StepsPerCell / 2;

    /// <summary>
    /// Passability: an object's entrance (where a hero visits it), a blocked cell (impassable terrain or an object's blocked cell), and a cell a
    /// monster guards (a hero stepping on it is attacked). A free cell is left untinted.
    /// </summary>
    public static MapOverlay Passability(GameState state, int level)
    {
        ArgumentNullException.ThrowIfNull(state);
        var cells = new List<OverlayCell>();
        foreach (var tile in state.Map.Positions(level))
        {
            int? band = state.ObjectCellAt(tile)?.Cell switch
            {
                FootprintCell.Visit => 0,
                FootprintCell.Blocked => 1,
                _ when !state.Terrains.IsPassable(state.Map.GetTerrain(tile)) => 1,
                _ when state.IsGuarded(tile) => 2,
                _ => null,
            };
            if (band is { } b)
            {
                cells.Add(new OverlayCell(tile, b));
            }
        }

        return new MapOverlay(OverlayKind.Passability, level,
            [new("Entrance", new(TintKind.Entrance)), new("Blocked", new(TintKind.Blocked)), new("Guarded by a monster", new(TintKind.Guarded))],
            cells, [], "");
    }

    /// <summary>
    /// Owners: the cells of every thing that can have an owner (towns, mines, dwellings, providers, garrisons…) and every hero, in its owner's
    /// colour; an ownable thing without one as neutral (a hero on an object's cell tints it in its own colour). The legend lists the players
    /// that own something, then neutral.
    /// </summary>
    public static MapOverlay Owners(GameState state, int level)
    {
        ArgumentNullException.ThrowIfNull(state);
        var heroes = state.Heroes.Where(h => h.Position.Level == level && state.Map.Contains(h.Position)).ToList();
        var heroTiles = heroes.Select(h => h.Position).ToHashSet();
        var tinted = new List<(MapPosition Tile, PlayerId? Owner)>();
        foreach (var mapObject in state.Objects.OfType<IFlaggable>().Cast<MapObject>())
        {
            tinted.AddRange(mapObject.OccupiedCells().Where(c => c.Position.Level == level && state.Map.Contains(c.Position) && !heroTiles.Contains(c.Position))
                .Select(c => (c.Position, ((IFlaggable)mapObject).Owner)));
        }

        tinted.AddRange(heroes.Select(h => (h.Position, (PlayerId?)h.Owner)));

        var owners = tinted.Where(t => t.Owner is not null).Select(t => t.Owner!.Value).Distinct().OrderBy(p => p.Value).ToList();
        var legend = owners.Select(p => new OverlayBand(Name(state, p), new(TintKind.Player, Colour(state, p)))).ToList();
        legend.Add(new OverlayBand("Neutral", new(TintKind.Neutral)));
        var cells = tinted.Select(t => new OverlayCell(t.Tile, t.Owner is { } owner ? owners.IndexOf(owner) : owners.Count)).ToList();
        return new MapOverlay(OverlayKind.Owners, level, legend, cells, [], tinted.Count == 0 ? "Nothing on this level can have an owner." : "");
    }

    /// <summary>
    /// Guard strength: the army power of the monsters guarding every cell (<see cref="MapAnalysis.GuardStrength"/>), in <see cref="GuardBands"/>
    /// heat bands that split the level's weakest to strongest guard evenly on a log scale (one band when they are all alike); the legend gives
    /// each band's powers.
    /// </summary>
    public static MapOverlay GuardStrength(GameState state, int level)
    {
        ArgumentNullException.ThrowIfNull(state);
        var analysis = new MapAnalysis(state);
        var guarded = state.Map.Positions(level).Select(t => (Tile: t, Power: analysis.GuardStrength(t))).Where(g => g.Power > 0).ToList();
        if (guarded.Count == 0)
        {
            return new MapOverlay(OverlayKind.GuardStrength, level, [], [], [], "No monster guards this level.");
        }

        const string note = "Army power of the monsters around a cell: count × fight value; an open count as the middle of its range.";
        var (weakest, strongest) = (guarded.Min(g => g.Power), guarded.Max(g => g.Power));
        if (weakest == strongest)
        {
            return new MapOverlay(OverlayKind.GuardStrength, level, [new($"Power {weakest:N0}", new(TintKind.Heat, 100))],
                [.. guarded.Select(g => new OverlayCell(g.Tile, 0))], [], note);
        }

        var (low, high) = (Math.Log(weakest), Math.Log(strongest));
        int Band(long power) => Math.Min(GuardBands - 1, (int)((Math.Log(power) - low) / (high - low) * GuardBands));
        long Edge(int band) => (long)Math.Round(Math.Exp(low + ((high - low) * band / GuardBands)));

        var legend = new List<OverlayBand>();
        for (var band = 0; band < GuardBands; band++)
        {
            legend.Add(new OverlayBand($"Power {Edge(band):N0} – {Edge(band + 1):N0}", new(TintKind.Heat, band * 100 / (GuardBands - 1))));
        }

        return new MapOverlay(OverlayKind.GuardStrength, level, legend, [.. guarded.Select(g => new OverlayCell(g.Tile, Band(g.Power)))], [], note);
    }

    /// <summary>
    /// Reach: the days a scout of <paramref name="start"/>'s player needs from it to every tile of the level (<see cref="MapAnalysis.Reach"/>),
    /// one band per day up to <see cref="ReachDays"/>, then one for more; a tile no route reaches is left untinted. The start's own day is day 1.
    /// </summary>
    public static MapOverlay Reach(GameState state, MapStart start, int level)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(start);
        var reach = new MapAnalysis(state).Reach(start.Tile, start.Player, start.Town);
        var cells = new List<OverlayCell>();
        foreach (var tile in state.Map.Positions(level))
        {
            if (reach.DaysTo(tile) is { } days)
            {
                cells.Add(new OverlayCell(tile, Math.Clamp(days, 1, ReachDays + 1) - 1));
            }
        }

        var legend = Enumerable.Range(1, ReachDays).Select(d => new OverlayBand(d == 1 ? "1 day" : $"{d} days", new(TintKind.Day, d))).ToList();
        legend.Add(new OverlayBand($"More than {ReachDays} days", new(TintKind.Day, ReachDays + 1)));
        var from = start.Tile.Level == level ? "" : $" The start is on the other level.";
        return new MapOverlay(OverlayKind.Reach, level, legend, cells, [],
            $"From {Name(state, start.Player)}'s start at {start.Tile.X}, {start.Tile.Y}, past guards and pickups.{from}");
    }

    /// <summary>
    /// Zones: the zones of a generated map when <paramref name="zones"/> gives them (each its own band, named as in its template), else the
    /// start zones painted on the map, in their players' colours. <paramref name="note"/> says where the zones come from or why the generated
    /// ones are not shown.
    /// </summary>
    public static MapOverlay Zones(GameState state, int level, ZoneMap? zones, string note = "")
    {
        ArgumentNullException.ThrowIfNull(state);
        if (zones is not null)
        {
            var cells = state.Map.Positions(level).Select(t => (Tile: t, Zone: zones.ZoneAt(t))).Where(z => z.Zone is not null)
                .Select(z => new OverlayCell(z.Tile, z.Zone!.Value)).ToList();
            return new MapOverlay(OverlayKind.Zones, level, [.. zones.Names.Select((n, i) => new OverlayBand(n, new(TintKind.Zone, i)))], cells, [],
                note.Length > 0 ? note : zones.Source);
        }

        var owners = state.Map.StartZoneOwners.OrderBy(p => p.Value).ToList();
        var startCells = state.Map.Positions(level).Where(t => state.Map.GetStartZone(t) is not null)
            .Select(t => new OverlayCell(t, owners.IndexOf(state.Map.GetStartZone(t)!.Value))).ToList();
        var legend = owners.Select(p => new OverlayBand($"Start zone of {Name(state, p)}", new(TintKind.Player, Colour(state, p)))).ToList();
        var none = owners.Count == 0 ? "No start zone is painted. " : "";
        return new MapOverlay(OverlayKind.Zones, level, legend, startCells, [], (none + note).Trim());
    }

    /// <summary>
    /// Height: contour lines of the ground every <see cref="ContourStep"/> eighths (a thicker band at each whole cell), traced over the tile
    /// corners' heights (marching squares).
    /// </summary>
    public static MapOverlay Height(GameState state, int level)
    {
        ArgumentNullException.ThrowIfNull(state);
        var map = state.Map;
        var (low, high) = (int.MaxValue, int.MinValue);
        for (var cy = 0; cy <= map.Height; cy++)
        {
            for (var cx = 0; cx <= map.Width; cx++)
            {
                var h = map.GetCorner(level, cx, cy);
                (low, high) = (Math.Min(low, h), Math.Max(high, h));
            }
        }

        string Cells(int eighths) => $"{eighths / (double)ReliefLimits.StepsPerCell:0.##}";
        OverlayBand[] legend =
        [
            new($"Every {Cells(ContourStep)} cell of height", new(TintKind.Contour, 0)),
            new("Every whole cell", new(TintKind.Contour, 1)),
        ];
        var lines = new List<OverlayLine>();
        // A line where some corner is below the height and some at or above it: the steps above the lowest corner, up to the highest.
        for (var height = ((int)Math.Floor(low / (double)ContourStep) * ContourStep) + ContourStep; height <= high; height += ContourStep)
        {
            Trace(map, level, height, height % ReliefLimits.StepsPerCell == 0 ? 1 : 0, lines);
        }

        var note = low == high ? $"The ground is flat at {Cells(low)} cells." : $"From {Cells(low)} to {Cells(high)} cells high.";
        return new MapOverlay(OverlayKind.Height, level, legend, [], lines, note);
    }

    /// <summary>The lines where the ground crosses <paramref name="height"/> (a corner at or above it is above), one tile square at a time.</summary>
    private static void Trace(AdventureMap map, int level, int height, int band, List<OverlayLine> lines)
    {
        var points = new (float X, float Y)[4];
        var count = 0;
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                // The square's corners clockwise from the top left, and its edges between them; a saddle (four crossings) pairs them in edge order.
                var (a, b, c, d) = (map.GetCorner(level, x, y), map.GetCorner(level, x + 1, y), map.GetCorner(level, x + 1, y + 1), map.GetCorner(level, x, y + 1));
                count = 0;
                Cross(a, b, x, y, x + 1, y);
                Cross(b, c, x + 1, y, x + 1, y + 1);
                Cross(c, d, x + 1, y + 1, x, y + 1);
                Cross(d, a, x, y + 1, x, y);
                for (var i = 0; i + 1 < count; i += 2)
                {
                    if (points[i] == points[i + 1])
                    {
                        continue; // a peak exactly at the height: both crossings on its corner
                    }

                    lines.Add(new OverlayLine(points[i].X, points[i].Y, points[i + 1].X, points[i + 1].Y, band));
                }
            }
        }

        void Cross(int from, int to, float x1, float y1, float x2, float y2)
        {
            if ((from >= height) == (to >= height))
            {
                return;
            }

            var t = (height - from) / (float)(to - from);
            points[count++] = (x1 + ((x2 - x1) * t), y1 + ((y2 - y1) * t));
        }
    }

    private static string Name(GameState state, PlayerId player) =>
        state.HasPlayer(player) ? state.GetPlayer(player).Name : $"Player {player.Value + 1}";

    private static int Colour(GameState state, PlayerId player) => state.HasPlayer(player) ? state.GetPlayer(player).Colour : player.Value;
}
