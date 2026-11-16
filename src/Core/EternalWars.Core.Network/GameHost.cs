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
/// The simultaneous first week (MP-2, ADR-197): the people play each day at once, each from its own computer. On a conflict the host goes back to
/// the day's start (its record up to <see cref="GameSession.SimultaneousDayStart"/>) and plays it again turn by turn: a new session
/// (<see cref="SessionReplaced"/>), whose view every client gets again.
/// </para>
/// </summary>
/// <remarks>Everything happens on the caller's thread in <see cref="Poll"/> (the game app calls it once a frame).</remarks>
public sealed class GameHost : ICommandRoute, IDisposable
{
    private readonly List<PlayerId> _remoteSeats;
    private readonly List<Peer> _peers = [];
    private GameSession _session;
    private IDisposable _subscription;

    /// <summary>The colours whose player left during the game, with why, until a player joins them again or the computer takes them over.</summary>
    private readonly Dictionary<PlayerId, string> _dropped = [];

    /// <summary>Set while a client's command runs: the route lets that one through.</summary>
    private bool _runningRemote;

    /// <summary>Set while the host hands a colour to the computer: the route lets that command through the pause.</summary>
    private bool _handingOver;

    /// <summary>The pause as the clients were last told it.</summary>
    private string? _pauseSent;

    /// <summary>The turn clock last sent to the clients.</summary>
    private TurnClock? _clockSent;

    /// <summary>Who fights the current battle, or fought the last one: they see its events.</summary>
    private IReadOnlyCollection<PlayerId> _battleSides = [];

    /// <param name="session">The game, started or about to start.</param>
    /// <param name="remoteSeats">The human colours played on other computers.</param>
    /// <param name="hostName">The host's player, in the chat.</param>
    public GameHost(GameSession session, IEnumerable<PlayerId> remoteSeats, string hostName = "Host")
    {
        _session = session;
        HostName = hostName;
        _remoteSeats = [.. remoteSeats.Distinct()];
        foreach (var seat in _remoteSeats)
        {
            if (!session.State.HasPlayer(seat) || !session.State.GetPlayer(seat).IsHuman)
            {
                throw new ArgumentException($"{seat} is not a human player of this game.", nameof(remoteSeats));
            }
        }

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
    public event Action? PauseChanged;

    /// <summary>A line of the chat (who wrote it, empty for the game's own lines; the text), the host's own included.</summary>
    public event Action<string, string>? ChatReceived;

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
    public IReadOnlyList<PlayerId> RemoteSeats => _remoteSeats;

    /// <summary>Whether <paramref name="player"/> plays on another computer.</summary>
    public bool IsRemote(PlayerId player) => _remoteSeats.Contains(player);

    /// <summary>The name of the client playing <paramref name="seat"/>; null while nobody has joined it.</summary>
    public string? PlayerAt(PlayerId seat) => _peers.FirstOrDefault(p => p.Seat == seat)?.Name;

    /// <summary>
    /// The remote colours that hold the game up, with why: those whose player left, and one nobody has joined whose turn it is or that fights the
    /// battle. The game is paused while there is one.
    /// </summary>
    public IReadOnlyList<(PlayerId Seat, string Reason)> Waiting
    {
        get
        {
            var state = _session.State;
            if (!state.IsStarted || state.IsOver)
            {
                return [];
            }

            var sides = state.Battle is { } battle
                ? new[] { battle.Attacker, battle.Defender }.Where(p => !p.IsComputer).Select(p => p.Owner).ToList()
                : [];
            var waiting = new List<(PlayerId, string)>();
            foreach (var seat in _remoteSeats.Where(s => PlayerAt(s) is null && !state.GetPlayer(s).IsDefeated))
            {
                if (_dropped.TryGetValue(seat, out var left))
                {
                    waiting.Add((seat, left));
                }
                else if (state.CurrentPlayer.Id == seat || sides.Contains(seat))
                {
                    waiting.Add((seat, $"Nobody plays {state.GetPlayer(seat).Name} yet."));
                }
            }

            return waiting;
        }
    }

    /// <summary>Whether the game is paused for a colour without a player (<see cref="Waiting"/>).</summary>
    public bool IsPaused => Waiting.Count > 0;

    /// <summary>Why the game is paused, in words, as the players see it; null while it goes on.</summary>
    public string? PauseReason => Waiting is { Count: > 0 } waiting
        ? $"{string.Join(" ", waiting.Select(w => w.Reason))} The game waits for a player to join "
          + $"{string.Join(", ", waiting.Select(w => _session.State.GetPlayer(w.Seat).Name))}, or for the host to let the computer play it."
        : null;

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

        CommandResult result;
        _handingOver = true;
        try
        {
            result = _session.Execute(new HandToComputerCommand(seat));
        }
        finally
        {
            _handingOver = false;
        }

        if (result.IsAccepted)
        {
            _remoteSeats.Remove(seat);
            _dropped.Remove(seat);
            Tell("", $"The computer plays {_session.State.GetPlayer(seat).Name}.");
            SendPause();
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
        foreach (var peer in _peers.Where(p => p.Feed is not null && !p.Connection.IsClosed))
        {
            peer.Connection.Send(new ClockUpdate(clock));
        }
    }

    /// <summary>Takes a new link; it gets a seat once its <see cref="JoinRequest"/> arrives.</summary>
    public void Accept(INetConnection connection) => _peers.Add(new Peer(connection));

    /// <summary>
    /// Seats a client that took <paramref name="seat"/> in the lobby; it gets the game at the first <see cref="Poll"/> once the game has started.
    /// </summary>
    internal void Admit(INetConnection connection, PlayerId seat, string name)
    {
        if (!IsRemote(seat) || PlayerAt(seat) is not null)
        {
            throw new ArgumentException($"{seat} is not a free remote colour of this game.", nameof(seat));
        }

        _peers.Add(new Peer(connection) { Seat = seat, Name = name });
    }

    /// <summary>The host's player writes in the chat.</summary>
    public void Say(string text)
    {
        if (ChatText.Clean(text) is { } line)
        {
            Tell(HostName, line);
        }
    }

    /// <summary>Handles what the clients have sent (joins, commands) and drops the links that went down.</summary>
    public void Poll()
    {
        PlayDayAgainIfConflicted();
        if (_session.State.IsStarted)
        {
            foreach (var peer in _peers.Where(p => p is { Seat: not null, Feed: null } && !p.Connection.IsClosed).ToList())
            {
                Seat(peer, peer.Seat!.Value, peer.Name);
            }
        }

        foreach (var peer in _peers.ToList())
        {
            while (!peer.Connection.IsClosed && peer.Connection.TryReceive(out var message))
            {
                Handle(peer, message);
            }

            if (peer.Connection.IsClosed)
            {
                _peers.Remove(peer);
                if (peer is { Seat: { } seat, Feed: not null })
                {
                    var why = peer.Connection.CloseReason ?? "The connection closed.";
                    var line = $"{peer.Name} ({_session.State.GetPlayer(seat).Name}) left the game: {why.TrimEnd('.')}.";
                    _dropped[seat] = line;
                    Tell("", line);
                    Left?.Invoke(seat, peer.Name, why);
                }
            }
        }

        PlayDayAgainIfConflicted();
        SendViews();
        SendPause();
    }

    /// <summary>
    /// A simultaneous day's conflict (ADR-197): the game goes back to the day's start and plays it again turn by turn, from the host's record. The
    /// clients first get what happened up to the conflict, then the new game's view; everyone is told why.
    /// </summary>
    private void PlayDayAgainIfConflicted()
    {
        if (_session.State.SimultaneousConflict is not { } reason || RecordOf is null)
        {
            return;
        }

        var record = RecordOf(null);
        var state = _session.State;
        var by = state.Options.HostPlayer ?? state.Players.First(p => p.IsHuman).Id;
        var again = GameReplay.Replay(
            record with { Commands = [.. record.Commands.Take(_session.SimultaneousDayStart), new PlayTurnByTurnCommand(by, reason)], FinalDigest = null },
            state.Content);

        SendViews();
        Replace(again);
        Tell("", $"{reason} The day is played again, turn by turn, and so is every day after it.");
        SessionReplaced?.Invoke(again);
    }

    /// <summary>
    /// "Play again" on an arena map's result screen (ADR-159, ADR-199): the same colours go on in <paramref name="next"/> (a new game of the same
    /// map, started), each client getting its view as on joining. Refused while the game is not over.
    /// </summary>
    public CommandResult PlayAgain(GameSession next)
    {
        ArgumentNullException.ThrowIfNull(next);
        if (!_session.State.IsOver)
        {
            return CommandResult.Rejected("The game is not over.");
        }

        if (!next.State.IsStarted || _remoteSeats.Any(s => !next.State.HasPlayer(s) || !next.State.GetPlayer(s).IsHuman))
        {
            return CommandResult.Rejected("The new game must be started, with the same colours played on other computers.");
        }

        SendViews();
        Replace(next);
        Tell("", $"{HostName} starts the game again.");
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
        _battleSides = [];
        _clockSent = null;
        foreach (var peer in _peers.Where(p => p is { Seat: not null, Feed: not null } && !p.Connection.IsClosed))
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
        if (_handingOver)
        {
            return null;
        }

        if (Waiting is { Count: > 0 } waiting)
        {
            return CommandResult.Rejected($"The game is paused: waiting for {string.Join(", ", waiting.Select(w => _session.State.GetPlayer(w.Seat).Name))}.");
        }

        if (command is AutoResolveBattleCommand && IsFoughtBetweenComputers())
        {
            return CommandResult.Rejected("A battle between players on two computers is fought by hand, each side by its own player.");
        }

        return !_runningRemote && IsRemote(command.Player)
            ? CommandResult.Rejected($"{_session.State.GetPlayer(command.Player).Name} plays on another computer.")
            : null;
    }

    /// <summary>Whether people on two computers command the two sides of the battle being fought.</summary>
    private bool IsFoughtBetweenComputers() =>
        _session.State.Battle is { Attacker: { IsComputer: false, Owner: { } attacker }, Defender: { IsComputer: false, Owner: { } defender } }
        && (IsRemote(attacker) || IsRemote(defender));

    public void Dispose()
    {
        _subscription.Dispose();
        if (ReferenceEquals(_session.Route, this))
        {
            _session.Route = null;
        }

        foreach (var peer in _peers)
        {
            peer.Connection.Close("The host has left the game.");
        }

        _peers.Clear();
    }

    private void Handle(Peer peer, NetMessage message)
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
                if (ChatText.Clean(say.Text) is { } line)
                {
                    Tell(peer.Name, line);
                }

                break;
            case TakeSeat:
                break; // asked in the lobby just before the game started

            case RecordRequest ask when peer.Feed is not null:
                peer.Connection.Send(RecordFor(ask));
                break;

            default:
                peer.Connection.Close($"The host did not expect {message.GetType().Name} now.");
                break;
        }
    }

    private void Join(Peer peer, JoinRequest request)
    {
        if (request.Protocol != NetSerializer.ProtocolVersion)
        {
            Refuse(peer, $"The host plays protocol {NetSerializer.ProtocolVersion}, this game {request.Protocol}: use the same version of the game.");
            return;
        }

        // A colour whose player left comes first: the game waits for it.
        var free = _remoteSeats
            .Where(s => PlayerAt(s) is null && !(_session.State.IsStarted && _session.State.GetPlayer(s).IsDefeated))
            .OrderBy(s => _dropped.ContainsKey(s) ? 0 : 1)
            .ToList();
        PlayerId? seat = request.Seat is { } wanted ? (free.Contains(wanted) ? wanted : null) : free.Count > 0 ? free[0] : null;
        if (seat is not { } taken)
        {
            Refuse(peer, request.Seat is { } asked && _session.State.HasPlayer(asked)
                ? $"{_session.State.GetPlayer(asked).Name} is not a free colour in this game."
                : "Every colour of this game is taken.");
            return;
        }

        Seat(peer, taken, request.Name);
    }

    /// <summary>Gives the client its colour, the game's ground and its first view.</summary>
    private void Seat(Peer peer, PlayerId taken, string name)
    {
        var state = _session.State;
        peer.Name = name;
        SendGame(peer, taken);
        if (_pauseSent is { } paused)
        {
            peer.Connection.Send(new GamePaused(paused)); // the next Poll tells everyone when this join ends it
        }

        _dropped.Remove(taken);
        Tell("", $"{name} plays {state.GetPlayer(taken).Name}.");
        Joined?.Invoke(taken, name);
    }

    /// <summary>The client's colour, the game's ground and its first view (again, for a game played again from a day's start).</summary>
    private void SendGame(Peer peer, PlayerId taken)
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

    /// <summary>The answer to a client's request for the game's record.</summary>
    private RecordSent RecordFor(RecordRequest ask)
    {
        if (RecordOf is null)
        {
            return new RecordSent(ask.Request, null, "The host keeps no record of this game.");
        }

        var name = ask.SaveName is null ? null : ChatText.Clean(ask.SaveName) ?? "save";
        return new RecordSent(ask.Request, RecordOf(name), null);
    }

    /// <summary>A line of the chat, to every seated client and the host.</summary>
    private void Tell(string from, string text)
    {
        foreach (var peer in _peers.Where(p => p.Feed is not null))
        {
            peer.Connection.Send(new ChatLine(from, text));
        }

        ChatReceived?.Invoke(from, text);
    }

    private static void Refuse(Peer peer, string reason)
    {
        peer.Connection.Send(new JoinRefused(reason));
        peer.Connection.Close(reason);
    }

    private void Run(Peer peer, PlayerId seat, SendCommand send)
    {
        if (send.Command.Player != seat)
        {
            peer.Connection.Send(new CommandRefused(send.Sequence, $"You play {_session.State.GetPlayer(seat).Name}."));
            return;
        }

        if (send.Command is HandToComputerCommand)
        {
            peer.Connection.Send(new CommandRefused(send.Sequence, "Only the host lets the computer play a colour."));
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

    /// <summary>Notes each event of the game for the clients that may see it.</summary>
    private void Capture(IEvent e)
    {
        if (_session.State.Battle is { } battle)
        {
            _battleSides = new[] { battle.Attacker.Owner, battle.Defender.Owner }.OfType<PlayerId>().ToHashSet();
        }

        foreach (var peer in _peers)
        {
            peer.Feed?.Capture(e, _battleSides);
        }
    }

    /// <summary>Tells the clients, and the host's screen, when the game is paused or goes on again.</summary>
    private void SendPause()
    {
        var reason = PauseReason;
        if (reason == _pauseSent)
        {
            return;
        }

        _pauseSent = reason;
        foreach (var peer in _peers.Where(p => p.Feed is not null && !p.Connection.IsClosed))
        {
            peer.Connection.Send(new GamePaused(reason));
        }

        PauseChanged?.Invoke();
    }

    /// <summary>Sends each seated client what changed in its view since its last update.</summary>
    private void SendViews()
    {
        foreach (var peer in _peers.Where(p => p.Feed is not null && !p.Connection.IsClosed))
        {
            if (peer.Feed!.Next() is { } update)
            {
                peer.Connection.Send(update);
            }
        }
    }

    private sealed class Peer(INetConnection connection)
    {
        public INetConnection Connection { get; } = connection;

        public PlayerId? Seat { get; set; }

        public string Name { get; set; } = "";

        /// <summary>The player's view, once seated.</summary>
        public PlayerViewFeed? Feed { get; set; }
    }
}
