using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;

namespace EternalWars.Core.Domain.Towns;

/// <summary>
/// Who a tavern offers (ADR-060). The six main towns sit on the ring of towns (docs/content/lore.md); a hero's <b>rank</b> for a town is
/// 0 native, 1 adjacent (the two ring neighbours), 2 not opposing (everything else) or 3 opposing (the town across the
/// ring). Stronghold and Swamp Bestiary have no ring: 0 native, 1 warriors of other towns, 2 everyone else. A basic tavern
/// offers two heroes, an upgraded one four, each slot asking for a rank; a rank with nobody left falls through to the
/// next ranks (and, last, back to the earlier ones). Heroes on the map are never offered; the order inside a rank is a
/// shuffle that is new every week and different for every player.
/// </summary>
public static class TavernRules
{
    /// <summary>What hiring a hero costs.</summary>
    public static ResourceSet HeroCost { get; } = new(Gold: 2500);

    /// <summary>A player has at most this many heroes.</summary>
    public const int MaxHeroes = 8;

    /// <summary>
    /// The ring of towns in order (docs/content/lore.md): the Dawn Covenant (Sylvan, Haven, Academy) and the Ashen Court (Dungeon,
    /// Necropolis, Inferno) are its two arcs, and the town across the ring is each town's arch-rival.
    /// </summary>
    public static IReadOnlyList<string> Ring { get; } =
        [TownIds.Sylvan, TownIds.Haven, TownIds.Academy, TownIds.Dungeon, TownIds.Necropolis, TownIds.Inferno];

    /// <summary>The towns next to <paramref name="town"/> on the ring; empty for towns without one.</summary>
    public static IReadOnlyList<string> Adjacent(string town) =>
        Ring.ToList().IndexOf(town) is var i and >= 0 ? [Ring[(i + 5) % 6], Ring[(i + 1) % 6]] : [];

    /// <summary>The town across the ring, or null for towns without one.</summary>
    public static string? Opposing(string town) => Ring.ToList().IndexOf(town) is var i and >= 0 ? Ring[(i + 3) % 6] : null;

    /// <summary>Whether the town offers by the ring (the six main towns) or by warriors (Stronghold, Swamp Bestiary and any other).</summary>
    public static bool UsesRing(string town) => Ring.Contains(town);

    /// <summary>How many ranks a town's pool has: 4 on the ring, 3 otherwise.</summary>
    public static int RankCount(string town) => UsesRing(town) ? 4 : 3;

    /// <summary>The rank a hero has for a town's tavern (0 is best).</summary>
    public static int RankOf(string town, HeroDefinition hero)
    {
        if (hero.Town == town)
        {
            return 0;
        }

        if (!UsesRing(town))
        {
            return hero.Class == HeroClass.Warrior ? 1 : 2;
        }

        return Adjacent(town).Contains(hero.Town) ? 1 : Opposing(town) == hero.Town ? 3 : 2;
    }

    /// <summary>The rank each slot of the tavern asks for: native + adjacent when basic, two natives + adjacent + not opposing when upgraded.</summary>
    public static IReadOnlyList<int> Slots(string town, int tavernLevel) => (UsesRing(town), tavernLevel >= 2) switch
    {
        (_, false) => [0, 1],
        (true, true) => [0, 0, 1, 2],
        (false, true) => [0, 0, 1, 1],
    };

    /// <summary>The ranks a slot tries: its own, the later ones, then the earlier ones.</summary>
    private static IEnumerable<int> Fallback(int wanted, int ranks) =>
        Enumerable.Range(wanted, ranks - wanted).Concat(Enumerable.Range(0, wanted).Reverse());

    /// <summary>
    /// The roster in this week's order for a player: a shuffle seeded from the game seed, the player and the week, so it
    /// needs no stored state and replays exactly (it does not draw from the game's random numbers).
    /// </summary>
    public static IReadOnlyList<HeroDefinition> PoolOrder(GameState state, PlayerId player)
    {
        var heroes = state.HeroRoster.All.OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
        var week = (ulong)((state.Date.AbsoluteDay - 1) / GameDate.DaysPerWeek);
        var seed = (state.Options.Seed * 0x9E3779B97F4A7C15UL) ^ ((ulong)(player.Value + 1) * 0xBF58476D1CE4E5B9UL) ^ (week * 0x94D049BB133111EBUL);
        var random = new GameRandom(seed);
        for (var i = heroes.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (heroes[i], heroes[j]) = (heroes[j], heroes[i]);
        }

        return heroes;
    }

    /// <summary>The heroes the town's tavern offers the player now, in slot order (none without a tavern).</summary>
    public static IReadOnlyList<HeroDefinition> Offers(GameState state, PlayerId player, Town town)
    {
        var level = town.Level(BuiltInBuildings.Tavern);
        if (level < 1)
        {
            return [];
        }

        var onMap = state.Heroes.Select(h => h.Definition).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var pool = PoolOrder(state, player).Where(h => !onMap.Contains(h.Id)).ToList();
        var ranks = RankCount(town.TownType);
        var offers = new List<HeroDefinition>();
        foreach (var wanted in Slots(town.TownType, level))
        {
            foreach (var rank in Fallback(wanted, ranks))
            {
                if (pool.FirstOrDefault(h => !offers.Contains(h) && RankOf(town.TownType, h) == rank) is { } hero)
                {
                    offers.Add(hero);
                    break;
                }
            }
        }

        return offers;
    }

    /// <summary>Why <paramref name="hero"/> cannot be hired in the town now, or null when it can.</summary>
    public static string? CheckHire(GameState state, Town town, string hero)
    {
        if (town.Level(BuiltInBuildings.Tavern) < 1)
        {
            return $"{town.Name} has no tavern.";
        }

        var owner = town.Owner ?? throw new InvalidOperationException("A town without an owner has no tavern customers.");
        if (state.HeroesOf(owner).Count() >= MaxHeroes)
        {
            return $"A player has at most {MaxHeroes} heroes.";
        }

        if (state.HeroAt(town.VisitPosition) is { } standing)
        {
            return $"{standing.Name} stands at the entrance; a new hero needs the place.";
        }

        if (Offers(state, owner, town).All(h => h.Id != hero))
        {
            return "That hero is not offered here.";
        }

        return state.GetPlayer(owner).Resources.CanAfford(HeroCost) ? null : "Not enough gold to hire a hero.";
    }
}
