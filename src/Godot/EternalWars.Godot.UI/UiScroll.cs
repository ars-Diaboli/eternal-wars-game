using Godot;

namespace EternalWars.Godot.UI;

/// <summary>
/// Scrolled panels that keep the vertical scroll bar's room while it is not needed (ADR-282). With the bar shown only when needed, a
/// panel whose height depends on its width (wrapping text, a flow of buttons or cards) can turn the bar on and off without end at some
/// widths, and the window hangs.
/// </summary>
public static class UiScroll
{
    /// <summary>
    /// A vertical scroll that keeps the bar's room. With <paramref name="horizontal"/> false (the default) it never scrolls sideways, so
    /// what it holds takes its width; true lets a panel wider than the room scroll sideways instead of widening it.
    /// </summary>
    public static ScrollContainer Vertical(string? name = null, bool horizontal = false)
    {
        var scroll = new ScrollContainer();
        if (name is not null)
        {
            scroll.Name = name;
        }

        return scroll.KeepBarRoom(horizontal);
    }

    /// <summary>Makes <paramref name="scroll"/> keep the vertical bar's room (and scroll sideways only with <paramref name="horizontal"/>).</summary>
    public static T KeepBarRoom<T>(this T scroll, bool horizontal = false) where T : ScrollContainer
    {
        scroll.VerticalScrollMode = ScrollContainer.ScrollMode.Reserve;
        scroll.HorizontalScrollMode = horizontal ? ScrollContainer.ScrollMode.Auto : ScrollContainer.ScrollMode.Disabled;
        return scroll;
    }
}
