using System.Net.Sockets;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Network;
using EternalWars.Godot.Shared;
using EternalWars.Godot.UI.Setup;
using Godot;

namespace EternalWars.Game;

/// <summary>
/// LAN games (ADR-192 to ADR-195): Multiplayer on the main menu lists the games found on the network and hosts or joins one. The host sets
/// the game up on the new-game screen, which is its lobby: players join, take open colours and chat, and the game starts with them seated. The
/// host runs the game and plays the computer players; a client holds only its player's view, which the host keeps up to date. The chat stays over
/// the map and the battle during the game. A battle opens only on the computers of its sides, each commanding its own. While a colour has nobody
/// to play it the game is paused for everyone (the pause panel); the host may let the computer play it. <c>--host[=port]</c> opens the host's lobby, <c>--join=address[:port]</c> joins one (<c>--name=</c> names the player).
/// </summary>
public partial class Main
{
    /// <summary>How often the games on the network are asked for, in seconds.</summary>
    private const double ProbeSeconds = 1.5;

    /// <summary>Takes clients' connections while this computer hosts (lobby and game).</summary>
    private TcpGameListener? _listener;

    /// <summary>Answers the computers looking for games while this computer hosts.</summary>
    private LanAnnouncer? _announcer;

    /// <summary>The lobby this computer hosts, until its game starts.</summary>
    private LobbyHost? _lobby;

    /// <summary>The LAN game this computer hosts; null otherwise.</summary>
    private GameHost? _host;

    /// <summary>The LAN game this computer has joined (its lobby, then its game); null otherwise.</summary>
    private GameClient? _client;

    /// <summary>Looks for games while the Multiplayer screen is shown.</summary>
    private LanBrowser? _browser;
    private double _probeClock;
    private MultiplayerScreen? _multiplayer;

    /// <summary>The joined player's lobby screen; null once the game is shown or the player went back.</summary>
    private LobbyScreen? _joining;

    /// <summary>The chat over the map in a LAN game; null otherwise.</summary>
    private ChatPanel? _chat;

    /// <summary>Why a LAN game is paused, over the map and the battle; null outside a LAN game.</summary>
    private PausePanel? _pause;

    /// <summary>The player's name in LAN games, as last typed (or <c>--name=</c>, or the computer's user name).</summary>
    private string _playerName = LaunchArguments.PlayerName ?? System.Environment.UserName;

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

    /// <summary>Multiplayer: the player's name, Host a game, the games found on the network, an address to type.</summary>
    private void ShowMultiplayer()
    {
        LeaveGame();
        var screen = new MultiplayerScreen();
        ShowSetup(screen, back: ShowMainMenu);
        screen.Bind(_playerName);
        _multiplayer = screen;
        screen.BackRequested += ShowMainMenu;
        screen.HostRequested += name =>
        {
            _playerName = name;
            HostLobby();
        };
        screen.JoinRequested += (name, address) =>
        {
            _playerName = name;
            JoinGame(address);
        };
        try
        {
            _browser = new LanBrowser();
            _probeClock = ProbeSeconds; // ask at once
        }
        catch (SocketException e)
        {
            screen.ShowMessage($"Cannot look for games on this network ({e.Message}); type the host's address.");
        }
    }

    /// <summary>
    /// The host's lobby: the new-game screen as a LAN host's, with the chat; players join while the host sets the game up. A port already in use
    /// (another game hosting on this computer) is said on the Multiplayer screen.
    /// </summary>
    private void HostLobby()
    {
        StopNetwork(keep: null);
        try
        {
            _listener = new TcpGameListener(LaunchArguments.HostPort ?? TcpGameListener.DefaultPort);
        }
        catch (SocketException e)
        {
            ShowMultiplayer();
            _multiplayer?.ShowMessage($"Cannot host on port {LaunchArguments.HostPort ?? TcpGameListener.DefaultPort}: {e.Message}");
            return;
        }

        try
        {
            _announcer = new LanAnnouncer(new LanGameInfo(NetSerializer.ProtocolVersion, _playerName, "", 0, _listener.Port, Started: false));
        }
        catch (SocketException e)
        {
            GD.PushWarning($"LAN discovery is off: {e.Message}"); // players can still type the address
        }

        var lobby = new LobbyHost(_playerName);
        _lobby = lobby;
        var addresses = LanAnnouncer.LocalAddresses();
        var port = _listener.Port == TcpGameListener.DefaultPort ? "" : $":{_listener.Port}";
        var setup = new NewGameScreen
        {
            Lan = new LanHosting(_playerName, addresses.Count == 0 ? "(no network found)" : string.Join(", ", addresses.Select(a => a + port))),
        };
        ShowSetup(setup, back: ShowMultiplayer);
        BindNewGame(setup);
        setup.BackRequested += ShowMultiplayer;
        setup.LanChanged += lan => lobby.SetGame(
            lan.Game,
            lan.Settings,
            [.. lan.Seats.Select(s => new LobbySeat(s.Id, s.Colour, KindOf(s.Kind), s.Kind == LanSeatKind.Here ? _playerName : null, s.Details))]);
        lobby.Changed += () => setup.ShowLanPlayers(lobby.PlayerAt);
        lobby.ChatReceived += setup.Chat.Add;
        setup.Chat.Said += lobby.Say;
        setup.Chat.Add("", $"You host. Players join from Multiplayer on their computers.");
    }

    private static SeatKind KindOf(LanSeatKind kind) => kind switch
    {
        LanSeatKind.Here => SeatKind.Host,
        LanSeatKind.Remote => SeatKind.Remote,
        _ => SeatKind.Computer,
    };

    /// <summary>
    /// The lobby's game is starting with <paramref name="session"/> (not started yet): its host takes the seated players, who get the game once
    /// it has started. Without a lobby nothing is hosted.
    /// </summary>
    private void HostLobbyGame(GameSession session, LobbyHost? lobby)
    {
        if (lobby is null)
        {
            return;
        }

        _host = lobby.StartGame(session);
        _host.ChatReceived += (from, text) => _chat?.Add(from, text);
        _host.PauseChanged += ShowPause;
    }

    /// <summary>Connects to the host at <paramref name="address"/> (<c>name</c> or <c>name:port</c>), joins its lobby or its game.</summary>
    private void JoinGame(string address)
    {
        StopNetwork(keep: null);
        var screen = new LobbyScreen();
        _joining = screen;
        ShowSetup(screen, back: ShowMultiplayer);
        screen.BackRequested += ShowMultiplayer;

        var colon = address.LastIndexOf(':');
        var (hostName, port) = colon > 0 && int.TryParse(address[(colon + 1)..], out var given)
            ? (address[..colon], given)
            : (address, TcpGameListener.DefaultPort);
        TcpConnection connection;
        try
        {
            connection = TcpConnection.Connect(hostName, port, TimeSpan.FromSeconds(5));
        }
        catch (Exception e) when (e is SocketException or TimeoutException or ArgumentException)
        {
            screen.ShowMessage($"No game answered at {hostName}:{port}: {e.Message}");
            return;
        }

        var client = new GameClient(connection, GameContent.All);
        _client = client;
        screen.SeatRequested += client.TakeSeat;
        screen.Chat.Said += client.Say;
        client.LobbyChanged += () =>
        {
            screen.ShowMessage($"In the lobby of {client.Lobby!.Host}'s game at {hostName}:{port}. The host starts the game.");
            screen.Show(ViewOf(client.Lobby, client.LobbySeat));
        };
        client.ChatReceived += (from, text) => (_joining?.Chat ?? _chat)?.Add(from, text);
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
                _joining.ShowMessage($"The host at {hostName}:{port}: {why}");
            }
            else
            {
                Tell($"Cut off from the host: {why}");
                _chat?.Add("", $"Cut off from the host: {why}");
            }
        };
        client.CommandRefused += Tell;
        client.PauseChanged += ShowPause;
        client.Join(_playerName);
        screen.ShowMessage($"Joining the game at {hostName}:{port}…");
    }

    /// <summary>The lobby as the joined player sees it: who plays each colour, which it may take.</summary>
    private static LobbyView ViewOf(LobbyState lobby, PlayerId? mine) => new(
        lobby.Game,
        lobby.Settings,
        [
            .. lobby.Seats.Select(s => new LobbySeatView(
                s.Id,
                s.Colour,
                s.Kind switch
                {
                    SeatKind.Host => $"{s.Player} (host)",
                    SeatKind.Computer => "computer",
                    _ => s.Player ?? "open",
                },
                s.Details,
                CanTake: s.Kind == SeatKind.Remote && s.Player is null,
                IsYours: s.Id == mine)),
        ],
        lobby.Waiting);

    /// <summary>
    /// The chat over the map in a LAN game, at the bottom left (Enter starts writing), and the pause panel at the top, over the battle too.
    /// </summary>
    private void BuildChat()
    {
        if (_host is null && _client is null)
        {
            _chat = null;
            _pause = null;
            return;
        }

        var top = new CanvasLayer { Name = "PauseLayer", Layer = 3 };
        _game!.AddChild(top);
        _pause = new PausePanel();
        top.AddChild(_pause);
        _pause.HandOverRequested += seat =>
        {
            if (_host?.HandToComputer(seat) is { IsAccepted: false } refused)
            {
                Tell(refused.Error ?? "");
            }
        };
        ShowPause();

        var layer = new CanvasLayer { Name = "Chat", Layer = 1 };
        _game!.AddChild(layer);
        _chat = new ChatPanel
        {
            AnchorTop = 1,
            AnchorBottom = 1,
            OffsetLeft = 8,
            OffsetRight = 388,
            OffsetTop = -190,
            OffsetBottom = -40,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        layer.AddChild(_chat);
        _chat.OverTheMap();
        _chat.Said += text =>
        {
            _host?.Say(text);
            _client?.Say(text);
        };
        _chat.Add("", "Enter writes to the other players.");
    }

    /// <summary>The chat at the bottom left of the map; over a battle, above the battle's log.</summary>
    private void PlaceChat(bool battle)
    {
        if (_chat is not null)
        {
            (_chat.OffsetTop, _chat.OffsetBottom) = battle ? (-430, -250) : (-190, -40);
        }
    }

    /// <summary>The pause panel as the game stands: why it is paused and, on the host, the colours the computer may take over.</summary>
    private void ShowPause()
    {
        if (_host is not null)
        {
            _pause?.ShowPause(
                _host.PauseReason,
                [.. _host.Waiting.Select(w => new PausedColour(w.Seat, _host.Session.State.GetPlayer(w.Seat).Name))]);
        }
        else if (_client is not null)
        {
            _pause?.ShowPause(_client.Paused, []);
        }
    }

    /// <summary>Whether the LAN game is paused for a colour nobody plays: the AI and the turn timer wait.</summary>
    private bool IsPaused => _host?.IsPaused == true || _client?.Paused is not null;

    /// <summary>Once a frame: new connections, what the clients or the host have sent, the games on the network.</summary>
    private void PollNetwork(double delta)
    {
        while (_listener is not null && _listener.TryAccept(out var connection))
        {
            if (_lobby is not null)
            {
                _lobby.Accept(connection);
            }
            else if (_host is not null)
            {
                _host.Accept(connection);
            }
            else
            {
                connection.Send(new JoinRefused("The host has not started a game yet."));
                connection.Close("No game yet.");
            }
        }

        _lobby?.Poll();
        _host?.Poll();
        _client?.Poll();
        Announce();
        if (_browser is not null && (_probeClock += delta) >= ProbeSeconds)
        {
            _probeClock = 0;
            if (_browser.Poll())
            {
                _multiplayer?.ShowGames([.. _browser.Games.Select(Listing)]);
            }

            _browser.Probe();
        }
    }

    /// <summary>What the host tells the computers looking for games: its lobby, or its game and the colours waiting for a player.</summary>
    private void Announce()
    {
        if (_announcer is null)
        {
            return;
        }

        var info = _announcer.Info;
        _announcer.Info = _lobby is not null
            ? info with { Game = _lobby.Lobby.Game, OpenColours = _lobby.OpenColours, Started = false }
            : _host is not null
                ? info with
                {
                    Game = _initialMap?.Name ?? "",
                    OpenColours = _host.RemoteSeats.Count(s => _host.PlayerAt(s) is null && !_host.Session.State.GetPlayer(s).IsDefeated),
                    Started = true,
                }
                : info with { OpenColours = 0 };
    }

    private static LanGameListing Listing(LanGameFound game)
    {
        var info = game.Info;
        var state = info.Started ? "playing" : "in the lobby";
        var open = info.OpenColours switch
        {
            0 => "no open colour",
            1 => "1 open colour",
            var n => $"{n} open colours",
        };
        return info.Protocol != NetSerializer.ProtocolVersion
            ? new LanGameListing(game.Address, $"{info.Host}: another version of the game ({game.Address})", CanJoin: false)
            : new LanGameListing(game.Address, $"{info.Host}: {info.Game}, {state}, {open} ({game.Address})", info.OpenColours > 0);
    }

    /// <summary>
    /// Leaves the LAN game, unless it is <paramref name="keep"/>'s (a game shown again keeps its host or its link); the lobby, the search for
    /// games and, once nothing is hosted (and the lobby's game is not about to be, <paramref name="listening"/>), the listener and the announcer
    /// close.
    /// </summary>
    private void StopNetwork(GameSession? keep, bool listening = false)
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

        _lobby?.Dispose();
        _lobby = null;
        _browser?.Dispose();
        _browser = null;
        _multiplayer = null;
        if (_host is null && !listening)
        {
            _announcer?.Dispose();
            _announcer = null;
            _listener?.Dispose();
            _listener = null;
        }
    }

    /// <summary>A LAN game's Enter: the chat line takes the keys.</summary>
    private bool HandleChatKey(InputEvent @event)
    {
        if (_chat is null || _chat.IsWriting || @event is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Enter or Key.KpEnter })
        {
            return false;
        }

        _chat.StartWriting();
        return true;
    }

    private void Tell(string text)
    {
        if (_status is not null)
        {
            _status.Text = text;
        }
    }
}
