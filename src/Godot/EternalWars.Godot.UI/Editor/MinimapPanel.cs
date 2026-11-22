using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Godot.Shared.Cameras;
using EternalWars.Godot.Shared.Events;
using EternalWars.Godot.Shared.Map;
using EternalWars.Godot.UI.Hud;
using Godot;

namespace EternalWars.Godot.UI.Editor;

/// <summary>
/// The editor's minimap (ADR-261), under the right region's tabs: the game's <see cref="MinimapWidget"/> of the shown level, fitted into a
/// square, with the camera's view outlined. A click or a drag on it moves the view there. On a map of two levels a box shows the other
/// level faintly through the shown one. It is made again for every map and every new size of one.
/// </summary>
public partial class MinimapPanel : VBoxContainer
{
    /// <summary>The side of the square the map is fitted into, in pixels.</summary>
    public const float Side = 200f;

    private readonly MinimapWidget _minimap = new() { Name = "Minimap" };

    /// <summary>The map's minimap; <paramref name="moveTo"/> shows a tile clicked or dragged over; the view's frame is what <paramref name="mapView"/> shows.</summary>
    public void Bind(MapEditorSession editor, MapViewContext context, EditorCamera camera, Control mapView, Action<MapPosition> moveTo)
    {
        var map = editor.Scenario.Map;
        _minimap.CellPixels = Side / Math.Max(map.Width, map.Height);
        _minimap.DragMoves = true;
        _minimap.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        _minimap.TileClicked += moveTo;
        _minimap.ViewFrame = () => Frame(camera, mapView);
        AddChild(UiIcons.IconLabel(UiIcon.Minimap, "Minimap"));
        AddChild(_minimap);
        _minimap.Bind(context);

        if (map.Levels > 1)
        {
            var ghost = new CheckBox { Text = "Show the other level faintly" };
            ghost.Toggled += on => _minimap.GhostOtherLevel = on;
            AddChild(ghost);
        }

        // Every edit: what the game's own events do not tell (a move, an owner, a road) shows too.
        this.SubscribeWhileInTree<UndoHistoryChanged>(editor.Events, _ => _minimap.Refresh());
    }

    /// <summary>The ground under the map view's corners, in tiles; null when a corner looks past the ground's plane.</summary>
    private static Vector2[]? Frame(EditorCamera camera, Control mapView)
    {
        if (!camera.IsInsideTree() || !mapView.IsVisibleInTree())
        {
            return null;
        }

        var rect = mapView.GetGlobalRect();
        var corners = new[] { rect.Position, rect.Position + new Vector2(rect.Size.X, 0), rect.End, rect.Position + new Vector2(0, rect.Size.Y) };
        var frame = new Vector2[corners.Length];
        for (var i = 0; i < corners.Length; i++)
        {
            if (camera.GroundAt(corners[i]) is not { } ground)
            {
                return null;
            }

            frame[i] = new Vector2(ground.X, ground.Z) / MapSpace.TileSize;
        }

        return frame;
    }
}
