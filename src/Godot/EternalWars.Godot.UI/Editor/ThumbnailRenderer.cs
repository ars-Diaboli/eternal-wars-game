using EternalWars.Core.Domain.Objects;
using EternalWars.Godot.Shared.Map;
using Godot;

namespace EternalWars.Godot.UI.Editor;

/// <summary>
/// The pictures of map objects in the editor's browser (EDIT-1, ADR-254): each model (<see cref="PlaceholderModel"/>) is rendered
/// once in a hidden <see cref="SubViewport"/> and kept as a PNG file named by the model's hash, so the next run only reads it and a
/// changed model gets a new picture. Rendering takes a few frames: <see cref="Get"/> answers at once when the picture is known and
/// calls back when it is not. Several models are rendered side by side, each in a viewport with a world of its own.
/// </summary>
public partial class ThumbnailRenderer : Node
{
    /// <summary>A picture's side in pixels; the browser shows it smaller in its list and at this size on its card.</summary>
    public const int Size = 96;

    /// <summary>Part of every file name: raise it when the camera, the light or the size changes, and the old files are not read any more.</summary>
    private const string Look = "v1";

    /// <summary>How many models are rendered at a time.</summary>
    private const int Stages = 6;

    /// <summary>The frames a stage waits for its picture: the viewport draws at the end of the frame it was asked in.</summary>
    private const int FramesToDraw = 2;

    /// <summary>From the south-east and above, as the map is mostly looked at.</summary>
    private static readonly Vector3 ViewFrom = new Vector3(0.55f, 1f, 1f).Normalized();

    /// <summary>The pictures read or rendered in this run, by hash: they outlive a browser when another map is opened.</summary>
    private static readonly Dictionary<string, Texture2D> Known = [];

    private readonly List<Stage> _stages = [];
    private readonly Queue<string> _waiting = new();
    private readonly Dictionary<string, Job> _jobs = [];

    /// <summary>The models that could not be rendered in this run (no display): not asked for again.</summary>
    private readonly HashSet<string> _failed = [];
    private string? _folder;

    /// <summary>The pictures are kept in <paramref name="folder"/> (made when the first is written); null keeps none on disk.</summary>
    public void Bind(string? folder) => _folder = folder;

    /// <summary>How many pictures are being rendered or wait for it.</summary>
    public int Pending => _jobs.Count;

    /// <summary>The name of <paramref name="definition"/>'s picture: definitions drawn alike share it; null when there is no model to draw.</summary>
    public static string? KeyOf(MapObjectDefinition definition)
    {
        var parts = PlaceholderModel.Of(definition, definition.Footprint);
        return parts.Count == 0 ? null : $"{PlaceholderModel.Hash(parts)}-{Look}-{Size}";
    }

    /// <summary>
    /// The picture of <paramref name="definition"/>'s model, or null when it has to be rendered first: <paramref name="whenReady"/> is
    /// then called with it some frames later (not at all where nothing can be rendered, as without a display).
    /// </summary>
    public Texture2D? Get(MapObjectDefinition definition, Action<Texture2D> whenReady)
    {
        if (KeyOf(definition) is not { } hash || _failed.Contains(hash))
        {
            return null;
        }

        if (Known.TryGetValue(hash, out var known))
        {
            return known;
        }

        if (Read(hash) is { } read)
        {
            return Known[hash] = read;
        }

        if (!_jobs.TryGetValue(hash, out var job))
        {
            _jobs[hash] = job = new Job(PlaceholderModel.Of(definition, definition.Footprint));
            _waiting.Enqueue(hash);
        }

        job.WhenReady.Add(whenReady);
        return null;
    }

    public override void _Process(double delta)
    {
        if (_jobs.Count == 0)
        {
            return;
        }

        while (_stages.Count < Stages)
        {
            var stage = new Stage();
            AddChild(stage.Viewport);
            _stages.Add(stage);
        }

        foreach (var stage in _stages)
        {
            if (stage.Hash is { } hash && ++stage.Frames >= FramesToDraw)
            {
                Finish(hash, stage.Take());
            }

            if (stage.Hash is null && _waiting.TryDequeue(out var next))
            {
                stage.Show(next, _jobs[next].Parts);
            }
        }
    }

    private void Finish(string hash, Image? image)
    {
        _jobs.Remove(hash, out var job);
        if (image is null || image.IsEmpty() || job is null)
        {
            _failed.Add(hash); // nothing was drawn (no display): the placeholder stays
            return;
        }

        var texture = ImageTexture.CreateFromImage(image);
        Known[hash] = texture;
        Write(hash, image);
        foreach (var ready in job.WhenReady)
        {
            ready(texture);
        }
    }

    private Texture2D? Read(string hash)
    {
        if (_folder is null || !File.Exists(PathOf(hash)))
        {
            return null;
        }

        var image = new Image();
        return image.Load(PathOf(hash)) == Error.Ok && image.GetWidth() == Size && image.GetHeight() == Size
            ? ImageTexture.CreateFromImage(image)
            : null; // a broken file is rendered again
    }

    private void Write(string hash, Image image)
    {
        if (_folder is null)
        {
            return;
        }

        // Written beside its place and then moved there, so another editor never reads half a file.
        var path = PathOf(hash);
        var partial = path + ".part.png";
        try
        {
            Directory.CreateDirectory(_folder);
            if (image.SavePng(partial) is not Error.Ok and var error)
            {
                GD.PushWarning($"Thumbnail not kept: {error}"); // it is rendered again next time
                return;
            }

            File.Move(partial, path, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            GD.PushWarning($"Thumbnail not kept: {e.Message}");
        }
    }

    private string PathOf(string hash) => Path.Combine(_folder!, hash + ".png");

    private sealed class Job(IReadOnlyList<ModelPart> parts)
    {
        public IReadOnlyList<ModelPart> Parts { get; } = parts;

        public List<Action<Texture2D>> WhenReady { get; } = [];
    }

    /// <summary>One hidden viewport with its camera and light; it shows a model, and some frames later its picture is taken.</summary>
    private sealed class Stage
    {
        private readonly Camera3D _camera = new() { Projection = Camera3D.ProjectionType.Orthogonal, Near = 0.1f, Far = 100f };
        private Node3D? _model;

        public Stage()
        {
            Viewport = new SubViewport
            {
                Size = new Vector2I(ThumbnailRenderer.Size, ThumbnailRenderer.Size), OwnWorld3D = true, TransparentBg = true,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled, Msaa3D = global::Godot.Viewport.Msaa.Msaa4X,
            };
            Viewport.AddChild(_camera);
            Viewport.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-50, -35, 0), LightEnergy = 1.1f });
            Viewport.AddChild(new WorldEnvironment
            {
                Environment = new global::Godot.Environment
                {
                    BackgroundMode = global::Godot.Environment.BGMode.ClearColor,
                    AmbientLightSource = global::Godot.Environment.AmbientSource.Color,
                    AmbientLightColor = Colors.White,
                    AmbientLightEnergy = 0.55f,
                },
            });
        }

        public SubViewport Viewport { get; }

        /// <summary>The model being drawn; null when the stage is free.</summary>
        public string? Hash { get; private set; }

        public int Frames { get; set; }

        public void Show(string hash, IReadOnlyList<ModelPart> parts)
        {
            (Hash, Frames) = (hash, 0);
            _model = PlaceholderModel.Build(parts);
            Viewport.AddChild(_model);

            // The box around the model, then the camera far enough along its line of sight, wide enough to hold every corner.
            var low = parts.Select(p => p.Position - p.HalfExtent).Aggregate((a, b) => a.Min(b));
            var high = parts.Select(p => p.Position + p.HalfExtent).Aggregate((a, b) => a.Max(b));
            var (centre, half) = ((low + high) / 2, (high - low) / 2);
            _camera.Position = centre + (ViewFrom * 40f);
            _camera.LookAt(centre, Vector3.Up);
            var basis = _camera.GlobalBasis;
            var reach = 0f;
            for (var corner = 0; corner < 8; corner++)
            {
                var offset = new Vector3((corner & 1) == 0 ? -half.X : half.X, (corner & 2) == 0 ? -half.Y : half.Y, (corner & 4) == 0 ? -half.Z : half.Z);
                reach = Mathf.Max(reach, Mathf.Max(Mathf.Abs(offset.Dot(basis.X)), Mathf.Abs(offset.Dot(basis.Y))));
            }

            _camera.Size = Mathf.Max(reach * 2.15f, 0.5f);
            Viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
        }

        /// <summary>The picture of the model shown; the stage is free again.</summary>
        public Image? Take()
        {
            var image = Viewport.GetTexture()?.GetImage();
            if (_model is not null)
            {
                Viewport.RemoveChild(_model);
                _model.QueueFree();
            }

            (_model, Hash) = (null, null);
            return image;
        }
    }
}
