using EternalWars.Core.Domain.Towns;
using Godot;

namespace EternalWars.Godot.UI;

/// <summary>
/// The frames of the UI (ADR-294). The painted pieces of ui-element-prompts.md are in UiFrames.Painted.cs; where one is missing, and in high
/// contrast, the greybox drawn here stands in: a window's nine-slice frame (a dark rim, a bevelled bronze band and an inner line in the
/// faction's accent). A slot's sunken frame and the check box and slider marks are always drawn in code. Each texture is made once and cached.
/// </summary>
public static partial class UiFrames
{
    /// <summary>The size of a window frame's texture; its corners are <see cref="FrameMargin"/> pixels.</summary>
    private const int FrameSize = 48;

    /// <summary>The width of the frame's corners and edges in the nine-slice.</summary>
    public const int FrameMargin = 10;

    private static readonly Dictionary<(Color, bool), Texture2D> Frames = [];
    private static readonly Dictionary<string, Texture2D> Marks = [];

    /// <summary>The colour of each faction's accent on its frames, from faction-art.md; bronze for none.</summary>
    public static Color Accent(string? townType) => townType switch
    {
        TownIds.Haven => new Color(0.88f, 0.72f, 0.34f), // gold sun
        TownIds.Sylvan => new Color(0.58f, 0.82f, 0.46f), // pale green lanterns
        TownIds.Academy => new Color(0.46f, 0.70f, 0.96f), // blue rune light
        TownIds.Necropolis => new Color(0.36f, 0.90f, 0.90f), // cyan
        TownIds.Inferno => new Color(0.96f, 0.46f, 0.16f), // orange fire
        TownIds.Dungeon => new Color(0.66f, 0.46f, 0.96f), // violet glow
        TownIds.Stronghold => new Color(0.82f, 0.42f, 0.26f), // red ochre
        TownIds.SwampBestiary => new Color(0.30f, 0.82f, 0.72f), // cyan over pine
        TownIds.Conflux => new Color(0.76f, 0.56f, 0.96f), // violet for thought
        TownIds.DragonUtopia => new Color(0.56f, 0.72f, 0.50f), // green-tarnished bronze
        _ => UiTheme.BorderBright,
    };

    /// <summary>A window's frame as a nine-slice box with <paramref name="accent"/> on its inner line and <paramref name="padding"/> inside.</summary>
    public static StyleBoxTexture Window(Color accent, int padding = UiSpacing.ScreenPadding)
    {
        if (PaintedWindow(accent) is { } painted)
        {
            return NineSlice(painted, WindowCorner, WindowCorner, Math.Max(padding, WindowBand + 1));
        }

        var style = new StyleBoxTexture { Texture = FrameTexture(accent) };
        style.SetTextureMarginAll(FrameMargin);
        style.SetContentMarginAll(Math.Max(padding, FrameMargin)); // the frame lies in the padding: a panel's content stays where it was
        return style;
    }

    /// <summary>An army or artifact slot's sunken frame; brighter when <paramref name="lit"/> (under the pointer, picked, a drop target).</summary>
    public static StyleBoxFlat Slot(bool lit = false)
    {
        var style = UiTheme.Box(UiTheme.ButtonFace.Darkened(0.35f), lit ? UiTheme.BorderBright : UiTheme.BorderDim, 2, lit ? 2 : 1);
        style.ShadowColor = new Color(0, 0, 0, 0.5f);
        style.ShadowSize = 2;
        style.ShadowOffset = new Vector2(0, 1);
        return style;
    }

    /// <summary>A check box's mark: an empty grey square (ADR-310), or one with a tick.</summary>
    public static Texture2D CheckMark(bool ticked, bool disabled = false) => Mark($"check-{ticked}-{disabled}", 20, image =>
    {
        var frame = disabled ? UiTheme.RimDim : UiTheme.Rim;
        DrawRect(image, 2, 2, 16, 16, frame);
        DrawRect(image, 3, 3, 14, 14, UiTheme.Face.Darkened(0.4f));
        if (ticked)
        {
            var tick = disabled ? UiTheme.TextDone : UiTheme.RimBright;
            for (var i = 0; i < 4; i++)
            {
                DrawRect(image, 5 + i, 9 + i, 2, 2, tick); // down stroke
            }

            for (var i = 0; i < 7; i++)
            {
                DrawRect(image, 8 + i, 12 - i, 2, 2, tick); // up stroke
            }
        }
    });

    /// <summary>A radio button's mark: an empty grey ring, or one with a dot.</summary>
    public static Texture2D RadioMark(bool chosen) => Mark($"radio-{chosen}", 20, image =>
    {
        DrawDisc(image, 10, 10, 8, UiTheme.Rim);
        DrawDisc(image, 10, 10, 7, UiTheme.Face.Darkened(0.4f));
        if (chosen)
        {
            DrawDisc(image, 10, 10, 3, UiTheme.RimBright);
        }
    });

    /// <summary>A slider's grabber: a grey disc.</summary>
    public static Texture2D Grabber(bool lit) => Mark($"grabber-{lit}", 18, image =>
    {
        DrawDisc(image, 9, 9, 8, lit ? UiTheme.RimBright : UiTheme.Rim);
        DrawDisc(image, 9, 9, 6, UiTheme.Face);
    });

    private static Texture2D FrameTexture(Color accent)
    {
        if (Frames.TryGetValue((accent, UiTheme.HighContrast), out var cached))
        {
            return cached;
        }

        var image = Image.CreateEmpty(FrameSize, FrameSize, false, Image.Format.Rgba8);
        image.Fill(UiTheme.Background);
        var bronze = UiTheme.Border;
        Ring(image, 0, new Color(0.03f, 0.03f, 0.04f)); // the dark rim
        Ring(image, 1, bronze.Lightened(0.25f)); // the bevel's lit edge
        Ring(image, 2, bronze);
        Ring(image, 3, bronze);
        Ring(image, 4, bronze.Darkened(0.25f));
        Ring(image, 5, bronze.Darkened(0.5f)); // the bevel's shade
        Ring(image, 6, new Color(0.03f, 0.03f, 0.04f));
        Ring(image, 7, accent); // the faction's line
        var texture = ImageTexture.CreateFromImage(image);
        Frames[(accent, UiTheme.HighContrast)] = texture;
        return texture;
    }

    private static Texture2D Mark(string key, int size, Action<Image> draw)
    {
        key += UiTheme.HighContrast ? "-contrast" : "";
        if (Marks.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        image.Fill(new Color(0, 0, 0, 0));
        draw(image);
        var texture = ImageTexture.CreateFromImage(image);
        Marks[key] = texture;
        return texture;
    }

    /// <summary>A one-pixel square ring <paramref name="inset"/> pixels inside the image's edge.</summary>
    private static void Ring(Image image, int inset, Color color)
    {
        var size = image.GetWidth() - (2 * inset);
        DrawRect(image, inset, inset, size, 1, color);
        DrawRect(image, inset, inset + size - 1, size, 1, color);
        DrawRect(image, inset, inset, 1, size, color);
        DrawRect(image, inset + size - 1, inset, 1, size, color);
    }

    private static void DrawRect(Image image, int x, int y, int width, int height, Color color) =>
        image.FillRect(new Rect2I(x, y, width, height), color);

    private static void DrawDisc(Image image, int cx, int cy, int radius, Color color)
    {
        for (var y = -radius; y <= radius; y++)
        {
            for (var x = -radius; x <= radius; x++)
            {
                if ((x * x) + (y * y) <= radius * radius)
                {
                    image.SetPixel(cx + x, cy + y, color);
                }
            }
        }
    }
}
