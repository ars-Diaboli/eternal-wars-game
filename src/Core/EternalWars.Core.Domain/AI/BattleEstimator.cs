using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Events;

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
/// What the AI's battle estimates have done on a thread: the estimates asked for, the fights played for those not kept, and the time the fights
/// took (diagnostics for <c>sim game --profile</c>; rules never read it).
/// </summary>
public readonly record struct BattleEstimateCounters(long Asked, long Played, TimeSpan Time)
{
    /// <summary>The estimates this thread has asked for so far. Per thread, so a game on one thread counts only its own.</summary>
    public static BattleEstimateCounters Current => BattleEstimator.Counters;

    public static BattleEstimateCounters operator +(BattleEstimateCounters a, BattleEstimateCounters b) => new(a.Asked + b.Asked, a.Played + b.Played, a.Time + b.Time);

    public static BattleEstimateCounters operator -(BattleEstimateCounters a, BattleEstimateCounters b) => new(a.Asked - b.Asked, a.Played - b.Played, a.Time - b.Time);
}

/// <summary>
/// Plays the battle engine on a battle of its own to see how a fight would go, which the army power sum alone cannot say (shooters, speed and
/// defence decide fights it gets wrong). The fights use fixed seeds of their own, so the game's random numbers are untouched and the estimate
/// depends only on the two armies, the sides' Attack and Defense and a siege's fortifications; so estimates are kept from turn to turn, the
/// last <see cref="Capacity"/> to <see cref="Capacity"/> × 2 of them.
/// </summary>
/// <remarks>
/// Each side is led by a bare hero (its Attack and Defense; no skills, spells or artifacts) controlled by the AI that never retreats, on grass,
/// set up as <see cref="BattleStarter.BeginHeroBattle"/> sets a hero battle up and played out as <see cref="BattleCommandHandler.Preview"/> plays
/// auto combat: unheard, outside any game.
/// </remarks>
internal sealed class BattleEstimator(GameState state)
{
    /// <summary>Estimates kept in each of the two generations: about a game's worth on a big map.</summary>
    public const int Capacity = 4096;

    private static readonly ulong[] Seeds = [0xA356BE306E9B126D, 0x993810E13B72E5F7, 0xB667B9039AE8A58A];

    [ThreadStatic]
    private static BattleEstimateCounters _counters;

    /// <summary>The newer generation, where estimates go; full, it becomes the older one and the oldest are dropped.</summary>
    private Dictionary<FightKey, BattleEstimate> _recent = [];

    private Dictionary<FightKey, BattleEstimate> _older = [];

    internal static BattleEstimateCounters Counters => _counters;

    /// <summary>
    /// A hero army attacking another army (a monster stack, a garrison or an enemy hero's troops); behind a town's fortifications when
    /// <paramref name="siege"/> is set (ADR-106), with the hero's own war machines.
    /// </summary>
    public BattleEstimate Fight(Hero attacker, Army defender, int defenderAttack = 0, int defenderDefense = 0, SiegeSpec? siege = null)
    {
        _counters = _counters with { Asked = _counters.Asked + 1 };
        var key = new FightKey(attacker, defender, defenderAttack, defenderDefense, siege);
        if (_recent.TryGetValue(key, out var estimate))
        {
            return estimate;
        }

        if (!_older.Remove(key, out estimate))
        {
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            estimate = Simulate(attacker, defender, defenderAttack, defenderDefense, siege);
            _counters = _counters with { Played = _counters.Played + 1, Time = _counters.Time + System.Diagnostics.Stopwatch.GetElapsedTime(started) };
        }

        if (_recent.Count >= Capacity)
        {
            (_older, _recent) = (_recent, _older);
            _recent.Clear();
        }

        _recent[key] = estimate;
        return estimate;
    }

    private BattleEstimate Simulate(Hero attacker, Army defender, int defenderAttack, int defenderDefense, SiegeSpec? siege)
    {
        var start = Power(attacker.Army);
        if (start == 0)
        {
            return new BattleEstimate(0, Seeds.Length, 1);
        }

        if (defender.IsEmpty)
        {
            // Nobody to fight: the defender is overrun without a battlefield.
            return new BattleEstimate(Seeds.Length, Seeds.Length, 0);
        }

        var leader = new Hero(new HeroId(1), "Attacker", new PlayerId(0), new MapPosition(0, 0), new Army(attacker.Army.Slots), attacker.TotalAttack, attacker.TotalDefense);
        if (siege is not null)
        {
            // Without its offensive machine a hero breaks no walls.
            leader.SetWarMachines(attacker.WarMachines);
        }

        var opponent = new Hero(new HeroId(2), "Defender", new PlayerId(1), new MapPosition(1, 0), new Army(defender.Slots), defenderAttack, defenderDefense);
        var (attackers, defenders) = (Participant(BattleSide.Attacker, leader), Participant(BattleSide.Defender, opponent));
        var (attackerStacks, defenderStacks) = (Stacks(leader, besieging: siege is not null), Stacks(opponent, besieging: false));
        var natives = state.Terrains.Get(Map.TerrainType.Grass).NativeTowns;
        var unheard = new EventBus();

        var wins = 0;
        var lost = 0.0;
        foreach (var seed in Seeds)
        {
            var battle = new BattleState(attackers, defenders, Map.TerrainType.Grass, opponent.Position, isAmbush: false, new GameRandom(seed), natives)
            {
                Creatures = state.Creatures,
                Spells = state.Spells,
                Siege = siege is null ? null : SiegeRules.Create(siege),
                Width = siege is null ? BattleState.DefaultWidth : BattleState.SiegeWidth,
            };
            BattleSetup.Deploy(battle, attackerStacks, defenderStacks);
            new BattleEngine(battle, unheard).StartFighting();
            BattleFlow.PlayComputer(battle, unheard, (BattleAiLevel.Latest, BattleAiLevel.Latest), mayRetreat: false);

            // The winner keeps what lived (a beaten attacker leaves the map with nothing).
            var survivors = 0L;
            if (battle.Winner == BattleSide.Attacker)
            {
                var army = new Army(attacker.Army.Slots);
                BattleOutcome.KeepSurvivors(battle, BattleSide.Attacker, army);
                survivors = Power(army);
            }

            wins += survivors > 0 ? 1 : 0;
            lost += 1 - ((double)survivors / start);
        }

        return new BattleEstimate(wins, Seeds.Length, lost / Seeds.Length);
    }

    /// <summary>A bare hero's side, as <see cref="BattleStarter"/> makes it: commanded by the AI, its army's mixed factions lowering morale.</summary>
    private BattleParticipant Participant(BattleSide side, Hero hero) => new(
        side, hero.Owner, hero.Id, null, hero.TotalAttack, hero.TotalDefense, hero.SkillLevelOf(BuiltInSkills.Tactics), IsComputer: true,
        HeroBattleBonuses.For(hero, state.Creatures),
        MoraleAndLuck.MixedFactions(hero.Army.Stacks.Select(s => state.Creatures.Get(s.Stack.Creature))),
        BattleCaster.For(hero, state.Spells));

    /// <summary>A bare hero's army and war machines, as <see cref="BattleStarter"/> fields them (the offensive machine only for a siege's attacker).</summary>
    private List<BattleStackSpec> Stacks(Hero hero, bool besieging)
    {
        var stacks = hero.Army.Stacks.Select(s => new BattleStackSpec(state.Creatures.Get(s.Stack.Creature), s.Stack.Count, s.Slot)).ToList();
        foreach (var slot in hero.WarMachines.Order())
        {
            if (slot != WarMachineSlot.Offensive || besieging || WarMachines.FieldsOffensive(hero.SkillLevelOf(BuiltInSkills.Ballistics)))
            {
                stacks.Add(BattleStackSpec.Of(WarMachines.KindOf(slot, null)));
            }
        }

        return stacks;
    }

    private long Power(Army army) => army.Stacks.Sum(s => (long)s.Stack.Count * state.Creatures.Get(s.Stack.Creature).FightValue);

    /// <summary>
    /// What an estimate depends on: both armies slot by slot, the sides' Attack and Defense, and a siege's fort level and building count with the
    /// attacker's war machines (they fight only in a siege). Its hash is worked out once.
    /// </summary>
    private readonly struct FightKey : IEquatable<FightKey>
    {
        private readonly string?[] _creatures;
        private readonly int[] _numbers;
        private readonly int _hash;

        public FightKey(Hero attacker, Army defender, int defenderAttack, int defenderDefense, SiegeSpec? siege)
        {
            _creatures = new string?[Army.SlotCount * 2];
            _numbers = new int[(Army.SlotCount * 2) + 7];
            var hash = new HashCode();
            for (var slot = 0; slot < Army.SlotCount; slot++)
            {
                Put(slot, attacker.Army.Slots[slot], ref hash);
                Put(Army.SlotCount + slot, defender.Slots[slot], ref hash);
            }

            var machines = 0;
            if (siege is not null)
            {
                foreach (var slot in attacker.WarMachines)
                {
                    machines |= 1 << (int)slot;
                }
            }

            var at = Army.SlotCount * 2;
            foreach (var number in (ReadOnlySpan<int>)[attacker.TotalAttack, attacker.TotalDefense, defenderAttack, defenderDefense,
                         siege?.FortLevel ?? -1, siege?.Buildings ?? -1, machines])
            {
                _numbers[at++] = number;
                hash.Add(number);
            }

            _hash = hash.ToHashCode();
        }

        private void Put(int index, CreatureStack? stack, ref HashCode hash)
        {
            _creatures[index] = stack?.Creature;
            _numbers[index] = stack?.Count ?? 0;
            hash.Add(stack?.Creature, StringComparer.Ordinal);
            hash.Add(_numbers[index]);
        }

        public bool Equals(FightKey other) =>
            _hash == other._hash && _numbers.AsSpan().SequenceEqual(other._numbers) && _creatures.AsSpan().SequenceEqual(other._creatures, StringComparer.Ordinal);

        public override bool Equals(object? obj) => obj is FightKey other && Equals(other);

        public override int GetHashCode() => _hash;
    }
}
