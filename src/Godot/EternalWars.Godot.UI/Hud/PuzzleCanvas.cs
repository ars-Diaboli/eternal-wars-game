using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;
using EternalWars.Godot.Shared.Map;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>The puzzle map's cells (<see cref="PuzzleMapPanel"/>, ADR-140).</summary>
public partial class PuzzleCanvas : Control
{
    public const float CellPixels = 28;
    private static readonly Color Parchment = new(0.78f, 0.69f, 0.50f);
    private GameState? _state;
    private PuzzleMap? _puzzle;

    public void Display(GameState state, PuzzleMap? puzzle)
    {
        (_state, _puzzle) = (state, puzzle);
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), Parchment);
        if (_state is null || _puzzle is not { } puzzle)
        {
            return;
        }

        for (var dy = 0; dy < puzzle.Size; dy++)
        {
            for (var dx = 0; dx < puzzle.Size; dx++)
            {
                var cell = puzzle.Origin.Offset(dx, dy);
                if (!puzzle.Shows(cell))
                {
                    continue;
                }

                var color = !_state.Map.Contains(cell) ? Colors.Black
                    : _state.ObjectCellAt(cell) is not null ? Palette.ForTerrain(_state.Map.GetTerrain(cell)).Darkened(0.55f)
                    : Palette.ForTerrain(_state.Map.GetTerrain(cell));
                DrawRect(new Rect2(dx * CellPixels, dy * CellPixels, CellPixels, CellPixels), color);
            }
        }

        // The pieces' edges, so the puzzle reads as pieces.
        var edge = new Color(0.35f, 0.28f, 0.18f);
        for (var line = 0; line <= puzzle.Size; line += GrailRules.PieceSize)
        {
            DrawLine(new Vector2(line * CellPixels, 0), new Vector2(line * CellPixels, puzzle.Size * CellPixels), edge, 2);
            DrawLine(new Vector2(0, line * CellPixels), new Vector2(puzzle.Size * CellPixels, line * CellPixels), edge, 2);
        }

        if (puzzle.Grail is { } grail)
        {
            var at = new Vector2((grail.X - puzzle.Origin.X) * CellPixels, (grail.Y - puzzle.Origin.Y) * CellPixels);
            var cross = new Color(0.85f, 0.1f, 0.1f);
            DrawLine(at + new Vector2(4, 4), at + new Vector2(CellPixels - 4, CellPixels - 4), cross, 4);
            DrawLine(at + new Vector2(CellPixels - 4, 4), at + new Vector2(4, CellPixels - 4), cross, 4);
        }
    }
}
