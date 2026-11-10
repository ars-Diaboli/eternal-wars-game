using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Visibility;
using EternalWars.Godot.Shared.Events;
using EternalWars.Godot.Shared.Map;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// Minimap of the shown level through the context's viewer (<see cref="Minimap.Build"/>): unexplored black,
/// terrain colours, objects and heroes as markers in owner colours. Clicking reports the tile. The tiles are an image, one pixel each, of which
/// only the changed pixels are written again; it is rebuilt once a frame however many events ask for it, and heroes are drawn over it.
/// </summary>
public partial class MinimapWidget : Control
{
    private static readonly Color Unexplored = new(0.03f, 0.03f, 0.04f);
    private MapViewContext _context = null!;
    private MinimapCell[] _cells = [];
    private Image? _image;
    private ImageTexture? _texture;
    private readonly List<(int Index, Color Color)> _heroes = [];

    /// <summary>Size of one tile in pixels.</summary>
    public float CellPixels { get; set; } = 6f;

    public event Action<MapPosition>? TileClicked;

    public void Bind(MapViewContext context)
    {
        _context = context;
        var map = context.State.Map;
        CustomMinimumSize = new Vector2(map.Width, map.Height) * CellPixels;
        MouseFilter = MouseFilterEnum.Stop;
        TextureFilter = TextureFilterEnum.Nearest;
        Rebuild();

        var rebuild = new RefreshScheduler(this, Rebuild);
        context.Changed += rebuild.Request;
        TreeExiting += () => context.Changed -= rebuild.Request;
        rebuild.On<TilesRevealed>(context.Events)
            .On<KnownObjectsChanged>(context.Events)
            .On<SightChanged>(context.Events)
            .On<HeroMoved>(context.Events)
            .On<HeroTeleported>(context.Events)
            .On<HeroDefeated>(context.Events)
            .On<TerrainPainted>(context.Events)
            .On<MapObjectPlaced>(context.Events)
            .On<MapObjectRemoved>(context.Events)
            .On<HeroPlaced>(context.Events)
            .On<HeroRemoved>(context.Events);
    }

    public override void _Draw()
    {
        if (_texture is null)
        {
            return;
        }

        var width = _context.State.Map.Width;
        DrawTextureRect(_texture, new Rect2(Vector2.Zero, CustomMinimumSize), tile: false);
        foreach (var (index, color) in _heroes)
        {
            DrawRect(new Rect2(new Vector2(index % width, index / width) * CellPixels, Vector2.One * CellPixels).Grow(1), color);
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

    private static Color ColorOf(MinimapCell cell) =>
        !cell.Explored ? Unexplored
        : cell.Marker == MinimapMarker.None ? Palette.ForTerrain(cell.Terrain)
        : cell.Owner is { } owner ? Palette.ForPlayer(owner)
        : cell.Marker == MinimapMarker.Object ? Palette.Neutral.Darkened(0.3f)
        : Palette.Neutral;

    private void Rebuild()
    {
        var map = _context.State.Map;
        var cells = Minimap.Build(_context.State, _context.Viewer, _context.Level);
        var fresh = _image is null || _image.GetWidth() != map.Width || _image.GetHeight() != map.Height || cells.Length != _cells.Length;
        if (fresh)
        {
            _image = Image.CreateEmpty(map.Width, map.Height, false, Image.Format.Rgba8);
        }

        _heroes.Clear();
        for (var i = 0; i < cells.Length; i++)
        {
            var cell = cells[i];
            if (fresh || cell != _cells[i])
            {
                _image!.SetPixel(i % map.Width, i / map.Width, ColorOf(cell));
            }

            if (cell.Explored && cell.Marker == MinimapMarker.Hero)
            {
                _heroes.Add((i, ColorOf(cell)));
            }
        }

        _cells = cells;
        if (_texture is null || fresh)
        {
            _texture = ImageTexture.CreateFromImage(_image);
        }
        else
        {
            _texture.Update(_image);
        }

        QueueRedraw();
    }
}
