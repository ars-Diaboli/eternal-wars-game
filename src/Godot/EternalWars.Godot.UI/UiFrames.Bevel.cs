using EternalWars.Godot.UI.Widgets;
using Godot;

namespace EternalWars.Godot.UI;

/// <summary>
/// The adventure HUD's frames (ADR-310): the shader-drawn <see cref="BevelFrame"/> behind a panel, a slot or a button in place of its painted
/// or flat box. The box gives way to an empty one with the same content margins, so the part keeps its size and place.
/// </summary>
public static partial class UiFrames
{
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
                box.SetContentMarginAll(padding ?? 0);
            }

            host.AddThemeStyleboxOverride(state, box);
        }

        return BevelFrame.Behind(host, kind);
    }
}
