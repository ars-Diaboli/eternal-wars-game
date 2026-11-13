using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Settings;
using EternalWars.Core.Events;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.Shared.Map;

/// <summary>
/// Placeholder hero marker (a coloured capsule). Each <see cref="HeroMoved"/> appends one step to a tween queue,
/// so a multi-step move plays step by step, the capsule hopping as it walks; <see cref="HeroTeleported"/> jumps once the queue reaches it. Hidden
/// on other levels and, through the context, when the viewer cannot see the hero.
/// </summary>
/// <remarks>
/// With a context, a step's length follows the speed settings (ADR-090, UI-1): a human player's hero at the own-heroes speed, a computer
/// player's at the computer speed, and at once when computer heroes are not shown or the viewer sees neither end of the step. Steps of all
/// heroes are booked on the context one after another, so a computer turn plays back hero by hero.
/// </remarks>
public partial class HeroView : Node3D
{
    private const float StepSeconds = 0.15f;
    private const float BodyHeight = 0.45f;
    private const float HopHeight = 0.12f;

    private readonly MeshInstance3D _ring = new() { Visible = false };
    private MeshInstance3D _body = null!;
    private Hero _hero = null!;
    private MapViewContext? _context;
    private Tween? _queue;

    /// <summary>When the queue ends, on the context's clock.</summary>
    private double _queueEnd;

    /// <summary>Where the hero is in the animation (catches up with the core position as the queue plays).</summary>
    private MapPosition _shownPosition;

    /// <summary>Where the hero will be once the queue has played.</summary>
    private MapPosition _queuedPosition;

    public bool Selected
    {
        get => _ring.Visible;
        set => _ring.Visible = value;
    }

    public void Bind(Hero hero, MapViewContext context)
    {
        var events = context.Events;
        _hero = hero;
        _context = context;
        _shownPosition = hero.Position;
        _queuedPosition = hero.Position;
        Name = $"Hero_{hero.Id.Value}";
        Position = OnGround(hero.Position);
        var color = Palette.ForPlayer(hero.Owner);
        _body = new MeshInstance3D
        {
            Mesh = new CapsuleMesh { Radius = 0.25f, Height = 0.9f, Material = new StandardMaterial3D { AlbedoColor = color } },
            Position = new Vector3(0, BodyHeight, 0),
        };
        AddChild(_body);
        _ring.Mesh = new TorusMesh
        {
            InnerRadius = 0.36f,
            OuterRadius = 0.46f,
            Material = new StandardMaterial3D { AlbedoColor = Colors.White, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded },
        };
        _ring.Position = new Vector3(0, 0.05f, 0);
        AddChild(_ring);

        this.SubscribeWhileInTree<HeroMoved>(events, e =>
        {
            if (e.Hero == hero.Id)
            {
                Enqueue(e.To, StepLength(e.To));
            }
            else
            {
                UpdateVisibility();
            }
        });
        this.SubscribeWhileInTree<HeroTeleported>(events, e =>
        {
            if (e.Hero == hero.Id)
            {
                Enqueue(e.To, 0);
            }
        });
        this.SubscribeWhileInTree<HeroPlaced>(events, e =>
        {
            if (e.Hero == hero.Id)
            {
                Enqueue(e.Position, 0);
            }
        });
        this.SubscribeWhileInTree<SightChanged>(events, _ => UpdateVisibility());
        // The editor reshaped the ground: a hero standing there moves with it (one walking takes the new ground as it goes).
        this.SubscribeWhileInTree<GroundHeightsChanged>(events, e =>
        {
            if (_queue is not { } queue || !queue.IsValid() || !queue.IsRunning())
            {
                if (e.Level == _shownPosition.Level && e.Area.Touches(_shownPosition))
                {
                    Position = OnGround(_shownPosition);
                }
            }
        });
        // Veil III hides a hero from other players whose heroes are not close (GameState.CanSee): re-check when anyone moves or a veil changes.
        this.SubscribeWhileInTree<HeroMoved>(events, e =>
        {
            if (e.Hero != hero.Id)
            {
                UpdateVisibility();
            }
        });
        this.SubscribeWhileInTree<HeroStatusGained>(events, e => UpdateVisibility());
        this.SubscribeWhileInTree<HeroStatusesExpired>(events, e => UpdateVisibility());
        if (context is not null)
        {
            context.Changed += UpdateVisibility;
            TreeExiting += () => context.Changed -= UpdateVisibility;
        }

        UpdateVisibility();
    }

    /// <summary>How long a walking step to <paramref name="to"/> takes; 0 = the hero jumps there.</summary>
    private float StepLength(MapPosition to)
    {
        if (_context is null)
        {
            return StepSeconds;
        }

        var speeds = _context.Speeds;
        var human = _context.State.GetPlayer(_hero.Owner).IsHuman;
        var shown = human || (speeds.ShowComputerHeroes && (_context.IsInSight(_queuedPosition) || _context.IsInSight(to)));
        return shown ? (float)SpeedSettings.Seconds(speeds.ForHeroOf(human), StepSeconds) : 0;
    }

    private void Enqueue(MapPosition to, float seconds)
    {
        var now = _context?.Now ?? 0;
        if (_queue is null || !_queue.IsValid() || !_queue.IsRunning())
        {
            _queue = CreateTween();
            _queueEnd = now;
        }

        var earliest = Math.Max(now, _queueEnd);
        var start = _context?.Schedule(earliest, seconds, computer: seconds > 0 && !_context.State.GetPlayer(_hero.Owner).IsHuman) ?? earliest;
        if (start > earliest + 0.001)
        {
            _queue.TweenInterval(start - earliest); // other heroes' steps play first
        }

        _queueEnd = start + seconds;
        var from = _queuedPosition;
        _queuedPosition = to;
        if (_context is { } context)
        {
            _queue.TweenCallback(Callable.From(() => context.ShowStep(_hero, to)));
        }

        if (seconds > 0)
        {
            // Along the ground, sampled every frame, so the hero walks over a slope rather than through it.
            _queue.TweenMethod(Callable.From<float>(t => Position = Between(from, to, t)), 0f, 1f, seconds);
            _queue.Parallel().TweenProperty(_body, "position:y", BodyHeight + HopHeight, seconds / 2)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            _queue.Parallel().TweenProperty(_body, "position:y", BodyHeight, seconds / 2).SetDelay(seconds / 2)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
        }
        else
        {
            _queue.TweenCallback(Callable.From(() => Position = OnGround(to)));
        }

        _queue.TweenCallback(Callable.From(() =>
        {
            _shownPosition = to;
            UpdateVisibility();
        }));
    }

    private void UpdateVisibility() =>
        Visible = _context is null || (_shownPosition.Level == _context.Level && _context.CanSee(_hero));

    private Vector3 OnGround(MapPosition position) => MapSpace.ToGround(_context!.State.Map, position);

    /// <summary>The ground a fraction <paramref name="t"/> of the way along a step (a step never changes level).</summary>
    private Vector3 Between(MapPosition from, MapPosition to, float t)
    {
        var at = MapSpace.ToWorld(from).Lerp(MapSpace.ToWorld(to), t);
        return at with { Y = MapSpace.GroundY(_context!.State.Map, to.Level, at.X, at.Z) };
    }
}
