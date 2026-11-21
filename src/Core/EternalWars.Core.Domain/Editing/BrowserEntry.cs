using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;

namespace EternalWars.Core.Domain.Editing;

/// <summary>The object browser's categories (EDIT-1, ADR-253), in the order the browser lists them.</summary>
public enum BrowserCategory
{
    Towns,
    Heroes,
    Monsters,
    Mines,
    Dwellings,
    Treasure,
    Artifacts,
    PowerUps,
    Utilities,
    Banks,
    Events,
    Scenery,
}

/// <summary>How a <see cref="BrowserEntry"/> gets on the map.</summary>
public enum BrowserPlacement
{
    /// <summary>An object of the entry's definition, as it is.</summary>
    Object,

    /// <summary>A monster of the entry's creature.</summary>
    Monster,

    /// <summary>A dwelling of the entry's creature.</summary>
    Dwelling,

    /// <summary>An artifact pickup that holds the entry's artifact.</summary>
    Artifact,

    /// <summary>A hero of the player the placer names.</summary>
    Hero,
}

/// <summary>
/// One thing the object browser offers (EDIT-1, ADR-253): a kind of map object as it is, or one made ready (a monster of one creature,
/// a dwelling of one creature, a pickup of one artifact), or a hero. <see cref="Id"/> names it in the favourites and the recent list:
/// the definition's id, with <c>/</c> and the creature or artifact after it for one made ready; <see cref="HeroId"/> for the hero.
/// </summary>
/// <param name="Id">The entry's name in the kept lists.</param>
/// <param name="Name">What the browser shows.</param>
/// <param name="Category">The category it is listed in.</param>
/// <param name="Group">The heading inside the category (a faction, a slot, a kind of scenery); empty: none.</param>
/// <param name="Definition">The map object's definition; null for the hero.</param>
public sealed record BrowserEntry(string Id, string Name, BrowserCategory Category, string Group, MapObjectDefinition? Definition)
{
    public const string HeroId = "hero";

    public BrowserPlacement Placement { get; init; }

    /// <summary>The creature of a monster or a dwelling.</summary>
    public CreatureId? Creature { get; init; }

    /// <summary>The artifact of a pickup; null: rolled from the definition's tier.</summary>
    public ArtifactId? Artifact { get; init; }

    /// <summary>Where the entry's group stands among its category's; groups of one number go by name.</summary>
    public int GroupOrder { get; init; }

    /// <summary>Where the entry stands among its group's: a creature's or an artifact's tier.</summary>
    public int Order { get; init; }

    /// <summary>The words the search finds the entry by, besides its name: lower case.</summary>
    public IReadOnlyList<string> Words { get; init; } = [];

    /// <summary>What the thing does, in one line.</summary>
    public string Rule { get; init; } = "";

    /// <summary>The cells it takes, from the clicked tile as the top-left one.</summary>
    public Footprint Footprint => Definition?.Footprint ?? Footprint.Single;

    /// <summary>The terrains it is made for; empty: any.</summary>
    public IReadOnlyList<TerrainType> Terrains => Definition?.Terrains ?? [];

    /// <summary>Whether it suits <paramref name="terrain"/>: one of <see cref="Terrains"/>, or it suits any.</summary>
    public bool Suits(TerrainType terrain) => Terrains.Count == 0 || Terrains.Contains(terrain);
}

/// <summary>
/// The browser's Favourites and Recent lists (EDIT-1, ADR-253), as lists of entry ids the editor's settings keep. The lists are values:
/// every change returns a new one. An id the browser no longer knows stays in the list and is not shown.
/// </summary>
public static class BrowserLists
{
    /// <summary>How many entries Recent holds.</summary>
    public const int RecentCapacity = 12;

    /// <summary><paramref name="recent"/> with <paramref name="id"/> first; the oldest falls off a full list.</summary>
    public static IReadOnlyList<string> WithRecent(IReadOnlyList<string> recent, string id) =>
        [id, .. recent.Where(other => other != id).Take(RecentCapacity - 1)];

    /// <summary><paramref name="favourites"/> with <paramref name="id"/> added at the end, or taken out when it is there.</summary>
    public static IReadOnlyList<string> Toggled(IReadOnlyList<string> favourites, string id) =>
        favourites.Contains(id) ? [.. favourites.Where(other => other != id)] : [.. favourites, id];
}
