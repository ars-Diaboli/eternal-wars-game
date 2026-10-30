using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Players;

namespace EternalWars.Core.Domain.AI;

/// <summary>What a few fights between two armies came to, from the attacker's side.</summary>
/// <param name="Wins">Fights the attacker won.</param>
/// <param name="Runs">Fights played.</param>
/// <param name="LossFraction">Average share of the attacker's army power lost (0–1).</param>
public readonly record struct BattleEstimate(int Wins, int Runs, double LossFraction)
{
    public bool AlwaysWins => Runs > 0 && Wins == Runs;

    /// <summary>Wins every fight and loses at most <paramref name="maxLoss"/> of its army power doing it.</summary>
    public bool IsSafe(double maxLoss) => AlwaysWins && LossFraction <= maxLoss;
}

/// <summary>
/// Plays the battle engine on a throwaway game to see how a fight would go, which the army power sum alone cannot say
/// (shooters, speed and defence decide fights it gets wrong). The fights use fixed seeds of their own, so the game's
/// random numbers are untouched and the estimate depends only on the two armies. Results are cached until
/// <see cref="Clear"/>.
/// </summary>
internal sealed class BattleEstimator(GameState state)
{
    private static readonly ulong[] Seeds = [11, 23, 37];

    private readonly Dictionary<string, BattleEstimate> _cache = [];

    public void Clear() => _cache.Clear();

    /// <summary>
    /// A hero army attacking another army (a monster stack, a garrison or an enemy hero's troops); behind a town's fortifications when
    /// <paramref name="siege"/> is set (ADR-106), with the hero's own war machines.
    /// </summary>
    public BattleEstimate Fight(Hero attacker, Army defender, int defenderAttack = 0, int defenderDefense = 0, Battles.SiegeSpec? siege = null)
    {
        var key = $"{Describe(attacker.Army)}|{attacker.Attack}/{attacker.Defense}|{Describe(defender)}|{defenderAttack}/{defenderDefense}"
                  + (siege is null ? "" : $"|siege {siege.FortLevel}/{siege.Buildings} {string.Join(",", attacker.WarMachines.Order())}");
        if (!_cache.TryGetValue(key, out var estimate))
        {
            estimate = Simulate(attacker, defender, defenderAttack, defenderDefense, siege);
            _cache[key] = estimate;
        }

        return estimate;
    }

    private static string Describe(Army army) => string.Join(",", army.Slots.Select(s => s is null ? "-" : $"{s.Creature}:{s.Count}"));

    private BattleEstimate Simulate(Hero attacker, Army defender, int defenderAttack, int defenderDefense, Battles.SiegeSpec? siege)
    {
        var start = Power(attacker.Army);
        if (start == 0)
        {
            return new BattleEstimate(0, Seeds.Length, 1);
        }

        var wins = 0;
        var lost = 0.0;
        foreach (var seed in Seeds)
        {
            var players = new[] { new Player(new PlayerId(0), "Attacker", isHuman: false), new Player(new PlayerId(1), "Defender", isHuman: false) };
            var heroes = new[]
            {
                new Hero(new HeroId(1), "Attacker", players[0].Id, new MapPosition(0, 0), new Army(attacker.Army.Slots), attacker.TotalAttack, attacker.TotalDefense),
                new Hero(new HeroId(2), "Defender", players[1].Id, new MapPosition(1, 0), new Army(defender.Slots), defenderAttack, defenderDefense),
            };
            if (siege is not null)
            {
                // Without its offensive machine a hero breaks no walls.
                heroes[0].SetWarMachines(attacker.WarMachines);
            }

            var session = new GameSession(new GameState(
                new AdventureMap(3, 1), players, heroes, options: new GameOptions(Seed: seed), creatures: state.Creatures, heroRoster: state.HeroRoster));
            session.Battles.AiMayRetreat = false; // guards never retreat; the estimate is a fight to the end
            session.Battles.EstimateSiege = siege;
            session.Start();
            session.Execute(new StepHeroCommand(players[0].Id, heroes[0].Id, Direction.East));

            var survivors = session.State.Heroes.Any(h => h.Id == heroes[0].Id) ? Power(heroes[0].Army) : 0;
            var won = session.State.Heroes.All(h => h.Id != heroes[1].Id) || Power(heroes[1].Army) == 0;
            wins += won && survivors > 0 ? 1 : 0;
            lost += 1 - ((double)survivors / start);
        }

        return new BattleEstimate(wins, Seeds.Length, lost / Seeds.Length);
    }

    private long Power(Army army) => army.Stacks.Sum(s => (long)s.Stack.Count * state.Creatures.Get(s.Stack.Creature).Value);
}
