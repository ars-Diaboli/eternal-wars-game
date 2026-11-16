using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Events;

namespace EternalWars.Core.Network;

/// <summary>
/// The computer that runs a LAN game (ADR-192): it holds the one real <see cref="GameSession"/>, takes clients into the human colours that play
/// elsewhere (<see cref="RemoteSeats"/>), runs their commands, and sends every command it has run, whoever gave it, to every client. The host's
/// own player and the computer players play here as in a game on one computer; a remote colour's command given here is refused.
/// </summary>
/// <remarks>Everything happens on the caller's thread in <see cref="Poll"/> (the game app calls it once a frame).</remarks>
public sealed class GameHost : ICommandRoute, IDisposable
{
    private readonly GameSession _session;
    private readonly MapDocument _initialMap;
    private readonly GenerationInfo? _generated;
    private readonly List<PlayerId> _remoteSeats;
    private readonly List<Peer> _peers = [];
    private readonly IDisposable _subscription;

    /// <summary>Set while a client's command runs: the route lets that one through.</summary>
    private bool _runningRemote;

    /// <param name="session">The game, started or about to start.</param>
    /// <param name="initialMap">The map the game started from (saves and clients replay from it).</param>
    /// <param name="remoteSeats">The human colours played on other computers.</param>
    /// <param name="generated">How the map was generated, when it was.</param>
    public GameHost(GameSession session, MapDocument initialMap, IEnumerable<PlayerId> remoteSeats, GenerationInfo? generated = null)
    {
        _session = session;
        _initialMap = initialMap;
        _generated = generated;
        _remoteSeats = [.. remoteSeats.Distinct()];
        foreach (var seat in _remoteSeats)
        {
            if (!session.State.HasPlayer(seat) || !session.State.GetPlayer(seat).IsHuman)
            {
                throw new ArgumentException($"{seat} is not a human player of this game.", nameof(remoteSeats));
            }
        }

        session.Route = this;
        _subscription = session.Events.Subscribe<CommandExecuted>(_ => SendNewCommands());
    }

    /// <summary>A client took a colour (the colour, the player's name).</summary>
    public event Action<PlayerId, string>? Joined;

    /// <summary>A client's link went down (the colour, the player's name, why); the colour is free to join again.</summary>
    public event Action<PlayerId, string, string>? Left;

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

    /// <summary>Handles what the clients have sent (joins, commands) and drops the links that went down.</summary>
    public void Poll()
    {
        foreach (var peer in _peers.ToList())
        {
            while (!peer.Connection.IsClosed && peer.Connection.TryReceive(out var message))
            {
                Handle(peer, message);
            }

            if (peer.Connection.IsClosed)
            {
                _peers.Remove(peer);
                if (peer.Seat is { } seat)
                {
                    Left?.Invoke(seat, peer.Name, peer.Connection.CloseReason ?? "The connection closed.");
                }
            }
        }

        SendNewCommands();
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

        var free = _remoteSeats.Where(s => _peers.All(p => p.Seat != s)).ToList();
        PlayerId? seat = request.Seat is { } wanted ? (free.Contains(wanted) ? wanted : null) : free.Count > 0 ? free[0] : null;
        if (seat is not { } taken)
        {
            Refuse(peer, request.Seat is { } asked && _session.State.HasPlayer(asked)
                ? $"{_session.State.GetPlayer(asked).Name} is not a free colour in this game."
                : "Every colour of this game is taken.");
            return;
        }

        peer.Seat = taken;
        peer.Name = request.Name;
        peer.Connection.Send(new JoinAccepted(taken, GameReplay.Record(_initialMap, _session, generated: _generated)));
        peer.Sent = _session.CommandLog.Count;
        Joined?.Invoke(taken, request.Name);
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

    /// <summary>Sends each seated client the commands run since its last one, in the log's order.</summary>
    private void SendNewCommands()
    {
        var log = _session.CommandLog;
        foreach (var peer in _peers.Where(p => p.Seat is not null && !p.Connection.IsClosed))
        {
            for (; peer.Sent < log.Count; peer.Sent++)
            {
                peer.Connection.Send(new CommandApplied(peer.Sent, log[peer.Sent]));
            }
        }
    }

    private sealed class Peer(INetConnection connection)
    {
        public INetConnection Connection { get; } = connection;

        public PlayerId? Seat { get; set; }

        public string Name { get; set; } = "";

        /// <summary>How many of the log's commands the client has been sent.</summary>
        public int Sent { get; set; }
    }
}
