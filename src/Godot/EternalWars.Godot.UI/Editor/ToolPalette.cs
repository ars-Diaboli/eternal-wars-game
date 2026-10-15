using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Godot.Shared.Map;
using Godot;

namespace EternalWars.Godot.UI.Editor;

public enum ToolKind
{
    Terrain,
    Road,
    Object,
    Monster,
    Hero,
    Erase,
    LinkGates,
    Select,
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
    /// <summary>Drag painting (one undo step per stroke) rather than one edit per click.</summary>
    public bool IsBrush => Kind is ToolKind.Terrain or ToolKind.Road;
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
        foreach (var child in _options.GetChildren())
        {
            child.QueueFree();
        }

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
                break;
            case ToolKind.Road:
                Choice("Road", Enum.GetValues<RoadType>().Select(r => (r.ToString(), r)), _tool.Road, r => _tool with { Road = r });
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
