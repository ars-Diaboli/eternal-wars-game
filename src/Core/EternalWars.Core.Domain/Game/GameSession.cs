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
    private long _published;
    private readonly EventBus _outside = new(); // Events: everyone outside the rules
    private int _unheard;

    /// <summary>
    /// Checks that a refused command left the game as it was (its digest text unchanged) and fails loudly otherwise. The tests turn it on for
    /// every game they play; it costs a digest a command.
    /// </summary>
    internal static bool CheckRefusals { get; set; }

    public GameSession(GameState state, IEventBus? bus = null)
        : this(state, bus, followsHost: false)
    {
    }

    private GameSession(GameState state, IEventBus? bus, bool followsHost)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
        Bus = bus ?? new EventBus();
        FollowsHost = followsHost;
        ViewSink = followsHost ? new ViewSink(State) : null;
        Bus.Subscribe<Core.Events.IEvent>(_ => _published++);

        // Core handlers subscribe first so their follow-up events are queued before views react. A LAN client's session runs no rules, so
        // nothing of the rules listens to the events its host sends (nor reveals anything: its fog is the host's).
        var visibility = new VisibilityService(State, Bus);
        if (!followsHost)
        {
            visibility.SubscribeTo(Bus);
        }

        Flow = new GameFlowService(State, Bus);
        Economy = new EconomyService(State, Bus);
        Armies = new ArmyService(State, Bus);
        Progression = new ProgressionService(State, Bus);
        Artifacts = new ArtifactService(State, Bus);
        var objects = new ObjectInteractionContext(State, Bus, Economy, visibility);
        var townVisits = new TownVisitService(State, Bus, Progression, Artifacts);
        var boxes = new PandorasBoxService(objects, Progression, Artifacts);
        var recruits = new HeroRecruitment(State, Progression);
        Battles = new BattleService(State, Bus, Armies, Progression, Artifacts, Economy, townVisits, boxes);
        Guards = new GuardService(State, Bus, Battles);
        Towns = new TownService(State, Bus, Economy, townVisits, recruits);
        Caravans = new CaravanService(State, Bus, Economy);
        Grail = new GrailService(objects, Artifacts);
        Interactions = new MapObjectInteractionService(objects, Battles, Guards, Progression, Artifacts, Grail, townVisits, recruits);
        ObjectDecisions = new ObjectDecisionService(objects, Battles, Progression, Artifacts, townVisits, boxes);
        ObjectShops = new ObjectShopService(objects, Progression, Artifacts, recruits);
        ObjectLifecycle = new ObjectLifecycleService(objects, Artifacts);
        MapEvents = new MapEventService(State, Bus, visibility, Economy, Progression, Artifacts, Battles, townVisits, recruits);
        if (!followsHost)
        {
            MapEvents.SubscribeTo(Bus);
        }

        Movement = new HeroMovementService(State, Bus, Interactions, MapEvents, () => ChangeStamp);
        AdventureMagic = new Magic.AdventureMagicService(State, Bus, visibility, townVisits);
        if (!followsHost)
        {
            AdventureMagic.SubscribeTo(Bus);
        }

        Arena = new ArenaService(State, Bus, Battles);

        // The list's order is the rules' order at every step of the calendar (ITurnPhaseParticipant): it fixes the events and the random draws.
        Days = new SimultaneousDayCoordinator(State, Bus);
        if (!followsHost)
        {
            Days.SubscribeTo(Bus);
        }

        Turns = new TurnService(State, Bus, Arena, Days,
            [MapEvents, Progression, Movement, new ManaRestoration(State, Bus), Towns, Caravans, Economy, Guards, ObjectLifecycle]);

        AdventureCommandHandlers.Register(_handlers, Movement, Turns, Guards, Progression, Armies, AdventureMagic, Grail);
        ObjectCommandHandlers.Register(_handlers, ObjectDecisions, ObjectShops, MapEvents);
        TownCommandHandlers.Register(_handlers, Towns, Caravans, Economy);
        ArtifactCommandHandlers.Register(_handlers, Artifacts, Armies);
        BattleCommandHandlers.Register(_handlers, Battles);
        _handlers.Add<HandToComputerCommand>(c =>
        {
            Flow.HandToComputer(c.Player);
            Battles.HandToComputer(c.Player);
            Turns.LeaveOpenTurns(c.Player); // on a simultaneous day the computer plays its turn after the people (ADR-197)
        });
        _handlers.Add<HandToPersonCommand>(c => Flow.HandToPerson(c.Player));
        _handlers.Add<PlayTurnByTurnCommand>(c => Turns.PlayTurnByTurn(c.Reason));

        // Everyone outside the rules (views, tools, a LAN host) hears the rules' events after the rules have, on a bus of their own, so they can be
        // left out while the game is moved on unheard (Unheard, ADR-234). Last, so each event reaches them after every rule, as before.
        Bus.Subscribe<Core.Events.IEvent>(e =>
        {
            if (_unheard == 0)
            {
                _outside.Publish(e);
            }
        });
    }

    /// <summary>
    /// Where today's commands begin in <see cref="CommandLog"/> on a simultaneous day (MP-2, ADR-197): the commands before it rebuild the game as
    /// the day opened, which a LAN host plays again turn by turn after a conflict. 0 before the first simultaneous day.
    /// </summary>
    public int SimultaneousDayStart => Days.DayStart;

    /// <summary>The simultaneous days: open turns, whose turn a command runs in, the conflict watch (ADR-217).</summary>
    private SimultaneousDayCoordinator Days { get; }

    public GameState State { get; }

    /// <summary>
    /// A LAN client's session (ADR-193, <see cref="FollowingHost"/>): its state is the player's view, which the host keeps up to date and whose
    /// events it publishes on <paramref name="bus"/>; the rules never run here, so every command must go to the host (<see cref="Route"/>).
    /// </summary>
    public static GameSession FollowingHost(GameState state, IEventBus bus) => new(state, bus, followsHost: true);

    /// <summary>
    /// Where a LAN client puts what the host sent (ADR-218): only a session that follows the host has one (<see cref="FollowingHost"/>); null
    /// otherwise.
    /// </summary>
    public IViewSink? ViewSink { get; }

    /// <summary>Whether this is a LAN client's session (<see cref="FollowingHost"/>): it runs no rules.</summary>
    public bool FollowsHost { get; }

    /// <summary>The game's events, for everyone outside the rules: views, tools, a LAN host. The rules hear them first.</summary>
    public IEventSubscriber Events => _outside;

    /// <summary>
    /// Until the returned scope is disposed, the game's events reach only the rules, not <see cref="Events"/>' subscribers: the replay viewer moves
    /// its game on by many commands at once without its views reacting to each, then builds them again on the state (ADR-234). Nests.
    /// </summary>
    public IDisposable Unheard()
    {
        _unheard++;
        return new UnheardScope(this);
    }

    /// <summary>
    /// Where a command goes once it is authorized, before it runs (ADR-192): null runs every command here. A LAN host refuses the commands of a
    /// player who plays on another computer; a LAN client sends its own player's commands to the host and runs only what the host sends back.
    /// </summary>
    public ICommandRoute? Route { get; set; }

    /// <summary>Accepted commands in order. Together with the map and <see cref="GameOptions.Seed"/> this replays the game.</summary>
    public IReadOnlyList<GameCommand> CommandLog => _log;

    /// <summary>
    /// A number that grows whenever the game may have changed: a command accepted, an event published or a hero moved (any game's, so it may
    /// also grow when this one did not). While it stays the same, what was worked out from the state still holds (route caches).
    /// </summary>
    internal long ChangeStamp => _log.Count + _published + Hero.Moves;

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

    private sealed class UnheardScope(GameSession session) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                session._unheard--;
            }
        }
    }

    /// <summary>Starts the game: day 1, the first player's turn. Part of every game, so it is not a command.</summary>
    public void Start()
    {
        new PlaceholderService(State, Bus).Resolve();
        Grail.Bury();
        Flow.RemoveSurplusHeroes();
        Flow.Begin();
        Artifacts.RollUnsetPickups();
        ObjectLifecycle.PreparePrisons();
        ObjectLifecycle.PrepareObjects();
        Towns.DrawGuilds();
        Turns.StartGame();
        Settle();
        Days.Started(_log.Count);
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

        // A simultaneous day's conflict stops the day until the host plays it again turn by turn (ADR-197).
        if (Days.Stopped(command) is { } stopped)
        {
            return CommandResult.Rejected(stopped);
        }

        if (Authorize(command) is { } refusal)
        {
            return CommandResult.Rejected(refusal);
        }

        if (Route?.Route(command) is { } routed)
        {
            return routed;
        }

        if (FollowsHost)
        {
            return CommandResult.Rejected("This computer follows the host's game: its commands go to the host.");
        }

        // On a simultaneous day a command runs as its player's turn (ADR-197); between commands the current player is the host's open turn.
        Days.TakeTurnOf(command);

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

            Days.Refused();
            return CommandResult.Rejected(e.Message);
        }

        _log.Add(command);

        // A map event waiting for an answer, a battle or a level-up goes on once nothing is pending.
        MapEvents.Pump();
        Settle();
        Days.Executed(_log.Count);
        Bus.Publish(new CommandExecuted(command));
        return CommandResult.Accepted(outcome);
    }

    /// <summary>
    /// Checks who is out and whether the game is won (ADR-057). A player defeated in its own turn passes it on, which
    /// may start a new day and decide more.
    /// </summary>
    private void Settle()
    {
        foreach (var gone in State.OpenTurns.Where(p => State.GetPlayer(p).IsDefeated).ToList())
        {
            Turns.LeaveOpenTurns(gone); // out of the game on a simultaneous day
        }

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
    /// setting and the computer taking a player over at any time, everything else in the player's own turn; a hero command (<see cref="IHeroCommand"/>) only for an own hero on the map.
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

        // A player setting, changed whenever the player likes; the computer taking a player over and a simultaneous day played again turn by turn,
        // whenever the LAN host decides (ADR-195, ADR-197).
        if (command is SetAutoCombatCommand or HandToComputerCommand or PlayTurnByTurnCommand)
        {
            return null;
        }

        // Its own turn; on a simultaneous day one battle or question at a time: the others wait for it (ADR-197).
        if (Days.Refusal(command.Player) is { } notNow)
        {
            return notNow;
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
