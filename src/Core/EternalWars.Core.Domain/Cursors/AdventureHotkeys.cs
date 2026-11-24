namespace EternalWars.Core.Domain.Cursors;

/// <summary>What a key or an action button does on the adventure map (ADR-309).</summary>
public enum AdventureAction
{
    NextHero,
    NextTown,
    HeroScreen,
    SpellBook,
    Kingdom,
    Quests,
    PuzzleMap,
    Dig,
    SwitchLevel,
    Menu,
    Marketplace,
    Move,
    EndTurn,
}

/// <summary>One adventure-map hotkey: the action, its key (a Godot key name: "Tab", "Space", "F10", "Z") and the action's name.</summary>
public sealed record AdventureHotkey(AdventureAction Action, string Key, string Name)
{
    /// <summary>The status bar's hint: "Dig [G]".</summary>
    public string Hint => $"{Name} [{Key}]";
}

/// <summary>
/// The adventure map's hotkeys (ADR-309), one table for the status bar's hints and the input controller's keys. M (Marketplace), Space
/// (Move) and Z (End turn) are new; Space is play/pause only in the replay viewer, where Move is disabled. The keys are Godot key names,
/// parsed by the input controller; they are fixed until the map's keys become rebindable input actions as the town screen's are (ADR-294).
/// </summary>
public static class AdventureHotkeys
{
    /// <summary>Every hotkey, in the order the plan lists them.</summary>
    public static IReadOnlyList<AdventureHotkey> All { get; } =
    [
        new(AdventureAction.NextHero, "Tab", "Next hero"),
        new(AdventureAction.NextTown, "T", "Next town"),
        new(AdventureAction.HeroScreen, "H", "Hero screen"),
        new(AdventureAction.SpellBook, "C", "Spell book"),
        new(AdventureAction.Kingdom, "K", "Kingdom"),
        new(AdventureAction.Quests, "Q", "Quests"),
        new(AdventureAction.PuzzleMap, "P", "Puzzle map"),
        new(AdventureAction.Dig, "G", "Dig"),
        new(AdventureAction.SwitchLevel, "L", "Switch level"),
        new(AdventureAction.Menu, "F10", "Menu"),
        new(AdventureAction.Marketplace, "M", "Marketplace"),
        new(AdventureAction.Move, "Space", "Move"),
        new(AdventureAction.EndTurn, "Z", "End turn"),
    ];

    private static readonly Dictionary<AdventureAction, AdventureHotkey> ByAction = All.ToDictionary(h => h.Action);
    private static readonly Dictionary<string, AdventureHotkey> ByKey = All.ToDictionary(h => h.Key, StringComparer.OrdinalIgnoreCase);

    /// <summary>The hotkey of <paramref name="action"/>.</summary>
    public static AdventureHotkey Of(AdventureAction action) => ByAction[action];

    /// <summary>The action on the key named <paramref name="key"/>; null for a key the map does not use.</summary>
    public static AdventureAction? ActionOf(string key) => ByKey.TryGetValue(key, out var hotkey) ? hotkey.Action : null;
}
