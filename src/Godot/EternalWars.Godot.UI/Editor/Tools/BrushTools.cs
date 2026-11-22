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

    public override string ClickHint => _fill ? $"Terrain: a click fills the joined area with {_terrain}" : $"Terrain: drag over the map to paint {_terrain}";

    public override UiIcon? Icon => UiIcon.ToolTerrain;

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
            Text = "Fill", Icon = UiIcons.Get(UiIcon.ToolFill), ButtonPressed = _fill,
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

    public override Key? Shortcut => Key.R;

    public override string ClickHint => "Road: drag over the map to lay the picked road";

    public override UiIcon? Icon => UiIcon.ToolRoad;

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

    public override Key? Shortcut => Key.I;

    public override string ClickHint => "River: drag over the map to lay the picked river";

    public override UiIcon? Icon => UiIcon.ToolRiver;

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

    public override Key? Shortcut => Key.Z;

    public override string ClickHint => _zone is null
        ? "StartZone: drag over the map to erase start zones"
        : "StartZone: drag over the map to paint the picked player's start zone";

    public override UiIcon? Icon => UiIcon.ToolStartZone;

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

    /// <summary>The tiles past the brush's full circle over which its strength fades, 0 … <see cref="HeightEditor.MaxFalloff"/>.</summary>
    private int _falloff = HeightEditor.DefaultFalloff;

    /// <summary>Level's target: the ground's height (eighths) where the stroke started.</summary>
    private int _levelTo;

    public override string Name => "Height";

    public override Key? Shortcut => Key.G;

    public override string ClickHint => _action == HeightAction.FromTerrain
        ? "Height: drag over the map to give the ground its terrain's heights"
        : $"Height: drag over the map to {StrokeDescription.ToLowerInvariant()}";

    public override UiIcon? Icon => UiIcon.ToolHeight;

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
            "Do", Enum.GetValues<HeightAction>().Select(h => (h == HeightAction.FromTerrain ? "From terrain" : h.ToString(), h)), _action, h => _action = h,
            h => h switch
            {
                HeightAction.Raise => UiIcon.HeightRaise,
                HeightAction.Lower => UiIcon.HeightLower,
                HeightAction.Level => UiIcon.HeightLevel,
                HeightAction.Smooth => UiIcon.HeightSmooth,
                _ => null,
            });
        Radius(options);
        var strength = new SpinBox
        {
            MinValue = 1, MaxValue = HeightEditor.MaxStrength, Value = _strength,
            TooltipText = $"Eighths of a cell width per dab ({HeightEditor.MaxStrength} = a whole cell)",
        };
        strength.ValueChanged += v => _strength = (int)v;
        options.Labelled("Strength", strength);
        var falloff = new SpinBox
        {
            MinValue = 0, MaxValue = HeightEditor.MaxFalloff, Value = _falloff,
            TooltipText = "Tiles past the brush over which its strength fades: 0 is a hard edge, more a softer slope (not for From terrain)",
        };
        falloff.ValueChanged += v => _falloff = (int)v;
        options.Labelled("Falloff", falloff);
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
            HeightAction.Raise => Editor.Heights.Raise(tile, radius, _strength, _falloff),
            HeightAction.Lower => Editor.Heights.Lower(tile, radius, _strength, _falloff),
            HeightAction.Level => Editor.Heights.Level(tile, radius, _strength, _levelTo, _falloff),
            HeightAction.Smooth => Editor.Heights.Smooth(tile, radius, _strength, _falloff),
            _ => Editor.Heights.FromTerrain(tile, radius),
        };
    }
}
