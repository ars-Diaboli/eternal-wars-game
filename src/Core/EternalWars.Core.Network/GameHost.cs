using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Turns;
using EternalWars.Core.Events;

namespace EternalWars.Core.Network;

/// <summary>
/// The computer that runs a LAN game (ADR-192): it holds the one real <see cref="GameSession"/>, takes clients into the human colours that play
/// elsewhere (<see cref="RemoteSeats"/>) and runs their commands. Each client gets only its player's view of the game (ADR-193,
/// <see cref="PlayerViewFeed"/>): what changed in it and the events it may see, once a <see cref="Poll"/>. The host's own player and the computer
/// players play here as in a game on one computer; a remote colour's command given here is refused. The players write to each other in the chat
/// (<see cref="Say"/>, <see cref="ChatReceived"/>). A game set up in the lobby (<see cref="LobbyHost"/>) starts with the clients already seated.
/// <para>
/// Live play between computers (ADR-195): a battle between players on two computers is fought by hand, each side from its own computer (no quick
/// combat). While a remote colour has nobody to play it and holds the game up (its player left, or it is its turn or its battle and nobody has
/// joined it), the game is paused for everyone (<see cref="Waiting"/>): every command is refused until a player joins that colour or the host
/// lets the computer play it (<see cref="HandToComputer"/>).
/// </para>
/// <para>
/// Saves (ADR-196): a client saves the game as the host's whole record, which the host sends when asked (<see cref="RecordOf"/>); the game a
/// host runs may be a loaded save.
/// </para>
/// <para>
/// The simultaneous first week (MP-2, ADR-197; a tournament's every day before its battle, ADR-366): the people play each day at once, each from its own computer. On a conflict the host goes back to
/// the day's start (its record up to <see cref="GameSession.SimultaneousDayStart"/>) and plays it again turn by turn: a new session
/// (<see cref="SessionReplaced"/>), whose view every client gets again.
/// </para>
/// </summary>
/// <remarks>
/// Everything happens on the caller's thread in <see cref="Poll"/> (the game app calls it once a frame). The host runs the session, routes its
/// commands and sends the views; who plays where (<see cref="SeatManager"/>), the pause (<see cref="PausePolicy"/>), the chat
/// (<see cref="ChatRelay"/>) and the day played again (<see cref="SimultaneousDayRewinder"/>) are its parts.
/// </remarks>
public sealed class GameHost : ICommandRoute, IDisposable
{
    private readonly SeatManager _seats;
    private readonly PausePolicy _pause;
    private readonly ChatRelay _chat;
    private GameSession _session;
    private IDisposable _subscription;

    /// <summary>Set while a client's command runs: the route lets that one through.</summary>
    private bool _runningRemote;

    /// <summary>The turn clock last sent to the clients.</summary>
    private TurnClock? _clockSent;

    /// <summary>Whether the game published anything since the views were last sent: every change to the state publishes an event.</summary>
    private bool _viewsStale;

    /// <summary>Who fights the current battle, or fought the last one: they see its events.</summary>
    private readonly Dictionary<int, IReadOnlyCollection<PlayerId>> _battleSides = []; // the sides of each turn's last battle, by its person (-1: shared)

    /// <summary>When each colour was last sent the game's record (ADR-203): the colour's, not the link's, so a new link does not start it over.</summary>
    private readonly Dictionary<PlayerId, DateTimeOffset> _recordSent = [];

    /// <param name="session">The game, started or about to start.</param>
    /// <param name="remoteSeats">The human colours played on other computers.</param>
    /// <param name="hostName">The host's player, in the chat.</param>
    public GameHost(GameSession session, IEnumerable<PlayerId> remoteSeats, string hostName = "Host")
    {
        _session = session;
        HostName = hostName;
        _seats = new SeatManager(session.State, remoteSeats);
        _pause = new PausePolicy(_seats);
        _chat = new ChatRelay(_seats);
        session.Route = this;
        _subscription = session.Events.Subscribe<IEvent>(Capture);
    }

    /// <summary>A client took a colour (the colour, the player's name).</summary>
    public event Action<PlayerId, string>? Joined;

    /// <summary>A client's link went down (the colour, the player's name, why); the colour is free to join again.</summary>
    public event Action<PlayerId, string, string>? Left;

    /// <summary>
    /// A simultaneous day's conflict took the day back (ADR-197): the game goes on in this new session, the day played again turn by turn; the
    /// host's screen shows it in place of the old one.
    /// </summary>
    public event Action<GameSession>? SessionReplaced;

    /// <summary>The game was paused or goes on again (<see cref="Waiting"/>).</summary>
    public event Action? PauseChanged
    {
        add => _pause.Changed += value;
        remove => _pause.Changed -= value;
    }

    /// <summary>A line of the chat (who wrote it, empty for the game's own lines; the text), the host's own included.</summary>
    public event Action<string, string>? ChatReceived
    {
        add => _chat.Received += value;
        remove => _chat.Received -= value;
    }

    /// <summary>The host's player, in the chat.</summary>
    public string HostName { get; }

    /// <summary>The game the host runs.</summary>
    public GameSession Session => _session;

    /// <summary>
    /// The game's whole record as the host would save it (ADR-196), given the save's name (null for a replay); a client asks for it to save the
    /// game on its computer. Null: the host keeps none, and a client's request is refused.
    /// </summary>
    public Func<string?, GameRecordDocument>? RecordOf { get; set; }

    /// <summary>The human colours played on other computers, taken or not.</summary>
    public IReadOnlyList<PlayerId> RemoteSeats => _seats.RemoteSeats;

    /// <summary>Whether <paramref name="player"/> plays on another computer.</summary>
    public bool IsRemote(PlayerId player) => _seats.IsRemote(player);

    /// <summary>The name of the client playing <paramref name="seat"/>; null while nobody has joined it.</summary>
    public string? PlayerAt(PlayerId seat) => _seats.PlayerAt(seat);

    /// <summary>
    /// The remote colours that hold the game up, with why: those whose player left, and one nobody has joined whose turn it is or that fights the
    /// battle. The game is paused while there is one.
    /// </summary>
    public IReadOnlyList<(PlayerId Seat, string Reason)> Waiting => _pause.Waiting(_session.State);

    /// <summary>Whether the game is paused for a colour without a player (<see cref="Waiting"/>).</summary>
    public bool IsPaused => Waiting.Count > 0;

    /// <summary>Why the game is paused, in words, as the players see it; null while it goes on.</summary>
    public string? PauseReason => _pause.Reason(_session.State);

    /// <summary>
    /// The computer plays <paramref name="seat"/>, a remote colour nobody plays now, from here on (ADR-195): it is no longer open to join, and the
    /// game goes on if it was waiting only for it.
    /// </summary>
    public CommandResult HandToComputer(PlayerId seat)
    {
        if (!IsRemote(seat))
        {
            return CommandResult.Rejected($"{_session.State.GetPlayer(seat).Name} is not played on another computer.");
        }

        if (PlayerAt(seat) is { } name)
        {
            return CommandResult.Rejected($"{name} plays {_session.State.GetPlayer(seat).Name}.");
        }

        var result = _pause.HandOver(() => _session.Execute(new HandToComputerCommand(seat)));
        if (result.IsAccepted)
        {
            _seats.HandToComputer(seat);
            _chat.Announce($"The computer plays {_session.State.GetPlayer(seat).Name}.");
            _pause.Send(_session.State);
        }

        return result;
    }

    /// <summary>
    /// Runs a command this computer gives for any colour, a remote one too: the turn timer's defaults and ends of turns (ADR-198), as the one
    /// clock of the game. Refused like any other while the game is paused.
    /// </summary>
    public CommandResult ExecuteAsHost(GameCommand command)
    {
        _runningRemote = true;
        try
        {
            return _session.Execute(command);
        }
        finally
        {
            _runningRemote = false;
        }
    }

    /// <summary>Sends the host's turn clock to the clients when it has changed (ADR-198); a client that joins gets it with the game.</summary>
    public void ShowClock(TurnClock clock)
    {
        if (clock == _clockSent)
        {
            return;
        }

        _clockSent = clock;
        foreach (var peer in _seats.Seated)
        {
            peer.Connection.Send(new ClockUpdate(clock));
        }
    }

    /// <summary>The clock for <see cref="NetLimits"/> (tests set their own).</summary>
    internal TimeProvider Time { get; set; } = TimeProvider.System;

    /// <summary>
    /// Takes a new link; it gets a seat once its <see cref="JoinRequest"/> arrives, which must come within <see cref="NetLimits.JoinTimeout"/>.
    /// Turned away while <see cref="NetLimits.MaxUnjoined"/> other links wait to join.
    /// </summary>
    public void Accept(INetConnection connection) => _seats.Accept(connection, Time.GetUtcNow());

    /// <summary>
    /// Seats a client that took <paramref name="seat"/> in the lobby; it gets the game at the first <see cref="Poll"/> once the game has started.
    /// </summary>
    internal void Admit(INetConnection connection, PlayerId seat, string name) => _seats.Admit(connection, seat, name);

    /// <summary>The host's player writes in the chat.</summary>
    public void Say(string text) => _chat.Say(HostName, text);

    /// <summary>Handles what the clients have sent (joins, commands) and drops the links that went down.</summary>
    public void Poll()
    {
        PlayDayAgainIfConflicted();
        if (_session.State.IsStarted)
        {
            foreach (var peer in _seats.Admitted.ToList())
            {
                Seat(peer, peer.Seat!.Value, peer.Name);
            }
        }

        var now = Time.GetUtcNow();
        foreach (var peer in _seats.Peers.ToList())
        {
            for (var taken = 0; taken < NetLimits.MaxMessagesPerPoll && !peer.Connection.IsClosed && peer.Connection.TryReceive(out var message); taken++)
            {
                Handle(peer, message);
            }

            if (peer.Seat is null && !peer.Connection.IsClosed)
            {
                NetLimits.CloseIfLate(peer.Connection, peer.Arrived, now);
            }

            if (peer.Connection.IsClosed && _seats.Forget(peer, _session.State) is { } left)
            {
                _chat.Announce(left.Line);
                Left?.Invoke(left.Seat, left.Name, left.Why);
            }
        }

        PlayDayAgainIfConflicted();
        SendViews();
        _pause.Send(_session.State);
    }

    /// <summary>
    /// A simultaneous day's conflict (ADR-197): the game goes back to the day's start and plays it again turn by turn, from the host's record
    /// (<see cref="SimultaneousDayRewinder"/>). The clients first get what happened up to the conflict, then the new game's view; everyone is told
    /// why.
    /// </summary>
    private void PlayDayAgainIfConflicted()
    {
        if (SimultaneousDayRewinder.PlayDayAgain(_session, RecordOf) is not { } day)
        {
            return;
        }

        SendViews();
        Replace(day.Again);
        _chat.Announce(day.Announcement);
        SessionReplaced?.Invoke(day.Again);
    }

    /// <summary>
    /// "Play again" on an arena's or a tournament's result screen (ADR-159, ADR-199, ADR-366): the same colours go on in <paramref name="next"/> (a new game of the same
    /// map, started), each client getting its view as on joining. Refused while the game is not over.
    /// </summary>
    public CommandResult PlayAgain(GameSession next)
    {
        ArgumentNullException.ThrowIfNull(next);
        if (!_session.State.IsOver)
        {
            return CommandResult.Rejected("The game is not over.");
        }

        if (!next.State.IsStarted || _seats.RemoteSeats.Any(s => !next.State.HasPlayer(s) || !next.State.GetPlayer(s).IsHuman))
        {
            return CommandResult.Rejected("The new game must be started, with the same colours played on other computers.");
        }

        SendViews();
        Replace(next);
        _chat.Announce($"{HostName} starts the game again.");
        return CommandResult.Accepted();
    }

    /// <summary>The game goes on in <paramref name="next"/>: its commands are routed here and every seated client gets its view again.</summary>
    private void Replace(GameSession next)
    {
        _subscription.Dispose();
        _session.Route = null;
        _session = next;
        next.Route = this;
        _subscription = next.Events.Subscribe<IEvent>(Capture);
        _battleSides.Clear();
        _clockSent = null;
        foreach (var peer in _seats.Seated)
        {
            SendGame(peer, peer.Seat!.Value);
        }
    }

    /// <summary>
    /// Refuses a remote colour's command given on this computer (a client's own command, run by <see cref="Poll"/>, goes through), every command
    /// while the game is paused, and quick combat in a battle between players on two computers.
    /// </summary>
    public CommandResult? Route(GameCommand command)
    {
        if (_pause.IsHandingOver)
        {
            return null;
        }

        if (_pause.Refusal(_session.State) is { } paused)
        {
            return paused;
        }

        if (command is AutoResolveBattleCommand && IsFoughtBetweenComputers(command.Player))
        {
            return CommandResult.Rejected("A battle between players on two computers is fought by hand, each side by its own player.");
        }

        return !_runningRemote && IsRemote(command.Player)
            ? CommandResult.Rejected($"{_session.State.GetPlayer(command.Player).Name} plays on another computer.")
            : null;
    }

    /// <summary>Whether people on two computers command the two sides of the battle <paramref name="player"/> fights.</summary>
    private bool IsFoughtBetweenComputers(PlayerId player) =>
        _session.State.BattleOf(player) is { Attacker: { IsComputer: false, Owner: { } attacker }, Defender: { IsComputer: false, Owner: { } defender } }
        && (IsRemote(attacker) || IsRemote(defender));

    public void Dispose()
    {
        _subscription.Dispose();
        if (ReferenceEquals(_session.Route, this))
        {
            _session.Route = null;
        }

        _seats.CloseAll("The host has left the game.");
    }

    private void Handle(HostPeer peer, NetMessage message)
    {
        switch (message)
        {
            case JoinRequest join when peer.Seat is null:
                Join(peer, join);
                break;
            case SendCommand send when peer.Seat is { } seat:
                Run(peer, seat, send);
                break;
            case ChatSay say when peer.Seat is not null:
                if (ChatText.Clean(say.Text) is not null && peer.Chat.Take(Time.GetUtcNow(), peer.Connection))
                {
                    _chat.Say(peer.Name, say.Text);
                }

                break;
            case TakeSeat:
                break; // asked in the lobby just before the game started

            case RecordRequest ask when peer.Feed is not null:
                peer.Connection.Send(RecordFor(peer, ask));
                break;

            default:
                peer.Connection.Close($"The host did not expect {message.GetType().Name} now.");
                break;
        }
    }

    private void Join(HostPeer peer, JoinRequest request)
    {
        if (NetSerializer.VersionRefusal(request) is { } version)
        {
            Refuse(peer, version);
            return;
        }

        if (_seats.Choose(request, _session.State, out var taken) is { } refusal)
        {
            Refuse(peer, refusal);
            return;
        }

        Seat(peer, taken, request.Name);
    }

    /// <summary>Gives the client its colour, the game's ground and its first view.</summary>
    private void Seat(HostPeer peer, PlayerId taken, string name)
    {
        var state = _session.State;
        peer.Name = name;
        SendGame(peer, taken);
        _pause.TellJoined(peer); // the next Poll tells everyone when this join ends the pause
        _seats.Joined(taken);
        _chat.Announce($"{name} plays {state.GetPlayer(taken).Name}.");
        Joined?.Invoke(taken, name);
    }

    /// <summary>The client's colour, the game's ground and its first view (again, for a game played again from a day's start).</summary>
    private void SendGame(HostPeer peer, PlayerId taken)
    {
        var state = _session.State;
        var feed = new PlayerViewFeed(state, taken);
        var codec = new StateCodec(state);
        peer.Seat = taken;
        peer.Feed = feed;
        peer.Connection.Send(new JoinAccepted(
            taken,
            MapConverter.ToDocument(state.Map),
            [.. state.Players.Select(p => new PlayerSeat(p.Id, p.Name, p.IsHuman, p.Team))],
            codec.Write(state.Options with { Seed = 0 })!,
            codec.Write(state.Rules)!));
        peer.Connection.Send(feed.First());
        if (_clockSent is { } clock)
        {
            peer.Connection.Send(new ClockUpdate(clock));
        }
    }

    /// <summary>The answer to a client's request for the game's record: at most one record per <see cref="NetLimits.RecordInterval"/> a colour.</summary>
    private RecordSent RecordFor(HostPeer peer, RecordRequest ask)
    {
        if (RecordOf is null)
        {
            return new RecordSent(ask.Request, null, "The host keeps no record of this game.");
        }

        var now = Time.GetUtcNow();
        var seat = peer.Seat!.Value;
        if (_recordSent.TryGetValue(seat, out var last) && now - last < NetLimits.RecordInterval)
        {
            return new RecordSent(ask.Request, null, $"The game was saved moments ago: wait {NetLimits.RecordInterval.TotalSeconds:0} seconds between saves.");
        }

        _recordSent[seat] = now;
        var name = ask.SaveName is null ? null : ChatText.Clean(ask.SaveName) ?? "save";
        return new RecordSent(ask.Request, RecordOf(name), null);
    }

    private static void Refuse(HostPeer peer, string reason)
    {
        peer.Connection.Send(new JoinRefused(reason));
        peer.Connection.Close(reason);
    }

    private void Run(HostPeer peer, PlayerId seat, SendCommand send)
    {
        if (send.Command.Player != seat)
        {
            peer.Connection.Send(new CommandRefused(send.Sequence, $"You play {_session.State.GetPlayer(seat).Name}."));
            return;
        }

        if (send.Command is HandToComputerCommand or HandToPersonCommand)
        {
            peer.Connection.Send(new CommandRefused(send.Sequence, send.Command is HandToComputerCommand
                ? "Only the host lets the computer play a colour."
                : "Only the replay viewer hands a computer's colour to a person."));
            return;
        }

        CommandResult result;
        _runningRemote = true;
        try
        {
            result = _session.Execute(send.Command);
        }
        finally
        {
            _runningRemote = false;
        }

        if (!result.IsAccepted)
        {
            peer.Connection.Send(new CommandRefused(send.Sequence, result.Error ?? "Refused."));
        }
    }

    /// <summary>
    /// Notes each event of the game for the clients that may see it. A battle's events are published in the turn whose battle it is (on a
    /// simultaneous day each person's own, ADR-366), so its sides are those of the battle the game is in.
    /// </summary>
    private void Capture(IEvent e)
    {
        // The sides are kept per turn, so a battle's last event, published once it is over, still reaches its sides.
        var turn = _session.State.SlotPlayer?.Value ?? -1;
        if (_session.State.Battle is { } battle)
        {
            _battleSides[turn] = new[] { battle.Attacker.Owner, battle.Defender.Owner }.OfType<PlayerId>().ToHashSet();
        }

        var sides = _battleSides.GetValueOrDefault(turn) ?? [];
        foreach (var peer in _seats.Peers)
        {
            peer.Feed?.Capture(e, sides);
        }

        _viewsStale = true;
    }

    /// <summary>
    /// Sends each seated client what changed in its view since its last update; nothing to do when the game published nothing since (a client
    /// seated meanwhile got the whole view on joining).
    /// </summary>
    private void SendViews()
    {
        if (!_viewsStale)
        {
            return;
        }

        foreach (var peer in _seats.Seated)
        {
            if (peer.Feed!.Next() is { } update)
            {
                peer.Connection.Send(update);
            }
        }

        _viewsStale = false;
    }
}
