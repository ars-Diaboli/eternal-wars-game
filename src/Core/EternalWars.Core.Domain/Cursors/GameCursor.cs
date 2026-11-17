namespace EternalWars.Core.Domain.Cursors;

/// <summary>
/// The mouse cursors (ADR-204, docs/content/cursor-prompts.md): what a click would do where the pointer is. The front-ends map each to its
/// image (<c>cursor_&lt;group&gt;_&lt;id&gt;.png</c>) or, while the image is missing, to the nearest system cursor.
/// </summary>
public enum GameCursor
{
    /// <summary>The standard arrow: menus, screens, panels, and the map with nothing to do there.</summary>
    Pointer,

    /// <summary>The computer is playing, or a game loads.</summary>
    Busy,

    /// <summary>Nothing can be done here (an unreachable tile, a cell no stack can reach).</summary>
    Blocked,

    /// <summary>Adventure map: go there.</summary>
    Move,

    /// <summary>Adventure map: go and visit the object there.</summary>
    Visit,

    /// <summary>Adventure map: go and fight what is there (an enemy hero or town, a neutral stack).</summary>
    Attack,

    /// <summary>Adventure map: go and meet an own hero (exchange armies and artifacts).</summary>
    Exchange,

    /// <summary>Adventure map: go into an own town.</summary>
    EnterTown,

    /// <summary>Adventure map: an adventure spell waits for its cell.</summary>
    AdventureSpell,

    /// <summary>Adventure map: select this own (or allied) hero.</summary>
    SelectHero,

    /// <summary>Adventure map: open this own (or allied) town.</summary>
    SelectTown,

    /// <summary>Battle: strike in melee from the north.</summary>
    MeleeFromNorth,

    /// <summary>Battle: strike in melee from the north-east.</summary>
    MeleeFromNorthEast,

    /// <summary>Battle: strike in melee from the east.</summary>
    MeleeFromEast,

    /// <summary>Battle: strike in melee from the south-east.</summary>
    MeleeFromSouthEast,

    /// <summary>Battle: strike in melee from the south.</summary>
    MeleeFromSouth,

    /// <summary>Battle: strike in melee from the south-west.</summary>
    MeleeFromSouthWest,

    /// <summary>Battle: strike in melee from the west.</summary>
    MeleeFromWest,

    /// <summary>Battle: strike in melee from the north-west.</summary>
    MeleeFromNorthWest,

    /// <summary>Battle: shoot at full or nearly full strength.</summary>
    Shoot,

    /// <summary>Battle: a shot that deals half or less (range, walls, obstacles).</summary>
    ShootWeak,

    /// <summary>Battle: the armed spell's one target.</summary>
    Spell,

    /// <summary>Battle: the armed area spell's centre.</summary>
    SpellArea,

    /// <summary>Battle: the controlled siege engine fires at a fortification.</summary>
    Siege,

    /// <summary>Battle: walk there.</summary>
    Walk,

    /// <summary>Battle: fly there.</summary>
    Fly,

    /// <summary>Battle: teleport there.</summary>
    Teleport,
}

/// <summary>A cursor and, for <see cref="GameCursor.Move"/> and the like, the days the route takes (1 = this turn; the cursor shows 2 and more).</summary>
public readonly record struct CursorChoice(GameCursor Cursor, int Days = 1)
{
    public static implicit operator CursorChoice(GameCursor cursor) => new(cursor);
}
