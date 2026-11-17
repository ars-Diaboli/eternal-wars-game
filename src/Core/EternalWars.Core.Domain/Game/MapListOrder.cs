using EternalWars.Core.Domain.Map;

namespace EternalWars.Core.Domain.Game;

/// <summary>What the new-game screen's map lists sort by (ADR-204).</summary>
public enum MapSort
{
    /// <summary>By name, A–Z (the default).</summary>
    Name,

    /// <summary>By size, small first, then by name.</summary>
    Size,
}

/// <summary>
/// The size class of a map on the new-game screen's lists (ADR-204): the smallest preset (<see cref="MapSizes.Standard"/>) that holds the map's
/// larger side, the largest preset for anything bigger. A random map template is in the class of every size it supports.
/// </summary>
public static class MapSizeClass
{
    /// <summary>The class of a map of <paramref name="width"/> × <paramref name="height"/>; null when its size is unknown (not positive).</summary>
    public static int? Of(int width, int height)
    {
        var side = Math.Max(width, height);
        if (side <= 0)
        {
            return null;
        }

        foreach (var preset in MapSizes.Standard)
        {
            if (side <= preset)
            {
                return preset;
            }
        }

        return MapSizes.Standard[^1];
    }

    /// <summary>The classes of a template's square sizes, smallest first, each once.</summary>
    public static IReadOnlyList<int> OfTemplate(IEnumerable<int> sizes) =>
        sizes.Select(size => Of(size, size)).OfType<int>().Distinct().Order().ToList();

    /// <summary>Whether <paramref name="size"/> is one of the presets a list can be filtered by.</summary>
    public static bool IsPreset(int size) => MapSizes.Standard.Contains(size);
}

/// <summary>
/// How the new-game screen's map lists are sorted and filtered (ADR-204), the same for every tab and kept in the settings file: by
/// <see cref="Sort"/>, <see cref="Reversed"/> after a second press of the same sort, showing the maps of the size class <see cref="Size"/> only
/// (<see cref="AllSizes"/>: every map).
/// </summary>
public sealed record MapListView(MapSort Sort = MapSort.Name, bool Reversed = false, int Size = MapListView.AllSizes)
{
    /// <summary>The filter that shows every map.</summary>
    public const int AllSizes = 0;

    public static MapListView Default { get; } = new();

    /// <summary>The view after pressing <paramref name="sort"/>: the active sort reverses, another one starts in its own order.</summary>
    public MapListView Pressed(MapSort sort) => sort == Sort ? this with { Reversed = !Reversed } : this with { Sort = sort, Reversed = false };

    /// <summary>The view showing size class <paramref name="size"/> only; anything that is not a preset shows every map.</summary>
    public MapListView Filtered(int size) => this with { Size = MapSizeClass.IsPreset(size) ? size : AllSizes };

    /// <summary>
    /// The items that pass the size filter, in this view's order. <paramref name="sizeClasses"/> gives an item's classes (a map's one, a template's
    /// several, none when unknown): the filter keeps an item with the chosen class (an item of unknown size only when every size is shown); the
    /// size sort uses its smallest class (unknown sizes last), then the name. Names compare case-insensitively; reversing reverses the whole order.
    /// </summary>
    public IReadOnlyList<T> Arrange<T>(IEnumerable<T> items, Func<T, string> name, Func<T, IReadOnlyList<int>> sizeClasses)
    {
        var size = MapSizeClass.IsPreset(Size) ? Size : AllSizes;
        var shown = items.Where(item => size == AllSizes || sizeClasses(item).Contains(size));
        var ordered = Sort == MapSort.Size
            ? shown.OrderBy(item => sizeClasses(item) is { Count: > 0 } classes ? classes.Min() : int.MaxValue).ThenBy(name, StringComparer.OrdinalIgnoreCase)
            : shown.OrderBy(name, StringComparer.OrdinalIgnoreCase);
        var list = ordered.ThenBy(name, StringComparer.Ordinal).ToList();
        if (Reversed)
        {
            list.Reverse();
        }

        return list;
    }
}
