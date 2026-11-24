namespace EternalWars.Core.Domain.Settings;

/// <summary>A part of the adventure map's HUD that stands on a side of the screen (ADR-309).</summary>
public enum HudPart
{
    /// <summary>The resource bar, along the top.</summary>
    Resources,

    /// <summary>The minimap with its level caption and zoom buttons, at the top.</summary>
    Minimap,

    /// <summary>The hero list, down an edge.</summary>
    HeroList,

    /// <summary>The town list, down an edge.</summary>
    TownList,

    /// <summary>The selected hero's card, in the bottom row.</summary>
    HeroCard,

    /// <summary>The selected hero's army, in the bottom row.</summary>
    ArmyStrip,

    /// <summary>The 2 × 5 action buttons, in the bottom row.</summary>
    Actions,

    /// <summary>The End turn hourglass, in the bottom row.</summary>
    EndTurn,
}

/// <summary>
/// Where the adventure map's HUD parts stand (ADR-309): one arrangement, or its mirror with left and right swapped. By default the resources
/// stand at the top left and the minimap at the top right; the hero list runs down the left edge and the town list down the right; the bottom
/// row holds, from the left, the hero card, the army strip, the action buttons and End turn. The turn banner stays at the top centre either way.
/// </summary>
public static class HudArrangement
{
    /// <summary>The side of the screen <paramref name="part"/> stands on.</summary>
    public static ScreenSide SideOf(LayoutSettings layout, HudPart part)
    {
        var side = part is HudPart.Resources or HudPart.HeroList or HudPart.HeroCard or HudPart.ArmyStrip ? ScreenSide.Left : ScreenSide.Right;
        return layout.Mirrored ? Opposite(side) : side;
    }

    /// <summary>The bottom row's parts, from the left.</summary>
    public static IReadOnlyList<HudPart> BottomRow(LayoutSettings layout) => layout.Mirrored
        ? [HudPart.EndTurn, HudPart.Actions, HudPart.ArmyStrip, HudPart.HeroCard]
        : [HudPart.HeroCard, HudPart.ArmyStrip, HudPart.Actions, HudPart.EndTurn];

    public static ScreenSide Opposite(ScreenSide side) => side == ScreenSide.Left ? ScreenSide.Right : ScreenSide.Left;
}
