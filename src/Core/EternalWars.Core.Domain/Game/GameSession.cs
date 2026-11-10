using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.MapEvents;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Domain.Turns;
using EternalWars.Core.Domain.Visibility;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Game;

/// <summary>
/// Composition root of a running game: one state, one bus, the services that mutate the state.
/// Front-ends (3D game, 2D harness, tests, AI, simulator) change the game exclusively through
/// <see cref="Execute"/> and observe it exclusively through <see cref="Events"/>. Every accepted command is
/// recorded in <see cref="CommandLog"/>; seed + map + log reproduce the game exactly (ADR-036).
/// </summary>
public sealed class GameSession
{
    private readonly List<GameCommand> _log = [];
    private readonly CommandHandlers _handlers = new();

    /// <summary>
    /// Checks that a refused command left the game as it was (its digest text unchanged) and fails loudly otherwise. The tests turn it on for
    /// every game they play; it costs a digest a command.
    /// </summary>
    internal static bool CheckRefusals { get; set; }

    public GameSession(GameState state, IEventBus? bus = null)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
        Bus = bus ?? new EventBus();

        // Core handlers subscribe first so their follow-up events are queued before views react.
        new VisibilityService(State, Bus).SubscribeTo(Bus);

        Flow = new GameFlowService(State, Bus);
        Economy = new EconomyService(State, Bus);
        Armies = new ArmyService(State, Bus);
        Progression = new ProgressionService(State, Bus);
        Artifacts = new ArtifactService(State, Bus);
        Battles = new BattleService(State, Bus, Armies, Progression, Artifacts, Economy);
        Guards = new GuardService(State, Bus, Battles);
        Towns = new TownService(State, Bus, Economy);
        Caravans = new CaravanService(State, Bus, Economy);
        var objects = new ObjectInteractionContext(State, Bus, Economy);
        Grail = new GrailService(objects, Artifacts);
        Interactions = new MapObjectInteractionService(objects, Battles, Guards, Progression, Artifacts, Grail);
        ObjectDecisions = new ObjectDecisionService(objects, Battles, Progression, Artifacts);
        ObjectShops = new ObjectShopService(objects, Progression, Artifacts);
        ObjectLifecycle = new ObjectLifecycleService(objects, Artifacts);
        MapEvents = new MapEventService(State, Bus, Economy, Progression, Artifacts, Battles);
        MapEvents.SubscribeTo(Bus);
        Movement = new HeroMovementService(State, Bus, Interactions, MapEvents);
        AdventureMagic = new Magic.AdventureMagicService(State, Bus);
        AdventureMagic.SubscribeTo(Bus);
        Arena = new ArenaService(State, Bus, Battles);

        // The list's order is the rules' order at every step of the calendar (ITurnPhaseParticipant): it fixes the events and the random draws.
        Turns = new TurnService(State, Bus, Arena,
            [MapEvents, Progression, Movement, new ManaRestoration(State, Bus), Towns, Caravans, Economy, Guards, ObjectLifecycle]);

        AdventureCommandHandlers.Register(_handlers, Movement, Turns, Guards, Progression, Armies, AdventureMagic, Grail);
        ObjectCommandHandlers.Register(_handlers, ObjectDecisions, ObjectShops, MapEvents);
        TownCommandHandlers.Register(_handlers, Towns, Caravans, Economy);
        ArtifactCommandHandlers.Register(_handlers, Artifacts, Armies);
        BattleCommandHandlers.Register(_handlers, Battles);
    }

    public GameState State { get; }

    public IEventSubscriber Events => Bus;

    /// <summary>Accepted commands in order. Together with the map and <see cref="GameOptions.Seed"/> this replays the game.</summary>
    public IReadOnlyList<GameCommand> CommandLog => _log;

    /// <summary>Queries: daily income.</summary>
    public EconomyService Economy { get; }

    /// <summary>Queries: route planning (<see cref="HeroMovementService.FindPath"/>).</summary>
    public HeroMovementService Movement { get; }

    /// <summary>Queries: who commands a battle side.</summary>
    public BattleService Battles { get; }

    /// <summary>Queries: army power (dispositions).</summary>
    public GuardService Guards { get; }

    /// <summary>The map events (ADR-115); <see cref="MapEventService.IsBusy"/> tells whether one is running.</summary>
    public MapEventService MapEvents { get; }

    /// <summary>The command types that have a handler: every concrete <see cref="GameCommand"/>.</summary>
    internal IReadOnlyCollection<Type> HandledCommandTypes => _handlers.CommandTypes;

    internal GameFlowService Flow { get; }

    internal ArmyService Armies { get; }

    internal ArtifactService Artifacts { get; }

    internal ProgressionService Progression { get; }

    internal MapObjectInteractionService Interactions { get; }

    internal ObjectDecisionService ObjectDecisions { get; }

    internal ObjectShopService ObjectShops { get; }

    internal ObjectLifecycleService ObjectLifecycle { get; }

    internal GrailService Grail { get; }

    internal TurnService Turns { get; }

    internal ArenaService Arena { get; }

    internal TownService Towns { get; }

    internal CaravanService Caravans { get; }

    private Magic.AdventureMagicService AdventureMagic { get; }

    private IEventBus Bus { get; }

    /// <summary>Starts the game: day 1, the first player's turn. Part of every game, so it is not a command.</summary>
    public void Start()
    {
        PlaceholderRules.Resolve(State, Bus);
        Objects.GrailRules.Bury(State);
        Flow.RemoveSurplusHeroes();
        Flow.Begin();
        Artifacts.RollUnsetPickups();
        PrisonRules.Prepare(State);
        ObjectLifecycle.PrepareObjects();
        Magic.MagicRules.DrawGuilds(State);
        Turns.StartGame();
        Settle();
    }

    /// <summary>
    /// Validates and applies a command. Refused commands change nothing and are not logged; game-rule outcomes
    /// that are not refusals (a step into a wall) come back in <see cref="CommandResult.Outcome"/>.
    /// </summary>
    public CommandResult Execute(GameCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!State.IsStarted)
        {
            return CommandResult.Rejected("The game has not started yet.");
        }

        if (State.IsOver)
        {
            return CommandResult.Rejected("The game is over.");
        }

        if (Authorize(command) is { } refusal)
        {
            return CommandResult.Rejected(refusal);
        }

        object? outcome;
        var before = CheckRefusals ? GameStateDigest.Describe(State) : null;
        try
        {
            // Handlers validate before they mutate, so a refusal leaves the state untouched. Only a rule's refusal is caught: any other
            // exception is a bug and goes on up.
            outcome = _handlers.Handle(command);
        }
        catch (CommandRefusedException e)
        {
            if (before is not null && GameStateDigest.Describe(State) != before)
            {
                throw new InvalidOperationException($"{command} was refused ({e.Message}) after it had changed the game.", e);
            }

            return CommandResult.Rejected(e.Message);
        }

        _log.Add(command);

        // A map event waiting for an answer, a battle or a level-up goes on once nothing is pending.
        MapEvents.Pump();
        Settle();
        Bus.Publish(new CommandExecuted(command));
        return CommandResult.Accepted(outcome);
    }

    /// <summary>
    /// Checks who is out and whether the game is won (ADR-057). A player defeated in its own turn passes it on, which
    /// may start a new day and decide more.
    /// </summary>
    private void Settle()
    {
        for (var i = 0; i < State.Players.Count + 1; i++)
        {
            Flow.Evaluate();
            if (State.IsOver || !State.CurrentPlayer.IsDefeated || State.PendingDecision is not null || State.Battle is not null)
            {
                return;
            }

            Turns.EndTurn();
        }
    }

    /// <summary>
    /// Who may give the command now: a battle command (<see cref="IBattleCommand"/>) whoever's turn it is while a battle is fought, the auto combat
    /// setting at any time, everything else in the player's own turn; a hero command (<see cref="IHeroCommand"/>) only for an own hero on the map.
    /// </summary>
    private string? Authorize(GameCommand command)
    {
        if (!State.HasPlayer(command.Player))
        {
            return $"Unknown player {command.Player}.";
        }

        // In battle each side is commanded by its own player, whoever's turn it is on the map.
        if (command is IBattleCommand)
        {
            return State.Battle is null ? "No battle is being fought." : null;
        }

        // A player setting, changed whenever the player likes.
        if (command is SetAutoCombatCommand)
        {
            return null;
        }

        if (command.Player != State.CurrentPlayer.Id)
        {
            return $"It is not {command.Player}'s turn.";
        }

        if (command is IHeroCommand { Hero: var hero })
        {
            if (State.Heroes.All(h => h.Id != hero))
            {
                return $"Unknown hero {hero}.";
            }

            if (State.GetHero(hero).Owner != command.Player)
            {
                return $"{hero} does not belong to {command.Player}.";
            }
        }

        // The stack's receiver must be on the map too; whether the two may trade is the army's rule.
        if (command is MoveStackCommand { ToHero: var to } && State.Heroes.All(h => h.Id != to))
        {
            return $"Unknown hero {to}.";
        }

        return null;
    }
}
