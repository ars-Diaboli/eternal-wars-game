using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using Godot;

namespace EternalWars.Godot.UI.Editor.Tools;

/// <summary>A tool that does nothing until a subclass says otherwise: most tools answer one or two of the pointer's moves.</summary>
public abstract class EditorTool(EditorToolContext context) : IEditorTool
{
    public abstract string Name { get; }

    public virtual UiIcon? Icon => null;

    public virtual Key? Shortcut => null;

    public abstract string ClickHint { get; }

    protected EditorToolContext Context { get; } = context;

    protected MapEditorSession Editor => Context.Editor;

    public virtual void Activate()
    {
    }

    public virtual void Hover(MapPosition? tile)
    {
    }

    public virtual void Press(MapPosition tile)
    {
    }

    public virtual void Drag(MapPosition tile)
    {
    }

    public virtual void Release()
    {
    }

    public virtual void Cancel()
    {
    }

    public virtual void Refresh()
    {
    }

    public virtual void BuildOptions(ToolOptions options)
    {
    }
}

/// <summary>
/// A tool that paints while dragged (EDIT-1, ADR-250): one undo step per stroke. A dab where the button goes down, then along the
/// straight line to every tile the pointer comes to, so a fast drag leaves no gaps. Shift + click draws a line from the tile painted
/// last. Before the click the tiles under the brush are outlined on the ground.
/// </summary>
public abstract class BrushTool(EditorToolContext context) : EditorTool(context)
{
    private bool _stroking;

    /// <summary>The tile painted last: where a Shift + click line and the next piece of a drag start.</summary>
    private MapPosition? _last;
    private MapPosition? _outlined;

    /// <summary>The undo step a stroke makes.</summary>
    protected abstract string StrokeDescription { get; }

    /// <summary>Whether a move of the pointer inside the tile it is on dabs again (the height brushes build up; the paints do not).</summary>
    protected virtual bool DabsInPlace => false;

    public override void Hover(MapPosition? tile) => ShowOutline(tile);

    public override void Press(MapPosition tile)
    {
        StartStroke(tile);
        _stroking = true;
        Editor.BeginStroke(StrokeDescription);
        if (Context.Pointer.Add && _last is { } from && from.Level == tile.Level && from != tile)
        {
            PaintLine(from, tile);
        }
        else
        {
            Paint(tile);
        }

        _last = tile;
        ShowOutline(tile);
    }

    public override void Drag(MapPosition tile)
    {
        if (!_stroking)
        {
            return;
        }

        if (_last is { } from && from != tile && from.Level == tile.Level)
        {
            PaintLine(from, tile);
        }
        else if (_last != tile || DabsInPlace)
        {
            Paint(tile);
        }

        _last = tile;
        ShowOutline(tile);
    }

    public override void Release()
    {
        if (_stroking)
        {
            _stroking = false;
            Editor.EndStroke();
        }
    }

    public override void Cancel()
    {
        Release();
        _last = null;
        ShowOutline(null);
    }

    /// <summary>A stroke starts on <paramref name="tile"/>, before its undo step opens.</summary>
    protected virtual void StartStroke(MapPosition tile)
    {
    }

    /// <summary>One dab of the brush on <paramref name="tile"/>.</summary>
    protected abstract void Paint(MapPosition tile);

    /// <summary>The brush along the line from <paramref name="from"/> (painted already) to <paramref name="to"/>: a dab on every tile after the first.</summary>
    protected virtual void PaintLine(MapPosition from, MapPosition to)
    {
        foreach (var tile in Brush.LineTiles(from, to).Skip(1))
        {
            Paint(tile);
        }
    }

    /// <summary>The tiles a dab on <paramref name="tile"/> would touch, for the outline.</summary>
    protected virtual IEnumerable<MapPosition> Outline(MapPosition tile) => Context.Brush.Tiles(tile);

    /// <summary>Draws the outline again where it was (the brush changed).</summary>
    protected void RefreshOutline()
    {
        var at = _outlined;
        _outlined = null;
        ShowOutline(at);
    }

    private void ShowOutline(MapPosition? tile)
    {
        if (tile == _outlined)
        {
            return;
        }

        _outlined = tile;
        Context.Marks?.ShowBrush(tile is { } at ? [.. Outline(at)] : null);
    }

    /// <summary>The height brushes' radius.</summary>
    protected void Radius(ToolOptions options)
    {
        var radius = new SpinBox { MinValue = 0, MaxValue = 5, Value = Context.Radius };
        radius.ValueChanged += v =>
        {
            Context.Radius = (int)v;
            RefreshOutline();
        };
        options.Labelled("Radius", radius);
    }

    /// <summary>The session's Auto height switch: painting terrain also gives the ground the new terrain's height.</summary>
    protected void AutoHeight(ToolOptions options)
    {
        var auto = new CheckBox
        {
            Text = "Auto height",
            ButtonPressed = Editor.Heights.AutoHeight,
            TooltipText = "Painting terrain also gives the ground there the new terrain's height",
        };
        auto.Toggled += on => Editor.Heights.AutoHeight = on;
        options.Add(auto);
    }
}

/// <summary>A brush that changes the tiles under it: the size and shape are the paints' shared <see cref="EditorToolContext.Brush"/>.</summary>
public abstract class TileBrushTool(EditorToolContext context) : BrushTool(context)
{
    /// <summary>The tool's work on <paramref name="tiles"/> (some may lie off the map).</summary>
    protected abstract void Paint(IEnumerable<MapPosition> tiles);

    protected sealed override void Paint(MapPosition tile) => Paint(Context.Brush.Tiles(tile));

    protected sealed override void PaintLine(MapPosition from, MapPosition to) => Paint(Context.Brush.Line(from, to));

    /// <summary>The paints' shared brush: its size (1 … 9 tiles across) and its shape.</summary>
    protected void BrushOptions(ToolOptions options)
    {
        var size = new SpinBox
        {
            MinValue = 1, MaxValue = Brush.MaxSize, Value = Context.Brush.Size,
            TooltipText = "The brush's size in tiles. Shift + click draws a line from the tile painted last",
        };
        size.ValueChanged += v =>
        {
            Context.Brush = Context.Brush with { Size = (int)v };
            RefreshOutline();
        };
        options.Labelled("Size", size);
        var round = new CheckBox { Text = "Round", ButtonPressed = Context.Brush.Shape == BrushShape.Round, TooltipText = "A round brush instead of a square one" };
        round.Icon = UiIcons.Get(round.ButtonPressed ? UiIcon.BrushRound : UiIcon.BrushSquare);
        round.Toggled += on =>
        {
            round.Icon = UiIcons.Get(on ? UiIcon.BrushRound : UiIcon.BrushSquare);
            Context.Brush = Context.Brush with { Shape = on ? BrushShape.Round : BrushShape.Square };
            RefreshOutline();
        };
        options.Add(round);
    }
}
