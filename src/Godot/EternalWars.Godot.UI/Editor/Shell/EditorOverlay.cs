using Godot;

namespace EternalWars.Godot.UI.Editor.Shell;

/// <summary>
/// What the editor's boxes over the whole window share (the <see cref="QuestionBox"/>, the <see cref="CommandPalette"/>, the
/// <see cref="PreferencesWindow"/>): a dim ground that takes the pointer, so nothing under it does, and a framed panel on it.
/// </summary>
internal static class EditorOverlay
{
    /// <summary>How far under the window's top a box placed there stands.</summary>
    private const int TopOffset = 90;

    /// <summary>The least room between a box and the window's edges.</summary>
    private const int Margin = 8;

    /// <summary>
    /// Fills <paramref name="host"/> with the dim ground and the panel, in the window's middle or, with <paramref name="atTop"/>, under its
    /// top edge. A panel larger than the window (a large UI scale, ADR-277) scrolls inside it instead of reaching past its edges. Returns the
    /// panel's rows.
    /// </summary>
    public static VBoxContainer Build(Control host, bool atTop = false)
    {
        host.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.5f), MouseFilter = Control.MouseFilterEnum.Stop };
        host.AddChild(dim);
        dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var room = new ScrollContainer { Name = "Room", MouseFilter = Control.MouseFilterEnum.Pass, FollowFocus = true };
        host.AddChild(room);
        room.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        room.OffsetTop = atTop ? TopOffset : Margin;
        room.OffsetBottom = -Margin;
        room.OffsetLeft = Margin;
        room.OffsetRight = -Margin;
        var centre = new CenterContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = atTop ? Control.SizeFlags.ShrinkBegin : Control.SizeFlags.ExpandFill,
        };
        room.AddChild(centre);

        var panel = new PanelContainer { ThemeTypeVariation = UiTheme.DialogPanel };
        centre.AddChild(panel);
        var rows = new VBoxContainer();
        rows.AddThemeConstantOverride("separation", UiSpacing.Padding * 2);
        panel.AddChild(rows);
        return rows;
    }

    /// <summary>Takes <paramref name="host"/> off the window at once (what runs next may open another box) and frees it. False when it was closed already.</summary>
    public static bool Close(Control host)
    {
        if (host.IsQueuedForDeletion())
        {
            return false;
        }

        host.GetParent()?.RemoveChild(host);
        host.QueueFree();
        return true;
    }
}
