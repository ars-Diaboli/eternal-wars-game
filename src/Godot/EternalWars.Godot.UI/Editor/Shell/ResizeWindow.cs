using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Map;
using Godot;

namespace EternalWars.Godot.UI.Editor.Shell;

/// <summary>
/// The "Resize map" window (EDIT-8, ADR-250), a box over the whole editor: a new width and height (the presets first) with a
/// nine-point anchor, or a shift of everything by some tiles. When objects, heroes, event cells or the Grail would end up off the
/// map, the window lists them and asks before they are removed in the same undo step. Escape or "Cancel" closes it with nothing changed.
/// </summary>
public partial class ResizeWindow : Control
{
    /// <summary>How many of the things that would leave the map are listed by name; the rest are counted.</summary>
    private const int Listed = 12;

    private static readonly string[] AnchorTexts = ["↖", "↑", "↗", "←", "•", "→", "↙", "↓", "↘"];

    private static readonly string[] AnchorNames =
        ["top left", "top", "top right", "left", "middle", "right", "bottom left", "bottom", "bottom right"];

    private MapEditorSession _editor = null!;
    private Action<string> _say = _ => { };
    private VBoxContainer _rows = null!;
    private ResizeAnchor _anchor = ResizeAnchor.TopLeft;

    /// <summary>Opens the window on its fields.</summary>
    public void Bind(MapEditorSession editor, Action<string> say)
    {
        (_editor, _say) = (editor, say);
        _rows = EditorOverlay.Build(this);
        ShowFields();
    }

    /// <summary>
    /// Crop to the selection, without the fields: runs at once when nothing would be lost, else opens the window on its question.
    /// Returns the window when it has to be shown (the caller adds it to the tree first), null when the crop was made or refused.
    /// </summary>
    public static ResizeWindow? Crop(MapEditorSession editor, Action<string> say)
    {
        var check = editor.CheckCrop();
        if (check.Result != EditResult.Ok)
        {
            say(check.Result == EditResult.InvalidTarget
                ? "Select the tiles to keep first (the Select tool's rectangle)"
                : $"The selection is under {MapSizes.MinDimension} tiles wide or high: too small for a map");
            return null;
        }

        if (!check.LosesThings)
        {
            Done(editor.Crop(), check, say);
            return null;
        }

        var window = new ResizeWindow { Name = "Resize" };
        (window._editor, window._say) = (editor, say);
        window._rows = EditorOverlay.Build(window);
        window.Ask(check, "Crop", () => editor.Crop(), back: null);
        return window;
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            GetViewport()?.SetInputAsHandled();
            EditorOverlay.Close(this);
        }
    }

    private static void Done(EditResult result, ResizeCheck check, Action<string> say)
    {
        var removed = check.Objects.Count + check.Heroes.Count;
        say(result != EditResult.Ok ? $"Refused: {result}"
            : check.IsNoChange ? "The map is as it was"
            : $"The map is {check.Width} × {check.Height} tiles" + (removed > 0 ? $"; {removed} thing(s) were removed with the ground they stood on (Ctrl+Z brings them back)" : ""));
    }

    private void ShowFields()
    {
        Clear();
        var map = _editor.Scenario.Map;
        _rows.AddChild(new Label { Text = "Resize map", HorizontalAlignment = HorizontalAlignment.Center });
        _rows.AddChild(new Label { Text = $"The map is {map.Width} × {map.Height} tiles." });

        var width = Spin(MapSizes.MinDimension, MapSizes.MaxDimension, map.Width);
        var height = Spin(MapSizes.MinDimension, MapSizes.MaxDimension, map.Height);
        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", UiSpacing.Section);
        grid.AddThemeConstantOverride("v_separation", UiSpacing.Row);
        _rows.AddChild(grid);
        grid.AddChild(new Label { Text = "Width" });
        grid.AddChild(width);
        grid.AddChild(new Label { Text = "Height" });
        grid.AddChild(height);

        var presets = new HBoxContainer();
        _rows.AddChild(presets);
        presets.AddChild(new Label { Text = "Square" });
        foreach (var size in MapSizes.Standard)
        {
            Button(presets, size.ToString(System.Globalization.CultureInfo.InvariantCulture), () => (width.Value, height.Value) = (size, size), 0)
                .TooltipText = $"{size} × {size} tiles";
        }

        // Where the map as it is now sits in the resized one: ground is added or cut on the other sides.
        var anchorRow = new HBoxContainer();
        _rows.AddChild(anchorRow);
        anchorRow.AddChild(new Label { Text = "The map stays at", SizeFlagsVertical = SizeFlags.ShrinkCenter });
        var anchors = new GridContainer { Columns = 3 };
        anchorRow.AddChild(anchors);
        var group = new ButtonGroup();
        for (var i = 0; i < AnchorTexts.Length; i++)
        {
            var anchor = (ResizeAnchor)i;
            var button = new Button
            {
                Text = AnchorTexts[i], ToggleMode = true, ButtonGroup = group, ButtonPressed = anchor == _anchor,
                CustomMinimumSize = new Vector2(34, 30), TooltipText = $"The map stays at the {AnchorNames[i]}",
            };
            button.Pressed += () => _anchor = anchor;
            anchors.AddChild(button);
        }

        var message = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(380, 0) };
        var resize = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        _rows.AddChild(resize);
        Button(resize, "Resize", () =>
        {
            width.Apply(); // a number typed but not yet entered counts
            height.Apply();
            var (w, h, anchor) = ((int)width.Value, (int)height.Value, _anchor);
            Try(_editor.CheckResize(w, h, anchor), "Resize", () => _editor.Resize(w, h, anchor), message);
        });

        // The width has the focus, all of it selected: typing a number replaces it, Tab goes on to the height.
        width.GetLineEdit().CallDeferred(Control.MethodName.GrabFocus);
        width.GetLineEdit().CallDeferred(LineEdit.MethodName.SelectAll);
        height.GetLineEdit().SelectAllOnFocus = true;

        _rows.AddChild(new HSeparator());
        var shift = new HBoxContainer();
        _rows.AddChild(shift);
        var dx = Spin(1 - map.Width, map.Width - 1, 0);
        var dy = Spin(1 - map.Height, map.Height - 1, 0);
        dx.TooltipText = "Tiles to the right (negative: to the left)";
        dy.TooltipText = "Tiles down (negative: up)";
        shift.AddChild(new Label { Text = "Shift everything by" });
        shift.AddChild(dx);
        shift.AddChild(dy);
        Button(shift, "Shift", () =>
        {
            dx.Apply();
            dy.Apply();
            var (x, y) = ((int)dx.Value, (int)dy.Value);
            Try(_editor.CheckShift(x, y), "Shift", () => _editor.Shift(x, y), message);
        });

        _rows.AddChild(message);
        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        _rows.AddChild(buttons);
        Button(buttons, "Cancel", () => EditorOverlay.Close(this));
    }

    /// <summary>Runs the change when nothing would be lost, asks first when something would, and says why when it is refused.</summary>
    private void Try(ResizeCheck check, string verb, Func<EditResult> run, Label message)
    {
        if (check.Result != EditResult.Ok)
        {
            message.Text = $"Refused: {check.Result}";
        }
        else if (check.LosesThings)
        {
            Ask(check, verb, run, ShowFields);
        }
        else if (EditorOverlay.Close(this))
        {
            Done(run(), check, _say);
        }
    }

    /// <summary>The question before things are removed: what would leave the map, then "Remove and …" or back (cancel when there are no fields to go back to).</summary>
    private void Ask(ResizeCheck check, string verb, Func<EditResult> run, Action? back)
    {
        Clear();
        _rows.AddChild(new Label { Text = $"{verb} to {check.Width} × {check.Height} tiles", HorizontalAlignment = HorizontalAlignment.Center });
        _rows.AddChild(new Label
        {
            Text = "These would be off the map. They are removed in the same step; Undo brings them back.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(420, 0),
        });

        var lines = check.Heroes.Select(h => $"Hero {h.Name} at {h.Position.X}, {h.Position.Y}")
            .Concat(check.Objects.Select(o => $"{o.Definition.Name} at {o.Position.X}, {o.Position.Y}"))
            .ToList();
        var list = new ItemList { CustomMinimumSize = new Vector2(420, 220), FocusMode = FocusModeEnum.None };
        _rows.AddChild(list);
        foreach (var line in lines.Take(Listed))
        {
            list.AddItem(line, selectable: false);
        }

        if (lines.Count > Listed)
        {
            list.AddItem($"… and {lines.Count - Listed} more objects and heroes", selectable: false);
        }

        if (check.EventCells > 0)
        {
            list.AddItem(check.EventCells == 1 ? "1 event cell" : $"{check.EventCells} event cells", selectable: false);
        }

        if (check.LosesGrail)
        {
            list.AddItem("The Grail (too near the new edge)", selectable: false);
        }

        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        _rows.AddChild(buttons);
        Button(buttons, $"Remove and {verb.ToLowerInvariant()}", () =>
        {
            if (EditorOverlay.Close(this))
            {
                Done(run(), check, _say);
            }
        }, 160).CallDeferred(Control.MethodName.GrabFocus);
        if (back is not null)
        {
            Button(buttons, "Back", back);
        }

        Button(buttons, "Cancel", () => EditorOverlay.Close(this));
    }

    private void Clear()
    {
        foreach (var child in _rows.GetChildren())
        {
            _rows.RemoveChild(child);
            child.QueueFree();
        }
    }

    private static SpinBox Spin(int min, int max, int value) =>
        new() { MinValue = min, MaxValue = max, Value = value, Rounded = true, CustomMinimumSize = new Vector2(110, 0) };

    private static Button Button(Container parent, string text, Action pressed, int width = 110)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(width, 0) };
        button.Pressed += pressed;
        parent.AddChild(button);
        return button;
    }
}
