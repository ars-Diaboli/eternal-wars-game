using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Editing;

/// <summary>One cell that moved things would take, and whether they fit there.</summary>
public readonly record struct MoveCell(MapPosition Position, bool Fits);

/// <summary>
/// Whether things can move by an offset: <see cref="Result"/> is <see cref="EditResult.Ok"/> when every cell fits, else the first
/// reason; <see cref="Cells"/> are the cells the things would take, for a ghost of the move.
/// </summary>
public sealed record MoveCheck(EditResult Result, IReadOnlyList<MoveCell> Cells);

/// <summary>
/// Moves objects and heroes over the map, together, by one offset on their level (EDIT-1, ADR-250). The rules are placement's: every
/// cell on the map, no cell on another object or hero (a hero may stand on its own town's entrance), an object's entrance and a hero's
/// tile on passable terrain. What moves along does not stand in the way. The ground under the moved objects is levelled as placement
/// does (ADR-182 f); the ground they left stays as it is. A move is one undo step that puts the positions and the ground back; an
/// object keeps its id, so its links, owner and contents stay.
/// </summary>
internal sealed class MapMover(EditorContext context)
{
    private Game.GameState Scenario => context.Scenario;

    public MoveCheck Check(IReadOnlyCollection<MapObject> objects, IReadOnlyCollection<Hero> heroes, int dx, int dy)
    {
        var result = EditResult.Ok;
        var cells = new List<MoveCell>();
        void Cell(MapPosition position, EditResult check)
        {
            cells.Add(new MoveCell(position, check == EditResult.Ok));
            if (result == EditResult.Ok)
            {
                result = check;
            }
        }

        foreach (var mapObject in objects)
        {
            foreach (var (from, kind) in mapObject.OccupiedCells())
            {
                var to = from.Offset(dx, dy);
                Cell(to,
                    !Scenario.Map.Contains(to) ? EditResult.OutOfBounds
                    : Scenario.ObjectCellAt(to) is { } other && !objects.Contains(other.Object) ? EditResult.Overlaps
                    : Scenario.HeroAt(to) is { } hero && !heroes.Contains(hero) ? EditResult.Overlaps
                    : kind == FootprintCell.Visit && !Scenario.Terrains.IsPassable(Scenario.Map.GetTerrain(to)) ? EditResult.ImpassableTerrain
                    : EditResult.Ok);
            }
        }

        foreach (var hero in heroes)
        {
            var to = hero.Position.Offset(dx, dy);
            Cell(to,
                !Scenario.Map.Contains(to) ? EditResult.OutOfBounds
                : Scenario.HeroAt(to) is { } other && !heroes.Contains(other) ? EditResult.Overlaps
                : Scenario.ObjectCellAt(to) is { } cell && !objects.Contains(cell.Object) && !MapValidator.IsOwnTownEntrance(cell, hero.Owner) ? EditResult.Overlaps
                : !Scenario.Terrains.IsPassable(Scenario.Map.GetTerrain(to)) ? EditResult.ImpassableTerrain
                : EditResult.Ok);
        }

        return new MoveCheck(result, cells);
    }

    /// <summary>Moves the things when they all fit (<see cref="Check"/>), as one undo step; an offset of nothing records nothing.</summary>
    public EditResult Move(string description, IReadOnlyCollection<MapObject> objects, IReadOnlyCollection<Hero> heroes, int dx, int dy)
    {
        if (Check(objects, heroes, dx, dy).Result is var check && check != EditResult.Ok)
        {
            return check;
        }

        if ((dx == 0 && dy == 0) || (objects.Count == 0 && heroes.Count == 0))
        {
            return EditResult.Ok;
        }

        var levelled = objects.Where(ReliefShaper.IsLevelled).ToList();
        var ground = levelled.Count == 0
            ? null
            : new GroundEdit(context, () => ReliefShaper.Level(Scenario.Map, levelled, Scenario.Objects.Except(levelled)));
        context.Run(
            description,
            () =>
            {
                var moved = Shift(objects, heroes, dx, dy);
                ground?.Apply(); // the ground first, so the views that follow the moves build on it
                Publish(moved);
            },
            () =>
            {
                var moved = Shift(objects, heroes, -dx, -dy);
                ground?.Revert();
                Publish(moved);
            });
        return EditResult.Ok;
    }

    /// <summary>Changes the positions; the objects keep their places in the scenario's order. Returns the events that tell of it.</summary>
    private List<IEvent> Shift(IReadOnlyCollection<MapObject> objects, IReadOnlyCollection<Hero> heroes, int dx, int dy)
    {
        var moved = new List<IEvent>();

        // All off the map's index first (the last in the order first, so the places of the others hold), then back in at the new cells.
        var places = objects
            .Select(o => (Object: o, Index: IndexOf(o)))
            .OrderByDescending(p => p.Index)
            .ToList();
        places.ForEach(p => Scenario.RemoveObject(p.Object));
        foreach (var (mapObject, index) in Enumerable.Reverse(places))
        {
            var from = mapObject.Position;
            mapObject.Position = from.Offset(dx, dy);
            Scenario.AddObject(mapObject, index);
            moved.Add(new MapObjectMoved(mapObject.Id, from, mapObject.Position));
        }

        foreach (var hero in heroes)
        {
            var from = hero.Position;
            hero.Position = from.Offset(dx, dy);
            moved.Add(new HeroRepositioned(hero.Id, from, hero.Position));
        }

        return moved;
    }

    private int IndexOf(MapObject mapObject)
    {
        for (var i = 0; i < Scenario.Objects.Count; i++)
        {
            if (Scenario.Objects[i] == mapObject)
            {
                return i;
            }
        }

        throw new InvalidOperationException($"{mapObject} is not on the map.");
    }

    private void Publish(List<IEvent> moved)
    {
        foreach (var @event in moved)
        {
            switch (@event)
            {
                case MapObjectMoved e:
                    context.Publish(e);
                    break;
                case HeroRepositioned e:
                    context.Publish(e);
                    break;
            }
        }
    }
}
