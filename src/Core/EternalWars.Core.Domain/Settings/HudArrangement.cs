namespace EternalWars.Core.Domain.Settings;

/// <summary>A part of the adventure map's HUD that the layout settings move.</summary>
public enum HudPart
{
    /// <summary>The minimap and its "Switch level" button.</summary>
    Minimap,

    /// <summary>The hero and town lists.</summary>
    Lists,

    /// <summary>The action panel: kingdom, quests, puzzle map, dig, marketplace.</summary>
    Actions,
}

/// <summary>
/// Where the adventure map's HUD parts stand for some <see cref="LayoutSettings"/> (ADR-156). Each corner holds a stack of parts, listed
/// from the corner outwards. The minimap is always nearest its corner. Vertical lists follow it in its stack. Horizontal lists go to the
/// bottom row between the bottom corners. The action panel goes to the top corner of its side, after the minimap and lists if they are
/// there. The turn, resources and status block is not moved: it stays at the top left, beside whatever the top-left stack holds.
/// </summary>
public static class HudArrangement
{
    /// <summary>The parts in the stack at <paramref name="corner"/>, from the corner outwards; empty when the corner holds none.</summary>
    public static IReadOnlyList<HudPart> Stack(LayoutSettings layout, ScreenCorner corner)
    {
        var parts = new List<HudPart>();
        if (layout.Minimap == corner)
        {
            parts.Add(HudPart.Minimap);
            if (layout.Lists == ListOrientation.Vertical)
            {
                parts.Add(HudPart.Lists);
            }
        }

        if (corner == TopCorner(layout.ActionPanel))
        {
            parts.Add(HudPart.Actions);
        }

        return parts;
    }

    /// <summary>Whether the hero and town lists run as rows along the bottom of the screen.</summary>
    public static bool ListsInBottomRow(LayoutSettings layout) => layout.Lists == ListOrientation.Horizontal;

    /// <summary>The corner whose stack holds <paramref name="part"/>, or null for lists in the bottom row.</summary>
    public static ScreenCorner? CornerOf(LayoutSettings layout, HudPart part) =>
        Enum.GetValues<ScreenCorner>().Where(c => Stack(layout, c).Contains(part)).Select(c => (ScreenCorner?)c).FirstOrDefault();

    public static bool IsTop(ScreenCorner corner) => corner is ScreenCorner.TopLeft or ScreenCorner.TopRight;

    public static bool IsLeft(ScreenCorner corner) => corner is ScreenCorner.TopLeft or ScreenCorner.BottomLeft;

    private static ScreenCorner TopCorner(ScreenSide side) => side == ScreenSide.Left ? ScreenCorner.TopLeft : ScreenCorner.TopRight;
}
