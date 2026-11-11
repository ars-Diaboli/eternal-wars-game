using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Settings;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.Shared.Map;

/// <summary>
/// Fog of war over one level for the context's viewer (ADR-176): a full-screen pass (<c>shaders/fog_of_war.gdshader</c>) that reads a fog mask,
/// one texel a tile. Unexplored tiles are hidden (a starry void or clouds, <see cref="MapViewContext.FogLook"/>); explored tiles out of sight
/// (refog mode) are veiled. Hidden entirely when there is no viewer (editor).
/// </summary>
/// <remarks>
/// The mask fades instead of switching: a tile the viewer's hero reveals starts opening when the hero's step starts to play (the core reveals it at
/// once, the views show the walk later), so the fog rolls back as the hero walks. A large reveal (a captured town, a Crypt, a spell) opens as a
/// wave from its middle. A new viewer or level snaps the mask without fading. Rules keep their cell-by-cell visibility; this is drawing only.
/// </remarks>
public partial class FogView : Node3D
{
    private const string ShaderPath = "res://shared/shaders/fog_of_war.gdshader";
    private const string TextureFolder = "res://shared/textures/fog/";

    /// <summary>How long a tile takes to open; how long a tile takes to be veiled or unveiled when it leaves or enters sight.</summary>
    private const double RevealSeconds = 0.9;
    private const double SightSeconds = 0.5;

    /// <summary>A large reveal opens outwards from its middle at this many seconds a tile.</summary>
    private const double WaveSecondsPerTile = 0.035;
    private const int WaveFromTiles = 30;

    private MapViewContext _context = null!;
    private int _level;
    private int _width;
    private int _height;
    private Image _image = null!;
    private ImageTexture _texture = null!;
    private ShaderMaterial _material = null!;
    private MeshInstance3D _screen = null!;

    // Per texel of the mask (map size plus a hidden border of one): shown and wanted amounts, and when a fade may start.
    private byte[] _bytes = [];
    private float[] _hidden = [];
    private float[] _hiddenTarget = [];
    private float[] _unseen = [];
    private float[] _unseenTarget = [];
    private double[] _startAt = [];
    private readonly HashSet<int> _fading = [];

    public void Bind(MapViewContext context, int level)
    {
        _context = context;
        _level = level;
        var map = context.State.Map;
        _width = map.Width + 2;
        _height = map.Height + 2;
        var count = _width * _height;
        _bytes = new byte[count * 2];
        _hidden = new float[count];
        _hiddenTarget = new float[count];
        _unseen = new float[count];
        _unseenTarget = new float[count];
        _startAt = new double[count];
        Array.Fill(_hidden, 1f);
        Array.Fill(_hiddenTarget, 1f);
        WriteAll();
        _image = Image.CreateFromData(_width, _height, false, Image.Format.Rg8, _bytes);
        _texture = ImageTexture.CreateFromImage(_image);

        _material = new ShaderMaterial { Shader = GD.Load<Shader>(ShaderPath) };
        _material.SetShaderParameter("fog_mask", _texture);
        _material.SetShaderParameter("mask_size", new Vector2(_width, _height));
        _material.SetShaderParameter("ground_y", MapSpace.ToWorld(new MapPosition(0, 0, level)).Y);
        SetOptionalTexture("nebula");
        SetOptionalTexture("cloud", "clouds");
        _screen = new MeshInstance3D
        {
            Name = "FogPass",
            Mesh = new QuadMesh { Size = new Vector2(2, 2) },
            MaterialOverride = _material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            // Drawn over the whole screen whatever the camera sees: never culled.
            ExtraCullMargin = 16384f,
        };
        AddChild(_screen);
        ApplyLook();
        Snap();

        context.Changed += Snap;
        context.FogLookChanged += ApplyLook;
        TreeExiting += () =>
        {
            context.Changed -= Snap;
            context.FogLookChanged -= ApplyLook;
        };
        this.SubscribeWhileInTree<TilesRevealed>(context.Events, e =>
        {
            if (e.Player == _context.Viewer)
            {
                Reveal(e.Positions);
            }
        });
        this.SubscribeWhileInTree<SightChanged>(context.Events, e =>
        {
            if (e.Player == _context.Viewer)
            {
                FadeSight(e.Entered.Concat(e.Left));
            }
        });
    }

    public override void _Process(double delta)
    {
        if (_fading.Count == 0)
        {
            return;
        }

        var now = _context.Now;
        var done = new List<int>();
        foreach (var index in _fading)
        {
            if (now < _startAt[index])
            {
                continue;
            }

            _hidden[index] = Approach(_hidden[index], _hiddenTarget[index], (float)(delta / RevealSeconds));
            _unseen[index] = Approach(_unseen[index], _unseenTarget[index], (float)(delta / SightSeconds));
            Write(index);
            if (_hidden[index] == _hiddenTarget[index] && _unseen[index] == _unseenTarget[index])
            {
                done.Add(index);
            }
        }

        _fading.ExceptWith(done);
        Upload();
    }

    private void SetOptionalTexture(string uniform, string? file = null)
    {
        var path = $"{TextureFolder}{file ?? uniform}.png";
        var exists = ResourceLoader.Exists(path);
        _material.SetShaderParameter($"has_{uniform}_texture", exists);
        if (exists)
        {
            _material.SetShaderParameter($"{uniform}_texture", GD.Load<Texture2D>(path));
        }
    }

    private void ApplyLook() => _material.SetShaderParameter("look", _context.FogLook == FogLook.Clouds ? 1 : 0);

    /// <summary>The whole mask as the viewer knows the map now, without fading.</summary>
    private void Snap()
    {
        Visible = _context.Viewer is not null;
        _fading.Clear();
        foreach (var position in _context.State.Map.Positions(_level))
        {
            var index = IndexOf(position);
            SetTargets(position, index);
            _hidden[index] = _hiddenTarget[index];
            _unseen[index] = _unseenTarget[index];
            Write(index);
        }

        Upload();
    }

    private void Reveal(IReadOnlyList<MapPosition> positions)
    {
        var onLevel = positions.Where(p => p.Level == _level).ToList();
        if (onLevel.Count == 0)
        {
            return;
        }

        // The core reveals at once; the view opens the fog when the step that revealed it starts to play.
        var start = Math.Max(_context.Now, _context.LastStepStart);
        var middleX = onLevel.Average(p => p.X);
        var middleY = onLevel.Average(p => p.Y);
        foreach (var position in onLevel)
        {
            var wave = onLevel.Count < WaveFromTiles ? 0 : Math.Sqrt(Math.Pow(position.X - middleX, 2) + Math.Pow(position.Y - middleY, 2)) * WaveSecondsPerTile;
            Fade(position, start + wave);
        }
    }

    private void FadeSight(IEnumerable<MapPosition> positions)
    {
        var start = Math.Max(_context.Now, _context.LastStepStart);
        foreach (var position in positions.Where(p => p.Level == _level))
        {
            Fade(position, start);
        }
    }

    private void Fade(MapPosition position, double start)
    {
        var index = IndexOf(position);
        SetTargets(position, index);
        // A tile already fading keeps its earlier start.
        _startAt[index] = _fading.Add(index) ? start : Math.Min(_startAt[index], start);
    }

    private void SetTargets(MapPosition position, int index)
    {
        var explored = _context.IsExplored(position);
        _hiddenTarget[index] = explored ? 0f : 1f;
        _unseenTarget[index] = explored && !_context.IsInSight(position) ? 1f : 0f;
    }

    private static float Approach(float value, float target, float step) =>
        value < target ? Math.Min(target, value + step) : Math.Max(target, value - step);

    private int IndexOf(MapPosition position) => ((position.Y + 1) * _width) + position.X + 1;

    private void Write(int index)
    {
        _bytes[index * 2] = (byte)Math.Round(_hidden[index] * 255);
        _bytes[(index * 2) + 1] = (byte)Math.Round(_unseen[index] * 255);
    }

    private void WriteAll()
    {
        for (var index = 0; index < _hidden.Length; index++)
        {
            Write(index);
        }
    }

    private void Upload()
    {
        _image.SetData(_width, _height, false, Image.Format.Rg8, _bytes);
        _texture.Update(_image);
    }
}
