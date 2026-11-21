using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Objects;

namespace EternalWars.Core.Domain.Editing;

/// <summary>
/// Turns and mirrors scenery on the grid (EDIT-6, ADR-252). A quarter turn is clockwise round the middle of the piece's box, so four
/// of them bring it back to its place; a mirroring is left to right in the box. The rules are placement's: every cell of the turned
/// shape on the map and on no other object or hero. The pieces turned together do not stand in each other's way where they were, only
/// where they come to lie. One call is one undo step; a piece keeps its id.
/// </summary>
internal sealed class SceneryTurner(EditorContext context)
{
    private Game.GameState Scenario => context.Scenario;

    public MoveCheck Check(IReadOnlyCollection<Scenery> pieces, bool mirror)
    {
        var result = pieces.Count == 0 ? EditResult.InvalidTarget : EditResult.Ok;
        var cells = new List<MoveCell>();
        var taken = new HashSet<MapPosition>();
        foreach (var piece in pieces)
        {
            var (to, orientation) = Turned(piece, mirror);
            foreach (var (dx, dy, _) in piece.Definition.Footprint.Oriented(orientation).OccupiedCells)
            {
                var cell = to.Offset(dx, dy);
                var check = !Scenario.Map.Contains(cell) ? EditResult.OutOfBounds
                    : Scenario.ObjectCellAt(cell) is { Object: var other } && !pieces.Contains(other) ? EditResult.Overlaps
                    : Scenario.HeroAt(cell) is not null || !taken.Add(cell) ? EditResult.Overlaps
                    : EditResult.Ok;
                cells.Add(new MoveCell(cell, check == EditResult.Ok));
                if (result == EditResult.Ok)
                {
                    result = check;
                }
            }
        }

        return new MoveCheck(result, cells);
    }

    public EditResult Turn(string description, IReadOnlyCollection<Scenery> pieces, bool mirror)
    {
        if (Check(pieces, mirror).Result is var check && check != EditResult.Ok)
        {
            return check;
        }

        var before = pieces.Select(p => (Piece: p, p.Position, p.Orientation)).ToList();
        var after = pieces.Select(p => (Piece: p, Turned(p, mirror).Position, Turned(p, mirror).Orientation)).ToList();
        context.Run(description, () => Lay(after), () => Lay(before));
        return EditResult.Ok;
    }

    /// <summary>Where the piece's box comes to lie and how, after a quarter turn clockwise or a mirroring.</summary>
    private static (MapPosition Position, Orientation Orientation) Turned(Scenery piece, bool mirror)
    {
        if (mirror)
        {
            return (piece.Position, piece.Orientation.Flipped());
        }

        // The box swaps its sides round its middle; half an odd difference rounds towards 0 both ways, so the way back is exact.
        var (width, height) = (piece.Footprint.Width, piece.Footprint.Height);
        return (piece.Position.Offset((width - height) / 2, (height - width) / 2), piece.Orientation.Rotated());
    }

    /// <summary>Gives the pieces their places and orientations; they keep their places in the scenario's order.</summary>
    private void Lay(List<(Scenery Piece, MapPosition Position, Orientation Orientation)> pieces)
    {
        // All off the map's index first (the last in the order first, so the places of the others hold), then back in as they lie now.
        var places = pieces
            .Select(p => (Turn: p, Index: Scenario.Objects.ToList().IndexOf(p.Piece)))
            .OrderByDescending(p => p.Index)
            .ToList();
        places.ForEach(p => Scenario.RemoveObject(p.Turn.Piece));
        var moved = new List<MapObjectMoved>();
        foreach (var ((piece, position, orientation), index) in Enumerable.Reverse(places))
        {
            var from = piece.Position;
            (piece.Position, piece.Orientation) = (position, orientation);
            Scenario.AddObject(piece, index);
            moved.Add(new MapObjectMoved(piece.Id, from, position));
        }

        moved.ForEach(context.Publish);
    }
}
