using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using static EternalWars.Core.Domain.Info.InfoSheets;

namespace EternalWars.Core.Domain.Info;

/// <summary>
/// What the adventure map says about the thing under the pointer (ADR-309). <see cref="Name"/> is the status bar's subject;
/// <see cref="Status"/> its detail, worded by ownership ("Yours: +2 ore a day" against "Produces 2 ore a day when captured");
/// <see cref="CardLines"/> the hover card (name, owner, what it gives). <see cref="Owner"/> is "you", the owning player's name, "none" for
/// an ownable thing nobody holds, and null for what cannot be owned. Null parts are left out. <see cref="IsGround"/> marks the bare ground,
/// which has no hover card.
/// </summary>
public sealed record MapDescription(string Name, string? Owner, string? Gives, string? Status, bool IsGround = false)
{
    /// <summary>Whether the map shows a hover card for it: for a hero or an object, live or remembered, not for the bare ground.</summary>
    public bool HasCard => !IsGround;

    /// <summary>The hover card's lines: the name, the owner, what it gives.</summary>
    public IReadOnlyList<string> CardLines =>
        [Name, .. Owner is { } owner ? [$"Owner: {owner}"] : Array.Empty<string>(), .. Gives is { } gives ? [gives] : Array.Empty<string>()];
}

/// <summary>The <see cref="MapDescription"/>s of map cells, as the viewing player may know them (the information window's rules, ADR-154).</summary>
public static class MapDescriptions
{
    /// <summary>The owner's word for one who holds nothing.</summary>
    public const string NoOwner = "none";

    /// <summary>
    /// What <paramref name="viewer"/> is told about <paramref name="cell"/>: a hero it sees there, else the object covering the cell (live while
    /// in sight, as last seen under the fog), else the ground. Null for a cell the player has not explored.
    /// </summary>
    public static MapDescription? At(GameState state, PlayerId viewer, MapPosition cell)
    {
        ArgumentNullException.ThrowIfNull(state);
        switch (Pointed(state, viewer, cell))
        {
            case null:
                return null;
            case { Hero: { } hero }:
                return new MapDescription(hero.Name, OwnerName(state, viewer, hero.Owner), null,
                    hero.Owner == viewer ? "Your hero" : $"{state.GetPlayer(hero.Owner).Name}'s hero");
            case { Live: { } live }:
                return Of(state, viewer, live);
            case { Remembered: { } known }:
                return new MapDescription(known.Definition.Name, known.Owner is { } then ? $"{OwnerName(state, viewer, then)} (then)" : null, null,
                    "Last seen here; it may have changed since.");
            default:
                var ground = MapCell(state, viewer, cell, detailed: false)!;
                return new MapDescription(ground.Title, null, null, ground.Lines.FirstOrDefault(), IsGround: true);
        }
    }

    /// <summary>A map object <paramref name="viewer"/> sees now.</summary>
    public static MapDescription Of(GameState state, PlayerId viewer, MapObject mapObject)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(mapObject);
        if (mapObject is not IFlaggable flaggable)
        {
            var sheet = MapObject(state, viewer, mapObject, detailed: false);
            return new MapDescription(sheet.Title, null, null, sheet.Lines.FirstOrDefault());
        }

        var name = mapObject is Town town ? town.Name : mapObject.Definition.Name;
        var owner = flaggable.Owner is { } o ? OwnerName(state, viewer, o) : NoOwner;
        var (gives, captured, waiting) = mapObject switch
        {
            Mine mine => Yield(mine),
            CreatureDwelling dwelling => Recruits(state, dwelling),
            ResourceProvider => ($"This week: {ResourceProviderRules.DescribeWeek(mapObject.Definition, state.Date)}", null, null),
            _ => ((string?)null, (string?)null, (string?)null),
        };

        var status = flaggable.Owner switch
        {
            { } mine when mine == viewer => gives is null ? "Yours" : $"Yours: {waiting ?? gives}",
            { } other => $"{state.GetPlayer(other).Name}'s{(gives is null ? "" : $": {gives}")}; yours when captured",
            null => captured ?? (gives is null ? "Yours when captured" : $"{gives}; yours when captured"),
        };

        return new MapDescription(name, owner, gives, status);
    }

    /// <summary>"you" for the viewer, else the player's name.</summary>
    private static string OwnerName(GameState state, PlayerId viewer, PlayerId owner) => owner == viewer ? "you" : state.GetPlayer(owner).Name;

    /// <summary>A mine's "+2 ore a day", and its unowned wording.</summary>
    private static (string?, string?, string?) Yield(Mine mine)
    {
        var resource = mine.Resource.ToString().ToLowerInvariant();
        return ($"+{N(mine.DailyYield)} {resource} a day", $"Produces {N(mine.DailyYield)} {resource} a day when captured", null);
    }

    /// <summary>A dwelling's "Recruits Footman", its unowned wording, and for its owner how many wait.</summary>
    private static (string?, string?, string?) Recruits(GameState state, CreatureDwelling dwelling)
    {
        var creature = state.Creatures.Get(dwelling.Creature).Name;
        return ($"Recruits {creature}", $"Recruits {creature} when captured", $"{N(dwelling.Available ?? 0)} {creature} waiting");
    }
}
