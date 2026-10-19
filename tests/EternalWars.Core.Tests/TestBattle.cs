using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Events;

namespace EternalWars.Core.Tests;

/// <summary>
/// Builds a battlefield with units exactly where a test wants them (no deployment, no obstacles unless added),
/// for rule-level tests of <see cref="BattleEngine"/>, <see cref="BattleGrid"/> and the damage formula.
/// </summary>
internal sealed class TestBattle
{
    private TestBattle(BattleState state)
    {
        State = state;
        Bus.Subscribe<IEvent>(Events.Add);
        Engine = new BattleEngine(state, Bus);
    }

    public BattleState State { get; }

    public BattleEngine Engine { get; }

    public EventBus Bus { get; } = new();

    public List<IEvent> Events { get; } = [];

    public static TestBattle Create(
        int width = BattleState.DefaultWidth, ulong seed = 7, int attackerAttack = 0, int defenderDefense = 0,
        HeroBattleBonuses? attackerBonuses = null, HeroBattleBonuses? defenderBonuses = null, IReadOnlyCollection<string>? nativeTowns = null)
    {
        var state = new BattleState(
            new BattleParticipant(BattleSide.Attacker, new PlayerId(0), new HeroId(1), null, attackerAttack, 0, SkillLevel.None, false, attackerBonuses),
            new BattleParticipant(BattleSide.Defender, new PlayerId(1), new HeroId(2), null, 0, defenderDefense, SkillLevel.None, false, defenderBonuses),
            TerrainType.Grass,
            new MapPosition(0, 0),
            isAmbush: false,
            new GameRandom(seed),
            nativeTowns)
        {
            Width = width,
        };
        return new TestBattle(state);
    }

    public BattleUnit Add(BattleSide side, string creature, int count, int x, int y)
    {
        var unit = new BattleUnit(State.Units.Count + 1, side, CreatureCatalog.Default.Get(creature), count, State.Units.Count, new CellPosition(x, y));
        State.MutableUnits.Add(unit);
        return unit;
    }

    public BattleUnit Add(BattleSide side, CreatureDefinition creature, int count, int x, int y)
    {
        var unit = new BattleUnit(State.Units.Count + 1, side, creature, count, State.Units.Count, new CellPosition(x, y));
        State.MutableUnits.Add(unit);
        return unit;
    }

    public void AddObstacle(ObstacleClass cover, int x, int y) =>
        State.MutableObstacles.Add(new BattleObstacle(State.Obstacles.Count + 1, new CellPosition(x, y), cover));

    /// <summary>Starts round 1 and then hands the turn to <paramref name="active"/>, whatever the initiative order says.</summary>
    public void StartWith(BattleUnit active)
    {
        Engine.StartFighting();
        State.MutableQueue.Remove(active.Id);
        if (State.ActiveUnit is { } first && first != active)
        {
            State.MutableQueue.Insert(0, first.Id);
        }

        State.ActiveUnit = active;
    }

    /// <summary>A creature with fixed damage (min = max), so expected numbers are exact.</summary>
    public static CreatureDefinition Fixed(
        string id,
        int damage,
        int health = 10,
        int attack = 10,
        int defense = 10,
        CreatureSize size = CreatureSize.Small,
        int speed = 10,
        int initiative = 10,
        int shots = 0,
        params CreatureCapability[] capabilities) =>
        new(id, id, 1, size)
        {
            Attack = attack, Defense = defense, MinDamage = damage, MaxDamage = damage, Health = health,
            Speed = speed, MapSpeed = 5, Initiative = initiative, Shots = shots, Capabilities = capabilities,
        };
}
