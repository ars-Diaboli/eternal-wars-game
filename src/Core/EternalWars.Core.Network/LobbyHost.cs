using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;

namespace EternalWars.Core.Network;

/// <summary>
/// The host's lobby (ADR-194): players join before the game exists, take an open colour and write in the chat while the host sets the game up
/// (<see cref="SetGame"/>: the map, its settings in words, how each colour is played). <see cref="StartGame"/> hands the seated clients to a
/// <see cref="GameHost"/> for the game built from that setup.
/// </summary>
/// <remarks>
/// A client that joins takes the first open colour, or waits without one when none is open; a colour the host takes back (to play it here or by
/// the computer) leaves its player waiting, who takes the next open colour there is. At most <see cref="NetLimits.MaxWaitingForColour"/> wait
/// so; one more joining is turned away. Everything happens on the caller's thread in <see cref="Poll"/>.
/// </remarks>
public sealed class LobbyHost(string hostName) : IDisposable
{
    private readonly List<Peer> _peers = [];
    private string _game = "";
    private IReadOnlyList<string> _settings = [];
    private IReadOnlyList<LobbySeat> _seats = [];
    private bool _done;

    /// <summary>Someone joined, left or changed colour.</summary>
    public event Action? Changed;

    /// <summary>A line of the chat (who wrote it, empty for the lobby's own lines; the text), the host's own included.</summary>
    public event Action<string, string>? ChatReceived;

    /// <summary>The host's player.</summary>
    public string HostName { get; } = hostName;

    /// <summary>The lobby as the clients see it.</summary>
    public LobbyState Lobby => new(
        HostName,
        _game,
        _settings,
        [.. _seats.Select(s => s.Kind == SeatKind.Remote ? s with { Player = PlayerAt(s.Id) } : s)],
        [.. Joined.Where(p => p.Seat is null).Select(p => p.Name!)]);

    /// <summary>The name of the client in <paramref name="seat"/>; null while nobody has taken it.</summary>
    public string? PlayerAt(PlayerId seat) => Joined.FirstOrDefault(p => p.Seat == seat)?.Name;

    /// <summary>The colours of the game played on other computers that nobody has taken yet.</summary>
    public int OpenColours => Lobby.Open.Count();

    private IEnumerable<Peer> Joined => _peers.Where(p => p.Name is not null && !p.Connection.IsClosed);

    /// <summary>The clock for <see cref="NetLimits"/> (tests set their own); the game's host gets it too.</summary>
    internal TimeProvider Time { get; set; } = TimeProvider.System;

    /// <summary>
    /// Takes a new link; the player is in the lobby once its <see cref="JoinRequest"/> arrives, which must come within
    /// <see cref="NetLimits.JoinTimeout"/>. Turned away while <see cref="NetLimits.MaxUnjoined"/> other links wait to join.
    /// </summary>
    public void Accept(INetConnection connection)
    {
        ObjectDisposedException.ThrowIf(_done, this);
        if (NetLimits.AdmitNew(connection, _peers.Count(p => p.Name is null && !p.Connection.IsClosed)))
        {
            _peers.Add(new Peer(connection) { Arrived = Time.GetUtcNow() });
        }
    }

    /// <summary>
    /// The game as the host has set it up: the map, the settings in words and the colours (a remote colour's <see cref="LobbySeat.Player"/> is
    /// filled in here). Players whose colour is no longer remote wait for another; waiting players take the open ones.
    /// </summary>
    public void SetGame(string game, IReadOnlyList<string> settings, IReadOnlyList<LobbySeat> seats)
    {
        (_game, _settings, _seats) = (game, settings, seats);
        var changed = false;
        foreach (var peer in Joined.Where(p => p.Seat is { } seat && !IsOpenOrTheirs(seat, p)))
        {
            peer.Seat = null;
            changed = true;
        }

        changed |= SeatWaiting();
        Send();
        if (changed)
        {
            Changed?.Invoke();
        }
    }

    /// <summary>The host's player writes in the chat.</summary>
    public void Say(string text)
    {
        if (ChatText.Clean(text) is { } line)
        {
            Tell(HostName, line);
        }
    }

    /// <summary>Handles what the players have sent (joins, colours, chat) and drops the links that went down.</summary>
    public void Poll()
    {
        var changed = false;
        var now = Time.GetUtcNow();
        foreach (var peer in _peers.ToList())
        {
            for (var taken = 0; taken < NetLimits.MaxMessagesPerPoll && !peer.Connection.IsClosed && peer.Connection.TryReceive(out var message); taken++)
            {
                changed |= Handle(peer, message);
            }

            if (peer.Name is null && !peer.Connection.IsClosed)
            {
                NetLimits.CloseIfLate(peer.Connection, peer.Arrived, now);
            }

            if (peer.Connection.IsClosed)
            {
                _peers.Remove(peer);
                if (peer.Name is not null)
                {
                    Tell("", $"{peer.Name} left: {peer.Connection.CloseReason ?? "the connection closed."}");
                    changed = true;
                }
            }
        }

        changed |= SeatWaiting();
        Send();
        if (changed)
        {
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Starts the LAN game: a <see cref="GameHost"/> for <paramref name="session"/> (built from the setup, not started yet), whose remote colours
    /// are the setup's; each seated player gets the game once it has started, a player without a colour is turned away. The lobby is done.
    /// </summary>
    public GameHost StartGame(GameSession session)
    {
        ObjectDisposedException.ThrowIf(_done, this);
        var host = new GameHost(session, _seats.Where(s => s.Kind == SeatKind.Remote).Select(s => s.Id), HostName) { Time = Time };
        foreach (var peer in _peers.Where(p => !p.Connection.IsClosed))
        {
            if (peer is { Name: { } name, Seat: { } seat })
            {
                host.Admit(peer.Connection, seat, name);
            }
            else
            {
                const string Reason = "The game started without a colour for you.";
                peer.Connection.Send(new JoinRefused(Reason));
                peer.Connection.Close(Reason);
            }
        }

        _peers.Clear();
        _done = true;
        return host;
    }

    /// <summary>Closes every link of the lobby (the host went back); after <see cref="StartGame"/> the game's host holds them.</summary>
    public void Dispose()
    {
        foreach (var peer in _peers)
        {
            peer.Connection.Close("The host has closed the lobby.");
        }

        _peers.Clear();
        _done = true;
    }

    private bool Handle(Peer peer, NetMessage message)
    {
        switch (message)
        {
            case JoinRequest join when peer.Name is null:
                return Join(peer, join);
            case TakeSeat take when peer.Name is not null:
                if (IsOpenOrTheirs(take.Seat, peer) && peer.Seat != take.Seat)
                {
                    peer.Seat = take.Seat;
                    return true;
                }

                peer.Sent = null; // the lobby again, as it is
                return false;
            case ChatSay say when peer.Name is not null:
                if (ChatText.Clean(say.Text) is { } line && peer.Chat.Take(Time.GetUtcNow(), peer.Connection))
                {
                    Tell(peer.Name, line);
                }

                return false;
            default:
                peer.Connection.Close($"The host did not expect {message.GetType().Name} now.");
                return false;
        }
    }

    private bool Join(Peer peer, JoinRequest request)
    {
        if (NetSerializer.VersionRefusal(request) is { } reason)
        {
            peer.Connection.Send(new JoinRefused(reason));
            peer.Connection.Close(reason);
            return false;
        }

        if (!Lobby.Open.Any() && Joined.Count(p => p.Seat is null) >= NetLimits.MaxWaitingForColour)
        {
            NetLimits.Refuse(peer.Connection, $"No colour is open and {NetLimits.MaxWaitingForColour} players already wait for one.");
            return false;
        }

        peer.Name = ChatText.Clean(request.Name) ?? "Player";
        if (request.Seat is { } wanted && IsOpenOrTheirs(wanted, peer))
        {
            peer.Seat = wanted;
        }

        Tell("", $"{peer.Name} joined.");
        return true;
    }

    /// <summary>Whether <paramref name="seat"/> is a remote colour nobody else has taken.</summary>
    private bool IsOpenOrTheirs(PlayerId seat, Peer peer) =>
        _seats.Any(s => s.Id == seat && s.Kind == SeatKind.Remote) && Joined.All(p => p == peer || p.Seat != seat);

    /// <summary>Players without a colour take the open ones, in the order they joined.</summary>
    private bool SeatWaiting()
    {
        var changed = false;
        foreach (var peer in Joined.Where(p => p.Seat is null).ToList())
        {
            if (Lobby.Open.FirstOrDefault() is not { } open)
            {
                break;
            }

            peer.Seat = open.Id;
            changed = true;
        }

        return changed;
    }

    /// <summary>Sends each player the lobby, when it differs from what that player last got.</summary>
    private void Send()
    {
        var lobby = Lobby;
        foreach (var peer in Joined)
        {
            var update = new LobbyUpdate(lobby, peer.Seat);
            var text = System.Text.Encoding.UTF8.GetString(NetSerializer.Serialize(update));
            if (text != peer.Sent)
            {
                peer.Connection.Send(update);
                peer.Sent = text;
            }
        }
    }

    private void Tell(string from, string text)
    {
        foreach (var peer in Joined)
        {
            peer.Connection.Send(new ChatLine(from, text));
        }

        ChatReceived?.Invoke(from, text);
    }

    private sealed class Peer(INetConnection connection)
    {
        public INetConnection Connection { get; } = connection;

        /// <summary>The player's name, once joined.</summary>
        public string? Name { get; set; }

        public PlayerId? Seat { get; set; }

        /// <summary>The lobby as last sent to this player.</summary>
        public string? Sent { get; set; }

        /// <summary>When the link came (it must join within <see cref="NetLimits.JoinTimeout"/>).</summary>
        public DateTimeOffset Arrived { get; init; }

        public ChatAllowance Chat { get; } = new();
    }
}

/// <summary>What a chat line may hold.</summary>
internal static class ChatText
{
    public const int MaxLength = 200;

    /// <summary>The text without control characters and surrounding blanks, cut to <see cref="MaxLength"/>; null when nothing is left.</summary>
    public static string? Clean(string? text)
    {
        var clean = new string((text ?? "").Where(c => !char.IsControl(c)).ToArray()).Trim();
        return clean.Length == 0 ? null : clean.Length > MaxLength ? clean[..MaxLength] : clean;
    }
}
