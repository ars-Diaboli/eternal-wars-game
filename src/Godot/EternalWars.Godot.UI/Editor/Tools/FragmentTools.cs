using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using Godot;

namespace EternalWars.Godot.UI.Editor.Tools;

/// <summary>
/// Erases by kind under the brush (EDIT-1, ADR-250): a stroke is one undo step. The boxes say which kinds go; a button erases those
/// kinds over the selection instead.
/// </summary>
public sealed class EraseTool(EditorToolContext context) : TileBrushTool(context)
{
    private static readonly (string Text, EraseKinds Kind)[] Kinds =
    [
        ("Objects", EraseKinds.Objects), ("Scenery", EraseKinds.Scenery), ("Monsters", EraseKinds.Monsters), ("Heroes", EraseKinds.Heroes),
        ("Roads", EraseKinds.Roads), ("Rivers", EraseKinds.Rivers), ("Events", EraseKinds.Events),
    ];

    private EraseKinds _kinds = EraseKinds.Objects | EraseKinds.Scenery | EraseKinds.Monsters | EraseKinds.Heroes;

    public override string Name => "Erase";

    public override Key? Shortcut => Key.E;

    protected override string StrokeDescription => "Erase";

    public override void BuildOptions(ToolOptions options)
    {
        foreach (var (text, kind) in Kinds)
        {
            var box = new CheckBox { Text = text, ButtonPressed = _kinds.HasFlag(kind), TooltipText = $"Whether the eraser takes {text.ToLowerInvariant()}" };
            box.Toggled += on => _kinds = on ? _kinds | kind : _kinds & ~kind;
            options.Add(box);
        }

        BrushOptions(options);
        var selection = new Button { Text = "Erase in selection", TooltipText = "Erases the ticked kinds over the selection (one undo step)" };
        selection.Pressed += () => Context.Say($"Erased {Editor.Erasing.EraseSelection(_kinds)} thing(s) in the selection");
        options.Add(selection);
        options.Hint("Drag over the map to erase the ticked kinds under the brush. An object goes when the brush touches any of its cells.");
    }

    protected override void Paint(IEnumerable<MapPosition> tiles) => Editor.Erasing.Erase(tiles, _kinds);
}

/// <summary>
/// Pastes a fragment where clicked (EDIT-4, ADR-250): the clipboard's, a copy of the selection (Duplicate) or a stamp. The fragment's middle
/// follows the pointer as a ghost, red where an object does not fit; every click pastes once more. The boxes say which layers go on the
/// map.
/// </summary>
public sealed class PasteTool(EditorToolContext context) : EditorTool(context)
{
    private static readonly (string Text, FragmentLayers Layer)[] Layers =
    [
        ("Terrain", FragmentLayers.Terrain), ("Heights", FragmentLayers.Heights), ("Roads", FragmentLayers.Roads),
        ("Rivers", FragmentLayers.Rivers), ("Objects", FragmentLayers.Objects),
    ];

    /// <summary>Duplicate or a stamp: a fragment of the tool's own, kept apart from the clipboard. Null: the clipboard is pasted.</summary>
    private MapFragment? _duplicate;
    private MapPosition? _hovered;

    public override string Name => "Paste";

    private MapFragment? Fragment => _duplicate ?? Context.Clipboard.Fragment;

    /// <summary>The tool pastes the clipboard (Paste).</summary>
    public void UseClipboard()
    {
        _duplicate = null;
        ShowGhost(_hovered);
    }

    /// <summary>The tool pastes a copy of what is selected now (Duplicate). False when no tile and no object is selected.</summary>
    public bool UseSelection()
    {
        if (Editor.Fragments.Copy() is not { } copy)
        {
            return false;
        }

        _duplicate = copy;
        ShowGhost(_hovered);
        return true;
    }

    /// <summary>The tool pastes <paramref name="fragment"/> (a stamp), leaving the clipboard as it was.</summary>
    public void UseFragment(MapFragment fragment)
    {
        _duplicate = fragment;
        ShowGhost(_hovered);
    }

    public override void BuildOptions(ToolOptions options)
    {
        foreach (var (text, layer) in Layers)
        {
            var box = new CheckBox { Text = text, ButtonPressed = Context.PasteLayers.HasFlag(layer), TooltipText = $"Whether a paste brings the {text.ToLowerInvariant()}" };
            box.Toggled += on =>
            {
                Context.PasteLayers = on ? Context.PasteLayers | layer : Context.PasteLayers & ~layer;
                ShowGhost(_hovered);
            };
            options.Add(box);
        }

        options.Hint("Click where the copied piece goes; every click pastes it again. Red cells show where an object does not fit. Escape ends.");
    }

    public override void Activate()
    {
        if (Fragment is null)
        {
            Context.Say("Nothing was copied: select something and copy it (Ctrl+C) first.");
        }
    }

    public override void Hover(MapPosition? tile)
    {
        if (tile != _hovered)
        {
            ShowGhost(tile);
        }
    }

    public override void Press(MapPosition tile)
    {
        if (Fragment is not { } fragment)
        {
            Context.Say("Nothing was copied.");
            return;
        }

        if (Context.Report(Editor.Fragments.Paste(fragment, Origin(fragment, tile), Context.PasteLayers)))
        {
            Context.Say(EditorToolContext.Describe(Editor.Selection).Replace("selected", "pasted", StringComparison.Ordinal));
        }

        ShowGhost(tile);
    }

    public override void Drag(MapPosition tile) => Hover(tile);

    public override void Refresh() => ShowGhost(_hovered);

    public override void Cancel()
    {
        _duplicate = null;
        ShowGhost(null);
    }

    /// <summary>The fragment's top-left tile when its middle is on <paramref name="tile"/>.</summary>
    private static MapPosition Origin(MapFragment fragment, MapPosition tile) => tile.Offset(-((fragment.Width - 1) / 2), -((fragment.Height - 1) / 2));

    private void ShowGhost(MapPosition? tile)
    {
        _hovered = tile;
        Context.Marks?.ShowMove(tile is { } at && Fragment is { } fragment ? Editor.Fragments.CheckPaste(fragment, Origin(fragment, at), Context.PasteLayers) : null);
    }
}
