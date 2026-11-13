using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.Shared.Map;
using Godot;

namespace EternalWars.Godot.UI.Editor;

public enum ToolKind
{
    Terrain,
    Road,
    River,

    /// <summary>Shapes the ground's height with a round brush (<see cref="HeightTool"/>, ADR-182 g).</summary>
    Height,
    Object,
    Monster,
    Hero,
    Erase,
    LinkGates,
    Select,

    /// <summary>Buries the Grail where clicked, or takes it away when its cell is clicked again (ADR-140).</summary>
    Grail,
}

/// <summary>What the Height tool does where it is dragged (<see cref="HeightEditor"/>).</summary>
public enum HeightTool
{
    Raise,
    Lower,

    /// <summary>Towards the height where the stroke started.</summary>
    Level,
    Smooth,

    /// <summary>The terrains' own height (a selection of the map; the whole map is a button).</summary>
    FromTerrain,
}

/// <summary>The active editor tool and its parameters; the app applies it where the user clicks.</summary>
public sealed record EditorTool(
    ToolKind Kind,
    TerrainType Terrain,
    RoadType Road,
    int Radius,
    string ObjectDefinition,
    string Creature,
    int? Count,
    GuardDisposition Disposition,
    PlayerId Player)
{
    /// <summary>The river kind the River tool paints (ADR-079).</summary>
    public RiverType River { get; init; } = RiverType.Clear;

    /// <summary>What the Height tool does.</summary>
    public HeightTool Height { get; init; } = HeightTool.Raise;

    /// <summary>The Height tool's strength: eighths of a cell width per dab, 1 … <see cref="HeightEditor.MaxStrength"/>.</summary>
    public int Strength { get; init; } = 2;

    /// <summary>Drag painting (one undo step per stroke) rather than one edit per click.</summary>
    public bool IsBrush => Kind is ToolKind.Terrain or ToolKind.Road or ToolKind.River or ToolKind.Height;

    /// <summary>The undo step a brush stroke makes.</summary>
    public string StrokeDescription => Kind switch
    {
        ToolKind.Road => "Paint road",
        ToolKind.River => "Paint river",
        ToolKind.Height => Height switch
        {
            HeightTool.Raise => "Raise ground",
            HeightTool.Lower => "Lower ground",
            HeightTool.Level => "Level ground",
            HeightTool.Smooth => "Smooth ground",
            _ => "Heights from terrain",
        },
        _ => $"Paint {Terrain}",
    };
}

/// <summary>Tool buttons plus the options of the selected tool. Raises <see cref="ToolChanged"/> on every change.</summary>
public partial class ToolPalette : VBoxContainer
{
    private readonly HBoxContainer _options = new();
    private MapEditorSession _editor = null!;
    private EditorTool _tool = null!;

    public event Action<EditorTool>? ToolChanged;

    public EditorTool Tool => _tool;

    public void Bind(MapEditorSession editor)
    {
        _editor = editor;
        _tool = new EditorTool(
            ToolKind.Terrain, TerrainType.Grass, RoadType.Dirt, 0, BuiltInMapObjects.TreasureChest,
            MonsterGuard.DefaultCreature, null, GuardDisposition.Aggressive, editor.Scenario.Players[0].Id);

        var kinds = new HBoxContainer();
        AddChild(kinds);
        var group = new ButtonGroup();
        foreach (var kind in Enum.GetValues<ToolKind>())
        {
            var button = new Button { Text = kind.ToString(), ToggleMode = true, ButtonGroup = group, ButtonPressed = kind == ToolKind.Terrain };
            button.Pressed += () => Set(_tool with { Kind = kind });
            kinds.AddChild(button);
        }

        AddChild(_options);
        ShowOptions();
    }

    private void Set(EditorTool tool)
    {
        var kindChanged = tool.Kind != _tool.Kind;
        _tool = tool;
        if (kindChanged)
        {
            ShowOptions();
        }

        ToolChanged?.Invoke(tool);
    }

    private void ShowOptions()
    {
        _options.ClearChildren();

        switch (_tool.Kind)
        {
            case ToolKind.Terrain:
                var group = new ButtonGroup();
                foreach (var terrain in Enum.GetValues<TerrainType>())
                {
                    var button = new Button { Text = terrain.ToString(), ToggleMode = true, ButtonGroup = group, ButtonPressed = terrain == _tool.Terrain };
                    button.AddThemeColorOverride("font_color", Palette.ForTerrain(terrain).Lightened(0.3f));
                    button.Pressed += () => Set(_tool with { Terrain = terrain });
                    _options.AddChild(button);
                }

                Radius();
                AutoHeight();
                break;
            case ToolKind.Height:
                Choice("Do", Enum.GetValues<HeightTool>().Select(h => (h == HeightTool.FromTerrain ? "From terrain" : h.ToString(), h)), _tool.Height,
                    h => _tool with { Height = h });
                Radius();
                _options.AddChild(new Label { Text = "Strength" });
                var strength = new SpinBox
                {
                    MinValue = 1, MaxValue = HeightEditor.MaxStrength, Value = _tool.Strength,
                    TooltipText = $"Eighths of a cell width per dab ({HeightEditor.MaxStrength} = a whole cell)",
                };
                strength.ValueChanged += v => Set(_tool with { Strength = (int)v });
                _options.AddChild(strength);
                var whole = new Button { Text = "Heights from terrain", TooltipText = "The whole map, every level, takes its terrains' heights (one undo step)" };
                whole.Pressed += () => _editor.Heights.FromTerrain();
                _options.AddChild(whole);
                AutoHeight();
                break;
            case ToolKind.Road:
                Choice("Road", Enum.GetValues<RoadType>().Select(r => (r.ToString(), r)), _tool.Road, r => _tool with { Road = r });
                Radius();
                break;
            case ToolKind.River:
                Choice("River", Enum.GetValues<RiverType>().Select(r => (r.ToString(), r)), _tool.River, r => _tool with { River = r });
                Radius();
                break;
            case ToolKind.Object:
                var definitions = _editor.Catalog.All.Where(d => d.Kind != MapObjectKind.Monster).OrderBy(d => d.Name, StringComparer.Ordinal);
                Choice("Object", definitions.Select(d => (d.Name, d.Id)), _tool.ObjectDefinition, id => _tool with { ObjectDefinition = id });
                break;
            case ToolKind.Monster:
                Choice("Creature", Creatures(), _tool.Creature, c => _tool with { Creature = c });
                var count = new SpinBox { MinValue = 0, MaxValue = 999, Value = _tool.Count ?? 0, TooltipText = "0 = rolled from the tier at game start" };
                count.ValueChanged += v => Set(_tool with { Count = v > 0 ? (int)v : null });
                _options.AddChild(new Label { Text = "Count" });
                _options.AddChild(count);
                Choice("Disposition", Enum.GetValues<GuardDisposition>().Select(d => (d.ToString(), d)), _tool.Disposition, d => _tool with { Disposition = d });
                break;
            case ToolKind.Hero:
                Choice("Owner", _editor.Scenario.Players.Select(p => (p.Name, p.Id)), _tool.Player, p => _tool with { Player = p });
                break;
            case ToolKind.Erase:
                _options.AddChild(new Label { Text = "Click a hero or object to remove it." });
                break;
            case ToolKind.LinkGates:
                _options.AddChild(new Label { Text = "Click a gate (or one-way entrance), then its partner (or exit)." });
                break;
            case ToolKind.Select:
                _options.AddChild(new Label { Text = "Click a hero or object to edit it in the inspector." });
                break;
            case ToolKind.Grail:
                _options.AddChild(new Label
                {
                    Text = "Click open ground at least 10 cells from the edges to bury the Grail; click it again to take it away "
                           + "(left unset, it is drawn at game start on maps with obelisks; at most 25 obelisks a map).",
                    AutowrapMode = TextServer.AutowrapMode.WordSmart,
                    CustomMinimumSize = new Vector2(1000, 0), // a wrapping label in the options row needs a width, or it wraps every letter
                });
                break;
        }
    }

    private IEnumerable<(string, string)> Creatures() =>
        _editor.Scenario.Creatures.All.OrderBy(c => c.Town ?? "~").ThenBy(c => c.Tier).ThenBy(c => c.Name, StringComparer.Ordinal)
            .Select(c => ($"{c.Name} (T{c.Tier})", c.Id));

    private void Radius()
    {
        _options.AddChild(new Label { Text = "Radius" });
        var radius = new SpinBox { MinValue = 0, MaxValue = 5, Value = _tool.Radius };
        radius.ValueChanged += v => Set(_tool with { Radius = (int)v });
        _options.AddChild(radius);
    }

    /// <summary>The session's Auto height switch: painting terrain also gives the ground the new terrain's height.</summary>
    private void AutoHeight()
    {
        var auto = new CheckBox
        {
            Text = "Auto height",
            ButtonPressed = _editor.Heights.AutoHeight,
            TooltipText = "Painting terrain also gives the ground there the new terrain's height",
        };
        auto.Toggled += on => _editor.Heights.AutoHeight = on;
        _options.AddChild(auto);
    }

    private void Choice<T>(string label, IEnumerable<(string Text, T Value)> items, T selected, Func<T, EditorTool> apply)
    {
        var list = items.ToList();
        var option = new OptionButton();
        foreach (var (text, _) in list)
        {
            option.AddItem(text);
        }

        option.Selected = Math.Max(0, list.FindIndex(i => EqualityComparer<T>.Default.Equals(i.Value, selected)));
        option.ItemSelected += index => Set(apply(list[(int)index].Value));
        _options.AddChild(new Label { Text = label });
        _options.AddChild(option);
    }
}
