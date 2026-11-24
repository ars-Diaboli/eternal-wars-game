using Godot;

namespace EternalWars.Godot.UI;

/// <summary>
/// The painted frames (ADR-294) of ui-element-prompts.md in <c>res://shared/textures/ui/frames/</c>, scaled there to their size on screen and
/// drawn as nine-slice boxes: a window (with a line in the faction's accent painted on), a panel over the map, a tooltip, a title plate, a
/// divider, the bars, the side column and the minimap's frame. Each is null while its file is missing and in high contrast, and its caller keeps
/// its flat box (the window, the greybox frame).
/// </summary>
public static partial class UiFrames
{
    private const string Folder = "res://shared/textures/ui/frames/";

    /// <summary>The painted window's corner plates and its bronze band, in its texture's pixels.</summary>
    private const int WindowCorner = 21, WindowBand = 15;

    /// <summary>The side column's corners and its ends, in its texture's pixels.</summary>
    private const int ColumnSide = 14, ColumnEnd = 16;

    /// <summary>Where the column's cast ornament lies at its top (and, mirrored, its bottom), in its texture's pixels.</summary>
    private static readonly Rect2 ColumnOrnament = new(16, 0, 56, ColumnEnd);

    private static readonly Dictionary<string, Texture2D?> Art = [];
    private static readonly Dictionary<Color, Texture2D> PaintedWindows = [];
    private static Texture2D? _columnPlain;

    /// <summary>A panel over the map (the bottom row's parts, the questions and notices): a thin bronze frame around opaque slate.</summary>
    public static StyleBoxTexture? Overlay(int padding = UiSpacing.Padding) =>
        Painted("overlay") is { } art ? NineSlice(art, 10, 10, Math.Max(padding, 9)) : null;

    /// <summary>A tooltip's and the map's hover card's box: a fine bronze line with corner studs.</summary>
    public static StyleBoxTexture? Tooltip(int padding = UiSpacing.Padding) =>
        Painted("tooltip") is { } art ? NineSlice(art, 7, 7, Math.Max(padding, 7)) : null;

    /// <summary>A window's title plate: slate between bronze rails with scrolled ends; the title sits on its slate.</summary>
    public static StyleBoxTexture? Header() => Painted("header") is { } art ? NineSlice(art, 27, 16, 8, horizontalPadding: 30) : null;

    /// <summary>A divider (the HSeparator's line): a thin bronze rod with a diamond at each end, <see cref="DividerHeight"/> tall.</summary>
    public static StyleBoxTexture? Divider()
    {
        if (Painted("divider") is not { } art)
        {
            return null;
        }

        var style = new StyleBoxTexture { Texture = art, TextureMarginLeft = 11, TextureMarginRight = 11 };
        style.ContentMarginTop = style.ContentMarginBottom = art.GetHeight() / 2f; // the separator draws the box at its minimum height
        return style;
    }

    /// <summary>The painted divider's height, or 0 without it.</summary>
    public static int DividerHeight => Painted("divider")?.GetHeight() ?? 0;

    /// <summary>The status bar's and the top bar's ground: a slate strip between bronze rails, an end cap at each end; <paramref name="padding"/> at the ends.</summary>
    public static StyleBoxTexture? Bar(int padding) =>
        Painted("status-bar") is { } art ? NineSlice(art, 22, 10, 2, horizontalPadding: Math.Max(padding, 22)) : null;

    /// <summary>The minimap's frame: a heavy bronze frame with studded corners, open in the middle.</summary>
    public static StyleBoxTexture? Minimap() => Painted("minimap") is { } art ? NineSlice(art, 20, 20, 18) : null;

    /// <summary>
    /// A side column's frame: plain bronze sides around slate, its cast ornaments covered. The adventure HUD keeps only its margins (ADR-310).
    /// </summary>
    public static StyleBoxTexture? Column(int padding = UiSpacing.Padding)
    {
        if (ColumnPlain() is not { } plain)
        {
            return null;
        }

        var style = NineSlice(plain, ColumnSide, ColumnEnd, Math.Max(padding, ColumnEnd), horizontalPadding: Math.Max(padding, 10));
        return style;
    }

    /// <summary>The column's art with its ornaments covered by the plain rail beside them, so the rail can stretch.</summary>
    private static Texture2D? ColumnPlain()
    {
        if (Painted("side-panel") is not { } art)
        {
            return null;
        }

        if (_columnPlain is not null)
        {
            return _columnPlain;
        }

        var image = Decompressed(art);
        var (from, to) = ((int)ColumnOrnament.Position.X, (int)ColumnOrnament.End.X);
        foreach (var top in new[] { 0, image.GetHeight() - ColumnEnd })
        {
            for (var x = from; x < to; x++)
            {
                image.BlitRect(image, new Rect2I(from - 1, top, 1, ColumnEnd), new Vector2I(x, top));
            }
        }

        return _columnPlain = ImageTexture.CreateFromImage(image);
    }

    /// <summary>The painted window with <paramref name="accent"/> drawn as a line just inside its bronze band; null without the art.</summary>
    private static Texture2D? PaintedWindow(Color accent)
    {
        if (Painted("window") is not { } art)
        {
            return null;
        }

        if (PaintedWindows.TryGetValue(accent, out var cached))
        {
            return cached;
        }

        var image = Decompressed(art);
        var line = accent with { A = 0.85f };
        var (w, h) = (image.GetWidth(), image.GetHeight());
        for (var x = WindowBand; x < w - WindowBand; x++)
        {
            Blend(image, x, WindowBand, line);
            Blend(image, x, h - WindowBand - 1, line);
        }

        for (var y = WindowBand + 1; y < h - WindowBand - 1; y++)
        {
            Blend(image, WindowBand, y, line);
            Blend(image, w - WindowBand - 1, y, line);
        }

        var texture = ImageTexture.CreateFromImage(image);
        PaintedWindows[accent] = texture;
        return texture;
    }

    /// <summary>A nine-slice box of <paramref name="art"/>: corners <paramref name="sides"/> wide and <paramref name="ends"/> tall, <paramref name="padding"/> inside.</summary>
    private static StyleBoxTexture NineSlice(Texture2D art, int sides, int ends, int padding, int? horizontalPadding = null)
    {
        var style = new StyleBoxTexture { Texture = art };
        style.TextureMarginLeft = style.TextureMarginRight = sides;
        style.TextureMarginTop = style.TextureMarginBottom = ends;
        style.ContentMarginTop = style.ContentMarginBottom = padding;
        style.ContentMarginLeft = style.ContentMarginRight = horizontalPadding ?? padding;
        return style;
    }

    /// <summary>The painted piece <c>ui_frame_&lt;id&gt;.png</c>; null while it is missing, and in high contrast, whose flat frames read better.</summary>
    private static Texture2D? Painted(string id)
    {
        if (UiTheme.HighContrast)
        {
            return null;
        }

        if (!Art.TryGetValue(id, out var art))
        {
            var path = $"{Folder}ui_frame_{id}.png";
            art = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
            Art[id] = art;
        }

        return art;
    }

    private static void Blend(Image image, int x, int y, Color color) => image.SetPixel(x, y, image.GetPixel(x, y).Blend(color));

    private static Image Decompressed(Texture2D art)
    {
        var image = art.GetImage();
        if (image.IsCompressed())
        {
            image.Decompress();
        }

        image.Convert(Image.Format.Rgba8);
        return image;
    }
}
