using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Players;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Generation;

/// <summary>
/// One player of a generated game, as the new-game screen sets it up: the template's start slot it takes, the town type it picked
/// (<see cref="RandomTown"/> = drawn from the seed), the hero it picked (roster id of a hero of that town; null = drawn from the town's heroes) and
/// the tier-1 dwelling its start town begins with when the template gives none (base creature id of a tier-1 line of the town; null = drawn).
/// </summary>
public sealed record PlayerSetup(int Slot, string Name, bool IsHuman, string TownType, string? Hero = null, int? Team = null, string? Tier1Dwelling = null)
{
    /// <summary>The town type of a player who chose "Random": resolved from the seed before the map is generated (RMG-7).</summary>
    public const string RandomTown = "random";
}

/// <summary>
/// The last stage of the generator (ADR-064, RMG-5): turns a generated map into the scenario <see cref="GameState"/>.
/// <list type="bullet">
/// <item>Players are made in slot order from <see cref="PlayerSetup"/> (player ids 0, 1, … in that order); a player's team is the one set up, else the start
/// zone's (<see cref="TemplateZone.Team"/>, RMG-6). A template with <see cref="MapTemplate.TeamsLocked"/> refuses other teams and locks them in the game.</item>
/// <item>A taken slot's start towns get the player's town type and the player as owner, the default start plus the start zone's
/// <see cref="TemplateZone.StartBuildings"/> and <see cref="TemplateZone.StartDwellings"/>; without a tier-1 dwelling there, the one the player picked
/// (a computer player, or a player who picked none, gets a random one). Every start dwelling opens with a week of creatures.</item>
/// <item>The picked hero (or one drawn from the town's heroes) stands at the first town's entrance with a HoMM3-style army: 2–3 stacks of the
/// town's tier-1 and tier-2 creatures, the hero's preferred creature among them when it is one, <see cref="StarterArmyMin"/>–<see cref="StarterArmyMax"/> power.</item>
/// <item>A slot nobody takes keeps its towns in place, neutral, garrisoned by the zone's strength like any neutral town.</item>
/// </list>
/// The map is used up: its terrain and objects become the game's. The game has the standard rules (<see cref="ScenarioRules.Default"/>) with the hero
/// limit of the template (<see cref="MapTemplate.MaxHeroes"/>) or of the caller (<c>maxHeroes</c>, ADR-068), and the
/// options of the new-game screen; the map is named after the template, size and seed. Streams: <c>heroes</c>, <c>dwellings</c>, <c>armies</c>,
/// <c>garrisons</c> per slot.
/// </summary>
public static class ScenarioAssembler
{
    public const int StarterArmyMin = 1000;

    public const int StarterArmyMax = 1500;

    /// <summary>Resolves <see cref="PlayerSetup.RandomTown"/> from the seed (stream <c>factions</c>), in slot order; refuses a town that is not playable.</summary>
    public static IReadOnlyList<PlayerSetup> ResolveTowns(IReadOnlyList<PlayerSetup> players, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(players);
        var random = new SeedStreams(seed).For("factions", 0);
        var towns = BuiltInMapObjects.PlayableTowns;
        if (players.FirstOrDefault(p => p.TownType != PlayerSetup.RandomTown && !towns.Contains(p.TownType)) is { } bad)
        {
            throw new ArgumentException($"'{bad.TownType}' is not a playable town.", nameof(players));
        }

        return players.OrderBy(p => p.Slot)
            .Select(p => p.TownType == PlayerSetup.RandomTown ? p with { TownType = towns[random.Next(towns.Count)] } : p)
            .ToList();
    }

    /// <summary>The town type of each taken slot, for the stages that paint and place start towns.</summary>
    public static IReadOnlyDictionary<int, string> StartTowns(IReadOnlyList<PlayerSetup> players) =>
        players.Where(p => p.TownType != PlayerSetup.RandomTown).ToDictionary(p => p.Slot, p => p.TownType);

    public static GameState Build(
        MapTemplate template,
        GeneratedMap map,
        IReadOnlyList<PlayerSetup> players,
        ulong seed,
        GameOptions? options = null,
        CreatureCatalog? creatures = null,
        HeroCatalog? heroRoster = null,
        BuildingCatalog? buildings = null,
        ArtifactCatalog? artifacts = null,
        int? maxHeroes = null)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(players);
        creatures ??= CreatureCatalog.Default;
        heroRoster ??= HeroCatalog.Default;
        var ordered = ResolveTowns(players, seed);
        Check(template, map, ordered, heroRoster, creatures);

        var streams = new SeedStreams(seed);
        var heroRandom = streams.For("heroes", 0);
        var made = new List<Player>();
        var heroes = new List<Hero>();
        var taken = ordered.Select(p => p.Hero).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var teams = Teams(template, ordered);
        for (var i = 0; i < ordered.Count; i++)
        {
            var setup = ordered[i];
            var id = new PlayerId(i);
            var zone = template.Zones.First(z => z.Owner == setup.Slot && z.Type is ZoneType.PlayerStart or ZoneType.AiStart);
            made.Add(new Player(id, setup.Name, setup.IsHuman, new TeamId(teams[i])));

            var start = map.Towns.Where(t => t.Slot == setup.Slot).Select(t => StartTown(map, t.Town, zone, setup, id, creatures, streams.For("dwellings", setup.Slot))).ToList();
            var hero = setup.Hero is { } picked ? heroRoster.Get(picked) : Draw(heroRoster, setup.TownType, taken, heroRandom);
            heroes.Add(Hero.Create(new HeroId(i + 1), id, start[0].VisitPosition, StarterArmy(hero, setup.TownType, creatures, streams.For("armies", setup.Slot)), hero));
        }

        foreach (var empty in map.Towns.Where(t => t.Slot is { } slot && ordered.All(p => p.Slot != slot)).ToList())
        {
            var zone = template.Zones[empty.Zone];
            var random = streams.For("garrisons", empty.Slot!.Value);
            var value = ZoneGuards.TownGarrisonValue(zone.Monsters);
            var (first, firstCount) = GuardPicker.Pick(creatures, value / 2, random);
            var (second, secondCount) = GuardPicker.Pick(creatures, value / 2, random);
            map.Replace(empty.Town, empty.Town.TownType, null, garrison: Army.Of(new CreatureStack(first.Id, firstCount), new CreatureStack(second.Id, secondCount)));
        }

        map.Map.Name = $"{template.Name} {map.Layout.Size}×{map.Layout.Size} #{seed}";
        var state = new GameState(map.Map, made, heroes, map.Objects, options, creatures: creatures, heroRoster: heroRoster, buildings: buildings, artifacts: artifacts);
        state.SetTeamsLocked(template.TeamsLocked);
        state.SetRules(ScenarioRules.Default with { MaxHeroes = maxHeroes ?? template.MaxHeroes, WeekEvents = template.WeekEvents.ToSettings() });
        return state;
    }

    private static void Check(MapTemplate template, GeneratedMap map, IReadOnlyList<PlayerSetup> players, HeroCatalog heroRoster, CreatureCatalog creatures)
    {
        if (players.Count == 0)
        {
            throw new ArgumentException("A game needs at least one player.", nameof(players));
        }

        if (players.Select(p => p.Slot).Distinct().Count() != players.Count)
        {
            throw new ArgumentException("Two players take the same start slot.", nameof(players));
        }

        foreach (var setup in players)
        {
            if (!map.Towns.Any(t => t.Slot == setup.Slot))
            {
                throw new ArgumentException($"Start slot {setup.Slot} has no town on this map.", nameof(players));
            }

            if (template.TeamsLocked && setup.Team is { } team && template.Zones.Any(z => z.Owner == setup.Slot && z.Team != team))
            {
                throw new ArgumentException($"The template's teams are locked: start slot {setup.Slot} cannot change its team.", nameof(players));
            }

            if (setup.IsHuman && template.Zones.Any(z => z.Owner == setup.Slot && z.Type == ZoneType.AiStart))
            {
                throw new ArgumentException($"Start slot {setup.Slot} is for computer players only.", nameof(players));
            }

            if (!BuiltInMapObjects.PlayableTowns.Contains(setup.TownType))
            {
                throw new ArgumentException($"'{setup.TownType}' is not a playable town.", nameof(players));
            }

            if (setup.Hero is { } hero && (!heroRoster.Contains(hero) || heroRoster.Get(hero).Town != setup.TownType))
            {
                throw new ArgumentException($"Hero '{hero}' is not a hero of {setup.TownType}.", nameof(players));
            }

            if (setup.Tier1Dwelling is { } line && !creatures.BaseCreaturesOf(setup.TownType).Any(c => c.Id == line && c.Tier == 1))
            {
                throw new ArgumentException($"'{line}' is not a tier-1 dwelling of {setup.TownType}.", nameof(players));
            }
        }

        if (players.Where(p => p.Hero is not null).GroupBy(p => p.Hero).Any(g => g.Count() > 1))
        {
            throw new ArgumentException("Two players picked the same hero.", nameof(players));
        }
    }

    /// <summary>Each player's team: set up, else its start zone's; a player with neither is on a team of its own, numbered past every team in use.</summary>
    private static int[] Teams(MapTemplate template, IReadOnlyList<PlayerSetup> players)
    {
        var chosen = players.Select(p => p.Team ?? template.Zones.FirstOrDefault(z => z.Owner == p.Slot && z.Type is ZoneType.PlayerStart or ZoneType.AiStart)?.Team).ToList();
        var next = chosen.OfType<int>().DefaultIfEmpty(-1).Max() + 1;
        return chosen.Select(t => t ?? next++).ToArray();
    }

    /// <summary>A player's start town:the player's type and ownership, the default start plus the zone's start buildings and dwellings.</summary>
    private static Town StartTown(GeneratedMap map, Town town, TemplateZone zone, PlayerSetup setup, PlayerId owner, CreatureCatalog creatures, GameRandom random)
    {
        var buildings = new Dictionary<string, int>(BuiltInBuildings.DefaultStart, StringComparer.Ordinal);
        foreach (var (building, level) in zone.StartBuildings ?? new Dictionary<string, int>())
        {
            buildings[building] = Math.Max(level, buildings.GetValueOrDefault(building));
        }

        var lines = creatures.BaseCreaturesOf(setup.TownType).OrderBy(c => c.Id, StringComparer.Ordinal).ToList();
        var dwellings = new Dictionary<string, int>(StringComparer.Ordinal);
        var tiers = zone.StartDwellings ?? [];
        if (!tiers.Contains(1))
        {
            var tier1 = setup.Tier1Dwelling ?? Choose(lines.Where(l => l.Tier == 1).ToList(), random)?.Id;
            if (tier1 is not null)
            {
                dwellings[tier1] = 1;
            }
        }

        foreach (var tier in tiers)
        {
            if (Choose(lines.Where(l => l.Tier == tier && !dwellings.ContainsKey(l.Id)).ToList(), random) is { } line)
            {
                dwellings[line.Id] = 1;
            }
        }

        // A week of creatures waits in every start dwelling, with the fort's growth bonus (ADR-066).
        var probe = new Town(town.Id, MapObjectCatalog.Default.Get(BuiltInMapObjects.TownId(setup.TownType)), town.Position, buildings: buildings);
        var available = dwellings.Keys.ToDictionary(l => l, l => TownRules.WeeklyGrowth(probe, creatures.Get(l)), StringComparer.Ordinal);
        return map.Replace(town, setup.TownType, owner, buildings, dwellings, available);
    }

    private static CreatureDefinition? Choose(List<CreatureDefinition> choices, GameRandom random) =>
        choices.Count == 0 ? null : choices[random.Next(choices.Count)];

    /// <summary>A hero of the town nobody has yet, drawn in roster-id order so it replays.</summary>
    private static HeroDefinition Draw(HeroCatalog roster, string town, HashSet<string> taken, GameRandom random)
    {
        var free = roster.All.Where(h => h.Town == town && !taken.Contains(h.Id)).OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
        var hero = free.Count > 0 ? free[random.Next(free.Count)] : throw new InvalidOperationException($"No hero of {town} is left.");
        taken.Add(hero.Id);
        return hero;
    }

    /// <summary>
    /// HoMM3-style: 2–3 stacks of the town's tier-1 and tier-2 creatures (the preferred creature first when it is one of them), sharing
    /// <see cref="StarterArmyMin"/>–<see cref="StarterArmyMax"/> power, the first stack the largest.
    /// </summary>
    private static Army StarterArmy(HeroDefinition hero, string town, CreatureCatalog creatures, GameRandom random)
    {
        var pool = creatures.BaseCreaturesOf(town).Where(c => c.Tier <= 2).OrderBy(c => c.Tier).ThenBy(c => c.Id, StringComparer.Ordinal).ToList();
        if (pool.Count == 0)
        {
            return new Army();
        }

        var count = Math.Min(pool.Count, 2 + random.Next(2));
        var picks = new List<CreatureDefinition>();
        if (pool.FirstOrDefault(c => c.Id == hero.PreferredCreature) is { } preferred)
        {
            picks.Add(preferred);
        }

        while (picks.Count < count)
        {
            var rest = pool.Where(c => !picks.Contains(c)).ToList();
            picks.Add(rest[random.Next(rest.Count)]);
        }

        var total = StarterArmyMin + random.Next(StarterArmyMax - StarterArmyMin + 1);
        int[] shares = count == 2 ? [60, 40] : [50, 30, 20];
        var stacks = picks.Select((c, i) => new CreatureStack(c.Id, Math.Max(1, (int)Math.Round(total * shares[i] / 100.0 / Math.Max(1, c.Value))))).ToArray();
        return Army.Of(stacks);
    }
}
