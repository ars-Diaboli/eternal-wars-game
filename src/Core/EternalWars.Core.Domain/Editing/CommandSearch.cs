namespace EternalWars.Core.Domain.Editing;

/// <summary>
/// The command palette's search (EDIT-1, ADR-250): which names fit what was typed, the best first. Letters' case does not count. Every
/// typed word must fit the name; a word fits best at the name's start, then at the start of one of its words, then anywhere inside it,
/// then as its letters in order with others between ("svas" finds "Save as"). Names that fit alike keep their order.
/// </summary>
public static class CommandSearch
{
    /// <summary>The indexes of the <paramref name="names"/> that fit <paramref name="query"/>, the best first; all of them for an empty query.</summary>
    public static IReadOnlyList<int> Find(string query, IReadOnlyList<string> names)
    {
        var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var found = new List<(int Index, int Score)>();
        for (var i = 0; i < names.Count; i++)
        {
            if (Score(words, names[i]) is { } score)
            {
                found.Add((i, score));
            }
        }

        return [.. found.OrderBy(f => f.Score).ThenBy(f => f.Index).Select(f => f.Index)];
    }

    /// <summary>How far <paramref name="name"/> is from the typed <paramref name="words"/> (0: the best); null when a word does not fit.</summary>
    private static int? Score(string[] words, string name)
    {
        var total = 0;
        foreach (var word in words)
        {
            if (Score(word, name) is not { } score)
            {
                return null;
            }

            total += score;
        }

        return total;
    }

    private static int? Score(string word, string name)
    {
        var at = name.IndexOf(word, StringComparison.OrdinalIgnoreCase);
        if (at == 0)
        {
            return 0;
        }

        if (at > 0)
        {
            // At a word's start somewhere in the name, or just inside it.
            for (; at >= 0; at = name.IndexOf(word, at + 1, StringComparison.OrdinalIgnoreCase))
            {
                if (!char.IsLetterOrDigit(name[at - 1]))
                {
                    return 1;
                }
            }

            return 2;
        }

        var next = 0;
        foreach (var letter in name)
        {
            if (next < word.Length && char.ToUpperInvariant(letter) == char.ToUpperInvariant(word[next]))
            {
                next++;
            }
        }

        return next == word.Length ? 3 : null;
    }
}
