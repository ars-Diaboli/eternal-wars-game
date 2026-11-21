using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using Godot;

namespace EternalWars.Godot.UI.Editor.Tools;

/// <summary>
/// Selects things on the map (EDIT-1, ADR-250). A click selects the hero, object or event cell under the pointer (nothing there: the
/// selection is dropped); a drag over the map selects the tiles of the rectangle and what stands in it. Shift adds to the selection,
/// Alt takes out of it. A double click on an object selects every object of its kind in view. The filter says which kinds of things
/// are picked. A drag that starts on a selected object or hero moves everything selected, with a ghost of where it lands (back on
/// the tile it started from, nothing moves). After a click, a rectangle or a move, a selection whose only thing is one object or one
/// hero opens in the inspector.
/// </summary>
public class SelectTool(EditorToolContext context) : EditorTool(context)
{
    private static readonly (string Text, SelectionFilter Kind)[] Kinds =
    [
        ("Objects", SelectionFilter.Objects), ("Heroes", SelectionFilter.Heroes), ("Monsters", SelectionFilter.Monsters),
        ("Scenery", SelectionFilter.Scenery), ("Events", SelectionFilter.Events),
    ];

    /// <summary>Where the button went down; null when no drag is on.</summary>
    private MapPosition? _from;
    private MapPosition _to;

    /// <summary>The drag moves the selection (it started on a selected thing); else it draws a marquee.</summary>
    private bool _moving;

    /// <summary>The pointer left the tile the button went down on: the release is no click, even back on that tile.</summary>
    private bool _dragged;
    private bool _add;
    private bool _remove;

    public override string Name => "Select";

    public override Key? Shortcut => Key.V;

    /// <summary>Whether a drag that starts on a thing that is not selected selects it and moves it (the Move tool), or draws a marquee.</summary>
    protected virtual bool MovesUnselected => false;

    protected virtual string HintText =>
        "Click a thing or drag a rectangle. Shift adds, Alt removes; a double click selects every object of that kind in view. "
        + "Drag a selected thing to move the selection.";

    public override void BuildOptions(ToolOptions options)
    {
        foreach (var (text, kind) in Kinds)
        {
            var box = new CheckBox { Text = text, ButtonPressed = Context.Filter.HasFlag(kind), TooltipText = $"Whether clicks and rectangles pick {text.ToLowerInvariant()}" };
            box.Toggled += on => Context.Filter = on ? Context.Filter | kind : Context.Filter & ~kind;
            options.Add(box);
        }

        options.Hint(HintText);
    }

    public override void Press(MapPosition tile)
    {
        var select = Editor.Selecting;
        var hit = select.At(tile, Context.Filter);
        (_add, _remove) = (Context.Pointer.Add, Context.Pointer.Remove);
        if (Context.Pointer.DoubleClick && hit.Objects.Count == 1)
        {
            Apply(select.OfKind(hit.Objects.First(), Context.VisibleArea()));
            Tell();
            return;
        }

        var thing = hit.Objects.Count + hit.Heroes.Count > 0;
        var selected = hit.Objects.Any(Editor.Selection.Contains) || hit.Heroes.Any(Editor.Selection.Contains);
        if (MovesUnselected && thing && !selected && !_add && !_remove)
        {
            select.Set(hit);
            selected = true;
        }

        (_from, _to) = (tile, tile);
        _dragged = false;
        _moving = selected && !_add && !_remove;
    }

    public override void Drag(MapPosition tile)
    {
        if (_from is not { } from || tile == _to || tile.Level != from.Level)
        {
            return;
        }

        _to = tile;
        _dragged = true;
        if (_moving)
        {
            Context.Marks?.ShowMove(tile == from ? null : Editor.Selecting.CheckMove(tile.X - from.X, tile.Y - from.Y));
        }
        else
        {
            Context.Marks?.ShowMarquee(TileArea.Between(from, tile));
        }
    }

    public override void Release()
    {
        if (_from is not { } from)
        {
            return;
        }

        Drop();
        var select = Editor.Selecting;
        if (_moving && _to != from)
        {
            if (Context.Report(select.Move(_to.X - from.X, _to.Y - from.Y)))
            {
                Tell();
            }

            return;
        }

        if (_moving && _dragged)
        {
            return; // dragged back to where it started: the move is called off, the selection stays
        }

        // A click picks what is under the pointer; a rectangle its tiles and what stands in it.
        Apply(_to == from ? select.At(from, Context.Filter) : select.In(TileArea.Between(from, _to), Context.Filter));
        Tell();
    }

    public override void Cancel() => Drop();

    private void Drop()
    {
        _from = null;
        Context.Marks?.ShowMarquee(null);
        Context.Marks?.ShowMove(null);
    }

    private void Apply(SelectionItems items)
    {
        if (_remove)
        {
            Editor.Selecting.Remove(items);
        }
        else if (_add)
        {
            Editor.Selecting.Add(items);
        }
        else
        {
            Editor.Selecting.Set(items);
        }
    }

    /// <summary>
    /// The status line counts what is selected; one object or one hero as the only thing opens in the inspector (which says so), and
    /// several objects with no hero open there together (ADR-255).
    /// </summary>
    private void Tell()
    {
        var selection = Editor.Selection;
        Context.Say(EditorToolContext.Describe(selection));
        if (selection.Objects.Count == 1 && selection.Heroes.Count == 0 && Editor.Scenario.FindObject(selection.Objects.First()) is { } mapObject)
        {
            Context.Inspect(mapObject);
        }
        else if (selection.Heroes.Count == 1 && selection.Objects.Count == 0)
        {
            Context.Inspect(selection.Heroes.First());
        }
        else if (selection.Objects.Count > 1 && selection.Heroes.Count == 0)
        {
            Context.Inspect([.. selection.Objects.OrderBy(o => o.Value)]);
        }
    }
}

/// <summary>
/// Moves things (EDIT-1, ADR-250): like the Select tool, but a drag that starts on an object or hero that is not selected selects it
/// alone and moves it.
/// </summary>
public sealed class MoveTool(EditorToolContext context) : SelectTool(context)
{
    public override string Name => "Move";

    public override Key? Shortcut => Key.M;

    protected override bool MovesUnselected => true;

    protected override string HintText =>
        "Drag an object or a hero to move it; drag a selected one to move the whole selection. Red cells show where it does not fit. "
        + "A drag over open ground selects a rectangle.";
}
