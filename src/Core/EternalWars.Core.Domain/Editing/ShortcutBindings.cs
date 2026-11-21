namespace EternalWars.Core.Domain.Editing;

/// <summary>
/// A key with its modifiers, as the editor's shortcuts are kept (ADR-275): the key by its name in the engine's key list ("S", "F5",
/// "Delete"). Its <see cref="Text"/> is how menus show it and how the editor settings keep it.
/// </summary>
public readonly record struct KeyChord(string Key, bool Ctrl = false, bool Shift = false, bool Alt = false)
{
    /// <summary>"Ctrl+Shift+S": the modifiers always in this order.</summary>
    public string Text => $"{(Ctrl ? "Ctrl+" : "")}{(Shift ? "Shift+" : "")}{(Alt ? "Alt+" : "")}{Key}";

    /// <summary>
    /// The chord <paramref name="text"/> names: modifiers in any order and case, then the key. Null for nothing, a lone modifier, an
    /// empty key or a modifier given twice.
    /// </summary>
    public static KeyChord? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var parts = text.Split('+', StringSplitOptions.TrimEntries);
        var key = parts[^1];
        if (key.Length == 0 || IsModifier(key))
        {
            return null;
        }

        var modifiers = parts[..^1].Select(m => m.ToUpperInvariant()).ToList();
        if (modifiers.Any(m => m is not ("CTRL" or "SHIFT" or "ALT")) || modifiers.Distinct().Count() != modifiers.Count)
        {
            return null;
        }

        return new KeyChord(key, modifiers.Contains("CTRL"), modifiers.Contains("SHIFT"), modifiers.Contains("ALT"));
    }

    private static bool IsModifier(string key) => key.ToUpperInvariant() is "CTRL" or "SHIFT" or "ALT" or "CONTROL" or "META";
}

/// <summary>
/// The editor's shortcuts (ADR-275): each command's default chord, the user's changes kept over them in the editor settings, and the
/// chords two commands share. A map holds every command's chord by its id, null for none.
/// </summary>
public static class ShortcutBindings
{
    /// <summary>
    /// The chords in effect: the <paramref name="defaults"/> with the <paramref name="kept"/> changes over them. A change for a command
    /// that no longer exists is dropped, and so are changes that give one chord to several commands (a hand-edited file: those commands
    /// keep their defaults); a default chord the user gave another command is taken from the command that had it, so the user's choice
    /// wins. So no chord is ever two commands'. The commands keep the defaults' order.
    /// </summary>
    public static IReadOnlyDictionary<string, KeyChord?> Resolve(
        IReadOnlyDictionary<string, KeyChord?> defaults, IReadOnlyDictionary<string, KeyChord?> kept)
    {
        var chosen = kept.Where(k => defaults.ContainsKey(k.Key) && k.Value != defaults[k.Key]).ToDictionary(k => k.Key, k => k.Value);
        var shared = chosen.Values.OfType<KeyChord>().GroupBy(c => c).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
        chosen = chosen.Where(c => c.Value is not { } chord || !shared.Contains(chord)).ToDictionary(c => c.Key, c => c.Value);
        var taken = chosen.Values.OfType<KeyChord>().ToHashSet();
        var result = new Dictionary<string, KeyChord?>();
        foreach (var (id, chord) in defaults)
        {
            result[id] = chosen.TryGetValue(id, out var own) ? own : chord is { } c && taken.Contains(c) ? null : chord;
        }

        return result;
    }

    /// <summary>What the editor settings keep of <paramref name="current"/>: only the commands whose chord is not their default.</summary>
    public static IReadOnlyDictionary<string, KeyChord?> Changes(
        IReadOnlyDictionary<string, KeyChord?> defaults, IReadOnlyDictionary<string, KeyChord?> current) =>
        current.Where(c => defaults.TryGetValue(c.Key, out var chord) && chord != c.Value).ToDictionary(c => c.Key, c => c.Value);

    /// <summary>The chords more than one command has, each with those commands in the map's order; the chords in the order first met.</summary>
    public static IReadOnlyList<(KeyChord Chord, IReadOnlyList<string> Commands)> Conflicts(IReadOnlyDictionary<string, KeyChord?> bindings) =>
    [
        .. bindings.Where(b => b.Value is not null)
            .GroupBy(b => b.Value!.Value)
            .Where(g => g.Count() > 1)
            .Select(g => (g.Key, (IReadOnlyList<string>)[.. g.Select(b => b.Key)])),
    ];
}
