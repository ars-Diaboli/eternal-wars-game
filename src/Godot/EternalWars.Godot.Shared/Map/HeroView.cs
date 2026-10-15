using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Events;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.Shared.Map;

/// <summary>
/// Placeholder hero marker (a coloured capsule). Each <see cref="HeroMoved"/> appends one step to a tween queue,
/// so a multi-step move plays step by step; <see cref="HeroTeleported"/> jumps once the queue reaches it. Hidden
/// on other levels and, through the context, when the viewer cannot see the hero.
/// </summary>
public partial class HeroView : Node3D
{
    private const float StepSeconds = 0.15f;

    private readonly MeshInstance3D _ring = new() { Visible = false };
    private Hero _hero = null!;
    private MapViewContext? _context;
    private Tween? _queue;

    /// <summary>Where the hero is in the animation (catches up with the core position as the queue plays).</summary>
    private MapPosition _shownPosition;

    public bool Selected
    {
        get => _ring.Visible;
        set => _ring.Visible = value;
    }

    /// <summary>Editor and simple front-ends: always visible, no level filter.</summary>
    public void Bind(Hero hero, IEventSubscriber events) => Bind(hero, events, null);

    public void Bind(Hero hero, MapViewContext context) => Bind(hero, context.Events, context);

    private void Bind(Hero hero, IEventSubscriber events, MapViewContext? context)
    {
        _hero = hero;
        _context = context;
        _shownPosition = hero.Position;
        Name = $"Hero_{hero.Id.Value}";
        Position = MapSpace.ToWorld(hero.Position);
        var color = Palette.ForPlayer(hero.Owner);
        AddChild(new MeshInstance3D
        {
            Mesh = new CapsuleMesh { Radius = 0.25f, Height = 0.9f, Material = new StandardMaterial3D { AlbedoColor = color } },
            Position = new Vector3(0, 0.45f, 0),
        });
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
                Enqueue(e.To, StepSeconds);
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
        if (context is not null)
        {
            context.Changed += UpdateVisibility;
            TreeExiting += () => context.Changed -= UpdateVisibility;
        }

        UpdateVisibility();
    }

    private void Enqueue(MapPosition to, float seconds)
    {
        if (_queue is null || !_queue.IsRunning())
        {
            _queue = CreateTween();
        }

        if (seconds > 0)
        {
            _queue.TweenProperty(this, "position", MapSpace.ToWorld(to), seconds);
        }
        else
        {
            _queue.TweenCallback(Callable.From(() => Position = MapSpace.ToWorld(to)));
        }

        _queue.TweenCallback(Callable.From(() =>
        {
            _shownPosition = to;
            UpdateVisibility();
        }));
    }

    private void UpdateVisibility() =>
        Visible = _context is null || (_shownPosition.Level == _context.Level && _context.CanSee(_hero));
}
