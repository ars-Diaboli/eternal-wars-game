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
/// surrendered wait in its taverns ahead of the others (ADR-081). In a tournament each player has a pool of its own (ADR-366): the other player's
/// heroes do not leave it (<see cref="Taken"/>).
/// </summary>
public static class TavernRules
{
    /// <summary>
    /// The rumour the taverns tell this week: the map's rumours in turn (ADR-257), the first in the first week, starting over after the last, the
    /// same in every tavern and for every player; on a map without rumours, <paramref name="player"/>'s rumour from the rumour table
    /// (<see cref="Rumours.OfWeek"/>, ADR-296). Null when there is none (no map rumours and no player given, or nothing the table can fill).
    /// </summary>
    public static string? Rumour(GameState state, PlayerId? player = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        var rumours = state.Rules.Rumours;
        if (rumours.Count > 0)
        {
            return rumours[(state.Date.AbsoluteWeek - 1) % rumours.Count];
        }

        return player is { } who ? Rumours.OfWeek(state, who) : null;
    }

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
        var heroes = state.HeroRoster.Standard.OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
        var week = (ulong)((state.Date.AbsoluteDay - 1) / GameDate.DaysPerWeek);
        var faction = town.Aggregate(14695981039346656037UL, (hash, c) => (hash ^ c) * 1099511628211UL); // FNV-1a: stable across runs
        var seed = (state.Options.Seed * 0x9E3779B97F4A7C15UL) ^ ((ulong)(player.Value + 1) * 0xBF58476D1CE4E5B9UL) ^ (week * 0x94D049BB133111EBUL) ^ faction;
        var random = new GameRandom(seed);
        for (var i = heroes.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (heroes[i], heroes[j]) = (heroes[j], heroes[i]);
        }

        return MapBans.Allowed(heroes, h => state.Rules.Bans.BansHero(h.Id)); // the map's bans (ADR-126), after the shuffle so it stays the same
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
        var taken = Taken(state, player);
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

    /// <summary>
    /// The roster heroes no tavern offers <paramref name="player"/>: every hero on the map or waiting in a player's taverns. In a tournament
    /// (ADR-366) each player has a pool of its own, so only the player's own heroes (on the map, in a garrison or waiting) are taken.
    /// </summary>
    public static IReadOnlySet<string> Taken(GameState state, PlayerId? player)
    {
        ArgumentNullException.ThrowIfNull(state);
        var heroes = state.Rules.Tournament && player is { } own
            ? state.Heroes.Concat(state.GarrisonHeroes).Concat(state.RetiredHeroes).Where(h => h.Owner == own)
            : state.Heroes.Concat(state.RetiredHeroes);
        return heroes.Select(h => h.Definition).OfType<string>().ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// Why <paramref name="owner"/> may take no other hero now that it has all it may (<see cref="GameState.HeroLimitReached"/>), for a tavern, a map
    /// tavern and a prison: in a tournament its one hero (ADR-366), who may be replaced only once fallen; elsewhere the map's limit.
    /// </summary>
    public static string LimitRefusal(GameState state, PlayerId owner)
    {
        ArgumentNullException.ThrowIfNull(state);
        var name = state.GetPlayer(owner).Name;
        var limit = state.HeroLimit(owner);
        if (state.Rules.Tournament)
        {
            var hero = state.AllHeroesOf(owner).FirstOrDefault()?.Name ?? "a hero";
            return $"A tournament allows one hero a player, and {name} has {hero}. Another may be hired only once it has fallen.";
        }

        return limit == 1 ? $"{name} may have only one hero on this map." : $"{name} may have at most {limit} heroes on this map.";
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
            return LimitRefusal(state, owner);
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
