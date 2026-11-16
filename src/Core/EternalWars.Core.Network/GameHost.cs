using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Events;

namespace EternalWars.Core.Network;

/// <summary>
/// The computer that runs a LAN game (ADR-192): it holds the one real <see cref="GameSession"/>, takes clients into the human colours that play
/// elsewhere (<see cref="RemoteSeats"/>) and runs their commands. Each client gets only its player's view of the game (ADR-193,
/// <see cref="PlayerViewFeed"/>): what changed in it and the events it may see, once a <see cref="Poll"/>. The host's own player and the computer
/// players play here as in a game on one computer; a remote colour's command given here is refused. The players write to each other in the chat
/// (<see cref="Say"/>, <see cref="ChatReceived"/>). A game set up in the lobby (<see cref="LobbyHost"/>) starts with the clients already seated.
/// </summary>
/// <remarks>Everything happens on the caller's thread in <see cref="Poll"/> (the game app calls it once a frame).</remarks>
public sealed class GameHost : ICommandRoute, IDisposable
{
    private readonly GameSession _session;
    private readonly List<PlayerId> _remoteSeats;
    private readonly List<Peer> _peers = [];
    private readonly IDisposable _subscription;

    /// <summary>Set while a client's command runs: the route lets that one through.</summary>
    private bool _runningRemote;

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

    /// <summary>A line of the chat (who wrote it, empty for the game's own lines; the text), the host's own included.</summary>
    public event Action<string, string>? ChatReceived;

    /// <summary>The host's player, in the chat.</summary>
    public string HostName { get; }

    /// <summary>The game the host runs.</summary>
    public GameSession Session => _session;

    /// <summary>The human colours played on other computers, taken or not.</summary>
    public IReadOnlyList<PlayerId> RemoteSeats => _remoteSeats;

    /// <summary>Whether <paramref name="player"/> plays on another computer.</summary>
    public bool IsRemote(PlayerId player) => _remoteSeats.Contains(player);

    /// <summary>The name of the client playing <paramref name="seat"/>; null while nobody has joined it.</summary>
    public string? PlayerAt(PlayerId seat) => _peers.FirstOrDefault(p => p.Seat == seat)?.Name;

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
                    Tell("", $"{peer.Name} ({_session.State.GetPlayer(seat).Name}) left the game: {why}");
                    Left?.Invoke(seat, peer.Name, why);
                }
            }
        }

        SendViews();
    }

    /// <summary>Refuses a remote colour's command given on this computer; a client's own command, run by <see cref="Poll"/>, goes through.</summary>
    public CommandResult? Route(GameCommand command) =>
        !_runningRemote && IsRemote(command.Player)
            ? CommandResult.Rejected($"{_session.State.GetPlayer(command.Player).Name} plays on another computer.")
            : null;

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

        var free = _remoteSeats.Where(s => PlayerAt(s) is null).ToList();
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
        var feed = new PlayerViewFeed(state, taken);
        var codec = new StateCodec(state);
        peer.Seat = taken;
        peer.Name = name;
        peer.Feed = feed;
        peer.Connection.Send(new JoinAccepted(
            taken,
            MapConverter.ToDocument(state.Map),
            [.. state.Players.Select(p => new PlayerSeat(p.Id, p.Name, p.IsHuman, p.Team))],
            codec.Write(state.Options with { Seed = 0 })!,
            codec.Write(state.Rules)!));
        peer.Connection.Send(feed.First());
        Tell("", $"{name} plays {state.GetPlayer(taken).Name}.");
        Joined?.Invoke(taken, name);
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
