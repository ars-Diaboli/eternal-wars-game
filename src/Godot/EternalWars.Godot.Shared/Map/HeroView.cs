using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Events;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.Shared.Map;

/// <summary>Placeholder hero marker (a coloured capsule) that follows <see cref="HeroMoved"/> events.</summary>
public partial class HeroView : Node3D
{
    private const float MoveSeconds = 0.15f;

    public void Bind(Hero hero, IEventSubscriber events)
    {
        Name = $"Hero_{hero.Name}";
        Position = MapSpace.ToWorld(hero.Position);
        AddChild(new MeshInstance3D
        {
            Mesh = new CapsuleMesh
            {
                Radius = 0.25f,
                Height = 0.9f,
                Material = new StandardMaterial3D { AlbedoColor = Palette.ForPlayer(hero.Owner) },
            },
            Position = new Vector3(0, 0.45f, 0),
        });

        this.SubscribeWhileInTree<HeroMoved>(events, e =>
        {
            if (e.Hero == hero.Id)
            {
                CreateTween().TweenProperty(this, "position", MapSpace.ToWorld(e.To), MoveSeconds);
            }
        });
    }
}
