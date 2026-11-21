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
/// The map editor (ADR-261) also has it follow a drag (<see cref="DragMoves"/>), draw the view's frame (<see cref="ViewFrame"/>) and show
/// the other level faintly through the shown one (<see cref="GhostOtherLevel"/>).
/// </summary>
public partial class MinimapWidget : Control
{
    private static readonly Color Unexplored = new(0.03f, 0.03f, 0.04f);
    private MapViewContext _context = null!;
    private MinimapCell[] _cells = [];
    private Image? _image;
    private ImageTexture? _texture;
    private readonly List<(int Index, Color Color)> _heroes = [];
    private RefreshScheduler? _rebuild;
    private Vector2[]? _frame;
    private bool _ghost;
    private bool _dragging;
    private MinimapCell[]? _ghostCells;

    /// <summary>How much of the other level's colour a cell shows with <see cref="GhostOtherLevel"/>.</summary>
    private const float GhostShare = 0.35f;

    /// <summary>Size of one tile in pixels.</summary>
    public float CellPixels { get; set; } = 6f;

    public event Action<MapPosition>? TileClicked;

    /// <summary>A drag with the left button reports every tile it passes, as clicks do (the editor moves its view along).</summary>
    public bool DragMoves { get; set; }

    /// <summary>
    /// Asked every frame, when set: the corners of what the camera shows, in tiles (a tile's centre at its whole numbers), drawn as an
    /// outline; null draws none.
    /// </summary>
    public Func<IReadOnlyList<Vector2>?>? ViewFrame { get; set; }

    /// <summary>On a map of two levels, the other level shows faintly through the shown one.</summary>
    public bool GhostOtherLevel
    {
        get => _ghost;
        set
        {
            _ghost = value;
            Refresh();
        }
    }

    /// <summary>Rebuilds at the end of the frame: for changes no event of its own tells (the editor calls it after every edit).</summary>
    public void Refresh() => _rebuild?.Request();

    public void Bind(MapViewContext context)
    {
        _context = context;
        var map = context.State.Map;
        CustomMinimumSize = new Vector2(map.Width, map.Height) * CellPixels;
        MouseFilter = MouseFilterEnum.Stop;
        TextureFilter = TextureFilterEnum.Nearest;
        Rebuild();

        var rebuild = new RefreshScheduler(this, Rebuild);
        _rebuild = rebuild;
        context.ViewerChanged += rebuild.Request;
        context.LevelChanged += rebuild.Request;
        TreeExiting += () =>
        {
            context.ViewerChanged -= rebuild.Request;
            context.LevelChanged -= rebuild.Request;
        };
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

        if (_frame is { Length: > 1 } frame)
        {
            // Drawn inside the map's square: a corner past the map's edge is held at it.
            var size = CustomMinimumSize;
            var points = frame.Select(p => ((p + (Vector2.One * 0.5f)) * CellPixels).Clamp(Vector2.Zero, size)).Append(Vector2.Zero).ToArray();
            points[^1] = points[0];
            DrawPolyline(points, Colors.White, 1.5f);
        }
    }

    public override void _Process(double delta)
    {
        if (ViewFrame is null || !IsVisibleInTree())
        {
            return;
        }

        var frame = ViewFrame() is { } corners ? corners.ToArray() : null;
        if (frame is null != _frame is null || (frame is not null && !frame.SequenceEqual(_frame!)))
        {
            _frame = frame;
            QueueRedraw();
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } button:
                _dragging = button.Pressed && DragMoves;
                if (button.Pressed)
                {
                    Report(button.Position);
                }

                AcceptEvent();
                break;
            case InputEventMouseMotion motion when _dragging:
                if (!motion.ButtonMask.HasFlag(MouseButtonMask.Left))
                {
                    _dragging = false; // let go outside the minimap
                    break;
                }

                Report(motion.Position);
                AcceptEvent();
                break;
        }
    }

    private void Report(Vector2 at)
    {
        var tile = new MapPosition((int)Math.Floor(at.X / CellPixels), (int)Math.Floor(at.Y / CellPixels), _context.Level);
        if (_context.State.Map.Contains(tile))
        {
            TileClicked?.Invoke(tile);
        }
    }

    private Color ColorOf(MinimapCell cell) =>
        !cell.Explored ? Unexplored
        : cell.Marker == MinimapMarker.None ? Palette.ForTerrain(cell.Terrain)
        : cell.Owner is { } owner ? Palette.ForPlayer(_context.State, owner)
        : cell.Marker == MinimapMarker.Object ? Palette.Neutral.Darkened(0.3f)
        : Palette.Neutral;

    private void Rebuild()
    {
        var map = _context.State.Map;
        var cells = Minimap.Build(_context.State, _context.Viewer, _context.Level);
        var ghost = _ghost && map.Levels > 1 ? Minimap.Build(_context.State, _context.Viewer, _context.Level == 0 ? 1 : 0) : null;
        // A ghosted pixel is its cell and the other level's: written again when either changed, and all of them when the ghost comes or goes.
        var fresh = _image is null || _image.GetWidth() != map.Width || _image.GetHeight() != map.Height || cells.Length != _cells.Length
            || (ghost is null) != (_ghostCells is null);
        if (fresh)
        {
            _image = Image.CreateEmpty(map.Width, map.Height, false, Image.Format.Rgba8);
        }

        _heroes.Clear();
        for (var i = 0; i < cells.Length; i++)
        {
            var cell = cells[i];
            if (fresh || cell != _cells[i] || (ghost is not null && ghost[i] != _ghostCells![i]))
            {
                var colour = ColorOf(cell);
                _image!.SetPixel(i % map.Width, i / map.Width, ghost is null ? colour : colour.Lerp(ColorOf(ghost[i]), GhostShare));
            }

            if (cell.Explored && cell.Marker == MinimapMarker.Hero)
            {
                _heroes.Add((i, ColorOf(cell)));
            }
        }

        _cells = cells;
        _ghostCells = ghost;
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
