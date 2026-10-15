using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Visibility;
using EternalWars.Godot.Shared.Events;
using EternalWars.Godot.Shared.Map;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// Minimap of the shown level through the context's viewer (<see cref="Minimap.Build"/>): unexplored black,
/// terrain colours, objects and heroes as markers in owner colours. Clicking reports the tile.
/// </summary>
public partial class MinimapWidget : Control
{
    private static readonly Color Unexplored = new(0.03f, 0.03f, 0.04f);
    private MapViewContext _context = null!;
    private MinimapCell[] _cells = [];

    /// <summary>Size of one tile in pixels.</summary>
    public float CellPixels { get; set; } = 6f;

    public event Action<MapPosition>? TileClicked;

    public void Bind(MapViewContext context)
    {
        _context = context;
        var map = context.State.Map;
        CustomMinimumSize = new Vector2(map.Width, map.Height) * CellPixels;
        MouseFilter = MouseFilterEnum.Stop;
        Rebuild();

        context.Changed += Rebuild;
        TreeExiting += () => context.Changed -= Rebuild;
        this.SubscribeWhileInTree<TilesRevealed>(context.Events, _ => Rebuild());
        this.SubscribeWhileInTree<KnownObjectsChanged>(context.Events, _ => Rebuild());
        this.SubscribeWhileInTree<SightChanged>(context.Events, _ => Rebuild());
        this.SubscribeWhileInTree<HeroMoved>(context.Events, _ => Rebuild());
        this.SubscribeWhileInTree<HeroTeleported>(context.Events, _ => Rebuild());
        this.SubscribeWhileInTree<HeroDefeated>(context.Events, _ => Rebuild());
        this.SubscribeWhileInTree<TerrainPainted>(context.Events, _ => Rebuild());
        this.SubscribeWhileInTree<MapObjectPlaced>(context.Events, _ => Rebuild());
        this.SubscribeWhileInTree<MapObjectRemoved>(context.Events, _ => Rebuild());
        this.SubscribeWhileInTree<HeroPlaced>(context.Events, _ => Rebuild());
        this.SubscribeWhileInTree<HeroRemoved>(context.Events, _ => Rebuild());
    }

    public override void _Draw()
    {
        var width = _context.State.Map.Width;
        DrawRect(new Rect2(Vector2.Zero, CustomMinimumSize), Unexplored);
        for (var i = 0; i < _cells.Length; i++)
        {
            var cell = _cells[i];
            var color = !cell.Explored ? Unexplored
                : cell.Marker == MinimapMarker.None ? Palette.ForTerrain(cell.Terrain)
                : cell.Owner is { } owner ? Palette.ForPlayer(owner)
                : cell.Marker == MinimapMarker.Object ? Palette.Neutral.Darkened(0.3f)
                : Palette.Neutral;
            var rect = new Rect2(new Vector2(i % width, i / width) * CellPixels, Vector2.One * CellPixels);
            DrawRect(cell.Marker == MinimapMarker.Hero ? rect.Grow(1) : rect, color);
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click)
        {
            var tile = new MapPosition((int)(click.Position.X / CellPixels), (int)(click.Position.Y / CellPixels), _context.Level);
            if (_context.State.Map.Contains(tile))
            {
                TileClicked?.Invoke(tile);
            }

            AcceptEvent();
        }
    }

    private void Rebuild()
    {
        _cells = Minimap.Build(_context.State, _context.Viewer, _context.Level);
        QueueRedraw();
    }
}
