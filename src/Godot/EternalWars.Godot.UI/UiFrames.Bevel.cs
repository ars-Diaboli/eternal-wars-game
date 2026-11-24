using EternalWars.Godot.UI.Widgets;
using Godot;

namespace EternalWars.Godot.UI;

/// <summary>
/// The game's frames drawn by a shader (ADR-310): the <see cref="BevelFrame"/> behind a panel, a slot or a button in place of a painted or flat
/// box. A HUD part takes one directly (<see cref="Bevel"/>); the theme's boxes (<see cref="Themed"/>) are empty boxes marked with a kind, and
/// every control drawn with one gets its frame from <see cref="ThemedBevels"/>. The box gives way to an empty one with the same content
/// margins, so a part keeps its size and place.
/// </summary>
public static partial class UiFrames
{
    /// <summary>The metadata on a themed box: its <see cref="BevelKind"/>, its own opacity (or none: the setting's) and its rim's tint.</summary>
    internal const string BevelKindMeta = "ew_bevel_kind", BevelOpacityMeta = "ew_bevel_opacity", BevelTintMeta = "ew_bevel_tint";

    private static readonly string[] ButtonStates = ["normal", "hover", "pressed", "hover_pressed", "disabled", "focus"];

    /// <summary>
    /// A <paramref name="kind"/> frame behind <paramref name="host"/>: a panel's box, or every state's box of a button, becomes empty with the
    /// content margins of <paramref name="margins"/> (default: the box it has now, else <paramref name="padding"/> on each side).
    /// </summary>
    public static BevelFrame Bevel(Control host, BevelKind kind, StyleBox? margins = null, int? padding = null)
    {
        var states = host is BaseButton ? ButtonStates : ["panel"];
        margins ??= padding is null && host.HasThemeStylebox(states[0]) ? host.GetThemeStylebox(states[0]) : null;
        foreach (var state in states)
        {
            host.AddThemeStyleboxOverride(state, Empty(margins, padding ?? 0));
        }

        return BevelFrame.Behind(host, kind);
    }

    /// <summary>
    /// A box for the theme or an override that draws nothing itself: the control it is given to gets a <paramref name="kind"/> frame behind it
    /// (<see cref="ThemedBevels"/>), <paramref name="padding"/> inside (<paramref name="horizontal"/> at the sides, when given). An
    /// <paramref name="opacity"/> holds its fill at that (a window, a tooltip); none follows the panel opacity setting. A
    /// <paramref name="tint"/> colours the rim (a faction's accent, a spell school). In high contrast it is the flat <paramref name="flat"/>.
    /// </summary>
    public static StyleBox Themed(BevelKind kind, int padding, int? horizontal = null, float? opacity = 1f, Color? tint = null, StyleBox? flat = null)
    {
        if (UiTheme.HighContrast && flat is not null)
        {
            return flat;
        }

        var box = new StyleBoxEmpty();
        box.SetContentMarginAll(padding);
        if (horizontal is { } side)
        {
            box.ContentMarginLeft = box.ContentMarginRight = side;
        }

        box.SetMeta(BevelKindMeta, (int)kind);
        if (opacity is { } own)
        {
            box.SetMeta(BevelOpacityMeta, own);
        }

        if (tint is { } colour)
        {
            box.SetMeta(BevelTintMeta, colour);
        }

        return box;
    }

    /// <summary><see cref="Themed"/> with the content margins of <paramref name="margins"/> (a painted frame's, so nothing moves).</summary>
    public static StyleBox Themed(BevelKind kind, StyleBox? margins, int padding, float? opacity = 1f)
    {
        var box = Themed(kind, padding, opacity: opacity);
        if (margins is not null && box is StyleBoxEmpty)
        {
            box.ContentMarginLeft = margins.GetMargin(Side.Left);
            box.ContentMarginTop = margins.GetMargin(Side.Top);
            box.ContentMarginRight = margins.GetMargin(Side.Right);
            box.ContentMarginBottom = margins.GetMargin(Side.Bottom);
        }

        return box;
    }

    private static StyleBoxEmpty Empty(StyleBox? margins, int padding)
    {
        var box = new StyleBoxEmpty();
        if (margins is not null)
        {
            box.ContentMarginLeft = margins.GetMargin(Side.Left);
            box.ContentMarginTop = margins.GetMargin(Side.Top);
            box.ContentMarginRight = margins.GetMargin(Side.Right);
            box.ContentMarginBottom = margins.GetMargin(Side.Bottom);
        }
        else
        {
            box.SetContentMarginAll(padding);
        }

        return box;
    }
}

/// <summary>
/// Gives every control drawn with a themed box (<see cref="UiFrames.Themed"/>) its <see cref="BevelFrame"/> (ADR-310): panels, buttons, lists
/// and text fields, as they enter the tree and whenever their theme or an override changes. The frame is an internal child, so the screens'
/// own children and their order are untouched; a box that is no longer themed takes its frame away.
/// </summary>
public static class ThemedBevels
{
    private const string Watched = "ew_bevel_watched", Made = "ew_bevel_themed";
    private static bool _installed;

    /// <summary>Starts watching the scene tree (once; the game's theme, <see cref="UiTheme.Apply"/>).</summary>
    public static void Install()
    {
        if (_installed || Engine.GetMainLoop() is not SceneTree tree)
        {
            return;
        }

        _installed = true;
        tree.NodeAdded += OnNodeAdded;
    }

    private static void OnNodeAdded(Node node)
    {
        if (node is not (PanelContainer or Panel or Button or ItemList or Tree or LineEdit or TextEdit) || node.HasMeta(Watched))
        {
            return;
        }

        var control = (Control)node;
        control.SetMeta(Watched, true);
        var update = Callable.From(() => Update(control));
        control.ThemeChanged += () => update.CallDeferred(); // not while the control is busy with the notification
        update.CallDeferred(); // after the builder has set its overrides
    }

    private static void Update(Control control)
    {
        if (!GodotObject.IsInstanceValid(control))
        {
            return;
        }

        var state = control is BaseButton or LineEdit or TextEdit ? "normal" : "panel";
        var box = control.HasThemeStylebox(state) ? control.GetThemeStylebox(state) : null;
        var frame = control.GetChildren(includeInternal: true).OfType<BevelFrame>().FirstOrDefault(f => f.HasMeta(Made));
        if (box is null || !box.HasMeta(UiFrames.BevelKindMeta))
        {
            if (frame is not null)
            {
                control.RemoveChild(frame);
                frame.QueueFree();
            }

            return;
        }

        var kind = (BevelKind)box.GetMeta(UiFrames.BevelKindMeta).AsInt32();
        if (frame is not null && frame.Kind != kind)
        {
            control.RemoveChild(frame);
            frame.QueueFree();
            frame = null;
        }

        if (frame is null)
        {
            frame = new BevelFrame(kind);
            frame.SetMeta(Made, true);
            control.AddChild(frame, false, Node.InternalMode.Back);
        }

        frame.OwnOpacity = box.HasMeta(UiFrames.BevelOpacityMeta) ? box.GetMeta(UiFrames.BevelOpacityMeta).AsSingle() : null;
        frame.Tint(box.HasMeta(UiFrames.BevelTintMeta) ? box.GetMeta(UiFrames.BevelTintMeta).AsColor() : null, 0.55f);
    }
}
