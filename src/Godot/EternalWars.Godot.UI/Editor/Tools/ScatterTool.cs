using EternalWars.Core.Domain.Common;
using EternalWars.Core.Generation;
using Godot;

namespace EternalWars.Godot.UI.Editor.Tools;

/// <summary>
/// Scatters scenery under the brush (EDIT-6, ADR-252), chosen as the generator chooses it (<see cref="ScatterEditor"/>): the density,
/// the kinds and whether ways stay open are the tool's options. A stroke marks the tiles the brush goes over and scatters over all of them
/// when the button comes up, as one undo step: pieces of several tiles fit across the brush's dabs, and what is scattered depends on
/// the tiles alone, not on how the brush was dragged. A button scatters over the selected tiles.
/// </summary>
public sealed class ScatterTool(EditorToolContext context) : TileBrushTool(context)
{
    private readonly HashSet<MapPosition> _marked = [];
    private readonly HashSet<string> _kinds = [.. ScatterEditor.Kinds(context.Editor.Catalog)];
    private int _density = ScatterOptions.DefaultDensity;
    private bool _keepPathsOpen = true;
    private bool _marking;

    public override string Name => "Scatter";

    public override Key? Shortcut => Key.C;

    public override string ClickHint => "Scatter: drag over the map to scatter scenery that suits the terrain";

    public override UiIcon? Icon => UiIcon.ToolScatter;

    protected override string StrokeDescription => "Scatter scenery";

    private ScatterOptions Options => new(_density, _kinds, _keepPathsOpen);

    public override void BuildOptions(ToolOptions options)
    {
        var density = new SpinBox
        {
            MinValue = 1, MaxValue = 100, Value = _density, Suffix = "%",
            TooltipText = "How many of the free tiles under the brush get scenery",
        };
        density.ValueChanged += v => _density = (int)v;
        options.Labelled("Density", density);
        var keep = new CheckBox
        {
            Text = "Keep paths open", ButtonPressed = _keepPathsOpen,
            TooltipText = "Leaves a piece out where it would close a way between the tiles round it, or to an entrance",
        };
        keep.Toggled += on => _keepPathsOpen = on;
        options.Add(keep);
        BrushOptions(options);

        var boxes = new List<(string Kind, CheckBox Box)>();
        foreach (var kind in ScatterEditor.Kinds(Editor.Catalog))
        {
            var text = kind.Replace('-', ' ');
            var box = new CheckBox
            {
                Text = char.ToUpperInvariant(text[0]) + text[1..], ButtonPressed = _kinds.Contains(kind),
                TooltipText = $"Whether {text} scenery is scattered",
            };
            box.Toggled += on => _ = on ? _kinds.Add(kind) : _kinds.Remove(kind);
            options.Add(box);
            boxes.Add((kind, box));
        }

        var all = new Button { Text = "All", TooltipText = "Ticks every kind" };
        all.Pressed += () => boxes.ForEach(b => b.Box.ButtonPressed = true);
        options.Add(all);
        var none = new Button { Text = "None", TooltipText = "Unticks every kind" };
        none.Pressed += () => boxes.ForEach(b => b.Box.ButtonPressed = false);
        options.Add(none);

        var selection = new Button { Text = "Scatter in selection", TooltipText = "Scatters over the selected tiles (one undo step)" };
        selection.Pressed += () => Context.Say(
            Editor.Selection.Tiles.Count == 0
                ? "Select the tiles first (the Select tool's rectangle)"
                : Said(ScatterEditor.ScatterSelection(Editor, Options)));
        options.Add(selection);
        options.Hint(
            "Drag over the map to mark tiles; on release the ticked kinds of scenery that suit the terrain are scattered there. Roads, rivers, " +
            "objects and event cells stay free. " +
            "Undo, then the same stroke, gives the same scenery.");
    }

    public override void Release()
    {
        if (_marking)
        {
            _marking = false;
            Context.Say(Said(ScatterEditor.Scatter(Editor, _marked, Options))); // inside the stroke: its one undo step
            _marked.Clear();
            RefreshOutline();
        }

        base.Release();
    }

    public override void Cancel()
    {
        _marking = false; // dropped: nothing is scattered
        _marked.Clear();
        base.Cancel();
    }

    protected override void StartStroke(MapPosition tile)
    {
        _marked.Clear();
        _marking = true;
    }

    protected override void Paint(IEnumerable<MapPosition> tiles)
    {
        _marked.UnionWith(tiles); // the outline follows: the brush tool shows it again after every dab
    }

    /// <summary>While the button is held: every tile the stroke has marked, with the brush where it is now.</summary>
    protected override IEnumerable<MapPosition> Outline(MapPosition tile) => _marking ? _marked.Union(base.Outline(tile)) : base.Outline(tile);

    private static string Said(int placed) => placed == 0 ? "No scenery fits there" : $"Scattered {placed} piece(s) of scenery";
}
