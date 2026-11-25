namespace EternalWars.Core.Domain.Cursors;

/// <summary>What a key does in a battle (ADR-314).</summary>
public enum BattleHotkeyAction
{
    Wait,
    Defend,
    QuickCombat,
    Retreat,
    SpellBook,
    Menu,

    /// <summary>Enter: starts the battle after deployment, leaves the result window.</summary>
    Confirm,

    /// <summary>Escape: puts back an armed spell, cancels a deployment drag, leaves the result window.</summary>
    Cancel,
}

/// <summary>One battle hotkey: the action, its key (a Godot key name: "W", "F10", "Enter") and the action's name.</summary>
public sealed record BattleHotkey(BattleHotkeyAction Action, string Key, string Name)
{
    /// <summary>The status bar's hint: "Wait [W]".</summary>
    public string Hint => $"{Name} [{Key}]";

    /// <summary>The hint under another name, for a key that does more than one thing: "Start battle [Enter]".</summary>
    public string HintAs(string name) => $"{name} [{Key}]";
}

/// <summary>
/// The battle's hotkeys (ADR-314), one table for the status bar's hints and the battle's input controller, as <see cref="AdventureHotkeys"/> is
/// the map's. Surrender and Combat settings have no key. The keys are Godot key names, parsed by the input controller; the numeric keypad's
/// Enter counts as Enter. F10 opens the game's menu, which the app handles on the map and in battle alike.
/// </summary>
public static class BattleHotkeys
{
    /// <summary>Every hotkey, in the action grid's order, then Enter and Escape.</summary>
    public static IReadOnlyList<BattleHotkey> All { get; } =
    [
        new(BattleHotkeyAction.Wait, "W", "Wait"),
        new(BattleHotkeyAction.Defend, "D", "Defend"),
        new(BattleHotkeyAction.QuickCombat, "Q", "Quick combat"),
        new(BattleHotkeyAction.Retreat, "R", "Retreat"),
        new(BattleHotkeyAction.SpellBook, "C", "Spell book"),
        new(BattleHotkeyAction.Menu, "F10", "Menu"),
        new(BattleHotkeyAction.Confirm, "Enter", "Confirm"),
        new(BattleHotkeyAction.Cancel, "Escape", "Cancel"),
    ];

    private static readonly Dictionary<BattleHotkeyAction, BattleHotkey> ByAction = All.ToDictionary(h => h.Action);
    private static readonly Dictionary<string, BattleHotkey> ByKey = All.ToDictionary(h => h.Key, StringComparer.OrdinalIgnoreCase);

    /// <summary>The hotkey of <paramref name="action"/>.</summary>
    public static BattleHotkey Of(BattleHotkeyAction action) => ByAction[action];

    /// <summary>The action on the key named <paramref name="key"/> ("KpEnter" is Enter); null for a key the battle does not use.</summary>
    public static BattleHotkeyAction? ActionOf(string key) =>
        ByKey.TryGetValue(string.Equals(key, "KpEnter", StringComparison.OrdinalIgnoreCase) ? "Enter" : key, out var hotkey) ? hotkey.Action : null;
}
