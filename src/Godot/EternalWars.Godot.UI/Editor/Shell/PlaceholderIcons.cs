using Godot;

namespace EternalWars.Godot.UI.Editor.Shell;

/// <summary>
/// Stand-in icons for the editor's tools and commands until their art exists (EDIT-1, ADR-250): a tile in a colour of the name's own
/// with the name's two letters ("Save as": SA, "Road": RO, "New (2 levels)": N2), drawn in code with a 5 × 7 pixel font. The same name
/// gives the same picture on every run. <see cref="Of(EditorCommand, int)"/> is a command's real icon when it has one.
/// </summary>
public static class PlaceholderIcons
{
    /// <summary>An icon's size in a menu, in the palette and before a button's text.</summary>
    public const int SmallSize = UiIcons.ButtonSize;

    /// <summary>An icon's size on the editor's toolbar: the letters are drawn twice as large.</summary>
    public const int ToolbarSize = 26;

    private const int GlyphWidth = 5;
    private const int GlyphHeight = 7;

    /// <summary>The letters and digits, each as its seven rows of five pixels from the top, the left pixel the highest bit.</summary>
    private static readonly Dictionary<char, byte[]> Glyphs = new()
    {
        ['A'] = [0b01110, 0b10001, 0b10001, 0b11111, 0b10001, 0b10001, 0b10001],
        ['B'] = [0b11110, 0b10001, 0b10001, 0b11110, 0b10001, 0b10001, 0b11110],
        ['C'] = [0b01110, 0b10001, 0b10000, 0b10000, 0b10000, 0b10001, 0b01110],
        ['D'] = [0b11110, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b11110],
        ['E'] = [0b11111, 0b10000, 0b10000, 0b11110, 0b10000, 0b10000, 0b11111],
        ['F'] = [0b11111, 0b10000, 0b10000, 0b11110, 0b10000, 0b10000, 0b10000],
        ['G'] = [0b01110, 0b10001, 0b10000, 0b10111, 0b10001, 0b10001, 0b01111],
        ['H'] = [0b10001, 0b10001, 0b10001, 0b11111, 0b10001, 0b10001, 0b10001],
        ['I'] = [0b01110, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b01110],
        ['J'] = [0b00111, 0b00010, 0b00010, 0b00010, 0b00010, 0b10010, 0b01100],
        ['K'] = [0b10001, 0b10010, 0b10100, 0b11000, 0b10100, 0b10010, 0b10001],
        ['L'] = [0b10000, 0b10000, 0b10000, 0b10000, 0b10000, 0b10000, 0b11111],
        ['M'] = [0b10001, 0b11011, 0b10101, 0b10101, 0b10001, 0b10001, 0b10001],
        ['N'] = [0b10001, 0b11001, 0b10101, 0b10011, 0b10001, 0b10001, 0b10001],
        ['O'] = [0b01110, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01110],
        ['P'] = [0b11110, 0b10001, 0b10001, 0b11110, 0b10000, 0b10000, 0b10000],
        ['Q'] = [0b01110, 0b10001, 0b10001, 0b10001, 0b10101, 0b10010, 0b01101],
        ['R'] = [0b11110, 0b10001, 0b10001, 0b11110, 0b10100, 0b10010, 0b10001],
        ['S'] = [0b01111, 0b10000, 0b10000, 0b01110, 0b00001, 0b00001, 0b11110],
        ['T'] = [0b11111, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100],
        ['U'] = [0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01110],
        ['V'] = [0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01010, 0b00100],
        ['W'] = [0b10001, 0b10001, 0b10001, 0b10101, 0b10101, 0b11011, 0b10001],
        ['X'] = [0b10001, 0b10001, 0b01010, 0b00100, 0b01010, 0b10001, 0b10001],
        ['Y'] = [0b10001, 0b10001, 0b01010, 0b00100, 0b00100, 0b00100, 0b00100],
        ['Z'] = [0b11111, 0b00001, 0b00010, 0b00100, 0b01000, 0b10000, 0b11111],
        ['0'] = [0b01110, 0b10001, 0b10011, 0b10101, 0b11001, 0b10001, 0b01110],
        ['1'] = [0b00100, 0b01100, 0b00100, 0b00100, 0b00100, 0b00100, 0b01110],
        ['2'] = [0b01110, 0b10001, 0b00001, 0b00010, 0b00100, 0b01000, 0b11111],
        ['3'] = [0b11110, 0b00001, 0b00001, 0b01110, 0b00001, 0b00001, 0b11110],
        ['4'] = [0b00010, 0b00110, 0b01010, 0b10010, 0b11111, 0b00010, 0b00010],
        ['5'] = [0b11111, 0b10000, 0b11110, 0b00001, 0b00001, 0b10001, 0b01110],
        ['6'] = [0b00110, 0b01000, 0b10000, 0b11110, 0b10001, 0b10001, 0b01110],
        ['7'] = [0b11111, 0b00001, 0b00010, 0b00100, 0b01000, 0b01000, 0b01000],
        ['8'] = [0b01110, 0b10001, 0b10001, 0b01110, 0b10001, 0b10001, 0b01110],
        ['9'] = [0b01110, 0b10001, 0b10001, 0b01111, 0b00001, 0b00010, 0b01100],
    };

    private static readonly Dictionary<(string, int), Texture2D> Cache = [];

    /// <summary>The command's icon at <paramref name="size"/> pixels: its own, or the placeholder for its name.</summary>
    public static Texture2D Of(EditorCommand command, int size = SmallSize) =>
        command.Icon is { } icon && UiIcons.Get(icon, size) is { } texture ? texture : Of(command.Name, size);

    /// <summary>The placeholder for <paramref name="name"/> at <paramref name="size"/> pixels.</summary>
    public static Texture2D Of(string name, int size = SmallSize)
    {
        if (Cache.TryGetValue((name, size), out var cached))
        {
            return cached;
        }

        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        var face = Color.FromHsv(Hue(name), 0.5f, 0.42f);
        var edge = face.Lightened(0.35f);
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var onX = x == 0 || x == size - 1;
                var onY = y == 0 || y == size - 1;
                if (onX && onY)
                {
                    continue; // the corners stay clear: a rounded tile
                }

                image.SetPixel(x, y, onX || onY ? edge : face);
            }
        }

        var letters = Letters(name);
        var scale = size >= ToolbarSize ? 2 : 1;
        var width = ((letters.Length * (GlyphWidth + 1)) - 1) * scale;
        var left = (size - width) / 2;
        var top = (size - (GlyphHeight * scale)) / 2;
        for (var i = 0; i < letters.Length; i++)
        {
            Draw(image, Glyphs[letters[i]], left + (i * (GlyphWidth + 1) * scale), top, scale);
        }

        var made = ImageTexture.CreateFromImage(image);
        Cache[(name, size)] = made;
        return made;
    }

    /// <summary>
    /// The two letters that stand for <paramref name="name"/>: the first letters or digits of its first two words, or the first two of
    /// its only word; none for a name without any.
    /// </summary>
    public static string Letters(string name)
    {
        var words = new List<string>();
        var word = "";
        foreach (var c in name.ToUpperInvariant() + " ")
        {
            if (Glyphs.ContainsKey(c))
            {
                word += c;
            }
            else if (word.Length > 0)
            {
                words.Add(word);
                word = "";
            }
        }

        return words.Count switch
        {
            0 => "",
            1 => words[0][..Math.Min(2, words[0].Length)],
            _ => $"{words[0][0]}{words[1][0]}",
        };
    }

    private static void Draw(Image image, byte[] glyph, int left, int top, int scale)
    {
        for (var row = 0; row < GlyphHeight; row++)
        {
            for (var column = 0; column < GlyphWidth; column++)
            {
                if ((glyph[row] & (1 << (GlyphWidth - 1 - column))) == 0)
                {
                    continue;
                }

                image.FillRect(new Rect2I(left + (column * scale), top + (row * scale), scale, scale), Colors.White);
            }
        }
    }

    /// <summary>A hue from the name's letters, the same on every run (a string's own hash is not).</summary>
    private static float Hue(string name)
    {
        var hash = 17u;
        foreach (var c in name)
        {
            hash = (hash * 31) + c;
        }

        return hash % 360 / 360f;
    }
}
