using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Towns;
using EternalWars.Godot.Shared.Battle;
using Godot;

namespace EternalWars.Godot.Shared.Towns;

/// <summary>
/// What a town layout shows besides its plots, as the town has it (the town screen plan's step 3, task 3, ADR-301; <see cref="TownScenery"/>):
/// <list type="bullet">
/// <item><b>Walls</b>: a <c>Walls</c> node whose <c>Post0</c>, <c>Post1</c>, … markers are the wall's line, in order, and whose <c>Gate</c> marker
/// stands on it. The siege's parts are built along it in the town's look: the wall sections as high as they are strong, the gatehouse, the moat
/// outside (away from the plots) with a bridge at the gate, and a tower with a banner at every post once the siege has towers. No fort, no
/// walls.</item>
/// <item><b>Streets</b>: <c>Streets/Village</c>, <c>Streets/Town</c> and <c>Streets/City</c>, each shown from its settlement on. Their
/// <c>Street*</c> markers become a strip of paving (metadata <c>size</c>, a <c>Vector2</c>: width × length along the marker's Z), their
/// <c>House*</c> markers a house (<c>size</c>, a <c>Vector3</c>), their <c>Banner*</c> markers a banner.</item>
/// <item><b>Banners</b> in the owner's colour (grey for a neutral town) with the faction's charge (ADR-211), and the garrison hero's banner, bigger
/// and with the charge of the hero's own town type, on the keep (TOWN-22): at the layout's <c>HeroBanner</c> marker, else on top of the town
/// hall's plot.</item>
/// </list>
/// A marker with children of its own is the layout's art and shows as it is. Everything is built when the layout loads and shown or hidden as the
/// town changes.
/// </summary>
internal sealed class TownDressing
{
    private const string HeraldryFolder = "res://shared/textures/ui/heraldry/";

    private readonly Node3D _layout;
    private readonly string _townType;
    private readonly SiegeLook _look;
    private readonly Node3D? _walls;
    private readonly Dictionary<Settlement, Node3D> _streets = [];
    private readonly Marker3D? _heroMarker;
    private (TownScenery Scenery, Color Colour)? _shown;
    private Node3D? _builtWalls;
    private Node3D? _heroBanner;
    private (Core.Domain.Common.HeroId Hero, string? Town, Color Colour, Aabb Keep)? _heroShown;

    public TownDressing(Node3D layout, string townType)
    {
        _layout = layout;
        _townType = townType;
        _look = SiegeLook.Of(townType);
        _walls = layout.FindChild("Walls", recursive: true, owned: false) as Node3D;
        if (layout.FindChild("Streets", recursive: true, owned: false) is Node3D streets)
        {
            foreach (var step in new[] { Settlement.Village, Settlement.Town, Settlement.City })
            {
                if (streets.GetNodeOrNull<Node3D>(step.ToString()) is { } group)
                {
                    _streets[step] = group;
                }
            }
        }

        _heroMarker = layout.FindChild("HeroBanner", recursive: true, owned: false) as Marker3D;
    }

    /// <summary>The faction figure of <paramref name="townType"/>, or null.</summary>
    public static Texture2D? Charge(string? townType) =>
        townType is not null && ResourceLoader.Exists($"{HeraldryFolder}charge_{townType}.png") ? GD.Load<Texture2D>($"{HeraldryFolder}charge_{townType}.png") : null;

    /// <summary>
    /// Shows the town's <paramref name="scenery"/> with banners of <paramref name="colour"/>; <paramref name="keep"/> is the town hall's plot, whose
    /// top carries the garrison hero's banner when the layout has no <c>HeroBanner</c> marker. The walls are rebuilt only when they changed.
    /// </summary>
    public void Show(TownScenery scenery, Color colour, TownPlot? keep)
    {
        var recolour = _shown?.Colour != colour;
        var rebuild = recolour || _shown is not { } shown || !SameWalls(shown.Scenery, scenery);
        _shown = (scenery, colour);
        var charge = Charge(_townType);
        foreach (var (step, group) in _streets)
        {
            group.Visible = scenery.Settlement >= step;
            if (recolour)
            {
                Dress(group, colour, charge);
            }
        }

        if (rebuild)
        {
            ShowWalls(scenery, colour, charge);
        }

        ShowHeroBanner(scenery, colour, keep);
    }

    private static bool SameWalls(TownScenery a, TownScenery b) => a.WallParts.SetEquals(b.WallParts) && a.Moat == b.Moat && a.WallHealth == b.WallHealth;

    /// <summary>Builds the street group's empty markers (once) and colours its banners.</summary>
    private void Dress(Node3D group, Color colour, Texture2D? charge)
    {
        foreach (var marker in group.FindChildren("*", nameof(Marker3D), recursive: true, owned: false).OfType<Marker3D>())
        {
            var name = marker.Name.ToString();
            if (name.StartsWith("Banner", StringComparison.Ordinal))
            {
                if (marker.GetNodeOrNull("Built") is { } old)
                {
                    marker.RemoveChild(old);
                    old.QueueFree();
                }
                else if (marker.GetChildCount() > 0)
                {
                    continue; // the layout's own banner
                }

                var banner = SceneryKit.Banner("Built", Vector3.Zero, colour, charge);
                marker.AddChild(banner);
                continue;
            }

            if (marker.GetChildCount() > 0)
            {
                continue; // built already, or the layout's own art
            }

            if (name.StartsWith("House", StringComparison.Ordinal))
            {
                marker.AddChild(SceneryKit.House(_look, Vector3.Zero, (Vector3)marker.GetMeta("size", new Vector3(4, 3, 4))));
            }
            else if (name.StartsWith("Street", StringComparison.Ordinal))
            {
                marker.AddChild(SceneryKit.Street(_look, (Vector2)marker.GetMeta("size", new Vector2(3, 10))));
            }
        }
    }

    /// <summary>The wall along the <c>Walls</c> posts as the siege has it; built anew when the parts, height or colour change.</summary>
    private void ShowWalls(TownScenery scenery, Color colour, Texture2D? charge)
    {
        if (_walls is null)
        {
            return;
        }

        if (_builtWalls is not null)
        {
            _walls.RemoveChild(_builtWalls);
            _builtWalls.QueueFree();
            _builtWalls = null;
        }

        var posts = _walls.GetChildren().OfType<Marker3D>().Where(m => m.Name.ToString().StartsWith("Post", StringComparison.Ordinal))
            .OrderBy(m => int.TryParse(m.Name.ToString()["Post".Length..], out var n) ? n : int.MaxValue).Select(m => m.Position).ToList();
        if (!scenery.WallParts.Contains(SiegeStructureKind.Wall) || posts.Count < 2)
        {
            return;
        }

        _builtWalls = new Node3D { Name = "Built" };
        _walls.AddChild(_builtWalls);
        var height = scenery.WallHealth * 2f;
        const float Thickness = 1.4f;
        var inside = InsidePoint();
        for (var i = 0; i + 1 < posts.Count; i++)
        {
            var (a, b) = (posts[i], posts[i + 1]);
            var flat = new Vector3(b.X - a.X, 0, b.Z - a.Z);
            var length = flat.Length();
            if (length < 0.01f)
            {
                continue;
            }

            var yaw = Mathf.RadToDeg(Mathf.Atan2(flat.X, flat.Z));
            var middle = (a + b) / 2;
            var foot = Mathf.Min(a.Y, b.Y);
            var rise = Mathf.Abs(a.Y - b.Y);
            _builtWalls.AddChild(SceneryKit.Box("Wall", new Vector3(middle.X, foot, middle.Z), new Vector3(Thickness, height + rise, length + Thickness), _look.Stone, yaw));
            _builtWalls.AddChild(SceneryKit.Box("WallTop", new Vector3(middle.X, foot + height + rise, middle.Z), new Vector3(Thickness + 0.5f, 0.4f, length + Thickness), _look.Stone.Darkened(0.12f), yaw));
            if (scenery.Moat)
            {
                var normal = new Vector3(flat.Z, 0, -flat.X).Normalized();
                if (normal.Dot(inside - middle) > 0)
                {
                    normal = -normal; // the moat lies outside, away from the plots
                }

                var water = new Color(_look.Moat.R, _look.Moat.G, _look.Moat.B, Mathf.Min(_look.Moat.A, 0.9f));
                _builtWalls.AddChild(SceneryKit.Box("Moat", new Vector3(middle.X, foot + 0.02f, middle.Z) + (normal * 4f), new Vector3(4f, 0.1f, length + 4f), water, yaw, _look.MoatGlows));
            }
        }

        if (_walls.GetNodeOrNull<Marker3D>("Gate") is { } gate && scenery.WallParts.Contains(SiegeStructureKind.Gate))
        {
            var (a, b) = Nearest(posts, gate.Position);
            var flat = new Vector3(b.X - a.X, 0, b.Z - a.Z);
            var yaw = Mathf.RadToDeg(Mathf.Atan2(flat.X, flat.Z));
            var at = gate.Position;
            _builtWalls.AddChild(SceneryKit.Box("Gatehouse", at, new Vector3(Thickness + 2.2f, height + 2.5f, 7f), _look.Stone.Darkened(0.08f), yaw));
            var normal = new Vector3(flat.Z, 0, -flat.X).Normalized();
            if (normal.Dot(inside - at) > 0)
            {
                normal = -normal;
            }

            _builtWalls.AddChild(SceneryKit.Box("Door", at + (normal * (Thickness / 2 + 1.15f)), new Vector3(0.2f, height - 0.6f, 3.6f), _look.Gate, yaw));
            _builtWalls.AddChild(SceneryKit.Banner("Banner", at + (Vector3.Up * (height + 2.5f)), colour, charge));
            if (scenery.Moat)
            {
                _builtWalls.AddChild(SceneryKit.Box("Bridge", at + (normal * 4f) + (Vector3.Up * 0.05f), new Vector3(6.5f, 0.3f, 4.4f), _look.Gate.Darkened(0.2f), yaw));
            }
        }

        if (scenery.WallParts.Contains(SiegeStructureKind.Tower))
        {
            foreach (var post in posts)
            {
                var towerHeight = height + 4f;
                _builtWalls.AddChild(SceneryKit.Tower(_look, post, 2.1f, towerHeight));
                _builtWalls.AddChild(SceneryKit.Banner("Banner", post + (Vector3.Up * (towerHeight + 3.2f)), colour, charge));
            }
        }
    }

    /// <summary>The garrison hero's banner on the keep, or none.</summary>
    private void ShowHeroBanner(TownScenery scenery, Color colour, TownPlot? keep)
    {
        var flies = scenery.GarrisonHero is { } hero && (_heroMarker is not null || keep is { IsShown: true })
            ? (hero, scenery.GarrisonHeroTown, colour, keep?.ShownBounds ?? default)
            : ((Core.Domain.Common.HeroId, string?, Color, Aabb)?)null;
        if (flies == _heroShown)
        {
            return; // nothing changed
        }

        _heroShown = flies;
        if (_heroBanner is not null)
        {
            _heroBanner.GetParent()?.RemoveChild(_heroBanner);
            _heroBanner.QueueFree();
            _heroBanner = null;
        }

        if (flies is null)
        {
            return;
        }

        var charge = Charge(scenery.GarrisonHeroTown ?? _townType);
        _heroBanner = SceneryKit.Banner("GarrisonHeroBanner", Vector3.Zero, colour, charge, 1.8f);
        if (_heroMarker is not null)
        {
            _heroMarker.AddChild(_heroBanner);
        }
        else if (keep is not null)
        {
            var bounds = keep.ShownBounds;
            _heroBanner.Position = new Vector3(bounds.GetCenter().X, bounds.End.Y - 1.5f, bounds.GetCenter().Z);
            keep.Marker.AddChild(_heroBanner);
        }
    }

    /// <summary>A point inside the walls, in the <c>Walls</c> node's space: the middle of the layout's plots.</summary>
    private Vector3 InsidePoint()
    {
        var plots = _layout.FindChildren(TownOverview.PlotNodePrefix + "*", nameof(Marker3D), recursive: true, owned: false).OfType<Marker3D>().ToList();
        if (plots.Count == 0 || _walls is null)
        {
            return Vector3.Zero;
        }

        var toWalls = InLayout(_walls).AffineInverse();
        return plots.Aggregate(Vector3.Zero, (sum, p) => sum + (toWalls * InLayout(p).Origin)) / plots.Count;
    }

    /// <summary>The node's transform in the layout's space (it works while the layout is out of the tree).</summary>
    private Transform3D InLayout(Node3D node)
    {
        var transform = node.Transform;
        for (var parent = node.GetParent() as Node3D; parent is not null && parent != _layout; parent = parent.GetParent() as Node3D)
        {
            transform = parent.Transform * transform;
        }

        return transform;
    }

    /// <summary>The wall segment nearest <paramref name="point"/>.</summary>
    private static (Vector3 A, Vector3 B) Nearest(List<Vector3> posts, Vector3 point)
    {
        var best = (posts[0], posts[1]);
        var bestDistance = float.MaxValue;
        for (var i = 0; i + 1 < posts.Count; i++)
        {
            var closest = Geometry3D.GetClosestPointToSegment(point, posts[i], posts[i + 1]);
            if (closest.DistanceTo(point) is var distance && distance < bestDistance)
            {
                (best, bestDistance) = ((posts[i], posts[i + 1]), distance);
            }
        }

        return best;
    }
}
