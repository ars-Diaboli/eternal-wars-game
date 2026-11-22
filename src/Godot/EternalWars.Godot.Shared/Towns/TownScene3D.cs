using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Towns;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.Shared.Towns;

/// <summary>
/// The town screen's 3D scene (the town screen plan's step 3, ADR-297): a town's layout (<c>scenes/towns/&lt;town&gt;.tscn</c>) with its own camera,
/// light and <see cref="WorldEnvironment"/>, shown behind the town screen's bars while that screen is open; the game app hides the adventure world
/// meanwhile, as it does for a battle (<see cref="Opened"/>, <see cref="Closed"/>). The layout's <c>Marker3D</c> plots are named after
/// <see cref="TownOverview.PlotNode"/>; each shows its building as the town's <see cref="TownOverview"/> has it (<see cref="TownPlot"/>). The layout's
/// <c>Camera</c> is the hero shot; a layout without its own environment, sun or camera gets defaults. Input is mapped by the screen that holds it:
/// <see cref="Hover"/> raises <see cref="BuildingHovered"/>, <see cref="Pick"/> raises <see cref="BuildingPicked"/>. The plots follow
/// <see cref="TownBuilt"/>, <see cref="DwellingBuilt"/>, <see cref="TownBuildingDowngraded"/> and the new day while it is open. One layout stays
/// loaded, out of the tree while the scene is closed. A layout missing a plot for one of the town type's buildings or lines reports an error
/// when it loads (smoke <c>game-town-layouts</c>). The town type's painting (<c>bg_town_&lt;town&gt;</c>), when there is one, is the sky of a
/// layout with a sky, spread over the part of it the hero shot sees (<c>town_sky.gdshader</c>).
/// </summary>
public partial class TownScene3D : Node3D
{
    /// <summary>Where the layouts are: one scene per town type.</summary>
    public const string LayoutFolder = "res://shared/scenes/towns/";

    /// <summary>Where the town types' sky paintings are (<c>bg_town_&lt;town&gt;.jpg</c>, ui-element-prompts.md).</summary>
    public const string SkyFolder = "res://shared/textures/ui/backgrounds/towns/";

    private const string SkyShader = "res://shared/shaders/town_sky.gdshader";

    private readonly Camera3D _camera = new() { Name = "TownCamera", Fov = 40, Far = 1500 };
    private readonly Dictionary<string, TownPlot> _plots = [];
    private GameSession _session = null!;
    private Node3D? _layout;
    private string? _layoutType;
    private MapObjectId? _town;
    private TownPlot? _hovered;

    /// <summary>The scene was shown (the app hides the adventure world, its light and its camera).</summary>
    public event Action? Opened;

    /// <summary>The scene was taken down (the app shows the adventure world again).</summary>
    public event Action? Closed;

    /// <summary>The pointer came to a shown plot, or left every plot (null).</summary>
    public event Action<PlotInfo?>? BuildingHovered;

    /// <summary>A shown plot was clicked.</summary>
    public event Action<PlotInfo>? BuildingPicked;

    public bool IsOpen => _town is not null;

    /// <summary>The town shown, while open.</summary>
    public MapObjectId? Town => _town;

    public Camera3D Camera => _camera;

    /// <summary>The plot under the pointer, if any.</summary>
    public PlotInfo? Hovered => _hovered?.Info;

    /// <summary>The plots of the layout shown, as the town has them.</summary>
    public IEnumerable<PlotInfo> Plots => _plots.Values.Where(p => p.Info is not null).Select(p => p.Info!);

    /// <summary>Whether a layout exists for the town type: the town screen shows the key painting for one without.</summary>
    public static bool HasLayout(string townType) => ResourceLoader.Exists(LayoutPath(townType));

    public static string LayoutPath(string townType) => $"{LayoutFolder}{townType}.tscn";

    public static string SkyPath(string townType) => $"{SkyFolder}bg_town_{townType}.jpg";

    public void Bind(GameSession session)
    {
        _session = session;
        AddChild(_camera);
        Visible = false;
        new RefreshScheduler(this, ShowPlots)
            .On<TownBuilt>(session.Events)
            .On<DwellingBuilt>(session.Events)
            .On<TownBuildingDowngraded>(session.Events)
            .On<TurnStarted>(session.Events);
    }

    /// <summary>
    /// Shows <paramref name="town"/> (or brings its plots up to date when it is shown already). False, and closed, when the town is gone or its
    /// type has no layout.
    /// </summary>
    public bool Open(MapObjectId town)
    {
        if (_session.State.FindObject(town) is not Town shown || !HasLayout(shown.TownType))
        {
            Close();
            return false;
        }

        var wasOpen = IsOpen;
        if (_layoutType != shown.TownType)
        {
            Load(shown.TownType);
            CheckPlots(shown);
        }
        else if (_layout!.GetParent() is null)
        {
            AddChild(_layout);
        }

        if (_town != town)
        {
            SetHovered(null);
        }

        _town = town;
        Visible = true;
        ShowPlots();
        _camera.MakeCurrent();
        if (!wasOpen)
        {
            Opened?.Invoke();
        }

        return true;
    }

    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }

        _town = null;
        SetHovered(null);
        Visible = false;
        if (_layout?.GetParent() == this)
        {
            RemoveChild(_layout); // its environment and lights leave with it
        }

        Closed?.Invoke();
    }

    public override void _ExitTree()
    {
        if (_layout is not null && _layout.GetParent() is null)
        {
            _layout.Free(); // the kept layout is out of the tree while closed: it goes with the scene (nothing is freed in a queue at exit)
            (_layout, _layoutType) = (null, null);
            _plots.Clear();
        }
    }

    /// <summary>The pointer is at <paramref name="screenPoint"/> (viewport coordinates), or off the scene (null).</summary>
    public void Hover(Vector2? screenPoint) => SetHovered(screenPoint is { } point && IsOpen ? PlotAt(point) : null);

    /// <summary>A click at <paramref name="screenPoint"/>: raises <see cref="BuildingPicked"/> for the plot there. True when one was.</summary>
    public bool Pick(Vector2 screenPoint)
    {
        if (!IsOpen || PlotAt(screenPoint) is not { Info: { } info })
        {
            return false;
        }

        BuildingPicked?.Invoke(info);
        return true;
    }

    /// <summary>Where the camera looks at a plot (its <c>Focus</c> marker), or null for a plot the layout has not.</summary>
    public Vector3? FocusOf(string plotId) =>
        IsOpen && _plots.TryGetValue(TownOverview.PlotNode(plotId), out var plot) ? plot.Focus : null; // the layout is out of the tree while closed

    /// <summary>The nearest shown plot whose hover shape the ray through <paramref name="screenPoint"/> meets.</summary>
    private TownPlot? PlotAt(Vector2 screenPoint)
    {
        var origin = _camera.ProjectRayOrigin(screenPoint);
        var direction = _camera.ProjectRayNormal(screenPoint);
        TownPlot? nearest = null;
        var best = float.MaxValue;
        foreach (var plot in _plots.Values)
        {
            if (plot.Hit(origin, direction) is { } distance && distance < best)
            {
                (nearest, best) = (plot, distance);
            }
        }

        return nearest;
    }

    private void SetHovered(TownPlot? plot)
    {
        if (plot == _hovered)
        {
            return;
        }

        _hovered = plot;
        BuildingHovered?.Invoke(plot?.Info);
    }

    /// <summary>Each plot as the town has it now.</summary>
    private void ShowPlots()
    {
        if (_town is not { } id || _session.State.FindObject(id) is not Town town)
        {
            return;
        }

        var catalog = TownRules.Buildings(_session.State, town);
        foreach (var info in TownOverview.PlotsOf(_session.State, town))
        {
            if (_plots.TryGetValue(TownOverview.PlotNode(info.Id), out var plot))
            {
                var tier = info.Line is { } line ? _session.State.Creatures.Get(line).Tier : (int?)null;
                plot.Show(info, catalog.KindOf(info.Building), tier, town.TownType);
            }
        }

        if (_hovered is { IsShown: false })
        {
            SetHovered(null);
        }
    }

    /// <summary>Loads the layout of <paramref name="townType"/> in place of the one loaded; its camera becomes the hero shot.</summary>
    private void Load(string townType)
    {
        if (_layout is not null)
        {
            if (_layout.GetParent() == this)
            {
                RemoveChild(_layout);
            }

            _layout.QueueFree();
        }

        SetHovered(null); // the old layout's building leaves the status bar and the cursor
        _plots.Clear();
        _layout = GD.Load<PackedScene>(LayoutPath(townType)).Instantiate<Node3D>();
        _layoutType = townType;
        foreach (var marker in _layout.FindChildren(TownOverview.PlotNodePrefix + "*", nameof(Marker3D), recursive: true, owned: false).OfType<Marker3D>())
        {
            _plots[marker.Name] = new TownPlot(marker);
        }

        if (_layout.GetNodeOrNull<Camera3D>("Camera") is { } shot)
        {
            (_camera.Transform, _camera.Fov, _camera.Near, _camera.Far) = (shot.Transform, shot.Fov, shot.Near, shot.Far);
            _layout.RemoveChild(shot);
            shot.Free();
        }
        else
        {
            var centre = _plots.Count == 0 ? Vector3.Zero : _plots.Values.Aggregate(Vector3.Zero, (sum, p) => sum + p.Marker.Position) / _plots.Count;
            _camera.Transform = new Transform3D(Basis.Identity, centre + new Vector3(0, 22, 48)).LookingAt(centre, Vector3.Up);
        }

        if (_layout.FindChildren("*", nameof(WorldEnvironment), recursive: false, owned: false).Count == 0)
        {
            _layout.AddChild(DefaultEnvironment());
        }

        if (_layout.FindChildren("*", nameof(DirectionalLight3D), recursive: false, owned: false).Count == 0)
        {
            _layout.AddChild(new DirectionalLight3D { Name = "Sun", RotationDegrees = new Vector3(-50, -35, 0), ShadowEnabled = true, LightEnergy = 1.3f });
        }

        PaintSky(townType);
        AddChild(_layout);
    }

    /// <summary>Reports the town type's buildings and lines the layout has no plot for (the core test checks the layouts against a default town).</summary>
    private void CheckPlots(Town town)
    {
        var missing = TownOverview.PlotsOf(_session.State, town).Select(p => TownOverview.PlotNode(p.Id)).Where(n => !_plots.ContainsKey(n)).ToList();
        if (missing.Count > 0)
        {
            GD.PushError($"{LayoutPath(town.TownType)} has no plot for: {string.Join(", ", missing)}");
        }
    }

    /// <summary>
    /// The town type's painting as the layout's sky, when both exist: the middle of the painting where the hero shot looks, its horizon on the
    /// horizon (the layout's <c>sky_span</c> and <c>sky_horizon</c> metadata: the painting's width in radians, its horizon from the bottom).
    /// </summary>
    private void PaintSky(string townType)
    {
        if (!ResourceLoader.Exists(SkyPath(townType))
            || _layout!.FindChildren("*", nameof(WorldEnvironment), recursive: false, owned: false).OfType<WorldEnvironment>().FirstOrDefault() is not { Environment: { } shared } world
            || shared.BackgroundMode != global::Godot.Environment.BGMode.Sky)
        {
            return;
        }

        var environment = (global::Godot.Environment)shared.Duplicate(); // the loaded scene's own environment stays as authored
        var above = (shared.Sky?.SkyMaterial as ProceduralSkyMaterial)?.SkyTopColor ?? new Color(0.3f, 0.45f, 0.7f);
        var forward = -_camera.Transform.Basis.Z;
        var material = new ShaderMaterial { Shader = GD.Load<Shader>(SkyShader) };
        material.SetShaderParameter("picture", GD.Load<Texture2D>(SkyPath(townType)));
        material.SetShaderParameter("azimuth", Mathf.Atan2(forward.X, -forward.Z));
        material.SetShaderParameter("span", (float)_layout.GetMeta("sky_span", 2.1f));
        material.SetShaderParameter("horizon", (float)_layout.GetMeta("sky_horizon", 0.35f));
        material.SetShaderParameter("above", above);
        environment.Sky = new Sky { SkyMaterial = material };
        world.Environment = environment;
    }

    private static WorldEnvironment DefaultEnvironment() => new()
    {
        Name = "Environment",
        Environment = new global::Godot.Environment
        {
            BackgroundMode = global::Godot.Environment.BGMode.Sky,
            Sky = new Sky { SkyMaterial = new ProceduralSkyMaterial() },
            AmbientLightSource = global::Godot.Environment.AmbientSource.Sky,
            TonemapMode = global::Godot.Environment.ToneMapper.Filmic,
        },
    };
}
