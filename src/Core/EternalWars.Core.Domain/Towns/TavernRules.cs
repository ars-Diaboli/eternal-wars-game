using EternalWars.Core.Domain.Armies;
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
/// shuffle that is new every week and different for every player and town faction (ADR-070). A player's heroes that retreated or
/// surrendered wait in its taverns ahead of the others (ADR-081).
/// </summary>
public static class TavernRules
{
    /// <summary>What hiring a hero costs.</summary>
    public static ResourceSet HeroCost { get; } = new(Gold: 2500);

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

    /// <summary>The rank each slot of the tavern asks for: native + adjacent when basic, two natives + two adjacent with the Brotherhood (ADR-070).</summary>
    public static IReadOnlyList<int> Slots(string town, int tavernLevel) => tavernLevel >= 2 ? [0, 0, 1, 1] : [0, 1];

    /// <summary>
    /// The army a newly hired roster hero brings (ADR-070, MAP-17): one week's growth of its preferred creature (placeholder, ADR-081); none
    /// when the preferred creature is unknown.
    /// </summary>
    public static CreatureStack? StarterStack(GameState state, HeroDefinition hero) =>
        state.Creatures.TryGet(hero.PreferredCreature, out var creature) && creature.WeeklyGrowth > 0 ? new CreatureStack(creature.Id, creature.WeeklyGrowth) : null;

    /// <summary>A hero of the player's waiting in its taverns after a retreat or a surrender (ADR-081), by roster id.</summary>
    public static Hero? Returning(GameState state, PlayerId player, string hero) =>
        state.RetiredHeroesOf(player).FirstOrDefault(h => h.Definition == hero);

    /// <summary>The ranks a slot tries: its own, the later ones, then the earlier ones.</summary>
    private static IEnumerable<int> Fallback(int wanted, int ranks) =>
        Enumerable.Range(wanted, ranks - wanted).Concat(Enumerable.Range(0, wanted).Reverse());

    /// <summary>
    /// The roster in this week's order for a player and a town faction (ADR-070: the player's towns of one faction share a pool): a shuffle
    /// seeded from the game seed, the player, the faction and the week, so it needs no stored state and replays exactly (it does not draw from
    /// the game's random numbers).
    /// </summary>
    public static IReadOnlyList<HeroDefinition> PoolOrder(GameState state, PlayerId player, string town)
    {
        var heroes = state.HeroRoster.All.OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
        var week = (ulong)((state.Date.AbsoluteDay - 1) / GameDate.DaysPerWeek);
        var faction = town.Aggregate(14695981039346656037UL, (hash, c) => (hash ^ c) * 1099511628211UL); // FNV-1a: stable across runs
        var seed = (state.Options.Seed * 0x9E3779B97F4A7C15UL) ^ ((ulong)(player.Value + 1) * 0xBF58476D1CE4E5B9UL) ^ (week * 0x94D049BB133111EBUL) ^ faction;
        var random = new GameRandom(seed);
        for (var i = heroes.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (heroes[i], heroes[j]) = (heroes[j], heroes[i]);
        }

        return heroes;
    }

    /// <summary>
    /// The heroes the town's tavern offers the player now, in slot order (none without a tavern). The player's own heroes that retreated or
    /// surrendered come first, oldest first, in every tavern of the player (ADR-081); the other slots follow the faction's pool.
    /// </summary>
    public static IReadOnlyList<HeroDefinition> Offers(GameState state, PlayerId player, Town town)
    {
        var level = town.Level(BuiltInBuildings.Tavern);
        if (level < 1)
        {
            return [];
        }

        var slots = Slots(town.TownType, level);
        var offers = state.RetiredHeroesOf(player)
            .Where(h => state.HeroRoster.Contains(h.Definition!))
            .Select(h => state.HeroRoster.Get(h.Definition!))
            .Take(slots.Count)
            .ToList();
        var taken = state.Heroes.Concat(state.RetiredHeroes).Select(h => h.Definition).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var pool = PoolOrder(state, player, town.TownType).Where(h => !taken.Contains(h.Id)).ToList();
        var ranks = RankCount(town.TownType);
        foreach (var wanted in slots.Skip(offers.Count))
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
        if (state.HeroLimitReached(owner))
        {
            return $"{state.GetPlayer(owner).Name} may have at most {state.HeroLimit(owner)} heroes on this map.";
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
