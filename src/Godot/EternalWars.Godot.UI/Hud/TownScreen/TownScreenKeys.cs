using Godot;

namespace EternalWars.Godot.UI.Hud.TownScreen;

/// <summary>What a key does on the town screen (the plan's hotkeys, ADR-294).</summary>
public enum TownKey
{
    Hall,
    Recruitment,
    Guild,
    Tavern,
    Marketplace,
    Blacksmith,
    Fort,
    Caravan,
    UpgradeAll,
    PreviousTown,
    NextTown,
    Hero,
    SwapHeroes,
    Back,
    TurnLeft,
    TurnRight,
    ZoomIn,
    ZoomOut,
    RingLeft,
    RingRight,
    RingOpen,
}

/// <summary>
/// The town screen's keys (ADR-294): B, R, G, V, M, S, F, C, U, [ and ], H, Space and Escape, and the camera's Q and E (held, turning it as on the
/// adventure map) and + and − (zoom, as the wheel; ADR-302), and the ring of plots (ADR-304): ← and → walk the buildings of the scene, Enter opens
/// the one reached. The controller has the ring on the d-pad with A to open, B for Escape and the shoulder buttons for [ and ]. They are input
/// actions the game app adds to its input map (<see cref="AddToInputMap"/>), so they can be rebound later. Each key's button shows its label with the key on the status bar
/// (<see cref="Hint"/>, ADR-204). A screen in an app without the actions (the 2D harness) falls back to the default keys.
/// </summary>
public static class TownScreenKeys
{
    /// <summary>Each key's action, default key and label.</summary>
    public static readonly IReadOnlyDictionary<TownKey, (string Action, Key Key, string Label)> All = new Dictionary<TownKey, (string, Key, string)>
    {
        [TownKey.Hall] = ("town_hall", Key.B, "Hall"),
        [TownKey.Recruitment] = ("town_recruitment", Key.R, "Recruitment"),
        [TownKey.Guild] = ("town_guild", Key.G, "Mage guild"),
        [TownKey.Tavern] = ("town_tavern", Key.V, "Tavern"),
        [TownKey.Marketplace] = ("town_marketplace", Key.M, "Marketplace"),
        [TownKey.Blacksmith] = ("town_blacksmith", Key.S, "Blacksmith"),
        [TownKey.Fort] = ("town_fort", Key.F, "Fort"),
        [TownKey.Caravan] = ("town_caravan", Key.C, "Caravan"),
        [TownKey.UpgradeAll] = ("town_upgrade_all", Key.U, "Upgrade all"),
        [TownKey.PreviousTown] = ("town_previous", Key.Bracketleft, "Previous town"),
        [TownKey.NextTown] = ("town_next", Key.Bracketright, "Next town"),
        [TownKey.Hero] = ("town_hero", Key.H, "Hero screen"),
        [TownKey.SwapHeroes] = ("town_swap_heroes", Key.Space, "Swap the heroes"),
        [TownKey.Back] = ("town_back", Key.Escape, "Back"),
        [TownKey.TurnLeft] = ("town_turn_left", Key.Q, "Turn left"),
        [TownKey.TurnRight] = ("town_turn_right", Key.E, "Turn right"),
        [TownKey.ZoomIn] = ("town_zoom_in", Key.Equal, "Zoom in"),
        [TownKey.ZoomOut] = ("town_zoom_out", Key.Minus, "Zoom out"),
        [TownKey.RingLeft] = ("town_ring_left", Key.Left, "Previous building"),
        [TownKey.RingRight] = ("town_ring_right", Key.Right, "Next building"),
        [TownKey.RingOpen] = ("town_ring_open", Key.Enter, "Open the building"),
    };

    /// <summary>The controller's button for a key, where it has one (ADR-304).</summary>
    private static JoyButton? JoyOf(TownKey key) => key switch
    {
        TownKey.Back => JoyButton.B,
        TownKey.PreviousTown => JoyButton.LeftShoulder,
        TownKey.NextTown => JoyButton.RightShoulder,
        TownKey.RingLeft => JoyButton.DpadLeft,
        TownKey.RingRight => JoyButton.DpadRight,
        TownKey.RingOpen => JoyButton.A,
        _ => null,
    };

    /// <summary>Adds the actions with their default keys to the app's input map (the game app, at start); an action already there stays.</summary>
    public static void AddToInputMap()
    {
        foreach (var (town, (action, key, _)) in All)
        {
            if (InputMap.HasAction(action))
            {
                continue;
            }

            InputMap.AddAction(action);
            InputMap.ActionAddEvent(action, new InputEventKey { Keycode = key });
            foreach (var also in AlsoKeys(key))
            {
                InputMap.ActionAddEvent(action, new InputEventKey { Keycode = also });
            }

            if (JoyOf(town) is { } button)
            {
                InputMap.ActionAddEvent(action, new InputEventJoypadButton { ButtonIndex = button });
            }
        }
    }

    /// <summary>The label with its key, as the status bar shows it: "Hall (B)".</summary>
    public static string Hint(TownKey key) => $"{All[key].Label} ({KeyName(All[key].Key)})";

    /// <summary>Whether <paramref name="event"/> presses <paramref name="key"/>: its action, or its default key without the action.</summary>
    public static bool Pressed(InputEvent @event, TownKey key)
    {
        var (action, defaultKey, _) = All[key];
        if (InputMap.HasAction(action))
        {
            // A zoom key takes Shift too: + is Shift and = on many keyboards.
            return @event.IsActionPressed(action, exactMatch: AlsoKeys(defaultKey).Length == 0) && @event is not InputEventKey { Echo: true };
        }

        if (@event is InputEventJoypadButton { Pressed: true } joy)
        {
            return JoyOf(key) == joy.ButtonIndex;
        }

        return @event is InputEventKey { Pressed: true, Echo: false, CtrlPressed: false, AltPressed: false } k
               && (AlsoKeys(defaultKey).Length > 0 ? k.Keycode == defaultKey || AlsoKeys(defaultKey).Contains(k.Keycode) : k.Keycode == defaultKey && !k.ShiftPressed);
    }

    /// <summary>The other keys of a zoom key: the numpad's, and + itself (shifted on some keyboards, its own key on others); the numpad's Enter.</summary>
    private static Key[] AlsoKeys(Key key) => key switch
    {
        Key.Equal => [Key.Plus, Key.KpAdd],
        Key.Minus => [Key.KpSubtract],
        Key.Enter => [Key.KpEnter],
        _ => [],
    };

    /// <summary>Whether <paramref name="key"/> is held down now (the camera's turn keys): its action, or its default key without the action.</summary>
    public static bool Held(TownKey key)
    {
        var (action, defaultKey, _) = All[key];
        return InputMap.HasAction(action) ? Input.IsActionPressed(action, exactMatch: true) : Input.IsKeyPressed(defaultKey);
    }

    private static string KeyName(Key key) => key switch
    {
        Key.Equal => "+",
        Key.Minus => "−",
        Key.Bracketleft => "[",
        Key.Bracketright => "]",
        Key.Left => "←",
        Key.Right => "→",
        _ => OS.GetKeycodeString(key),
    };
}
