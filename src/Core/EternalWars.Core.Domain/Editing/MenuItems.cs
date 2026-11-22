namespace EternalWars.Core.Domain.Editing;

/// <summary>The items of an editor menu as command ids, null for a separator line (ADR-250, ADR-286).</summary>
public static class MenuItems
{
    /// <summary>
    /// <paramref name="items"/> without the separators a context menu would show for nothing: at either end, or twice in a row (a group of
    /// commands that did not apply there was left out).
    /// </summary>
    public static IReadOnlyList<string?> Tidy(IEnumerable<string?> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var tidy = new List<string?>();
        foreach (var item in items)
        {
            if (item is not null || (tidy.Count > 0 && tidy[^1] is not null))
            {
                tidy.Add(item);
            }
        }

        if (tidy.Count > 0 && tidy[^1] is null)
        {
            tidy.RemoveAt(tidy.Count - 1);
        }

        return tidy;
    }
}
