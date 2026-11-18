using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Magic;
using EternalWars.Core.Domain.Objects;

namespace EternalWars.Core.Domain.AI;

/// <summary>
/// What the parts of the <see cref="AdventureAi"/> share: the game, the weights, the battle estimator, the commands issued, and the turn's
/// main hero and claimed targets. It also holds the AI's measures: army power (Σ count × creature value, the measure the neutral guards use
/// too), armies as the player knows them (ADR-125) and resources in gold.
/// </summary>
internal sealed class AiContext(GameSession session, AiWeights weights, bool controlHumanSlots)
{
    private readonly List<string> _refusals = [];

    public GameSession Session => session;

    public GameState State { get; } = session.State;

    public AiWeights Weights => weights;

    /// <summary>Whether the AI also plays human slots and the battle sides of human players (the simulator).</summary>
    public bool ControlHumanSlots => controlHumanSlots;

    public BattleEstimator Estimator { get; } = new(session.State);

    /// <summary>The player's main hero this turn: the strongest army; the others carry troops to it.</summary>
    public HeroId? Main { get; set; }

    /// <summary>The targets a hero of the player already heads for this turn.</summary>
    public HashSet<MapPosition> Claimed { get; } = [];

    /// <summary>Commands the AI issued, and how many of them the game refused (should stay 0).</summary>
    public int Commands { get; private set; }

    public int Refused { get; private set; }

    /// <summary>The refused commands with the game's reasons.</summary>
    public IReadOnlyList<string> Refusals => _refusals;

    /// <summary>Whether today is an arena map's preparation day (ADR-160).</summary>
    public bool Preparing => ArenaRules.IsPreparationDay(State);

    /// <summary>Whether the player is still the one to move and the game goes on.</summary>
    public bool Active(PlayerId player) => !State.IsOver && State.CurrentPlayer.Id == player;

    /// <summary>Runs a command through the session (ADR-036) and counts it, and its refusal.</summary>
    public CommandResult Execute(GameCommand command)
    {
        var result = session.Execute(command);
        Commands++;
        if (!result.IsAccepted)
        {
            Refused++;
            _refusals.Add($"day {State.Date.AbsoluteDay}: {command}: {result.Error}");
        }

        return result;
    }

    public long Power(CreatureStack stack) => (long)stack.Count * State.Creatures.Get(stack.Creature).FightValue;

    public long ArmyPower(Army army) => army.Stacks.Sum(s => Power(s.Stack));

    /// <summary>
    /// What the player knows of armies it does not own (ADR-125): the counts its heroes' Scouting shows, the middle of each amount's range for
    /// the rest. <paramref name="owner"/>: the army's owner, null for neutral.
    /// </summary>
    public Army Seen(IEnumerable<CreatureStack> stacks, MapPosition at, PlayerId? owner) =>
        AdventureIntel.Reckoned(AdventureIntel.ScoutStacks(State, stacks, at, owner, State.CurrentPlayer.Id));

    public Army Seen(Hero enemy) => AdventureIntel.Reckoned(AdventureIntel.ScoutHero(State, enemy, State.CurrentPlayer.Id).Stacks);

    public CreatureStack SeenMonster(MonsterGuard monster) =>
        Seen([new CreatureStack(monster.Creature, Math.Max(1, monster.Count ?? 1))], monster.Position, null)[0]!;

    /// <summary>The stacks a hero entering <paramref name="position"/> must beat: a pickup only by its Area-mode guards (ADR-065).</summary>
    public IEnumerable<MonsterGuard> GuardsToBeat(MapPosition position, bool pickup) =>
        State.GuardsOf(position).Where(m => !pickup || m.Mode == GuardMode.Area);

    /// <summary>The gold kept back when spending at a map object: a third of the gold.</summary>
    public ResourceSet Spare(PlayerId player)
    {
        var resources = State.GetPlayer(player).Resources;
        return resources - new ResourceSet(Gold: resources.Gold / 3);
    }

    public bool OwnTeam(PlayerId player, PlayerId? other) => other is { } o && (o == player || State.AreAllies(o, player));

    /// <summary>Resources in gold (<see cref="GoldValue"/>).</summary>
    public static double Value(ResourceSet resources) => Enum.GetValues<ResourceType>().Sum(r => GoldValue(r, resources[r]));

    /// <summary>An amount of a resource in gold: lumber and ore 125 each, the magic resources 500.</summary>
    public static double GoldValue(ResourceType resource, int amount) => amount * resource switch
    {
        ResourceType.Gold => 1,
        ResourceType.Lumber or ResourceType.Ore => 125,
        _ => 500,
    };
}
