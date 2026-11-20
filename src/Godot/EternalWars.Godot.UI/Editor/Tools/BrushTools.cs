using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Map;
using EternalWars.Godot.Shared.Map;
using Godot;

namespace EternalWars.Godot.UI.Editor.Tools;

/// <summary>Paints a terrain; with Fill on, a click gives it to the whole joined area of the terrain clicked.</summary>
public sealed class TerrainTool(EditorToolContext context) : TileBrushTool(context)
{
    private TerrainType _terrain = TerrainType.Grass;
    private bool _fill;

    public override string Name => "Terrain";

    public override Key? Shortcut => Key.T;

    protected override string StrokeDescription => $"Paint {_terrain}";

    public override void BuildOptions(ToolOptions options)
    {
        var group = new ButtonGroup();
        foreach (var terrain in Enum.GetValues<TerrainType>())
        {
            var button = new Button { Text = terrain.ToString(), ToggleMode = true, ButtonGroup = group, ButtonPressed = terrain == _terrain };
            button.AddThemeColorOverride("font_color", Palette.ForTerrain(terrain).Lightened(0.3f));
            button.Pressed += () => _terrain = terrain;
            options.Add(button);
        }

        BrushOptions(options);
        var fill = new CheckBox
        {
            Text = "Fill", ButtonPressed = _fill,
            TooltipText = "A click fills the joined area of the terrain clicked (tiles side by side, not across corners)",
        };
        fill.Toggled += on =>
        {
            _fill = on;
            RefreshOutline();
        };
        options.Add(fill);
        AutoHeight(options);
    }

    public override void Press(MapPosition tile)
    {
        if (_fill)
        {
            Context.Say($"Filled {Editor.Terrain.Fill(tile, _terrain)} tile(s) with {_terrain}");
        }
        else
        {
            base.Press(tile);
        }
    }

    protected override IEnumerable<MapPosition> Outline(MapPosition tile) => _fill ? [tile] : base.Outline(tile);

    protected override void Paint(IEnumerable<MapPosition> tiles) => Editor.Terrain.Paint(tiles, _terrain);
}

/// <summary>Paints a road.</summary>
public sealed class RoadTool(EditorToolContext context) : TileBrushTool(context)
{
    private RoadType _road = RoadType.Dirt;

    public override string Name => "Road";

    protected override string StrokeDescription => "Paint road";

    public override void BuildOptions(ToolOptions options)
    {
        options.Choice("Road", Enum.GetValues<RoadType>().Select(r => (r.ToString(), r)), _road, r => _road = r);
        BrushOptions(options);
    }

    protected override void Paint(IEnumerable<MapPosition> tiles) => Editor.Terrain.PaintRoad(tiles, _road);
}

/// <summary>Paints a river of one kind (ADR-079).</summary>
public sealed class RiverTool(EditorToolContext context) : TileBrushTool(context)
{
    private RiverType _river = RiverType.Clear;

    public override string Name => "River";

    protected override string StrokeDescription => "Paint river";

    public override void BuildOptions(ToolOptions options)
    {
        options.Choice("River", Enum.GetValues<RiverType>().Select(r => (r.ToString(), r)), _river, r => _river = r);
        BrushOptions(options);
    }

    protected override void Paint(IEnumerable<MapPosition> tiles) => Editor.Terrain.PaintRiver(tiles, _river);
}

/// <summary>Paints a player's start zone, or erases zones (MP-4, ADR-197).</summary>
public sealed class StartZoneTool(EditorToolContext context) : TileBrushTool(context)
{
    /// <summary>Whose zone is painted; null erases.</summary>
    private PlayerId? _zone = context.Editor.Scenario.Players[0].Id;

    public override string Name => "StartZone";

    protected override string StrokeDescription => _zone is null ? "Erase start zone" : "Paint start zone";

    public override void BuildOptions(ToolOptions options)
    {
        options.Choice(
            "Zone", Editor.Scenario.Players.Select(pl => (pl.Name, (PlayerId?)pl.Id)).Append(("Erase", (PlayerId?)null)), _zone, z => _zone = z);
        BrushOptions(options);
        options.Hint("Where each player's day stays its own in a simultaneous first week; another player's hero entering it ends those days.");
    }

    protected override void Paint(IEnumerable<MapPosition> tiles) => Editor.Terrain.PaintStartZone(tiles, _zone);
}

/// <summary>What the Height tool does where it is dragged (<see cref="HeightEditor"/>).</summary>
public enum HeightAction
{
    Raise,
    Lower,

    /// <summary>Towards the height where the stroke started.</summary>
    Level,
    Smooth,

    /// <summary>The terrains' own height (a selection of the map; the whole map is a button).</summary>
    FromTerrain,
}

/// <summary>Shapes the ground's height with a round brush (ADR-182 g).</summary>
public sealed class HeightTool(EditorToolContext context) : BrushTool(context)
{
    private HeightAction _action = HeightAction.Raise;

    /// <summary>Eighths of a cell width per dab, 1 … <see cref="HeightEditor.MaxStrength"/>.</summary>
    private int _strength = 2;

    /// <summary>Level's target: the ground's height (eighths) where the stroke started.</summary>
    private int _levelTo;

    public override string Name => "Height";

    protected override string StrokeDescription => _action switch
    {
        HeightAction.Raise => "Raise ground",
        HeightAction.Lower => "Lower ground",
        HeightAction.Level => "Level ground",
        HeightAction.Smooth => "Smooth ground",
        _ => "Heights from terrain",
    };

    public override void BuildOptions(ToolOptions options)
    {
        options.Choice(
            "Do", Enum.GetValues<HeightAction>().Select(h => (h == HeightAction.FromTerrain ? "From terrain" : h.ToString(), h)), _action, h => _action = h);
        Radius(options);
        var strength = new SpinBox
        {
            MinValue = 1, MaxValue = HeightEditor.MaxStrength, Value = _strength,
            TooltipText = $"Eighths of a cell width per dab ({HeightEditor.MaxStrength} = a whole cell)",
        };
        strength.ValueChanged += v => _strength = (int)v;
        options.Labelled("Strength", strength);
        var whole = new Button { Text = "Heights from terrain", TooltipText = "The whole map, every level, takes its terrains' heights (one undo step)" };
        whole.Pressed += () => Editor.Heights.FromTerrain();
        options.Add(whole);
        AutoHeight(options);
    }

    protected override bool DabsInPlace => true;

    protected override IEnumerable<MapPosition> Outline(MapPosition tile) => new Brush((2 * Context.Radius) + 1, BrushShape.Round).Tiles(tile);

    protected override void StartStroke(MapPosition tile) => _levelTo = Editor.Heights.HeightAt(tile);

    protected override void Paint(MapPosition tile)
    {
        var radius = Context.Radius;
        _ = _action switch
        {
            HeightAction.Raise => Editor.Heights.Raise(tile, radius, _strength),
            HeightAction.Lower => Editor.Heights.Lower(tile, radius, _strength),
            HeightAction.Level => Editor.Heights.Level(tile, radius, _strength, _levelTo),
            HeightAction.Smooth => Editor.Heights.Smooth(tile, radius, _strength),
            _ => Editor.Heights.FromTerrain(tile, radius),
        };
    }
}
