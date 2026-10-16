using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Heroes;
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

    public GameSession(GameState state, IEventBus? bus = null)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
        Bus = bus ?? new EventBus();

        // Core handlers subscribe first so their follow-up events are queued before views react.
        var visibility = new VisibilityService(State, Bus);
        Bus.Subscribe<GameStarted>(visibility);
        Bus.Subscribe<HeroMoved>(visibility);
        Bus.Subscribe<HeroTeleported>(visibility);
        Bus.Subscribe<MineOwnerChanged>(visibility);
        Bus.Subscribe<TownOwnerChanged>(visibility);
        Bus.Subscribe<MapObjectRemoved>(visibility);
        Bus.Subscribe<HeroDefeated>(visibility);
        Bus.Subscribe<HeroHired>(visibility);

        Flow = new GameFlowService(State, Bus);
        Economy = new EconomyService(State, Bus);
        Armies = new ArmyService(State, Bus);
        Progression = new ProgressionService(State, Bus);
        Artifacts = new ArtifactService(State, Bus);
        Battles = new BattleService(State, Bus, Armies, Progression, Artifacts);
        Guards = new GuardService(State, Bus, Battles);
        Towns = new TownService(State, Bus, Economy);
        Interactions = new MapObjectInteractionService(State, Bus, Economy, Battles, Guards, Progression, Artifacts);
        Movement = new HeroMovementService(State, Bus, Interactions);
        Turns = new TurnService(State, Bus, Economy, Movement, Guards, Progression, Towns);
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

    internal GameFlowService Flow { get; }

    internal ArmyService Armies { get; }

    internal ArtifactService Artifacts { get; }

    internal ProgressionService Progression { get; }

    internal MapObjectInteractionService Interactions { get; }

    internal TurnService Turns { get; }

    internal TownService Towns { get; }

    private IEventBus Bus { get; }

    /// <summary>Starts the game: day 1, the first player's turn. Part of every game, so it is not a command.</summary>
    public void Start()
    {
        Flow.Begin();
        Artifacts.RollUnsetPickups();
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
        try
        {
            // Services validate before they mutate, so a refusal leaves the state untouched.
            outcome = Dispatch(command);
        }
        catch (InvalidOperationException e)
        {
            return CommandResult.Rejected(e.Message);
        }

        _log.Add(command);
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

    private static bool IsBattleCommand(GameCommand command) => command is ChooseBattleWidthCommand or BattleMoveCommand
        or BattleAttackCommand or BattleShootCommand or BattleWaitCommand or BattleDefendCommand or BattleRetreatCommand
        or AutoResolveBattleCommand;

    private string? Authorize(GameCommand command)
    {
        if (!State.HasPlayer(command.Player))
        {
            return $"Unknown player {command.Player}.";
        }

        // In battle each side is commanded by its own player, whoever's turn it is on the map.
        if (IsBattleCommand(command))
        {
            return State.Battle is null ? "No battle is being fought." : null;
        }

        if (command.Player != State.CurrentPlayer.Id)
        {
            return $"It is not {command.Player}'s turn.";
        }

        var hero = command switch
        {
            StepHeroCommand step => step.Hero,
            MoveHeroCommand move => move.Hero,
            MoveStackCommand stack => stack.FromHero,
            _ => (Common.HeroId?)null,
        };
        if (hero is { } id && State.Heroes.All(h => h.Id != id))
        {
            return $"Unknown hero {id}.";
        }

        if (hero is { } owned && State.GetHero(owned).Owner != command.Player)
        {
            return $"{owned} does not belong to {command.Player}.";
        }

        if (command is MoveStackCommand { ToHero: var to } && State.Heroes.All(h => h.Id != to))
        {
            return $"Unknown hero {to}.";
        }

        return null;
    }

    private object? Dispatch(GameCommand command)
    {
        switch (command)
        {
            case StepHeroCommand step:
                return Movement.TryStep(step.Hero, step.Direction);
            case MoveHeroCommand move:
                return Movement.MoveTo(move.Hero, move.Target);
            case EndTurnCommand:
                Turns.EndTurn();
                return null;
            case ResolveTreasureChestCommand chest:
                Interactions.ResolveTreasureChest(chest.Reward);
                return null;
            case CompleteExchangeCommand:
                Interactions.CompleteExchange();
                return null;
            case ChooseSkillCommand skill:
                Progression.Choose(skill.Skill);
                return null;
            case MoveStackCommand stack:
                Armies.MoveStack(stack.Player, stack.FromHero, stack.FromSlot, stack.ToHero, stack.ToSlot, stack.Count);
                return null;
            case BuildCommand build:
                Towns.Build(build.Player, build.Town, build.Building);
                return null;
            case BuildDwellingCommand dwelling:
                Towns.BuildDwelling(dwelling.Player, dwelling.Town, dwelling.Line);
                return null;
            case RecruitCommand recruit:
                Towns.Recruit(recruit.Player, recruit.Town, recruit.Creature, recruit.Count, recruit.Hero);
                return null;
            case UpgradeStackCommand upgrade:
                Towns.UpgradeStack(upgrade.Player, upgrade.Town, upgrade.Army, upgrade.Slot);
                return null;
            case MoveTownStackCommand move:
                Towns.MoveStack(move.Player, move.Town, move.From, move.FromSlot, move.To, move.ToSlot, move.Count);
                return null;
            case EquipArtifactCommand equip:
                Artifacts.Equip(equip.Player, equip.Hero, equip.Artifact);
                return null;
            case UnequipArtifactCommand unequip:
                Artifacts.Unequip(unequip.Player, unequip.Hero, unequip.Artifact);
                return null;
            case GiveArtifactCommand give:
                Artifacts.Give(give.Player, give.FromHero, give.Artifact, give.ToHero);
                return null;
            case HireHeroCommand hire:
                Towns.HireHero(hire.Player, hire.Town, hire.Hero);
                return null;
            case BuyWarMachineCommand buy:
                Towns.BuyWarMachine(buy.Player, buy.Town, buy.Slot);
                return null;
            case RespondToGuardCommand guard:
                Guards.Respond(guard.Accept);
                return null;
            case ChooseBattleWidthCommand width:
                Battles.ChooseWidth(width.Player, width.Width);
                return null;
            case BattleMoveCommand move:
                Battles.Act(move.Player, new MoveAction(move.To));
                return null;
            case BattleAttackCommand attack:
                Battles.Act(attack.Player, new AttackAction(attack.Target, attack.From));
                return null;
            case BattleShootCommand shoot:
                Battles.Act(shoot.Player, new ShootAction(shoot.Target));
                return null;
            case BattleWaitCommand wait:
                Battles.Act(wait.Player, new WaitAction());
                return null;
            case BattleDefendCommand defend:
                Battles.Act(defend.Player, new DefendAction());
                return null;
            case BattleRetreatCommand retreat:
                Battles.Retreat(retreat.Player);
                return null;
            case AutoResolveBattleCommand auto:
                Battles.AutoResolve(auto.Player);
                return null;
            default:
                throw new ArgumentOutOfRangeException(nameof(command), command, "Unknown command type.");
        }
    }
}
