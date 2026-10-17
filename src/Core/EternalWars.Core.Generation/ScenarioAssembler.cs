using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Players;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Generation;

/// <summary>
/// One player of a generated game, as the new-game screen sets it up: the template's start slot it takes, the town type it picked and the
/// hero it picked (roster id of a hero of that town; null = drawn from the town's heroes).
/// </summary>
public sealed record PlayerSetup(int Slot, string Name, bool IsHuman, string TownType, string? Hero = null, int? Team = null);

/// <summary>
/// The last stage of the generator (ADR-064, RMG-5): turns a generated map into the scenario <see cref="GameState"/>.
/// <list type="bullet">
/// <item>Players are made in slot order from <see cref="PlayerSetup"/> (player ids 0, 1, … in that order).</item>
/// <item>A configured slot's start towns take the picked town type and the player as owner; its picked hero (or one drawn from the town's
/// heroes) stands at the first town's entrance with a starter army (<see cref="StarterArmyValue"/> of its preferred creature).</item>
/// <item>A slot nobody takes keeps its towns where they are, neutral (user rule).</item>
/// </list>
/// The map is used up: its terrain and objects become the game's. Stream: <c>heroes</c>.
/// </summary>
public static class ScenarioAssembler
{
    /// <summary>Army power of a start hero's army (placeholder until play-testing).</summary>
    public const int StarterArmyValue = 1000;

    public static GameState Build(
        MapTemplate template,
        GeneratedMap map,
        IReadOnlyList<PlayerSetup> players,
        ulong seed,
        GameOptions? options = null,
        CreatureCatalog? creatures = null,
        HeroCatalog? heroRoster = null)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(players);
        creatures ??= CreatureCatalog.Default;
        heroRoster ??= HeroCatalog.Default;
        Check(template, map, players, heroRoster);

        var random = new SeedStreams(seed).For("heroes", 0);
        var ordered = players.OrderBy(p => p.Slot).ToList();
        var made = new List<Player>();
        var heroes = new List<Hero>();
        var taken = players.Select(p => p.Hero).OfType<string>().ToHashSet(StringComparer.Ordinal);
        for (var i = 0; i < ordered.Count; i++)
        {
            var setup = ordered[i];
            var id = new PlayerId(i);
            made.Add(new Player(id, setup.Name, setup.IsHuman, setup.Team is { } team ? new TeamId(team) : null));

            var towns = map.Towns.Where(t => t.Slot == setup.Slot).ToList();
            var first = towns.Select(t => map.Replace(t.Town, setup.TownType, id)).ToList()[0];
            var hero = setup.Hero is { } picked ? heroRoster.Get(picked) : Draw(heroRoster, setup.TownType, taken, random);
            heroes.Add(Hero.Create(new HeroId(i + 1), id, first.VisitPosition, StarterArmy(hero, creatures), hero));
        }

        return new GameState(map.Map, made, heroes, map.Objects, options, creatures: creatures, heroRoster: heroRoster);
    }

    private static void Check(MapTemplate template, GeneratedMap map, IReadOnlyList<PlayerSetup> players, HeroCatalog heroRoster)
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

            if (setup.IsHuman && template.Zones.Any(z => z.Owner == setup.Slot && z.Type == ZoneType.AiStart))
            {
                throw new ArgumentException($"Start slot {setup.Slot} is for computer players only.", nameof(players));
            }

            if (!Domain.Objects.BuiltInMapObjects.PlayableTowns.Contains(setup.TownType))
            {
                throw new ArgumentException($"'{setup.TownType}' is not a playable town.", nameof(players));
            }

            if (setup.Hero is { } hero && (!heroRoster.Contains(hero) || heroRoster.Get(hero).Town != setup.TownType))
            {
                throw new ArgumentException($"Hero '{hero}' is not a hero of {setup.TownType}.", nameof(players));
            }
        }

        if (players.Where(p => p.Hero is not null).GroupBy(p => p.Hero).Any(g => g.Count() > 1))
        {
            throw new ArgumentException("Two players picked the same hero.", nameof(players));
        }
    }

    /// <summary>A hero of the town nobody has yet, drawn in roster-id order so it replays.</summary>
    private static HeroDefinition Draw(HeroCatalog roster, string town, HashSet<string> taken, GameRandom random)
    {
        var free = roster.All.Where(h => h.Town == town && !taken.Contains(h.Id)).OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
        var hero = free.Count > 0 ? free[random.Next(free.Count)] : throw new InvalidOperationException($"No hero of {town} is left.");
        taken.Add(hero.Id);
        return hero;
    }

    /// <summary>One stack of the hero's preferred creature worth about <see cref="StarterArmyValue"/>.</summary>
    private static Army StarterArmy(HeroDefinition hero, CreatureCatalog creatures)
    {
        var creature = creatures.Get(hero.PreferredCreature);
        return Army.Of(new CreatureStack(creature.Id, Math.Max(1, (int)Math.Round((double)StarterArmyValue / Math.Max(1, creature.Value)))));
    }
}
