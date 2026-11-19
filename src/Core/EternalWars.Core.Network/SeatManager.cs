using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;

namespace EternalWars.Core.Network;

/// <summary>
/// Who plays where in a LAN game (ADR-192): the human colours played on other computers, the host's links to the clients (seated, seated in the
/// lobby and still to get the game, or still to join) and the colours whose player left (ADR-195). It picks a free colour for a joining client;
/// the host sends the game.
/// </summary>
internal sealed class SeatManager
{
    private readonly List<PlayerId> _remoteSeats;
    private readonly List<HostPeer> _peers = [];

    /// <summary>The colours whose player left during the game, with why, until a player joins them again or the computer takes them over.</summary>
    private readonly Dictionary<PlayerId, string> _dropped = [];

    /// <param name="state">The game, started or about to start.</param>
    /// <param name="remoteSeats">The human colours played on other computers.</param>
    public SeatManager(GameState state, IEnumerable<PlayerId> remoteSeats)
    {
        _remoteSeats = [.. remoteSeats.Distinct()];
        foreach (var seat in _remoteSeats)
        {
            if (!state.HasPlayer(seat) || !state.GetPlayer(seat).IsHuman)
            {
                throw new ArgumentException($"{seat} is not a human player of this game.", nameof(remoteSeats));
            }
        }
    }

    /// <summary>The human colours played on other computers, taken or not.</summary>
    public IReadOnlyList<PlayerId> RemoteSeats => _remoteSeats;

    /// <summary>Every link the host holds, in the order they came.</summary>
    public IReadOnlyList<HostPeer> Peers => _peers;

    /// <summary>The clients that have the game, on links still up.</summary>
    public IEnumerable<HostPeer> Seated => _peers.Where(p => p.Feed is not null && !p.Connection.IsClosed);

    /// <summary>The clients seated in the lobby that do not have the game yet, on links still up.</summary>
    public IEnumerable<HostPeer> Admitted => _peers.Where(p => p is { Seat: not null, Feed: null } && !p.Connection.IsClosed);

    /// <summary>Whether <paramref name="player"/> plays on another computer.</summary>
    public bool IsRemote(PlayerId player) => _remoteSeats.Contains(player);

    /// <summary>The name of the client playing <paramref name="seat"/>; null while nobody has joined it.</summary>
    public string? PlayerAt(PlayerId seat) => _peers.FirstOrDefault(p => p.Seat == seat)?.Name;

    /// <summary>Why the player of <paramref name="seat"/> left, while nobody has joined it since; null otherwise.</summary>
    public string? LeftReason(PlayerId seat) => _dropped.GetValueOrDefault(seat);

    /// <summary>Takes a new link, which must join; turned away while <see cref="NetLimits.MaxUnjoined"/> other links wait to join.</summary>
    public void Accept(INetConnection connection, DateTimeOffset now)
    {
        if (NetLimits.AdmitNew(connection, _peers.Count(p => p.Seat is null && !p.Connection.IsClosed)))
        {
            _peers.Add(new HostPeer(connection) { Arrived = now });
        }
    }

    /// <summary>Seats a client that took <paramref name="seat"/> in the lobby.</summary>
    public void Admit(INetConnection connection, PlayerId seat, string name)
    {
        if (!IsRemote(seat) || PlayerAt(seat) is not null)
        {
            throw new ArgumentException($"{seat} is not a free remote colour of this game.", nameof(seat));
        }

        _peers.Add(new HostPeer(connection) { Seat = seat, Name = name });
    }

    /// <summary>
    /// The colour a joining client gets: the one it asks for if free, else the first free one, a colour whose player left first (the game waits
    /// for it). Returns why the client is turned away, null when <paramref name="seat"/> is its colour.
    /// </summary>
    public string? Choose(JoinRequest request, GameState state, out PlayerId seat)
    {
        var free = _remoteSeats
            .Where(s => PlayerAt(s) is null && !(state.IsStarted && state.GetPlayer(s).IsDefeated))
            .OrderBy(s => _dropped.ContainsKey(s) ? 0 : 1)
            .ToList();
        PlayerId? chosen = request.Seat is { } wanted ? (free.Contains(wanted) ? wanted : null) : free.Count > 0 ? free[0] : null;
        if (chosen is { } taken)
        {
            seat = taken;
            return null;
        }

        seat = default;
        return request.Seat is { } asked && state.HasPlayer(asked)
            ? $"{state.GetPlayer(asked).Name} is not a free colour in this game."
            : "Every colour of this game is taken.";
    }

    /// <summary>A player joined <paramref name="seat"/>: it no longer waits for one.</summary>
    public void Joined(PlayerId seat) => _dropped.Remove(seat);

    /// <summary>
    /// Forgets a link that went down. A seated client's colour waits for a player again: returns who left and the chat's line for it; null for a
    /// link that never had the game.
    /// </summary>
    public Departure? Forget(HostPeer peer, GameState state)
    {
        _peers.Remove(peer);
        if (peer is not { Seat: { } seat, Feed: not null })
        {
            return null;
        }

        var why = peer.Connection.CloseReason ?? "The connection closed.";
        var line = $"{peer.Name} ({state.GetPlayer(seat).Name}) left the game: {why.TrimEnd('.')}.";
        _dropped[seat] = line;
        return new Departure(seat, peer.Name, why, line);
    }

    /// <summary>The computer plays <paramref name="seat"/> from here on: it is no longer a remote colour, nor waits for a player.</summary>
    public void HandToComputer(PlayerId seat)
    {
        _remoteSeats.Remove(seat);
        _dropped.Remove(seat);
    }

    /// <summary>Closes every link, telling the clients why, and forgets them.</summary>
    public void CloseAll(string reason)
    {
        foreach (var peer in _peers)
        {
            peer.Connection.Close(reason);
        }

        _peers.Clear();
    }

    /// <summary>A seated client whose link went down: its colour, its player's name, why, and the chat's line for it.</summary>
    public readonly record struct Departure(PlayerId Seat, string Name, string Why, string Line);
}

/// <summary>A link to a client on the host: the colour it plays and its player's view once it has the game.</summary>
internal sealed class HostPeer(INetConnection connection)
{
    public INetConnection Connection { get; } = connection;

    public PlayerId? Seat { get; set; }

    public string Name { get; set; } = "";

    /// <summary>The player's view, once seated.</summary>
    public PlayerViewFeed? Feed { get; set; }

    /// <summary>When the link came (it must join within <see cref="NetLimits.JoinTimeout"/>).</summary>
    public DateTimeOffset Arrived { get; init; }

    /// <summary>What the client may still write in the chat.</summary>
    public ChatAllowance Chat { get; } = new();
}
