using EternalWars.Core.Domain.Map;
using EternalWars.Godot.Shared.Map;
using Godot;

namespace EternalWars.Godot.UI.Editor;

/// <summary>
/// The whole map top-down as a picture (File → Export, ADR-266): a hidden <see cref="SubViewport"/> that shares the map view's 3D world
/// renders the level shown with an orthographic camera straight down, north up, <see cref="PixelsPerTile"/> pixels a tile. What the map
/// view draws is in the picture (an overlay shown, the grid), but not the objects' captions (each shows only near the view's camera) nor
/// the nodes handed to <see cref="Take"/> (the selection's marks): they are hidden while it is taken. It takes a few frames; the picture
/// comes back through a callback and the node frees itself.
/// </summary>
public partial class MapPicture : Node
{
    /// <summary>The picture's longer side is at most this many pixels.</summary>
    public const int MaxSide = 4096;

    /// <summary>The frames the viewport is given to draw: it draws at the end of the frame it was asked in.</summary>
    private const int FramesToDraw = 3;

    private readonly SubViewport _viewport = new() { Name = "PictureViewport", RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled, Msaa3D = Viewport.Msaa.Msaa4X };
    private readonly List<(Node3D Node, bool Visible)> _hidden = [];
    private Action<Image?> _done = null!;
    private int _frames = -1;

    /// <summary>Pixels a tile: as many as fit <see cref="MaxSide"/>, from 4 to 32.</summary>
    public static int PixelsPerTile(AdventureMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        return Math.Clamp(MaxSide / Math.Max(map.Width, map.Height), 4, 32);
    }

    /// <summary>
    /// Takes the picture of <paramref name="level"/> of <paramref name="map"/> as the 3D world under <paramref name="host"/> shows it, with
    /// <paramref name="hide"/> and the captions under <paramref name="world"/> hidden meanwhile; <paramref name="done"/> gets it (null when
    /// nothing was drawn, as without a display).
    /// </summary>
    public static MapPicture Take(Node host, AdventureMap map, int level, Node world, IEnumerable<Node3D> hide, Action<Image?> done)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(hide);
        ArgumentNullException.ThrowIfNull(done);
        var picture = new MapPicture { Name = "MapPicture", _done = done };
        var ppt = PixelsPerTile(map);
        picture._viewport.Size = new Vector2I(map.Width * ppt, map.Height * ppt);
        var floor = -level * MapSpace.LevelSpacing;
        var camera = new Camera3D
        {
            Projection = Camera3D.ProjectionType.Orthogonal, KeepAspect = Camera3D.KeepAspectEnum.Height, Size = map.Height * MapSpace.TileSize,
            Near = 0.05f, Far = MapSpace.LevelSpacing,

            // Over the middle of the map, between this level and the one above, looking straight down with north (−Z) up.
            Position = new Vector3((map.Width - 1) * MapSpace.TileSize / 2, floor + (MapSpace.LevelSpacing * 0.6f), (map.Height - 1) * MapSpace.TileSize / 2),
            RotationDegrees = new Vector3(-90, 0, 0),
        };
        picture._viewport.AddChild(camera);
        picture.AddChild(picture._viewport);
        foreach (var node in hide.Concat(world.FindChildren("*", nameof(Label3D), recursive: true, owned: false).OfType<Node3D>()))
        {
            picture._hidden.Add((node, node.Visible));
            node.Visible = false;
        }

        host.AddChild(picture);
        camera.Current = true;
        picture._viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
        picture._frames = 0;
        return picture;
    }

    public override void _Process(double delta)
    {
        if (_frames < 0 || ++_frames < FramesToDraw)
        {
            return;
        }

        _frames = -1;
        var image = _viewport.GetTexture()?.GetImage();
        foreach (var (node, visible) in _hidden)
        {
            if (IsInstanceValid(node))
            {
                node.Visible = visible;
            }
        }

        QueueFree();
        _done(image is null || image.IsEmpty() ? null : image);
    }
}
