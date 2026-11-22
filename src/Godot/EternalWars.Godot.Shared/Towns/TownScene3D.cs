using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Settings;
using EternalWars.Core.Domain.Towns;
using EternalWars.Godot.Shared.Events;
using EternalWars.Godot.Shared.Map;
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
/// layout with a sky, spread over the part of it the hero shot sees (<c>town_sky.gdshader</c>). Besides the plots the layout shows the town's
/// <see cref="TownScenery"/> (<see cref="TownDressing"/>, ADR-301): streets and houses by the town hall's level, the walls as the siege has them,
/// banners in the owner's colour and the garrison hero's banner on the keep; they follow the owner and the garrison hero too.
/// The camera (TOWN-14, ADR-302) starts at the hero shot each time a town opens; the player turns it up to <see cref="MaxTurnDegrees"/> either way
/// round the vertical through the plots' middle (<see cref="Turn"/>) and zooms in two steps by narrowing its lens, so it never runs into the
/// scenery (<see cref="Zoom"/>); <see cref="FocusOn"/> eases it toward a building whose window has opened, and <see cref="Unfocus"/> back. Every move
/// eases; at rest the camera is exactly the hero shot.
/// Hover and pick (ADR-304): the building under the pointer gets an outline in <see cref="HighlightColour"/>; unused land the town can still build
/// on picks too and shows a translucent ghost of its building while hovered. <see cref="Step"/> walks the pickable plots as a ring round the plots'
/// middle (the controller's way in) and <see cref="PickHovered"/> picks the one reached; <see cref="ShowGhost"/> ("Show in town" in the Hall) keeps
/// a ghost of a level not built yet on its plot, the camera eased toward it, until <see cref="HideGhost"/>. The construction scene (ADR-304): on
/// <see cref="TownBuilt"/> or <see cref="DwellingBuilt"/> for the town shown, the camera eases to the plot while the new building rises out of the
/// ground through its scaffolding in a cloud of dust (with the Capitol, the citizens' homes rise too), then eases back: 1.5 s at normal speed,
/// shorter or longer by <see cref="Speed"/> (ADR-090), none at instant speed; a click cuts it short (<see cref="FinishConstruction"/>).
/// </summary>
public partial class TownScene3D : Node3D
{
    /// <summary>Where the layouts are: one scene per town type.</summary>
    public const string LayoutFolder = "res://shared/scenes/towns/";

    /// <summary>Where the town types' sky paintings are (<c>bg_town_&lt;town&gt;.jpg</c>, ui-element-prompts.md).</summary>
    public const string SkyFolder = "res://shared/textures/ui/backgrounds/towns/";

    /// <summary>How far the player may turn the hero shot either way (TOWN-14).</summary>
    public const float MaxTurnDegrees = 25f;

    /// <summary>How long the construction scene plays at normal speed (TOWN-14).</summary>
    public const double ConstructionSeconds = 1.5;

    private const string SkyShader = "res://shared/shaders/town_sky.gdshader";

    private const string OutlineShader = "res://shared/shaders/town_outline.gdshader";

    /// <summary>The camera's distance from a building it eases toward, as a share of the hero shot's.</summary>
    private const float FocusScale = 0.42f;

    /// <summary>How quickly the camera closes on where it is going: about 90 % of the way in 0.45 s.</summary>
    private const float EaseRate = 5f;

    /// <summary>The lens at each zoom step, as a share of the hero shot's field (the tangent of half its angle): the hero shot and two steps in.</summary>
    private static readonly float[] ZoomLenses = [1f, 0.75f, 0.56f];

    private readonly Camera3D _camera = new() { Name = "TownCamera", Fov = 40, Far = 1500 };
    private readonly Dictionary<string, TownPlot> _plots = [];
    private GameSession _session = null!;
    private Node3D? _layout;
    private TownDressing? _dressing;
    private string? _layoutType;
    private MapObjectId? _town;
    private TownPlot? _hovered;
    private Transform3D _shot = Transform3D.Identity;
    private Vector3 _pivot;
    private float _turn;
    private int _zoom;
    private Vector3? _focus;
    private float _shotFov = 40f;
    private (Vector3 At, float Turn, float Scale, float Lens) _now;
    private Vector2? _pointer;
    private ShaderMaterial? _outline;
    private StandardMaterial3D? _ghostMaterial;
    private TownPlot? _pinned;
    private int _pinnedLevel;
    private readonly List<string> _builds = [];
    private Construction? _construction;

    /// <summary>The construction scene playing: the plot built, what rises (it and, with the Capitol, the homes), the dust, the time.</summary>
    private sealed class Construction(TownPlot plot, IReadOnlyList<TownPlot> rising, CpuParticles3D dust, double seconds)
    {
        public TownPlot Plot { get; } = plot;

        public IReadOnlyList<TownPlot> Rising { get; } = rising;

        public CpuParticles3D Dust { get; } = dust;

        public double Seconds { get; } = seconds;

        public double Elapsed { get; set; }
    }

    /// <summary>The scene was shown (the app hides the adventure world, its light and its camera).</summary>
    public event Action? Opened;

    /// <summary>The scene was taken down (the app shows the adventure world again).</summary>
    public event Action? Closed;

    /// <summary>The pointer came to a shown plot, or left every plot (null).</summary>
    public event Action<PlotInfo?>? BuildingHovered;

    /// <summary>A shown plot, or land the town can still build on, was clicked (or reached by <see cref="Step"/> and picked).</summary>
    public event Action<PlotInfo>? BuildingPicked;

    /// <summary>The construction scene of a new building began (the screen closes the window over it).</summary>
    public event Action<PlotInfo>? ConstructionStarted;

    /// <summary>The colour of the outline round the building under the pointer: the UI's bronze, set by the screen.</summary>
    public Color HighlightColour { get; set; } = new(0.85f, 0.68f, 0.36f);

    /// <summary>How fast the construction scene plays (ADR-090): the own heroes' speed, set by the app.</summary>
    public Func<AnimationSpeed> Speed { get; set; } = () => AnimationSpeed.Normal;

    /// <summary>A construction scene is playing.</summary>
    public bool IsConstructing => _construction is not null;

    /// <summary>A ghost of a building shows on its plot ("Show in town", <see cref="ShowGhost"/>).</summary>
    public bool HasGhost => _pinned is not null;

    public bool IsOpen => _town is not null;

    /// <summary>The town shown, while open.</summary>
    public MapObjectId? Town => _town;

    public Camera3D Camera => _camera;

    /// <summary>
    /// How far the player has turned the hero shot, in degrees: + circles the camera to the right, as E turns the adventure map's; at most
    /// <see cref="MaxTurnDegrees"/> either way.
    /// </summary>
    public float TurnDegrees => _turn;

    /// <summary>The zoom step: 0 is the hero shot, 1 and 2 closer.</summary>
    public int ZoomStep => _zoom;

    /// <summary>The camera is easing toward, or resting at, a building (<see cref="FocusOn"/>).</summary>
    public bool IsFocused => _focus is not null;

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
            .On<TownOwnerChanged>(session.Events)
            .On<HeroEnteredGarrison>(session.Events)
            .On<HeroLeftGarrison>(session.Events)
            .On<TurnStarted>(session.Events);

        // A building put up in the town shown plays its construction scene once the plots show it (ShowPlots, at the end of the frame).
        this.SubscribeWhileInTree<TownBuilt>(session.Events, e => Built(e.Town, e.Building.Value));
        this.SubscribeWhileInTree<DwellingBuilt>(session.Events, e => Built(e.Town, TownOverview.DwellingPlot(e.Line)));
    }

    private void Built(MapObjectId town, string plotId)
    {
        if (town == _town)
        {
            _builds.Add(plotId);
        }
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
            FinishConstruction();
            HideGhost();
            _builds.Clear();
            SetHovered(null);
            ResetCamera();
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

        FinishConstruction();
        HideGhost();
        _builds.Clear();
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
            _dressing = null;
        }
    }

    /// <summary>The pointer is at <paramref name="screenPoint"/> (viewport coordinates), or off the scene (null).</summary>
    public void Hover(Vector2? screenPoint)
    {
        _pointer = screenPoint;
        SetHovered(screenPoint is { } point && IsOpen && _construction is null ? PlotAt(point) : null);
    }

    /// <summary>
    /// Turns the view by <paramref name="degrees"/> (+ circles the camera to the right), within <see cref="MaxTurnDegrees"/> of the hero shot; not
    /// while focused on a building.
    /// </summary>
    public void Turn(float degrees)
    {
        if (IsOpen && _focus is null)
        {
            _turn = Mathf.Clamp(_turn + degrees, -MaxTurnDegrees, MaxTurnDegrees);
        }
    }

    /// <summary>
    /// Zooms <paramref name="steps"/> steps in (+) or out (−), between the hero shot and two steps in; not while focused on a building.
    /// </summary>
    public void Zoom(int steps)
    {
        if (IsOpen && _focus is null)
        {
            _zoom = Math.Clamp(_zoom + steps, 0, ZoomLenses.Length - 1);
        }
    }

    /// <summary>
    /// Eases the camera toward the plot <paramref name="plotId"/> (its <c>Focus</c> marker), keeping the view's turn: the window over it has opened
    /// already and does not wait. False, and nothing moves, for a plot the layout has not or that shows nothing.
    /// </summary>
    public bool FocusOn(string plotId)
    {
        if (!IsOpen || !_plots.TryGetValue(TownOverview.PlotNode(plotId), out var plot) || !plot.IsShown)
        {
            return false;
        }

        _focus = ToLocal(plot.Focus); // the camera stands in this node's space
        return true;
    }

    /// <summary>Eases the camera back to the player's view (its turn and zoom) from a building.</summary>
    public void Unfocus() => _focus = null;

    /// <summary>
    /// "Show in town" (ADR-304): a translucent ghost of the plot <paramref name="plotId"/>'s building at <paramref name="level"/> stands on its plot,
    /// and the camera eases toward it, until <see cref="HideGhost"/>. False, and nothing shows, for a plot the layout has not.
    /// </summary>
    public bool ShowGhost(string plotId, int level)
    {
        if (!IsOpen || !_plots.TryGetValue(TownOverview.PlotNode(plotId), out var plot) || plot.Info is null)
        {
            return false;
        }

        FinishConstruction();
        var old = _pinned;
        (_pinned, _pinnedLevel) = (plot, level);
        Highlight(old);
        Highlight(plot);
        _focus = ToLocal(plot.Focus);
        return true;
    }

    /// <summary>The ghost of <see cref="ShowGhost"/> goes, and the camera eases back.</summary>
    public void HideGhost()
    {
        if (_pinned is not { } old)
        {
            return;
        }

        _pinned = null;
        _focus = null;
        Highlight(old);
    }

    /// <summary>
    /// The controller's ring of plots (ADR-304): the next pickable plot round the plots' middle as the screen shows them, to the right along the
    /// front (+1) or to the left (−1); it becomes the plot under the pointer, outlined, with its name on the status line. False when none can be.
    /// </summary>
    public bool Step(int direction)
    {
        if (!IsOpen || _construction is not null)
        {
            return false;
        }

        var centre = _camera.UnprojectPosition(ToGlobal(_pivot));
        var ring = _plots.Values
            .Where(p => p.IsPickable && !_camera.IsPositionBehind(p.Marker.GlobalPosition))
            .Select(p => (Plot: p, Angle: ScreenAngle(_camera.UnprojectPosition(p.Marker.GlobalPosition) - centre)))
            .OrderBy(p => p.Angle)
            .ToList();
        if (ring.Count == 0)
        {
            return false;
        }

        var index = _hovered is null ? -1 : ring.FindIndex(p => p.Plot == _hovered);
        var next = index >= 0
            ? (index + Math.Sign(direction) + ring.Count) % ring.Count
            : ring.Select((p, i) => (Distance: Mathf.Abs(Mathf.AngleDifference(p.Angle, -Mathf.Pi / 2)), Index: i)).Min().Index; // the front's middle first
        _pointer = null; // the ring, not the pointer, says what is hovered until the pointer moves
        SetHovered(ring[next].Plot);
        return true;
    }

    /// <summary>The angle of a point round the screen's centre as a map draws it: 0 to the right, −90° below (the screen's y runs down).</summary>
    private static float ScreenAngle(Vector2 offset) => Mathf.Atan2(-offset.Y, offset.X);

    /// <summary>Picks the plot under the pointer or reached by <see cref="Step"/> (the controller's A). True when there was one.</summary>
    public bool PickHovered()
    {
        if (_construction is not null)
        {
            FinishConstruction();
            return true;
        }

        if (!IsOpen || _hovered is not { Info: { } info })
        {
            return false;
        }

        BuildingPicked?.Invoke(info);
        return true;
    }

    /// <summary>Cuts the construction scene short: the building stands, the dust goes, the camera eases back.</summary>
    public void FinishConstruction()
    {
        if (_construction is not { } scene)
        {
            return;
        }

        _construction = null;
        foreach (var plot in scene.Rising)
        {
            plot.EndRise();
        }

        if (GodotObject.IsInstanceValid(scene.Dust))
        {
            scene.Dust.Emitting = false;
            var dust = scene.Dust;
            GetTree().CreateTimer(dust.Lifetime).Timeout += () =>
            {
                if (GodotObject.IsInstanceValid(dust))
                {
                    dust.QueueFree(); // the last of the dust settles first
                }
            };
        }

        if (_pointer is { } pointer && IsOpen && _focus is null)
        {
            SetHovered(PlotAt(pointer));
        }
    }

    public override void _Process(double delta)
    {
        if (!IsOpen)
        {
            return;
        }

        if (_construction is { } scene)
        {
            scene.Elapsed += delta;
            var progress = scene.Elapsed / scene.Seconds;
            var rise = Mathf.SmoothStep(0.2f, 0.9f, (float)progress); // the camera arrives first, then the building comes up
            foreach (var plot in scene.Rising)
            {
                plot.Rise(rise);
            }

            if (progress >= 1)
            {
                FinishConstruction();
            }
        }

        (Vector3 At, float Turn, float Scale, float Lens) goal =
            _construction is { } building ? (ToLocal(building.Plot.Focus), _turn, FocusScale, 1f)
            : _focus is { } focus ? (focus, _turn, FocusScale, 1f)
            : (_pivot, _turn, 1f, ZoomLenses[_zoom]);
        if (_now == goal)
        {
            return;
        }

        if (_now.At.DistanceSquaredTo(goal.At) < 1e-4f && Mathf.Abs(_now.Turn - goal.Turn) < 0.01f && Mathf.Abs(_now.Scale - goal.Scale) < 1e-4f
            && Mathf.Abs(_now.Lens - goal.Lens) < 1e-4f)
        {
            _now = goal; // close enough: settle exactly (at rest the hero shot is the hero shot)
        }
        else
        {
            var t = 1f - Mathf.Exp(-EaseRate * (float)delta);
            _now = (_now.At.Lerp(goal.At, t), Mathf.Lerp(_now.Turn, goal.Turn, t), Mathf.Lerp(_now.Scale, goal.Scale, t),
                Mathf.Lerp(_now.Lens, goal.Lens, t));
        }

        PlaceCamera();
        if (_pointer is { } pointer && _focus is null && _construction is null)
        {
            SetHovered(PlotAt(pointer)); // the building under a still pointer changes as the view moves (not under an open window)
        }
    }

    /// <summary>A click at <paramref name="screenPoint"/>: raises <see cref="BuildingPicked"/> for the plot there. True when one was.</summary>
    public bool Pick(Vector2 screenPoint)
    {
        if (_construction is not null)
        {
            FinishConstruction(); // a click cuts the construction scene short
            return true;
        }

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

        var old = _hovered;
        _hovered = plot;
        Highlight(old);
        Highlight(plot);
        BuildingHovered?.Invoke(plot?.Info);
    }

    /// <summary>
    /// The plot's outline and ghost as they should be: the building under the pointer outlined; the land under it, if the town can build there, with
    /// a ghost of its first level; the ghost of <see cref="ShowGhost"/> on its plot.
    /// </summary>
    private void Highlight(TownPlot? plot)
    {
        if (plot is null)
        {
            return;
        }

        var hovered = plot == _hovered;
        plot.Outline(hovered && plot.IsShown ? OutlineMaterial() : null);
        if (plot == _pinned)
        {
            plot.ShowGhost(_pinnedLevel, GhostMaterial());
        }
        else if (hovered && !plot.IsShown && plot.Info is { Buildable: true })
        {
            plot.ShowGhost(1, GhostMaterial());
        }
        else
        {
            plot.HideGhost();
        }
    }

    private ShaderMaterial OutlineMaterial()
    {
        _outline ??= new ShaderMaterial { Shader = GD.Load<Shader>(OutlineShader) };
        _outline.SetShaderParameter("colour", HighlightColour);
        return _outline;
    }

    private StandardMaterial3D GhostMaterial() => _ghostMaterial ??= new StandardMaterial3D
    {
        AlbedoColor = new Color(HighlightColour, 0.35f),
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        EmissionEnabled = false,
    };

    /// <summary>
    /// The construction scene of the plot <paramref name="plotId"/> (ADR-304), when the speed setting plays one: the building sinks into the ground and
    /// rises through its scaffolding in dust while the camera eases to it; with the Capitol, the citizens' homes rise as well.
    /// </summary>
    private void StartConstruction(string plotId)
    {
        var seconds = SpeedSettings.Seconds(Speed(), ConstructionSeconds);
        if (seconds <= 0 || !_plots.TryGetValue(TownOverview.PlotNode(plotId), out var plot) || plot is not { IsShown: true, Info: { } info })
        {
            return;
        }

        FinishConstruction();
        HideGhost();
        SetHovered(null);
        var rising = new List<TownPlot>();
        if (plot.BeginRise())
        {
            rising.Add(plot);
        }

        if (info.Building.Value == BuiltInBuildings.TownHall && info.Level >= BuiltInBuildings.CapitolLevel)
        {
            rising.AddRange(_plots.Values.Where(p => p.Info?.State == PlotState.CitizensHomes && p.BeginRise()));
        }

        _construction = new Construction(plot, rising, Dust(plot, seconds), seconds);
        ConstructionStarted?.Invoke(info);
    }

    private static Texture2D? _puff;

    /// <summary>The construction scene's dust: soft puffs rising round the plot's foot while the building comes up.</summary>
    private static CpuParticles3D Dust(TownPlot plot, double seconds)
    {
        _puff ??= new GradientTexture2D
        {
            Width = 64,
            Height = 64,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo = new Vector2(0.5f, 0f),
            Gradient = new Gradient { Colors = [Colors.White, new Color(1, 1, 1, 0)], Offsets = [0f, 1f] },
        };
        var bounds = plot.ShownBounds;
        var dust = new CpuParticles3D
        {
            Name = "Dust",
            Amount = 96,
            Lifetime = Math.Clamp(seconds * 0.8, 0.5, 1.6),
            Explosiveness = 0.15f,
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3((bounds.Size.X / 2) + 0.6f, 0.2f, (bounds.Size.Z / 2) + 0.6f),
            Direction = Vector3.Up,
            Spread = 75,
            InitialVelocityMin = 1.5f,
            InitialVelocityMax = 4f,
            Gravity = new Vector3(0, -0.6f, 0),
            ScaleAmountMin = 2f,
            ScaleAmountMax = 4f,
            Color = new Color(0.78f, 0.71f, 0.6f, 0.85f),
            ColorRamp = new Gradient { Colors = [Colors.White, new Color(1, 1, 1, 0)], Offsets = [0.35f, 1f] },
            Position = new Vector3(bounds.GetCenter().X, 0.3f, bounds.GetCenter().Z),
            Mesh = new QuadMesh
            {
                Size = new Vector2(1.2f, 1.2f),
                Material = new StandardMaterial3D
                {
                    AlbedoTexture = _puff,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
                    BillboardKeepScale = true,
                    VertexColorUseAsAlbedo = true,
                },
            },
            Emitting = true,
        };
        plot.Marker.AddChild(dust);
        return dust;
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

        var colour = town.Owner is { } owner ? Palette.ForPlayer(_session.State, owner) : Palette.Neutral;
        _dressing?.Show(TownScenery.Of(_session.State, town), colour, _plots.GetValueOrDefault(TownOverview.PlotNode(BuiltInBuildings.TownHall)));
        if (_pinned?.Info is { } pinned && pinned.Level >= _pinnedLevel)
        {
            HideGhost(); // what the ghost showed stands now
        }

        if (_hovered is { IsPickable: false })
        {
            SetHovered(null);
        }

        foreach (var plot in _plots.Values)
        {
            Highlight(plot); // a plot shown anew lost its outline and ghost
        }

        if (_builds.Count > 0)
        {
            var built = _builds[^1]; // one building a day
            _builds.Clear();
            StartConstruction(built);
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

        // The layout is not in the tree yet: its nodes' places in this node's space (the camera's) are their transforms up to the layout's.
        Vector3? middle = _plots.Count == 0 ? null : _plots.Values.Aggregate(Vector3.Zero, (sum, p) => sum + InLayout(p.Marker).Origin) / _plots.Count;
        if (_layout.GetNodeOrNull<Camera3D>("Camera") is { } shot)
        {
            (_shot, _shotFov, _camera.Near, _camera.Far) = (InLayout(shot), shot.Fov, shot.Near, shot.Far);
            _layout.RemoveChild(shot);
            shot.Free();
        }
        else
        {
            var centre = middle ?? Vector3.Zero;
            (_shot, _shotFov) = (new Transform3D(Basis.Identity, centre + new Vector3(0, 22, 48)).LookingAt(centre, Vector3.Up), 40f);
        }

        _pivot = middle ?? PivotOf(_shot); // the camera turns round the plots' middle
        ResetCamera();

        if (_layout.FindChildren("*", nameof(WorldEnvironment), recursive: false, owned: false).Count == 0)
        {
            _layout.AddChild(DefaultEnvironment());
        }

        if (_layout.FindChildren("*", nameof(DirectionalLight3D), recursive: false, owned: false).Count == 0)
        {
            _layout.AddChild(new DirectionalLight3D { Name = "Sun", RotationDegrees = new Vector3(-50, -35, 0), ShadowEnabled = true, LightEnergy = 1.3f });
        }

        _dressing = new TownDressing(_layout, townType);
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
        var forward = -_shot.Basis.Z;
        var material = new ShaderMaterial { Shader = GD.Load<Shader>(SkyShader) };
        material.SetShaderParameter("picture", GD.Load<Texture2D>(SkyPath(townType)));
        material.SetShaderParameter("azimuth", Mathf.Atan2(forward.X, -forward.Z));
        material.SetShaderParameter("span", (float)_layout.GetMeta("sky_span", 2.1f));
        material.SetShaderParameter("horizon", (float)_layout.GetMeta("sky_horizon", 0.35f));
        material.SetShaderParameter("above", above);
        environment.Sky = new Sky { SkyMaterial = material };
        world.Environment = environment;
    }

    /// <summary><paramref name="node"/>'s transform in this node's space, through its parents up to and with the layout's own (out of the tree or not).</summary>
    private Transform3D InLayout(Node3D node)
    {
        var transform = node.Transform;
        for (var parent = node.GetParent(); parent is not null && parent != this; parent = parent.GetParent())
        {
            if (parent is Node3D spatial)
            {
                transform = spatial.Transform * transform;
            }

            if (parent == _layout)
            {
                break;
            }
        }

        return transform;
    }

    /// <summary>Back to the hero shot at once: no turn, no zoom, no building.</summary>
    private void ResetCamera()
    {
        (_turn, _zoom, _focus) = (0, 0, null);
        _now = (_pivot, 0, 1, 1);
        PlaceCamera();
    }

    /// <summary>
    /// The camera where it is now: the hero shot turned by <c>_now.Turn</c> round the vertical through the plots' middle (which stays where it is on
    /// the screen), moved to <c>_now.At</c> from <c>_now.Scale</c> of its distance, its lens narrowed to <c>_now.Lens</c>. The hero shot's own
    /// direction is kept, so at rest it is the hero shot exactly.
    /// </summary>
    private void PlaceCamera()
    {
        var turn = new Basis(Vector3.Up, Mathf.DegToRad(_now.Turn)); // + circles the camera to the right, as E turns the adventure map's camera
        _camera.Transform = new Transform3D(turn * _shot.Basis, _now.At + (turn * (_shot.Origin - _pivot) * _now.Scale));
        _camera.Fov = Mathf.RadToDeg(2 * Mathf.Atan(Mathf.Tan(Mathf.DegToRad(_shotFov) / 2) * _now.Lens));
    }

    /// <summary>Where the hero shot of a layout without plots looks: its ray's meeting with the ground, or 40 m ahead for a shot that does not look down.</summary>
    private static Vector3 PivotOf(Transform3D shot)
    {
        var forward = -shot.Basis.Z;
        return forward.Y < -0.05f ? shot.Origin + (forward * (-shot.Origin.Y / forward.Y)) : shot.Origin + (forward * 40f);
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
