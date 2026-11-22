using Godot;

namespace EternalWars.Godot.UI;

/// <summary>The two font pairings the town screen plan's step 1 proposes (ADR-294): a heading face and a body face, both under the OFL.</summary>
public enum FontPairing
{
    /// <summary>Cinzel headings (Roman capitals, a carved look) over Alegreya Sans (a humanist sans with calligraphic roots).</summary>
    CinzelAlegreya,

    /// <summary>Marcellus SC headings (flared small capitals) over Fira Sans (a plain, very legible sans).</summary>
    MarcellusFira,
}

/// <summary>
/// The game's fonts (ADR-294) in <c>res://shared/fonts/</c> (CREDITS.md there): <see cref="UiTheme"/> sets the body face as the default font and
/// the heading face on the heading and title labels (<see cref="UiTheme.HeadingLabel"/>, <see cref="UiTheme.TitleLabel"/>). Body text is at
/// least <see cref="BodySize"/> pixels at 1080p. A missing file leaves Godot's own font, so a clone without the fonts still runs. The map editor
/// keeps Godot's font in its compact variant (ADR-250): a tool's dense panels.
/// </summary>
public static class UiFonts
{
    /// <summary>The body text's size: no game text is smaller at 1080p.</summary>
    public const int BodySize = 16;

    /// <summary>A section's heading in a window.</summary>
    public const int HeadingSize = 22;

    /// <summary>A window's or a screen's title.</summary>
    public const int TitleSize = 28;

    private const string Folder = "res://shared/fonts/";

    /// <summary>The pairing in use; the game's <c>--fonts=b</c> shows the second one for the owner's choice.</summary>
    public static FontPairing Pairing { get; set; } = FontPairing.CinzelAlegreya;

    /// <summary>The body face of <see cref="Pairing"/>, or null when its file is missing.</summary>
    public static Font? Body => Load(Pairing == FontPairing.CinzelAlegreya ? "AlegreyaSans-Regular.ttf" : "FiraSans-Regular.ttf");

    /// <summary>The body face's bold, for a figure that must stand out (a stack's count).</summary>
    public static Font? BodyBold => Load(Pairing == FontPairing.CinzelAlegreya ? "AlegreyaSans-Bold.ttf" : "FiraSans-SemiBold.ttf");

    /// <summary>The heading face of <see cref="Pairing"/> (Cinzel at a semibold weight), or null when its file is missing.</summary>
    public static Font? Heading
    {
        get
        {
            if (Pairing == FontPairing.MarcellusFira)
            {
                return Load("MarcellusSC-Regular.ttf");
            }

            if (Load("Cinzel-Variable.ttf") is not { } cinzel)
            {
                return null;
            }

            var weight = TextServerManager.GetPrimaryInterface().NameToTag("wght");
            return new FontVariation { BaseFont = cinzel, VariationOpentype = new global::Godot.Collections.Dictionary { { weight, 600 } } };
        }
    }

    private static readonly Dictionary<string, Font?> Cache = [];

    private static Font? Load(string file)
    {
        if (Cache.TryGetValue(file, out var cached))
        {
            return cached;
        }

        var path = Folder + file;
        var font = ResourceLoader.Exists(path) ? GD.Load<FontFile>(path) : null;
        if (font is not null)
        {
            // Symbols the faces lack (✓ ✗ ⚠ ◄ ►) come from Godot's own font, then the system's.
            font.Fallbacks = [ThemeDB.FallbackFont];
        }

        Cache[file] = font;
        return font;
    }
}
