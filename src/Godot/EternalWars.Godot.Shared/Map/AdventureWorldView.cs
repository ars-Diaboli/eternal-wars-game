using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.Shared.Map;

/// <summary>
/// The whole adventure map as seen through a <see cref="MapViewContext"/>: terrain + roads, objects and fog per
/// level (only the context's level is shown), hero markers, and the path preview. Used by the game and the
/// editor; heroes appearing or leaving (editor placement, undo, defeat) get or lose their view automatically.
/// </summary>
public partial class AdventureWorldView : Node3D
{
    private readonly Dictionary<HeroId, HeroView> _heroes = [];
    private readonly List<Node3D> _levels = [];
    private MapViewContext _context = null!;

    public PathPreview PathPreview { get; } = new() { Name = "Path" };

    public void Bind(MapViewContext context)
    {
        _context = context;
        var state = context.State;
        for (var level = 0; level < state.Map.Levels; level++)
        {
            var root = new Node3D { Name = $"Level{level}" };
            AddChild(root);
            _levels.Add(root);

            var map = new AdventureMapView { Name = "Terrain" };
            root.AddChild(map);
            map.Bind(state.Map, context.Events, level);

            var objects = new MapObjectsView { Name = "Objects" };
            root.AddChild(objects);
            objects.Bind(context, level);

            var fog = new FogView { Name = "Fog" };
            root.AddChild(fog);
            fog.Bind(context, level);
        }

        AddChild(PathPreview);
        foreach (var hero in state.Heroes)
        {
            AddHero(hero.Id);
        }

        this.SubscribeWhileInTree<HeroPlaced>(context.Events, e => AddHero(e.Hero));
        this.SubscribeWhileInTree<HeroRemoved>(context.Events, e => RemoveHero(e.Hero));
        this.SubscribeWhileInTree<HeroDefeated>(context.Events, e => RemoveHero(e.Hero));
        context.Changed += ShowLevel;
        TreeExiting += () => context.Changed -= ShowLevel;
        ShowLevel();
    }

    public HeroView? HeroViewOf(HeroId hero) => _heroes.GetValueOrDefault(hero);

    /// <summary>Highlights one hero (or none).</summary>
    public void Select(HeroId? hero)
    {
        foreach (var (id, view) in _heroes)
        {
            view.Selected = id == hero;
        }
    }

    private void AddHero(HeroId id)
    {
        if (_heroes.ContainsKey(id) || _context.State.Heroes.FirstOrDefault(h => h.Id == id) is not { } hero)
        {
            return;
        }

        var view = new HeroView();
        AddChild(view);
        view.Bind(hero, _context);
        _heroes[id] = view;
    }

    private void RemoveHero(HeroId id)
    {
        if (_heroes.Remove(id, out var view))
        {
            view.QueueFree();
        }
    }

    private void ShowLevel()
    {
        for (var level = 0; level < _levels.Count; level++)
        {
            _levels[level].Visible = level == _context.Level;
        }
    }
}
