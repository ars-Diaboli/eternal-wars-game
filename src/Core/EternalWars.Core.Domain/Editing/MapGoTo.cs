using System.Globalization;
using System.Text.RegularExpressions;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Map;

namespace EternalWars.Core.Domain.Editing;

/// <summary>
/// The editor's Go to (Ctrl+G, ADR-261): what fits a typed text, as links the editor shows and selects (<see cref="MapLink"/>). Two numbers
/// ("12, 7" or "12 7"), and a third for the level, are a tile of the map; the shown level when no third is typed. Any text is also searched
/// among the names of the map's heroes, named objects (a town always has a name) and map events with a cell, ranked as the command palette
/// ranks commands (<see cref="CommandSearch"/>); an empty text lists them all. "#12" is the object and the hero whose id is 12, named or
/// not (the ids the inspector and the problems show). The tile or the id comes first.
/// </summary>
public static partial class MapGoTo
{
    /// <summary>The most it offers.</summary>
    public const int MaxResults = 50;

    /// <summary>What fits <paramref name="query"/> in <paramref name="state"/>, the best first; <paramref name="level"/> is the shown level.</summary>
    public static IReadOnlyList<MapLink> Find(GameState state, string query, int level)
    {
        ArgumentNullException.ThrowIfNull(state);
        query ??= "";
        var results = new List<MapLink>();
        if (Tile(state.Map, query, level) is { } tile)
        {
            results.Add(new MapLink($"Tile {Where(state.Map, tile)}", tile));
        }

        results.AddRange(ById(state, query));

        var named = Named(state);
        results.AddRange(CommandSearch.Find(query.Trim(), [.. named.Select(n => n.Name)]).Select(i => named[i].Link));
        return [.. results.Take(MaxResults)];
    }

    /// <summary>
    /// The tile <paramref name="query"/> names: "x, y" or "x y" on <paramref name="level"/>, or "x, y, level"; null when it is not two or
    /// three numbers or the tile is off the map.
    /// </summary>
    public static MapPosition? Tile(AdventureMap map, string query, int level)
    {
        ArgumentNullException.ThrowIfNull(map);
        var match = Coordinates().Match(query ?? "");
        if (!match.Success)
        {
            return null;
        }

        int Number(int group) => int.Parse(match.Groups[group].Value, CultureInfo.InvariantCulture);
        var tile = new MapPosition(Number(1), Number(2), match.Groups[3].Success ? Number(3) : level);
        return map.Contains(tile) ? tile : null;
    }

    /// <summary>The object and the hero whose id "#n" names; none for any other text.</summary>
    private static IEnumerable<MapLink> ById(GameState state, string query)
    {
        var text = query.Trim();
        if (text.Length < 2 || text[0] != '#' || !int.TryParse(text[1..], NumberStyles.None, CultureInfo.InvariantCulture, out var id))
        {
            yield break;
        }

        var map = state.Map;
        if (state.Objects.FirstOrDefault(o => o.Id.Value == id) is { } found)
        {
            yield return new MapLink($"#{id}: {found.Name ?? found.Definition.Name} at {Where(map, found.VisitPosition)}", found.VisitPosition, found.Id);
        }

        if (state.Heroes.FirstOrDefault(h => h.Id.Value == id) is { } hero)
        {
            yield return new MapLink($"#{id}: {hero.Name}, hero at {Where(map, hero.Position)}", hero.Position, Hero: hero.Id);
        }
    }

    /// <summary>The heroes, the named objects and the events with a cell, with their links, in the map's order.</summary>
    private static List<(string Name, MapLink Link)> Named(GameState state)
    {
        var map = state.Map;
        var named = new List<(string Name, MapLink Link)>();
        named.AddRange(state.Heroes.Select(h => (h.Name, new MapLink($"{h.Name}: hero at {Where(map, h.Position)}", h.Position, Hero: h.Id))));
        named.AddRange(state.Objects
            .Where(o => o.Name is not null)
            .Select(o => (o.Name!, new MapLink($"{o.Name}: {o.Definition.Name} at {Where(map, o.VisitPosition)}", o.VisitPosition, o.Id))));
        var cells = state.MapEvents.Set.Cells;
        foreach (var definition in state.MapEvents.Set.Events)
        {
            if (cells.FirstOrDefault(c => c.Event == definition.Name) is { } cell)
            {
                named.Add((definition.Name, new MapLink($"{definition.Name}: event at {Where(map, cell.Position)}", cell.Position)));
            }
        }

        return named;
    }

    /// <summary>"12, 7", and "underground" on a map's second level.</summary>
    private static string Where(AdventureMap map, MapPosition position) =>
        $"{position.X}, {position.Y}{(map.Levels > 1 ? position.Level == 0 ? " on the surface" : " underground" : "")}";

    // ASCII digits only: \d also takes other scripts' digits, which int.Parse refuses.
    [GeneratedRegex(@"^\s*([0-9]{1,3})\s*[,\s]\s*([0-9]{1,3})(?:\s*[,\s]\s*([0-9]))?\s*$")]
    private static partial Regex Coordinates();
}
