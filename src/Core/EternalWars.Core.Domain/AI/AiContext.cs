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

    /// <summary>
    /// Whether there is no tomorrow before the deciding battle, so everything goes into the army that fights it: an arena map's preparation day
    /// (ADR-160) or a tournament's last building day (ADR-366).
    /// </summary>
    public bool Preparing => ArenaRules.IsPreparationDay(State) || DaysLeft == 1;

    /// <summary>
    /// The building days a tournament has left, today included (ADR-366): 1 on its last day, 0 on the battle's; null on any other map.
    /// </summary>
    public int? DaysLeft => State.Rules.Tournament ? Math.Max(0, TournamentRules.BattleDay(State.Options) - State.Date.AbsoluteDay) : null;

    /// <summary>
    /// Whether the deciding battle is so near that the hero risks only small losses (<see cref="AiWeights.ArenaLoss"/>): the preparation day, or a
    /// tournament's last <see cref="AiWeights.CarefulDays"/> days, when a fallen hero's army could not be rebuilt (ADR-366).
    /// </summary>
    public bool Careful => Preparing || DaysLeft <= weights.CarefulDays;

    /// <summary>
    /// Whether the towns spend everything on the army that fights the deciding battle (<see cref="TownManager.PrepareTown"/>): on the
    /// preparation day, and in a tournament's last <see cref="AiWeights.ArmingDays"/> days once no week begins before its battle, so no growth is
    /// left to build for (ADR-366).
    /// </summary>
    public bool Arming => Preparing || (DaysLeft > 0 && DaysLeft <= weights.ArmingDays && WeeksToCome == 0);

    /// <summary>The days after today whose income still comes before a tournament's battle; unlimited on any other map.</summary>
    public int IncomeDays => DaysLeft is { } left ? Math.Max(0, left - 1) : int.MaxValue;

    /// <summary>The weeks that still begin before a tournament's battle, after today (each brings the towns' and dwellings' growth); unlimited on any other map.</summary>
    public int WeeksToCome => DaysLeft is { } left
        ? Enumerable.Range(State.Date.AbsoluteDay + 1, Math.Max(0, left - 1)).Count(day => (day - 1) % GameDate.DaysPerWeek == 0)
        : int.MaxValue;

    /// <summary>
    /// Whether <paramref name="position"/> is ground the player has yet to explore: on the map, unexplored, and not in the other player's
    /// tournament zone, which it never sees (<see cref="TournamentRules.Reaches"/>).
    /// </summary>
    public bool Unknown(PlayerId owner, MapPosition position) =>
        State.Map.Contains(position) && !State.Fog.IsExplored(owner, position) && TournamentRules.Reaches(State, owner, position);

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

    /// <summary>What may be spent at a map object: the resources less the part of the gold kept back (<see cref="AiWeights.KeptGoldPart"/>).</summary>
    public ResourceSet Spare(PlayerId player)
    {
        var resources = State.GetPlayer(player).Resources;
        return resources - new ResourceSet(Gold: resources.Gold / Math.Max(1, weights.KeptGoldPart));
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
