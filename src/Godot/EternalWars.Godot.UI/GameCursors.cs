using EternalWars.Core.Domain.Cursors;
using Godot;

namespace EternalWars.Godot.UI;

/// <summary>
/// Shows the mouse cursor the core chose (<see cref="GameCursor"/>, ADR-204) over the adventure map and the battlefield. Each cursor's image is
/// <c>res://shared/textures/ui/cursors/cursor_&lt;group&gt;_&lt;id&gt;.png</c> (docs/content/prompts/cursor-prompts.md), scaled to <see cref="Size"/>
/// pixels with its hotspot from the prompts' table; while an image is missing the nearest system cursor stands in (an arrow, a hand, a cross,
/// the forbidden sign, the busy one). Controls keep their own cursor (the arrow, with <c>cursor_ui_pointer</c> once it exists), so the HUD
/// never shows a sword. The map's cursor goes into one spare system shape (<see cref="Slot"/>), made the viewport's default.
/// </summary>
public static class GameCursors
{
    /// <summary>An action cursor's size on screen (the map's and the battlefield's), in pixels.</summary>
    public const int Size = 43;

    /// <summary>The pointer's, the hourglass's and the blocked sign's size on screen, in pixels: half the art's 64.</summary>
    public const int PlainSize = 32;

    private const string Folder = "res://shared/textures/ui/cursors/";

    /// <summary>The system shape whose image the map's cursor takes: one the game uses nowhere else.</summary>
    private const Input.CursorShape Slot = Input.CursorShape.Help;

    private static readonly Dictionary<GameCursor, (Texture2D Image, Vector2 Hotspot)?> Cache = [];
    private static GameCursor? _shown;
    private static bool _arrowSet;

    /// <summary>Shows <paramref name="cursor"/> where no control is under the pointer (the map, the battlefield).</summary>
    public static void Show(GameCursor cursor)
    {
        if (!_arrowSet)
        {
            // The arrow over every control, and on the map with nothing to do, is the pointer image once it exists.
            _arrowSet = true;
            if (Image(GameCursor.Pointer) is { } pointer)
            {
                Input.SetCustomMouseCursor(pointer.Image, Input.CursorShape.Arrow, pointer.Hotspot);
            }

            if (Image(GameCursor.Busy) is { } busy)
            {
                Input.SetCustomMouseCursor(busy.Image, Input.CursorShape.Busy, busy.Hotspot);
            }
        }

        if (_shown == cursor)
        {
            return;
        }

        _shown = cursor;
        if (cursor is GameCursor.Pointer or GameCursor.Busy)
        {
            Input.SetDefaultCursorShape(cursor == GameCursor.Busy ? Input.CursorShape.Busy : Input.CursorShape.Arrow);
            return;
        }

        if (Image(cursor) is { } shown)
        {
            Input.SetCustomMouseCursor(shown.Image, Slot, shown.Hotspot);
            Input.SetDefaultCursorShape(Slot);
            return;
        }

        Input.SetDefaultCursorShape(Fallback(cursor));
    }

    /// <summary>
    /// Gives the system cursors back and drops the images. The app calls it when it quits: the engine's input keeps the images it was given
    /// and would otherwise free them after the renderer is gone (leaked textures at exit).
    /// </summary>
    public static void Release()
    {
        foreach (var shape in new[] { Input.CursorShape.Arrow, Input.CursorShape.Busy, Slot })
        {
            Input.SetCustomMouseCursor(null, shape);
        }

        Input.SetDefaultCursorShape(Input.CursorShape.Arrow);
        foreach (var image in Cache.Values)
        {
            image?.Image.Dispose();
        }

        Cache.Clear();
        _shown = null;
        _arrowSet = false;
    }

    /// <summary>The cursor's image file name (without the folder and extension) and its hotspot in eighths of the image.</summary>
    public static (string File, int X, int Y) Definition(GameCursor cursor) => cursor switch
    {
        GameCursor.Pointer => ("cursor_ui_pointer", 0, 0),
        GameCursor.Busy => ("cursor_ui_busy", 4, 4),
        GameCursor.Blocked => ("cursor_ui_blocked", 4, 4),
        GameCursor.Move => ("cursor_adv_move", 4, 7),
        GameCursor.Visit => ("cursor_adv_visit", 4, 7),
        GameCursor.Attack => ("cursor_adv_attack", 4, 4),
        GameCursor.Exchange => ("cursor_adv_exchange", 4, 4),
        GameCursor.EnterTown => ("cursor_adv_enter-town", 4, 7),
        GameCursor.AdventureSpell => ("cursor_adv_spell", 4, 4),
        GameCursor.SelectHero => ("cursor_adv_select-hero", 4, 4),
        GameCursor.SelectTown => ("cursor_adv_select-town", 4, 4),
        GameCursor.MeleeFromNorth => ("cursor_bat_melee_from-n", 4, 4),
        GameCursor.MeleeFromNorthEast => ("cursor_bat_melee_from-ne", 4, 4),
        GameCursor.MeleeFromEast => ("cursor_bat_melee_from-e", 4, 4),
        GameCursor.MeleeFromSouthEast => ("cursor_bat_melee_from-se", 4, 4),
        GameCursor.MeleeFromSouth => ("cursor_bat_melee_from-s", 4, 4),
        GameCursor.MeleeFromSouthWest => ("cursor_bat_melee_from-sw", 4, 4),
        GameCursor.MeleeFromWest => ("cursor_bat_melee_from-w", 4, 4),
        GameCursor.MeleeFromNorthWest => ("cursor_bat_melee_from-nw", 4, 4),
        GameCursor.Shoot => ("cursor_bat_shoot", 0, 0),
        GameCursor.ShootWeak => ("cursor_bat_shoot-weak", 0, 0),
        GameCursor.Spell => ("cursor_bat_spell", 0, 0),
        GameCursor.SpellArea => ("cursor_bat_spell-area", 4, 4),
        GameCursor.Siege => ("cursor_bat_siege", 4, 4),
        GameCursor.Walk => ("cursor_bat_walk", 4, 7),
        GameCursor.Fly => ("cursor_bat_fly", 4, 4),
        _ => ("cursor_bat_teleport", 4, 4),
    };

    /// <summary>The system cursor standing in for a missing image.</summary>
    private static Input.CursorShape Fallback(GameCursor cursor) => cursor switch
    {
        GameCursor.Blocked => Input.CursorShape.Forbidden,
        GameCursor.Move or GameCursor.Visit or GameCursor.Exchange or GameCursor.EnterTown or GameCursor.SelectHero or GameCursor.SelectTown
            or GameCursor.Walk or GameCursor.Fly or GameCursor.Teleport => Input.CursorShape.PointingHand,
        _ => Input.CursorShape.Cross,
    };

    private static (Texture2D Image, Vector2 Hotspot)? Image(GameCursor cursor)
    {
        if (Cache.TryGetValue(cursor, out var cached))
        {
            return cached;
        }

        var (file, x, y) = Definition(cursor);
        var size = cursor is GameCursor.Pointer or GameCursor.Busy or GameCursor.Blocked ? PlainSize : Size;
        var image = UiIcons.LoadScaled($"{Folder}{file}.png", size);
        (Texture2D, Vector2)? found = image is null ? null : (image, new Vector2(Math.Min(x * size / 8, size - 1), Math.Min(y * size / 8, size - 1)));
        Cache[cursor] = found;
        return found;
    }
}
