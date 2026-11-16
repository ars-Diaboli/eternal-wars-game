using System.Net.Sockets;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Network;
using EternalWars.Godot.Shared;
using EternalWars.Godot.UI.Setup;

namespace EternalWars.Game;

/// <summary>
/// LAN games (ADR-192) until the lobby exists: <c>--host[=port]</c> hosts the game started from the new-game screen, its human colours after the
/// first played on other computers; <c>--join=address[:port]</c> joins such a game in the first free colour (<c>--name=</c> names the player).
/// The host runs the game and plays the computer players; a client holds only its player's view, which the host keeps up to date (ADR-193).
/// </summary>
public partial class Main
{
    /// <summary>Takes clients' connections while the app hosts (<c>--host</c>), for the whole run.</summary>
    private TcpGameListener? _listener;

    /// <summary>The LAN game this computer hosts; null otherwise.</summary>
    private GameHost? _host;

    /// <summary>The LAN game this computer has joined; null otherwise.</summary>
    private GameClient? _client;

    /// <summary>The screen shown while joining; null once the game is shown or the player went back.</summary>
    private JoinGameScreen? _joining;

    /// <summary>Whether a player plays on another computer: on the host the remote colours, on a client every colour but its own.</summary>
    private bool PlaysElsewhere(PlayerId player) => _host?.IsRemote(player) == true || (_client is not null && _client.Seat != player);

    /// <summary>
    /// Whether a player plays at this screen, so the map turns to it and its questions open here: every human (hot-seat), except in a LAN game,
    /// where only this computer's colours do.
    /// </summary>
    private bool PlaysHere(PlayerId player) => _session.State.GetPlayer(player).IsHuman && !PlaysElsewhere(player);

    /// <summary>Whose eyes the map is drawn through when a game is shown: the player whose turn it is if it plays here, else the first who does.</summary>
    private PlayerId ViewerHere(GameState state) =>
        PlaysHere(state.CurrentPlayer.Id) ? state.CurrentPlayer.Id : state.Players.FirstOrDefault(p => PlaysHere(p.Id))?.Id ?? state.CurrentPlayer.Id;

    /// <summary>A game about to start from the new-game screen is hosted when the app was started with <c>--host</c>.</summary>
    private void HostIfAsked(GameSession session)
    {
        if (!LaunchArguments.Host)
        {
            return;
        }

        var humans = session.State.Players.Where(p => p.IsHuman).Select(p => p.Id).ToList();
        if (humans.Count < 2)
        {
            return; // nobody to wait for: the status bar says so once the game is shown
        }

        _listener ??= new TcpGameListener(LaunchArguments.HostPort ?? TcpGameListener.DefaultPort);
        _host = new GameHost(session, humans.Skip(1));
        _host.Joined += (seat, name) => Tell($"{name} joined the game as {_session.State.GetPlayer(seat).Name}.");
        _host.Left += (seat, name, why) => Tell($"{name} ({_session.State.GetPlayer(seat).Name}) left the game: {why} The colour waits for them to join again.");
    }

    /// <summary>What the status bar says once a hosted game is shown.</summary>
    private void TellHosting()
    {
        if (!LaunchArguments.Host)
        {
            return;
        }

        Tell(_host is null
            ? "Hosting needs two human colours or more: this game is played on this computer only."
            : $"Hosting on port {_listener!.Port}: {string.Join(", ", _host.RemoteSeats.Select(s => _session.State.GetPlayer(s).Name))} play on other computers (--join=<this computer's address>).");
    }

    /// <summary>Connects to the host at <paramref name="address"/> (<c>name</c> or <c>name:port</c>) and asks for a colour.</summary>
    private void JoinGame(string address)
    {
        _joining = new JoinGameScreen();
        ShowSetup(_joining, back: ShowMainMenu);
        _joining.BackRequested += ShowMainMenu;

        var colon = address.LastIndexOf(':');
        var (hostName, port) = colon > 0 && int.TryParse(address[(colon + 1)..], out var given)
            ? (address[..colon], given)
            : (address, TcpGameListener.DefaultPort);
        TcpConnection connection;
        try
        {
            connection = TcpConnection.Connect(hostName, port, TimeSpan.FromSeconds(5));
        }
        catch (Exception e) when (e is SocketException or TimeoutException)
        {
            _joining.ShowMessage($"No game answered at {hostName}:{port}: {e.Message}");
            return;
        }

        var client = new GameClient(connection, GameContent.All);
        _client = client;
        client.Joined += () =>
        {
            CloseSetup();
            _joining = null;
            _generated = null;
            BeginGame(client.Session!, client.Ground!, resume: true);
            Tell($"Joined the game at {hostName}:{port} as {client.Session!.State.GetPlayer(client.Seat!.Value).Name}.");
        };
        client.Disconnected += why =>
        {
            if (_joining is not null)
            {
                _joining.ShowMessage($"The host at {hostName}:{port} did not take you in: {why}");
            }
            else
            {
                Tell($"Cut off from the host: {why}");
            }
        };
        client.CommandRefused += Tell;
        client.Join(LaunchArguments.PlayerName ?? System.Environment.UserName);
        _joining.ShowMessage($"Joining the game at {hostName}:{port}…");
    }

    /// <summary>Once a frame: new connections, what the clients or the host have sent.</summary>
    private void PollNetwork()
    {
        while (_listener is not null && _listener.TryAccept(out var connection))
        {
            if (_host is null)
            {
                connection.Send(new JoinRefused("The host has not started a game yet."));
                connection.Close("No game yet.");
            }
            else
            {
                _host.Accept(connection);
            }
        }

        _host?.Poll();
        _client?.Poll();
    }

    /// <summary>Leaves the LAN game, unless it is <paramref name="keep"/>'s (a game shown again keeps its host or its link).</summary>
    private void StopNetwork(GameSession? keep)
    {
        if (_host is not null && _host.Session != keep)
        {
            _host.Dispose();
            _host = null;
        }

        if (_client is not null && (_client.Session is null || _client.Session != keep))
        {
            _client.Dispose();
            _client = null;
            _joining = null;
        }
    }

    private void Tell(string text)
    {
        if (_status is not null)
        {
            _status.Text = text;
        }
    }
}
