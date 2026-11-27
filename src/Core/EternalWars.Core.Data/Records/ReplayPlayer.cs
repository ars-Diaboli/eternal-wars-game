using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;

namespace EternalWars.Core.Data.Records;

/// <summary>A turn of a recorded game: the number of commands played before it starts, its date and whose turn it is.</summary>
public sealed record ReplayTurn(int Position, GameDate Date, PlayerId Player);

/// <summary>
/// Plays a game record (a replay or a save, ADR-101) one command at a time for the replay viewer. <see cref="Open"/> replays the whole record
/// once to check it (a refused command or another final digest fails like a load: "made with other content") and to note where every turn
/// and day starts. <see cref="Session"/> is the game being watched; <see cref="Step"/> runs its next command, so the views see the events.
/// <see cref="Seek"/> is how the viewer jumps to a turn or a day: forward, the watched session plays the commands in between unheard by the
/// views (ADR-234); back, a new session is replayed unheard from the start to that point (ADR-058). <see cref="TakeOver"/> (in a battle) and <see cref="PlayOn"/> (on the adventure map) give a separate session at the
/// current point, for "Play from here"; the record and the watched session do not change.
/// </summary>
public sealed class ReplayPlayer
{
    private readonly Func<GameSession> _create;
    private readonly bool[] _inBattle;

    /// <summary>Whether a battle a person commands a side of is being fought after a position: one that may be played on from there.</summary>
    private readonly bool[] _commanded;

    /// <summary>The battles only the computer fought, each within one command (ADR-233): the command's index and the attacker's player.</summary>
    private readonly IReadOnlyDictionary<int, PlayerId> _computerBattles;

    private ReplayPlayer(
        GameRecordDocument record, Func<GameSession> create, IReadOnlyList<ReplayTurn> turns, bool[] inBattle, bool[] commanded,
        IReadOnlyDictionary<int, PlayerId> computerBattles)
    {
        Record = record;
        _create = create;
        Turns = turns;
        _inBattle = inBattle;
        _commanded = commanded;
        _computerBattles = computerBattles;
        Session = Build(0);
    }

    public GameRecordDocument Record { get; }

    /// <summary>The session being watched; replaced by a jump back (<see cref="Seek"/>) and by <see cref="Reload"/>.</summary>
    public GameSession Session { get; private set; }

    /// <summary>How many of the record's commands the watched session has played.</summary>
    public int Position { get; private set; }

    /// <summary>The record's command count.</summary>
    public int Count => Record.Commands.Count;

    public bool IsAtEnd => Position >= Count;

    /// <summary>Every turn of the record in order, the first at position 0.</summary>
    public IReadOnlyList<ReplayTurn> Turns { get; }

    /// <summary>The last day the record reaches.</summary>
    public int LastDay => Turns[^1].Date.AbsoluteDay;

    /// <summary>The turn being watched: the last one that started at or before <see cref="Position"/>.</summary>
    public ReplayTurn CurrentTurn => Turns.Last(t => t.Position <= Position);

    /// <summary>
    /// Checks the record by replaying it to its end and indexes its turns. Throws <see cref="InvalidOperationException"/> when a command is
    /// refused or the final digest differs (other content, or another version of the rules). <paramref name="content"/> is the game's content
    /// tables (the built-in tables when left out).
    /// </summary>
    public static ReplayPlayer Open(GameRecordDocument record, ContentCatalogs? content = null)
    {
        GameSession Create() =>
            new(MapConverter.ToGameState(record.Map, content, record.Options));

        var session = Create();
        var turns = new List<ReplayTurn>();
        var computerBattles = new Dictionary<int, PlayerId>();
        var position = 0;
        PlayerId? computerAttacker = null;
        using (session.Events.Subscribe<TurnStarted>(e => turns.Add(new ReplayTurn(position, e.Date, e.Player))))
        using (session.Events.Subscribe<BattleStarted>(_ =>
               {
                   // A command's first battle, when nobody commands either side: the core fights it to its end within the command.
                   if (computerAttacker is null && session.State.Battle is { Attacker: { IsComputer: true, Owner: { } attacker }, Defender.IsComputer: true })
                   {
                       computerAttacker = attacker;
                   }
               }))
        {
            session.Start();
            Follow(session, null);
            var inBattle = new bool[record.Commands.Count + 1];
            var commanded = new bool[record.Commands.Count + 1];
            inBattle[0] = session.State.Battle is not null;
            commanded[0] = Commanded(session);
            for (var i = 0; i < record.Commands.Count; i++)
            {
                position = i + 1; // a turn that starts during this command starts after it
                computerAttacker = null;
                var result = Execute(session, record.Commands[i]);
                if (!result.IsAccepted)
                {
                    throw new InvalidOperationException($"Replay diverged at command {i} ({record.Commands[i]}): {result.Error}");
                }

                inBattle[i + 1] = session.State.Battle is not null;
                commanded[i + 1] = Commanded(session);
                if (computerAttacker is { } attacker && !inBattle[i + 1])
                {
                    computerBattles[i] = attacker;
                }
            }

            session.Watch(null); // an older digest gives the current player as it was between the commands, the host's turn
            record.CheckFinalDigest(session.State);
            return new ReplayPlayer(record, Create, turns, inBattle, commanded, computerBattles);
        }
    }

    /// <summary>Runs the next command on the watched session; false at the end of the record.</summary>
    public bool Step()
    {
        if (IsAtEnd)
        {
            return false;
        }

        var result = Execute(Session, Record.Commands[Position]);
        if (!result.IsAccepted)
        {
            // Only a command from outside the viewer could make the watched game differ from its record.
            throw new InvalidOperationException($"Replay diverged at command {Position}: {result.Error}");
        }

        Position++;
        return true;
    }

    /// <summary>
    /// Puts the watched game after <paramref name="position"/> commands (clamped to the record). Forward, the watched session runs the commands in
    /// between with its events unheard by <see cref="GameSession.Events"/>' subscribers (<see cref="GameSession.Unheard"/>), so a jump costs only
    /// the commands it skips (ADR-234); the views are then built again on its state. Back, a new session is replayed from the start and the old
    /// one is dropped.
    /// </summary>
    public void Seek(int position)
    {
        position = Math.Clamp(position, 0, Count);
        if (position < Position)
        {
            Session = Build(position);
            Position = position;
            return;
        }

        using (Session.Unheard())
        {
            while (Position < position)
            {
                Step();
            }
        }
    }

    /// <summary>A new watched session replayed from the start to <see cref="Position"/>: the record's game put back when the watched one differs from it.</summary>
    public void Reload() => Session = Build(Position);

    /// <summary>Where the next turn starts (the end of the record after the last turn).</summary>
    public int NextTurnPosition() => Turns.FirstOrDefault(t => t.Position > Position)?.Position ?? Count;

    /// <summary>Where the next day's first turn starts (the end of the record on the last day).</summary>
    public int NextDayPosition() =>
        Turns.FirstOrDefault(t => t.Date.AbsoluteDay > CurrentTurn.Date.AbsoluteDay)?.Position ?? Count;

    /// <summary>Where <paramref name="day"/>'s first turn starts (clamped to the record's first and last days).</summary>
    public int DayPosition(int day)
    {
        day = Math.Clamp(day, 1, LastDay);
        return Turns.First(t => t.Date.AbsoluteDay >= day).Position;
    }

    /// <summary>Whether a battle is being fought after <paramref name="position"/> commands (one played out in a single command never is).</summary>
    public bool IsInBattle(int position) => _inBattle[Math.Clamp(position, 0, Count)];

    /// <summary>Where the battle being watched has ended: the first position after <see cref="Position"/> without a battle; null outside one.</summary>
    public int? BattleEndPosition()
    {
        if (!IsInBattle(Position))
        {
            return null;
        }

        for (var p = Position + 1; p <= Count; p++)
        {
            if (!_inBattle[p])
            {
                return p;
            }
        }

        return Count; // the record stops in the battle (a save made there)
    }

    /// <summary>
    /// Whether "Play from here" is offered in a battle (ADR-101, UI-2 f): one being fought that a person commands a side of; one the last command
    /// ended, in a battle a person commanded (quick combat, <see cref="TakesOverBeforeItsEnd"/>) or a battle only the computer
    /// fought (<see cref="TakesOverFromTheStart"/>, ADR-233).
    /// </summary>
    public bool CanTakeOver => CanTakeOverInProgress || TakesOverBeforeItsEnd || TakesOverFromTheStart;

    /// <summary>
    /// Whether the battle to take over is one a person commanded that the last command ended (quick combat playing it out, or its last blow,
    /// ADR-233): taking it over goes on from just before that command.
    /// </summary>
    public bool TakesOverBeforeItsEnd => Session.State.Battle is null && Position > 0 && _commanded[Position - 1];

    /// <summary>
    /// Whether the battle to take over is one only the computer fought, in the last command played (ADR-233): it has no point between its moves
    /// to go on from, so taking it over plays it again from its start, a person commanding the attacker.
    /// </summary>
    public bool TakesOverFromTheStart => Session.State.Battle is null && _computerBattles.ContainsKey(Position - 1);

    /// <summary>The player a person commands in a battle taken over from its start (<see cref="TakesOverFromTheStart"/>); null otherwise.</summary>
    public PlayerId? TakeOverAttacker => TakesOverFromTheStart ? _computerBattles[Position - 1] : null;

    private bool CanTakeOverInProgress => Commanded(Session);

    private static bool Commanded(GameSession session) =>
        session.State.Battle is { Phase: BattlePhase.Fighting or BattlePhase.Deploying }
        && (session.Battles.Commander(BattleSide.Attacker) ?? session.Battles.Commander(BattleSide.Defender)) is not null;

    /// <summary>
    /// A separate game at the current point, to play the battle on by hand: replayed from the record to <see cref="Position"/>, or to just
    /// before the command that ended it (<see cref="TakesOverBeforeItsEnd"/>). A battle only the computer fought (<see cref="TakesOverFromTheStart"/>) starts again instead: the game is replayed to the command that fought it, a person
    /// plays the attacker's player from then on (<see cref="HandToPersonCommand"/>) and that command runs again, so the battle waits for the
    /// person; a battlefield width the person may now choose is the one the computer fought on. Playing it changes neither the record nor the
    /// watched session. Throws when <see cref="CanTakeOver"/> is false, or when the command no longer starts a battle the person commands.
    /// </summary>
    public GameSession TakeOver()
    {
        if (CanTakeOverInProgress)
        {
            return Build(Position);
        }

        if (TakesOverBeforeItsEnd)
        {
            return Build(Position - 1);
        }

        if (TakeOverAttacker is not { } attacker)
        {
            throw new InvalidOperationException("Play from here works only in a battle a person commands, or one only the computer fought.");
        }

        var position = Position - 1;
        var session = Build(position);
        session.Execute(new HandToPersonCommand(attacker)).ThrowIfRejected();
        session.Execute(Record.Commands[position]).ThrowIfRejected();
        if (session.State.Battle is { Phase: BattlePhase.ChoosingWidth, WidthChooser: { } side } && session.Battles.Commander(side) is { } chooser)
        {
            session.Execute(new ChooseBattleWidthCommand(chooser, BattleState.DefaultWidth)).ThrowIfRejected();
        }

        if (session.State.Battle is not { Phase: BattlePhase.Fighting or BattlePhase.Deploying } || session.Battles.Commander(BattleSide.Attacker) != attacker)
        {
            throw new InvalidOperationException("The computer's battle does not start again for a person here.");
        }

        return session;
    }

    /// <summary>
    /// Whether "Play from here" on the adventure map is offered: outside a battle (one being fought is <see cref="CanTakeOver"/>'s), while the
    /// game is not over and a person still plays in it.
    /// </summary>
    public bool CanPlayOn =>
        Session.State.Battle is null && Session.State.Outcome is null && Session.State.Players.Any(p => p.IsHuman && !p.IsDefeated);

    /// <summary>
    /// "Play from here" on the adventure map: a new game at the current point, replayed from the record to <see cref="Position"/>, so its
    /// command log is the record cut there and it saves like any game (a save played on with Load). The record and the watched session do not
    /// change. Throws when <see cref="CanPlayOn"/> is false.
    /// </summary>
    public GameSession PlayOn()
    {
        if (!CanPlayOn)
        {
            throw new InvalidOperationException("Play from here on the map works only outside a battle, in a game a person still plays.");
        }

        return Build(Position);
    }

    /// <summary>
    /// Whose eyes a game played on from a replay is shown through: the current player when a person, else the next person in turn order who is
    /// still in the game (the computer's turn plays on first); null when no person is.
    /// </summary>
    public static PlayerId? PersonToShow(GameState state)
    {
        var count = state.Players.Count;
        for (var i = 0; i < count; i++)
        {
            var player = state.Players[(state.CurrentPlayerIndex + i) % count];
            if (player.IsHuman && !player.IsDefeated)
            {
                return player.Id;
            }
        }

        return null;
    }

    private GameSession Build(int position)
    {
        var session = _create();
        session.Start();
        Follow(session, null);
        for (var i = 0; i < position; i++)
        {
            var result = Execute(session, Record.Commands[i]);
            if (!result.IsAccepted)
            {
                throw new InvalidOperationException($"Replay diverged at command {i}: {result.Error}");
            }
        }

        return session;
    }

    /// <summary>Runs one of the record's commands on <paramref name="session"/>, then follows its battles (<see cref="Follow"/>).</summary>
    private static CommandResult Execute(GameSession session, GameCommand command)
    {
        var state = session.State;
        var before = state.OpenTurns.ToDictionary(p => p, state.BattleOf);
        var result = session.Execute(command);
        var began = state.OpenTurns.Where(p => state.BattleOf(p) is { } battle && !ReferenceEquals(battle, before.GetValueOrDefault(p)))
            .Cast<PlayerId?>().FirstOrDefault();
        Follow(session, began);
        return result;
    }

    /// <summary>
    /// On a simultaneous day the viewer shows one battle at a time (ADR-366): the one it watches while it lasts, else the one the last command
    /// began (<paramref name="began"/>), else the first person's in turn order that is being fought; the others are fought off screen. Every
    /// session of the record follows them alike, so the battle positions noted when it opened are those it shows.
    /// </summary>
    private static void Follow(GameSession session, PlayerId? began)
    {
        var state = session.State;
        if (!state.IsSimultaneous)
        {
            return;
        }

        if (session.Watched is { } watched && state.OpenTurns.Contains(watched) && state.BattleOf(watched) is not null)
        {
            return;
        }

        var next = began ?? state.OpenTurns.Where(p => state.BattleOf(p) is not null).Cast<PlayerId?>().FirstOrDefault();
        if (next != session.Watched)
        {
            session.Watch(next);
        }
    }
}
