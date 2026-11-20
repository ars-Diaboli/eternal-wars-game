using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.MapEvents;
using EternalWars.Core.Domain.Objects;

namespace EternalWars.Core.Domain.Editing;

/// <summary>Which part of the map stays where it is when the map is resized (EDIT-8): the old map sits at that corner, edge or middle of the new one.</summary>
public enum ResizeAnchor
{
    TopLeft,
    Top,
    TopRight,
    Left,
    Centre,
    Right,
    BottomLeft,
    Bottom,
    BottomRight,
}

/// <summary>
/// What a resize, crop or shift would do (EDIT-8), for the question before it: the map's new size, how far its content moves, and what
/// would end up off the map and be removed with it. <see cref="Result"/> other than Ok: it is refused and the rest is empty.
/// </summary>
public sealed record ResizeCheck(
    EditResult Result, int Width, int Height, int Dx, int Dy,
    IReadOnlyList<MapObject> Objects, IReadOnlyList<Hero> Heroes, int EventCells, bool LosesGrail)
{
    /// <summary>Nothing would change: the same size and no move.</summary>
    public bool IsNoChange { get; init; }

    /// <summary>Whether anything would be removed.</summary>
    public bool LosesThings => Objects.Count > 0 || Heroes.Count > 0 || EventCells > 0 || LosesGrail;

    internal static ResizeCheck Refused(EditResult result) => new(result, 0, 0, 0, 0, [], [], 0, false);
}

/// <summary>
/// Resize, crop and shift (EDIT-8, ADR-250): one operation, a new frame round the map's content. Terrain, roads, rivers, heights and start
/// zones are cut where they fall outside; new ground is grass (subterranean below the surface) at its terrain's height. Objects with a
/// cell outside, heroes and event cells outside leave the map, and a Grail too near the new edge is taken away, all in the same undo
/// step. What stays keeps its ids and links; the places event actions name move along.
/// </summary>
internal sealed class MapResizer(EditorContext context, ObjectEditor objects, HeroEditor heroes, MapEventEditor mapEvents, MapPropertiesEditor properties)
{
    private Game.GameState Scenario => context.Scenario;

    private AdventureMap Map => Scenario.Map;

    public ResizeCheck CheckResize(int width, int height, ResizeAnchor anchor) =>
        !Enum.IsDefined(anchor)
        || width is < MapSizes.MinDimension or > MapSizes.MaxDimension || height is < MapSizes.MinDimension or > MapSizes.MaxDimension
            ? ResizeCheck.Refused(EditResult.InvalidValue)
            : Check(width, height, Along((int)anchor % 3, width - Map.Width), Along((int)anchor / 3, height - Map.Height));

    /// <summary>The box round the selected tiles (of every level) becomes the map.</summary>
    public ResizeCheck CheckCrop()
    {
        var tiles = context.Selection.Tiles;
        if (tiles.Count == 0)
        {
            return ResizeCheck.Refused(EditResult.InvalidTarget);
        }

        var (left, top) = (tiles.Min(t => t.X), tiles.Min(t => t.Y));
        var (width, height) = (tiles.Max(t => t.X) - left + 1, tiles.Max(t => t.Y) - top + 1);
        return width < MapSizes.MinDimension || height < MapSizes.MinDimension
            ? ResizeCheck.Refused(EditResult.InvalidValue)
            : Check(width, height, -left, -top);
    }

    /// <summary>The content moves by (<paramref name="dx"/>, <paramref name="dy"/>) on a map of the same size; a move of the whole map's width or height is refused.</summary>
    public ResizeCheck CheckShift(int dx, int dy) =>
        dx <= -Map.Width || dx >= Map.Width || dy <= -Map.Height || dy >= Map.Height ? ResizeCheck.Refused(EditResult.InvalidValue) : Check(Map.Width, Map.Height, dx, dy);

    public EditResult Apply(string description, ResizeCheck check)
    {
        if (check.Result != EditResult.Ok || check.IsNoChange)
        {
            return check.Result;
        }

        var (dx, dy) = (check.Dx, check.Dy);
        context.AsOneStep(description, () =>
        {
            // What does not fit leaves first, each through its own editor (links to a removed gate go, a hero leaves the selection).
            foreach (var hero in check.Heroes)
            {
                heroes.Remove(hero.Id);
            }

            foreach (var mapObject in check.Objects)
            {
                objects.Remove(mapObject.Id);
            }

            mapEvents.RemoveCells(cell => !InFrame(check, cell.Position));
            if (check.LosesGrail)
            {
                properties.SetGrail(null);
            }

            var before = Map.Reframed(Map.Width, Map.Height, 0, 0);
            var after = Shaped(Map.Reframed(check.Width, check.Height, dx, dy), before, dx, dy);
            var events = Scenario.MapEvents.Set;
            var grail = Scenario.Grail.Position;
            context.Run(
                description,
                () => Reframe(after, dx, dy, Moved(events, dx, dy), grail?.Offset(dx, dy)),
                () => Reframe(before, -dx, -dy, events, grail));
        });
        return EditResult.Ok;
    }

    /// <summary>
    /// Where the old map's left (or top) edge lies on the new one: 0, half or all of <paramref name="growth"/> for the anchor's column
    /// (or row). Half an odd number rounds towards 0, so growing by it and shrinking back cuts exactly what was added.
    /// </summary>
    private static int Along(int third, int growth) => third switch
    {
        0 => 0,
        1 => growth / 2,
        _ => growth,
    };

    private static bool InFrame(ResizeCheck check, MapPosition position) =>
        position.X + check.Dx >= 0 && position.X + check.Dx < check.Width && position.Y + check.Dy >= 0 && position.Y + check.Dy < check.Height;

    private ResizeCheck Check(int width, int height, int dx, int dy)
    {
        var check = new ResizeCheck(EditResult.Ok, width, height, dx, dy, [], [], 0, false);
        if (width == Map.Width && height == Map.Height && dx == 0 && dy == 0)
        {
            return check with { IsNoChange = true };
        }

        // The Grail keeps placement's rule on the new map: far enough from every edge (ADR-140).
        var grail = Scenario.Grail.Position;
        return check with
        {
            Objects = [.. Scenario.Objects.Where(o => o.OccupiedCells().Any(c => !InFrame(check, c.Position)))],
            Heroes = [.. Scenario.Heroes.Where(h => !InFrame(check, h.Position))],
            EventCells = Scenario.MapEvents.Set.Cells.Count(c => !InFrame(check, c.Position)),
            LosesGrail = grail is { } at
                && (at.X + dx < GrailRules.EdgeDistance || at.Y + dy < GrailRules.EdgeDistance
                    || at.X + dx >= width - GrailRules.EdgeDistance || at.Y + dy >= height - GrailRules.EdgeDistance),
        };
    }

    /// <summary>The new ground gets its terrain's height: every corner the old map did not have.</summary>
    private static AdventureMap Shaped(AdventureMap map, AdventureMap old, int dx, int dy)
    {
        var kept = new CornerArea(dx, dy, dx + old.Width, dy + old.Height);
        CornerArea[] added =
        [
            new(0, 0, map.Width, kept.Top - 1), new(0, kept.Bottom + 1, map.Width, map.Height),
            new(0, kept.Top, kept.Left - 1, kept.Bottom), new(kept.Right + 1, kept.Top, map.Width, kept.Bottom),
        ];
        for (var level = 0; level < map.Levels; level++)
        {
            foreach (var area in added)
            {
                ReliefShaper.FromTerrain(map, level, area);
            }
        }

        return map;
    }

    /// <summary>The event cells and the places actions name, moved with the map.</summary>
    private static MapEventSet Moved(MapEventSet set, int dx, int dy)
    {
        EventAction Move(EventAction action) => action switch
        {
            RevealAction reveal => reveal with { X = reveal.X + dx, Y = reveal.Y + dy },
            GiveHeroAction { X: { } x, Y: { } y } give => give with { X = x + dx, Y = y + dy },
            _ => action,
        };
        return set with
        {
            Events = [.. set.Events.Select(e => MapEventTreeEdit.Rewrite(e, Move))],
            Cells = [.. set.Cells.Select(c => c with { X = c.X + dx, Y = c.Y + dy })],
        };
    }

    private void Reframe(AdventureMap contents, int dx, int dy, MapEventSet events, MapPosition? grail)
    {
        context.Selecting?.Clear(); // selected tiles name places of the old frame
        Scenario.Reframe(contents, dx, dy);
        Scenario.SetGrail(grail);
        Scenario.SetMapEvents(events, check: false);
        context.Publish(new MapResized(contents.Width, contents.Height, dx, dy));
        if (!events.IsEmpty)
        {
            context.Publish(new MapEventsChanged());
        }

        if (grail is not null)
        {
            context.Publish(new GrailMoved(grail));
        }
    }
}
