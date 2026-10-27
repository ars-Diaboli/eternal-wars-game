using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;

namespace EternalWars.Core.Domain.Objects;

/// <summary>The four colours of the Eyes and Windows of the Magi and of the Keymasters, border guards and gates (ADR-070, ADR-071).</summary>
public enum ObjectColour
{
    Red,
    Green,
    Blue,
    Black,
}

/// <summary>
/// A deposit (ADR-070, MAP-21): a visiting hero's player pays <see cref="MapObjectDefinition.Price"/> and the deposit becomes the mine of its kind
/// (<see cref="MapObjectDefinition.Becomes"/>), flagged for that player. Any player's hero may pay; the guard, if any, is an ordinary monster.
/// </summary>
public sealed class Deposit(MapObjectId id, MapObjectDefinition definition, MapPosition position)
    : MapObject(id, definition, position, MapObjectKind.Deposit)
{
    public ResourceSet Price => Definition.Price;
}

/// <summary>A Window of the Magi (ADR-070): every visit reveals <see cref="Radius"/> tiles around each Eye of its colour (ADR-073: like HoMM3).</summary>
public sealed class MagiWindow(MapObjectId id, MapObjectDefinition definition, MapPosition position)
    : MapObject(id, definition, position, MapObjectKind.MagiWindow)
{
    public const int Radius = 6;

    /// <summary>The most Eyes a colour may have on a map (the editor refuses more, the validator reports them).</summary>
    public const int MaxEyes = 4;

    public ObjectColour Colour => Definition.Colour!.Value;
}

/// <summary>An Eye of the Magi (ADR-070): what a Window of its colour shows; walking over it does nothing.</summary>
public sealed class MagiEye(MapObjectId id, MapObjectDefinition definition, MapPosition position)
    : MapObject(id, definition, position, MapObjectKind.MagiEye)
{
    public ObjectColour Colour => Definition.Colour!.Value;

    public override bool EndsRoute => false;
}

/// <summary>A Keymaster's Tent (ADR-071, MAP-20): a visit gives the hero's player the key of its colour; every hero of that player holds it.</summary>
public sealed class KeymasterTent(MapObjectId id, MapObjectDefinition definition, MapPosition position)
    : MapObject(id, definition, position, MapObjectKind.KeymasterTent)
{
    public ObjectColour Colour => Definition.Colour!.Value;
}

/// <summary>
/// A border guard or border gate (ADR-071, MAP-20): a one-tile barrier only heroes of a player holding the key of its colour pass. A border guard
/// vanishes once the first key holder passes; a gate stays and lets every key holder through.
/// </summary>
public sealed class BorderPost(MapObjectId id, MapObjectDefinition definition, MapPosition position)
    : MapObject(id, definition, position, MapObjectKind.BorderPost)
{
    public ObjectColour Colour => Definition.Colour!.Value;

    /// <summary>A gate stays after letting a key holder through; a guard leaves.</summary>
    public bool IsGate => Definition.Stays;

    /// <summary>A key holder walks through without stopping.</summary>
    public override bool EndsRoute => false;
}

/// <summary>
/// A tavern on the adventure map (ADR-070, ADR-071): four heroes of any faction, the same for every player, a new pool every week. The hero on its
/// entrance hires one for its player; the new hero appears on a free cell next to the entrance. Its pool is a shuffle seeded from the game, the
/// tavern and the week (<see cref="MapTavernRules.Offers"/>), so it has no stored state.
/// </summary>
public sealed class MapTavern(MapObjectId id, MapObjectDefinition definition, MapPosition position)
    : MapObject(id, definition, position, MapObjectKind.MapTavern);

/// <summary>
/// A garrison (ADR-070, MAP-19): flagged like a mine; its owner's heroes (and allies') pass both ways and may leave or take creatures; others
/// must beat the troops inside to pass, and beating them flags it for the winner. A neutral garrison's troops come from the map.
/// </summary>
public sealed class MapGarrison(MapObjectId id, MapObjectDefinition definition, MapPosition position, PlayerId? owner = null, Army? troops = null)
    : MapObject(id, definition, position, MapObjectKind.Garrison), IFlaggable
{
    public PlayerId? Owner { get; internal set; } = owner;

    public int ScoutingRadius => Mine.MineScoutingRadius;

    /// <summary>The creatures inside.</summary>
    public Army Troops { get; } = troops ?? new Army();

    /// <summary>Own and allied heroes walk through; anyone else stops for a fight or the flag.</summary>
    public bool LetsThrough(GameState state, PlayerId player) => Owner is { } owner && (owner == player || state.AreAllies(owner, player));
}

/// <summary>The rules of border posts and keys (ADR-071).</summary>
public static class BorderRules
{
    /// <summary>The border post on <paramref name="position"/> that bars <paramref name="player"/> (no key of its colour), if any.</summary>
    public static BorderPost? Barring(GameState state, PlayerId player, MapPosition position) =>
        state.ObjectCellAt(position) is { Object: BorderPost post } && !state.GetPlayer(player).Keys.Contains(post.Colour) ? post : null;
}

/// <summary>The map tavern's pool and the cell a hired hero appears on (ADR-070, ADR-071).</summary>
public static class MapTavernRules
{
    public const int OfferCount = 4;

    /// <summary>
    /// This week's offers: the roster shuffled from the game seed, the tavern and the week, without the heroes on the map or waiting in a
    /// player's taverns; the same for every player. A hired hero leaves the pool at once (it is on the map).
    /// </summary>
    public static IReadOnlyList<HeroDefinition> Offers(GameState state, MapTavern tavern)
    {
        var heroes = state.HeroRoster.All.OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
        var week = (ulong)((state.Date.AbsoluteDay - 1) / GameDate.DaysPerWeek);
        var seed = (state.Options.Seed * 0xD6E8FEB86659FD93UL) ^ ((ulong)(tavern.Id.Value + 1) * 0xBF58476D1CE4E5B9UL) ^ (week * 0x94D049BB133111EBUL);
        var random = new GameRandom(seed);
        for (var i = heroes.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (heroes[i], heroes[j]) = (heroes[j], heroes[i]);
        }

        var pool = heroes.Take(OfferCount * 4).ToList(); // the week's pool, before anyone is hired
        var taken = state.Heroes.Concat(state.RetiredHeroes).Select(h => h.Definition).OfType<string>().ToHashSet(StringComparer.Ordinal);
        return [.. pool.Where(h => !taken.Contains(h.Id)).Take(OfferCount)];
    }

    /// <summary>Where a hero hired at the tavern appears: the cell below the entrance, else the first free neighbour clockwise from north; null if none.</summary>
    public static MapPosition? Arrival(GameState state, MapTavern tavern)
    {
        var entrance = tavern.VisitPosition;
        Direction[] order = [Direction.South, Direction.North, Direction.NorthEast, Direction.East, Direction.SouthEast, Direction.SouthWest, Direction.West, Direction.NorthWest];
        return order.Select(d => entrance.Step(d))
            .Where(p => MovementRules.CheckTile(state, p) == MoveResult.Moved && state.HeroAt(p) is null && state.ObjectCellAt(p) is null && !state.IsGuarded(p))
            .Select(p => (MapPosition?)p)
            .FirstOrDefault();
    }
}

/// <summary>Deposit prices and their mines (ADR-070, MAP-21; drafts).</summary>
public static class DepositRules
{
    /// <summary>Definition id of the deposit that becomes a resource's mine: <c>deposit.gold</c>.</summary>
    public static string Id(ResourceType resource) => $"deposit.{resource.ToString().ToLowerInvariant()}";

    /// <summary>The Gold Vein 5000 gold + 10 ore; the wood pile and the ore deposit 2000 + 5 of the other basic resource; the magic veins 3000 + 5 ore.</summary>
    public static ResourceSet Price(ResourceType resource) => resource switch
    {
        ResourceType.Gold => new ResourceSet(Gold: 5000, Ore: 10),
        ResourceType.Lumber => new ResourceSet(Gold: 2000, Ore: 5),
        ResourceType.Ore => new ResourceSet(Gold: 2000, Lumber: 5),
        _ => new ResourceSet(Gold: 3000, Ore: 5),
    };
}
