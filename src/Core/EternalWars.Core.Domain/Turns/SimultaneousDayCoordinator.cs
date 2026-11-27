using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Turns;

/// <summary>
/// The simultaneous days (MP-2, ADR-197, ADR-217; <see cref="GameOptions.SimultaneousFirstWeek"/>: the first week, or a tournament's every day before
/// its battle, ADR-366) in one place: which days are played at once,
/// opening and ending each person's turn, whose turn a command runs in, the conflict watch (<see cref="SimultaneousDayWatch"/>) and where the day's
/// commands begin (<see cref="DayStart"/>). Each open turn has a slot of its own for its battle, question and map events (<see cref="TurnSlot"/>,
/// ADR-366), so the people fight and answer at the same time and nobody waits for another. It alone changes the game's record of them
/// (<see cref="GameState.OpenTurns"/>, the turns ended today, the turns' slots, <see cref="GameState.SimultaneousEnded"/>,
/// <see cref="GameState.SimultaneousConflict"/>); <see cref="TurnService"/> begins and passes the turns, and <see cref="GameSession"/> tells it when
/// a command is done.
/// </summary>
internal sealed class SimultaneousDayCoordinator
{
    private readonly GameState _state;
    private readonly IEventPublisher _events;
    private readonly SimultaneousDayWatch _watch;
    private bool _opened;
    private PlayerId? _watched;

    internal SimultaneousDayCoordinator(GameState state, IEventPublisher events)
    {
        _state = state;
        _events = events;
        _watch = new SimultaneousDayWatch(state, events);
    }

    /// <summary>
    /// Where today's commands begin in the session's command log (MP-2, ADR-197): the commands before it rebuild the game as the day opened, which a
    /// LAN host plays again turn by turn after a conflict. 0 before the first simultaneous day.
    /// </summary>
    internal int DayStart { get; private set; }

    /// <summary>Listens for the visits and battles the conflict watch checks; a LAN client's session, which runs no rules, does not.</summary>
    internal void SubscribeTo(IEventSubscriber bus)
    {
        bus.Subscribe<MapObjectVisited>(e => _watch.Visited(e.Hero, e.Object));
        bus.Subscribe<BattleStarted>(_ => _watch.BattleStarted());
    }

    // The calendar (TurnService) ------------------------------------------------------------------------------

    /// <summary>A new day: nobody has ended today's turn yet.</summary>
    internal void NewDay() => _state.DoneToday.Clear();

    /// <summary>Whether <paramref name="player"/> ended its turn today on a simultaneous day; it does not play again until the next day.</summary>
    internal bool HasEnded(PlayerId player) => _state.DoneToday.Contains(player);

    /// <summary>
    /// Opens today's turns at once when today is simultaneous: every person still in the game, in order, each with a slot of its own, each turn
    /// begun by <paramref name="beginTurn"/> as the current player. False on a day played turn by turn.
    /// </summary>
    internal bool TryOpen(Action beginTurn)
    {
        if (!IsSimultaneousDay())
        {
            return false;
        }

        var people = _state.Players.Where(p => p.IsHuman && !p.IsDefeated).Select(p => p.Id).ToList();
        _state.OpenTurnList.AddRange(people);
        _state.OpenSlots(people);
        foreach (var person in people)
        {
            _state.CurrentPlayerIndex = _state.IndexOf(person);
            beginTurn();
        }

        PointAtOpenTurn();
        _opened = true;
        _events.Publish(new SimultaneousDayStarted(_state.Date, people));
        return true;
    }

    /// <summary>
    /// <paramref name="player"/> ended its turn of a simultaneous day: the others go on. Called before <see cref="TurnEnded"/> tells of it
    /// (ADR-214).
    /// </summary>
    internal void EndTurn(PlayerId player)
    {
        _state.OpenTurnList.Remove(player);
        _state.DoneToday.Add(player);
        _state.CloseIdleSlots(); // before the next day opens, whose start belongs in the shared slot
        PointAtOpenTurn();
    }

    /// <summary>
    /// <paramref name="player"/>'s turn leaves today's open turns (the computer took it over, ADR-195, or it is out). True when that closed the
    /// last open turn, so the computer players now play in order. A player out of the game loses its slot; one the computer took over keeps what
    /// is open in it for the turn the computer plays later today.
    /// </summary>
    internal bool Leave(PlayerId player)
    {
        if (!_state.OpenTurnList.Remove(player))
        {
            return false;
        }

        if (_state.GetPlayer(player).IsDefeated)
        {
            _state.DropSlot(player);
        }

        _state.CloseIdleSlots();
        PointAtOpenTurn();
        return !_state.IsSimultaneous;
    }

    /// <summary>
    /// Today is played turn by turn from its start, and every day after it (<see cref="PlayTurnByTurnCommand"/>): the conflict is settled, and who
    /// has ended today's turn keeps it ended. A battle or question still open waits in its person's slot for that person's turn; of two battles
    /// against one foe (the day's conflict) the later person's in turn order goes, unfought. The LAN host plays the day again from its start
    /// instead, so only what those turns began with is open then.
    /// </summary>
    internal void EndDays(string reason)
    {
        if (!_state.IsSimultaneous)
        {
            throw new CommandRefusedException("Today is not played at once.");
        }

        _state.SimultaneousEnded = true;
        _state.SimultaneousConflict = null;
        _state.OpenTurnList.Clear();
        var fought = new List<BattleState>();
        foreach (var (_, slot) in _state.Slots.ToList())
        {
            if (slot.Battle is not { } battle)
            {
                continue;
            }

            if (fought.Any(f => SimultaneousDayWatch.Share(f, battle)))
            {
                Drop(slot);
            }
            else
            {
                fought.Add(battle);
            }
        }

        _events.Publish(new SimultaneousDaysEnded(_state.Date, reason));
    }

    /// <summary>A battle that may not be fought goes from its slot unfought: its question too, and a map event waiting for it stops.</summary>
    private void Drop(TurnSlot slot)
    {
        slot.Battle = null;
        slot.Events.Fight = null;
        if (slot.Events.Current is { Waiting: MapEvents.MapEventWaiting.Battle })
        {
            slot.Events.Current = null;
        }

        if (slot.Decision is BattleDecision decision)
        {
            slot.Decision = null;
            slot.DecisionPlayer = null;
            _events.Publish(new DecisionResolved(decision));
        }
    }

    // The commands (GameSession) ------------------------------------------------------------------------------

    /// <summary>Why every command but <see cref="PlayTurnByTurnCommand"/> is refused: the day's conflict, until the host plays it again turn by turn.</summary>
    internal string? Stopped(GameCommand command) =>
        _state.SimultaneousConflict is { } conflict && command is not PlayTurnByTurnCommand ? $"The day is played again turn by turn: {conflict}" : null;

    /// <summary>
    /// Why <paramref name="player"/> may not give a map command now: not its turn (or ended today); null when it may. On a simultaneous day nobody
    /// waits for another's battle or question: each turn has its own (ADR-366).
    /// </summary>
    internal string? Refusal(PlayerId player) =>
        _state.IsTurnOf(player) ? null
        : HasEnded(player) ? $"{_state.GetPlayer(player).Name} has ended the turn: the others still play today."
        : $"It is not {player}'s turn.";

    /// <summary>Why a battle command of <paramref name="player"/> is refused before it runs: no battle is being fought (on a simultaneous day, none of its own).</summary>
    internal string? BattleRefusal(PlayerId player) =>
        (_state.IsSimultaneous ? _state.BattleOf(player) : _state.Battle) is null ? "No battle is being fought." : null;

    /// <summary>
    /// On a simultaneous day a command runs in its own player's turn, with that turn's battle and question: a battle command in the turn whose
    /// battle its player fights, the computer taking a player over in that player's; the settings any player gives at any time and the day played
    /// again turn by turn in none.
    /// </summary>
    internal void TakeTurnOf(GameCommand command)
    {
        if (!_state.IsSimultaneous)
        {
            return;
        }

        var turn = command switch
        {
            IBattleCommand => _state.TurnFighting(command.Player),
            SetAutoCombatCommand or PlayTurnByTurnCommand => null,
            _ => command.Player,
        };
        if (turn is { } player)
        {
            _state.CurrentPlayerIndex = _state.IndexOf(player);
        }
    }

    /// <summary>
    /// The open turn whose battle and question the game shows between commands (<see cref="GameSession.Watch"/>): the replay viewer follows the
    /// battle it shows; null is the host's colour.
    /// </summary>
    internal PlayerId? Watched
    {
        get => _watched;
        set
        {
            _watched = value;
            PointAtOpenTurn();
        }
    }

    /// <summary>
    /// Whether the game is in the turn it shows between commands: always on a day played turn by turn; on a simultaneous day, while a command of
    /// that turn runs (another person's battle and question are that person's to see).
    /// </summary>
    internal bool InShownTurn => !_state.IsSimultaneous || _state.CurrentPlayer.Id == Anchor();

    /// <summary>The game started: when its first day opened at once, its commands begin at <paramref name="logged"/>.</summary>
    internal void Started(int logged) => NoteOpenedDay(logged);

    /// <summary>A command was refused: the current player is again the host's open turn.</summary>
    internal void Refused() => PointAtOpenTurn();

    /// <summary>
    /// A command was done (<paramref name="logged"/> commands in the log): the current player is again the host's open turn, a day it opened
    /// begins its commands here, the slots of turns no longer open with nothing left in them go, and the watch looks for a conflict.
    /// </summary>
    internal void Executed(int logged)
    {
        PointAtOpenTurn();
        NoteOpenedDay(logged);
        _state.CloseIdleSlots();
        _watch.Check();
    }

    /// <summary>
    /// Between commands of a simultaneous day the current player is the watched open turn, else the host's colour while its turn is open, else the
    /// first open turn (<see cref="GameOptions.HostPlayer"/>).
    /// </summary>
    private void PointAtOpenTurn()
    {
        if (_state.IsSimultaneous)
        {
            _state.CurrentPlayerIndex = _state.IndexOf(Anchor());
        }
    }

    private PlayerId Anchor() => _watched is { } watched && _state.OpenTurns.Contains(watched) ? watched : _state.HostTurn;

    /// <summary>A simultaneous day just opened: where its commands begin, and what is no conflict.</summary>
    private void NoteOpenedDay(int logged)
    {
        if (!_opened)
        {
            return;
        }

        _opened = false;
        DayStart = logged;
        _watch.DayOpened();
    }

    /// <summary>
    /// Whether today is played at once: the option is on, no conflict has ended it, it is the first week (an arena map's preparation day, ADR-199)
    /// or any day of a tournament before its battle's (ADR-366; that day opens no turns), nothing the day's start opened waits in the shared slot,
    /// and at least two people still play, each with a start zone (MP-2 a).
    /// </summary>
    private bool IsSimultaneousDay()
    {
        var day = _state.Date.AbsoluteDay;
        if (!_state.Options.SimultaneousFirstWeek || _state.SimultaneousEnded
            || (day > GameDate.DaysPerWeek && !_state.Rules.Tournament)
            || (DecidingBattles.Day(_state) is { } battle && day >= battle)
            || !_state.SharedSlot.IsIdle)
        {
            return false;
        }

        var people = _state.Players.Where(p => p.IsHuman && !p.IsDefeated).Select(p => p.Id).ToList();
        var zones = _state.Map.StartZoneOwners;
        return people.Count >= 2 && people.All(zones.Contains);
    }
}
