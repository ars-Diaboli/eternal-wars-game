using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;

namespace EternalWars.Core.Domain.Objects;

/// <summary>A sign post (ADR-068): a visiting hero reads <see cref="Message"/>, set by the map author.</summary>
public sealed class SignPost(MapObjectId id, MapObjectDefinition definition, MapPosition position, string message = "")
    : MapObject(id, definition, position, MapObjectKind.Sign)
{
    public string Message { get; internal set; } = message;
}

/// <summary>An observatory (ADR-068): a visit reveals the terrain within <see cref="Radius"/> tiles of it to the visitor's owner.</summary>
public sealed class Observatory(MapObjectId id, MapObjectDefinition definition, MapPosition position)
    : MapObject(id, definition, position, MapObjectKind.Observatory)
{
    public const int Radius = 20;
}

/// <summary>Scenery (ADR-079, MAP-25): a rock, a tree, a pond, a ruin, a piece of wall… It only blocks; it has no entrance and no data.</summary>
public sealed class Scenery(MapObjectId id, MapObjectDefinition definition, MapPosition position)
    : MapObject(id, definition, position, MapObjectKind.Scenery);

/// <summary>The Den of Thieves (ADR-079, TOWN-11): every visit shows the whole Thieves' Guild table; nothing is kept.</summary>
public sealed class DenOfThieves(MapObjectId id, MapObjectDefinition definition, MapPosition position)
    : MapObject(id, definition, position, MapObjectKind.DenOfThieves);

/// <summary>
/// A sanctuary (ADR-079, MAP-28): a hero standing on its entrance cannot be attacked, so enemies cannot enter while it is occupied. A hero may
/// stay as long as it likes; one hero at a time (the entrance holds one); nothing else (no healing, no army storage).
/// </summary>
public sealed class Sanctuary(MapObjectId id, MapObjectDefinition definition, MapPosition position)
    : MapObject(id, definition, position, MapObjectKind.Sanctuary)
{
    /// <summary>Whether a hero on <paramref name="position"/> is safe from attack: it is a sanctuary's entrance.</summary>
    public static bool Protects(GameState state, MapPosition position) =>
        state.ObjectCellAt(position) is { Cell: FootprintCell.Visit, Object: Sanctuary };
}

/// <summary>A hill fort (ADR-068): the hero on its entrance upgrades stacks of tiers 1–3, stack by stack, for 150 % of the upgrade price.</summary>
public sealed class HillFort(MapObjectId id, MapObjectDefinition definition, MapPosition position)
    : MapObject(id, definition, position, MapObjectKind.HillFort);

/// <summary>A town gate (ADR-068): the visitor picks one of its owner's towns and is moved to its entrance at no movement cost.</summary>
public sealed class TownGate(MapObjectId id, MapObjectDefinition definition, MapPosition position)
    : MapObject(id, definition, position, MapObjectKind.TownGate);

/// <summary>A war machine factory (ADR-068): the hero on its entrance buys its own offensive machine, the cart and the tent at the blacksmith's prices.</summary>
public sealed class WarMachineFactory(MapObjectId id, MapObjectDefinition definition, MapPosition position)
    : MapObject(id, definition, position, MapObjectKind.WarMachineFactory);

/// <summary>
/// A prison (ADR-068): the hero in it joins the visitor's player, at <see cref="Level"/>, and the prison goes. <see cref="Hero"/> (a roster id) and
/// <see cref="Level"/> left open by the map are drawn when the game starts (<see cref="PrisonRules"/>). The freed hero takes the prison's place.
/// </summary>
public sealed class Prison(MapObjectId id, MapObjectDefinition definition, MapPosition position, string? hero = null, int? level = null)
    : MapObject(id, definition, position, MapObjectKind.Prison)
{
    public string? Hero { get; internal set; } = hero;

    public int? Level { get; internal set; } = level;
}

/// <summary>Hill fort prices (ADR-068, MAP-11).</summary>
public static class HillFortRules
{
    /// <summary>Tiers a hill fort upgrades; tiers 4 and 5 only in towns.</summary>
    public const int MaxTier = 3;

    /// <summary>The creature a stack becomes at a hill fort, or null when it cannot be upgraded there.</summary>
    public static CreatureDefinition? UpgradeOf(CreatureCatalog creatures, string creature) =>
        creatures.TryGet(creature, out var line) && line.Tier <= MaxTier && line.UpgradesTo is { } upgrade && creatures.TryGet(upgrade, out var upgraded)
            ? upgraded
            : null;

    /// <summary>150 % of the price difference, rounded up per resource.</summary>
    public static ResourceSet Cost(CreatureCatalog creatures, CreatureStack stack)
    {
        var upgraded = UpgradeOf(creatures, stack.Creature) ?? throw new ArgumentException($"{stack.Creature} cannot be upgraded at a hill fort.", nameof(stack));
        var difference = (upgraded.Cost - creatures.Get(stack.Creature).Cost) * stack.Count;
        return new ResourceSet(
            Up(difference.Gold), Up(difference.Lumber), Up(difference.Ore), Up(difference.Crystal), Up(difference.Mercury), Up(difference.Sulfur));

        static int Up(int value) => ((value * 3) + 1) / 2;
    }
}

/// <summary>What the town gate's picker shows: an own town, and whether its entrance is free to arrive at.</summary>
public sealed record TownGateOption(MapObjectId Town, string Name, bool Available);

/// <summary>Prison numbers (ADR-068, MAP-5).</summary>
public static class PrisonRules
{
    /// <summary>The levels a prisoner may have, with their odds.</summary>
    public static IReadOnlyList<(int Weight, int Level)> LevelOdds { get; } = [(30, 1), (30, 5), (20, 10), (15, 20), (5, 30)];

    /// <summary>
    /// Fills in the prisons the map left open (game start): a random playable faction, then one of its heroes neither on the map nor in another
    /// prison (the players' start heroes are on the map), else any such hero; the level from <see cref="LevelOdds"/>.
    /// </summary>
    internal static void Prepare(GameState state)
    {
        var taken = state.Heroes.Select(h => h.Definition).OfType<string>()
            .Concat(state.ObjectsOf<Prison>().Select(p => p.Hero).OfType<string>())
            .ToHashSet(StringComparer.Ordinal);
        foreach (var prison in state.ObjectsOf<Prison>().OrderBy(p => p.Id.Value).ToList())
        {
            if (prison.Hero is null)
            {
                var towns = BuiltInMapObjects.PlayableTowns;
                var town = towns[state.Random.Next(towns.Count)];
                var free = state.HeroRoster.All.Where(h => !taken.Contains(h.Id)).OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
                var native = free.Where(h => h.Town == town).ToList();
                var pool = native.Count > 0 ? native : free;
                if (pool.Count > 0)
                {
                    prison.Hero = pool[state.Random.Next(pool.Count)].Id;
                    taken.Add(prison.Hero);
                }
            }

            prison.Level ??= WeightedRoll.Pick(LevelOdds, state.Random);
        }
    }
}
