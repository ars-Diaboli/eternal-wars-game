using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Pathfinding;
using EternalWars.Godot.Shared.Controls;
using Godot;

namespace EternalWars.Godot.Shared.Map;

/// <summary>
/// HoMM-style route preview (ADR-309): one marker per step coloured by the day it is reached (<see cref="Palette.ForPathDay"/>), a bigger one
/// at the end, over the end a mark of what the hero does there (attack, visit, pick up, enter a town, meet a hero) when the caller gives
/// one, and a "Day N" tag at the start of the route's first two days and its last when the caller gives today's date.
/// </summary>
public partial class PathPreview : Node3D
{
    /// <summary>How wide the end's mark is, in map units (a cell is one).</summary>
    private const float EndMarkWidth = 1.2f;

    /// <summary>How high the end's mark floats over the ground, to its middle.</summary>
    private const float EndMarkHeight = 1.0f;

    /// <summary>How high a day's tag floats over its step.</summary>
    private const float TagHeight = 0.45f;

    private readonly MeshCache _meshes = new();
    private AdventureMap? _map;

    public HeroPath? Path { get; private set; }

    /// <summary>A route was shown (the town scene starts loading the town it ends at, ADR-306).</summary>
    public event Action<HeroPath>? Shown;

    /// <summary>The route was taken away (the Move button greys).</summary>
    public event Action? Cleared;

    /// <summary>The map whose ground the markers sit on.</summary>
    public void Bind(AdventureMap map) => _map = map;

    /// <summary>
    /// Shows <paramref name="path"/>, with <paramref name="endMark"/> (null: none) floating over its last step and, given
    /// <paramref name="today"/>, the days' tags.
    /// </summary>
    public void Show(HeroPath path, Texture2D? endMark = null, GameDate? today = null)
    {
        Clear();
        Path = path;
        foreach (var step in path.Steps)
        {
            var last = step == path.Steps[^1];
            var size = last ? 0.35f : 0.16f;
            AddChild(new MeshInstance3D
            {
                Mesh = _meshes.Cylinder(size, size, 0.04f, Palette.ForPathDay(step.Day), unshaded: true), // one per size and day colour
                Transform = _map is null
                    ? new Transform3D(Basis.Identity, MapSpace.ToWorld(step.Position) + (Vector3.Up * 0.05f))
                    : new Transform3D(MapSpace.OnSlope(_map, step.Position), MapSpace.ToGround(_map, step.Position) + (MapSpace.GroundNormal(_map, step.Position) * 0.05f)),
            });
        }

        if (today is { } date)
        {
            foreach (var tag in RouteDays.Tags(path, date))
            {
                var at = path.Steps[tag.Step].Position;
                AddChild(new Label3D
                {
                    Text = tag.Text,
                    FontSize = 64,
                    OutlineSize = 14,
                    PixelSize = 0.008f,
                    Modulate = Palette.ForPathDay(tag.Day),
                    OutlineModulate = new Color(0, 0, 0, 0.85f),
                    Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                    NoDepthTest = true,
                    Position = (_map is null ? MapSpace.ToWorld(at) : MapSpace.ToGround(_map, at)) + (Vector3.Up * TagHeight),
                });
            }
        }

        if (endMark is not null)
        {
            var end = path.Steps[^1].Position;
            var ground = _map is null ? MapSpace.ToWorld(end) : MapSpace.ToGround(_map, end);
            AddChild(new Sprite3D
            {
                Texture = endMark,
                PixelSize = EndMarkWidth / endMark.GetWidth(),
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                Shaded = false,
                NoDepthTest = true, // over the monster, the hero or the object standing there
                TextureFilter = BaseMaterial3D.TextureFilterEnum.Linear,
                Position = ground + (Vector3.Up * EndMarkHeight),
            });
        }

        Shown?.Invoke(path);
    }

    public void Clear()
    {
        var had = Path is not null;
        Path = null;
        this.ClearChildren();
        if (had)
        {
            Cleared?.Invoke();
        }
    }
}
