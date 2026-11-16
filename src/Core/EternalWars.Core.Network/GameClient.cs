using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Players;
using EternalWars.Core.Events;

namespace EternalWars.Core.Network;

/// <summary>
/// A computer that joins a LAN game (ADR-192). It keeps its player's view of the game (ADR-193): a <see cref="Session"/> that runs no rules
/// (<see cref="GameSession.FollowingHost"/>), whose state holds only what the host sent (<see cref="ViewMirror"/>) and whose events are the
/// ones the host let through. Its own player's commands go to the host (<see cref="CommandResult.Sent"/>); another colour's are refused here.
/// Before the game starts the client is in the host's lobby (ADR-194): it sees the game being set up (<see cref="Lobby"/>), may take another
/// open colour (<see cref="TakeSeat"/>) and writes in the chat (<see cref="Say"/>), as it may during the game. It saves the game as the host's
/// whole record, which it asks for (<see cref="RequestRecord"/>, ADR-196).
/// </summary>
/// <remarks>Everything happens on the caller's thread in <see cref="Poll"/>.</remarks>
public sealed class GameClient(INetConnection connection, ContentCatalogs? content = null) : ICommandRoute, IDisposable
{
    private readonly Dictionary<int, Action<GameRecordDocument?, string?>> _records = [];
    private int _sequence;
    private int _recordRequests;
    private int _updates;
    private ViewMirror? _mirror;
    private GameSession? _session;

    /// <summary>The host took the client in and its view is in place: <see cref="Session"/> and <see cref="Seat"/> are set.</summary>
    public event Action? Joined;

    /// <summary>The lobby changed (<see cref="Lobby"/>, <see cref="LobbySeat"/>).</summary>
    public event Action? LobbyChanged;

    /// <summary>A line of the chat (who wrote it, empty for the host's game itself; the text).</summary>
    public event Action<string, string>? ChatReceived;

    /// <summary>The game was paused or goes on again (<see cref="Paused"/>).</summary>
    public event Action? PauseChanged;

    /// <summary>The host refused one of this client's commands (why); nothing changed.</summary>
    public event Action<string>? CommandRefused;

    /// <summary>The link went down or the view could not follow the host (why); the client is done.</summary>
    public event Action<string>? Disconnected;

    /// <summary>The player's view of the game; null until joined.</summary>
    public GameSession? Session => _updates > 0 ? _session : null;

    /// <summary>The colour this computer plays; null until joined.</summary>
    public PlayerId? Seat { get; private set; }

    /// <summary>The ground of the map (no object, no hero), as the host sent it; null until joined.</summary>
    public MapDocument? Ground { get; private set; }

    /// <summary>Why the client is done; null while it plays.</summary>
    public string? Failure { get; private set; }

    /// <summary>The game being set up in the host's lobby; null before the host has sent it, and for a game that had started already.</summary>
    public LobbyState? Lobby { get; private set; }

    /// <summary>The client's colour in the lobby; null while it has none.</summary>
    public PlayerId? LobbySeat { get; private set; }

    /// <summary>Why the game is paused (a colour has nobody to play it, ADR-195); null while it goes on.</summary>
    public string? Paused { get; private set; }

    /// <summary>Asks the host for <paramref name="seat"/>, or for any free colour.</summary>
    public void Join(string name, PlayerId? seat = null) => connection.Send(new JoinRequest(NetSerializer.ProtocolVersion, name, seat));

    /// <summary>Asks for an open colour of the lobby in place of the one the client has.</summary>
    public void TakeSeat(PlayerId seat)
    {
        if (Failure is null && _session is null)
        {
            connection.Send(new TakeSeat(seat));
        }
    }

    /// <summary>Writes in the chat; the line comes back from the host with everyone's (<see cref="ChatReceived"/>).</summary>
    public void Say(string text)
    {
        if (Failure is null && ChatText.Clean(text) is { } line)
        {
            connection.Send(new ChatSay(line));
        }
    }

    /// <summary>
    /// Asks the host for the game's whole record (ADR-196), labelled <paramref name="saveName"/> for a save, or without a label (null) for a replay;
    /// <paramref name="answered"/> gets it (or why there is none) in a later <see cref="Poll"/>, at once when the game is not joined. A link that
    /// goes down first leaves it unanswered (<see cref="Disconnected"/> says why).
    /// </summary>
    public void RequestRecord(string? saveName, Action<GameRecordDocument?, string?> answered)
    {
        if (Failure is not null)
        {
            answered(null, $"The game is cut off from the host: {Failure}");
            return;
        }

        if (Session is null)
        {
            answered(null, "The game has not started yet.");
            return;
        }

        _records[++_recordRequests] = answered;
        connection.Send(new RecordRequest(_recordRequests, saveName));
    }

    /// <summary>Handles what the host has sent: the seat, the view's updates, refusals.</summary>
    public void Poll()
    {
        while (Failure is null && connection.TryReceive(out var message))
        {
            Handle(message);
        }

        if (Failure is null && connection.IsClosed)
        {
            Fail(connection.CloseReason ?? "The connection closed.");
        }
    }

    /// <summary>Sends its own player's command to the host; refuses another colour's, and everything once the client is done.</summary>
    public CommandResult? Route(GameCommand command)
    {
        if (Failure is not null)
        {
            return CommandResult.Rejected($"The game is cut off from the host: {Failure}");
        }

        if (command.Player != Seat)
        {
            return CommandResult.Rejected($"{_session!.State.GetPlayer(command.Player).Name} plays on another computer.");
        }

        if (Paused is not null)
        {
            return CommandResult.Rejected("The game is paused.");
        }

        connection.Send(new SendCommand(++_sequence, command));
        return CommandResult.Sent();
    }

    public void Dispose()
    {
        if (_session is not null && ReferenceEquals(_session.Route, this))
        {
            _session.Route = null;
        }

        connection.Close("The player has left the game.");
    }

    private void Handle(NetMessage message)
    {
        switch (message)
        {
            case JoinAccepted accepted when _session is null:
                Start(accepted);
                break;
            case JoinAccepted again:
                // The host played a simultaneous day again from its start (ADR-197): a new view of the new game, joined again at its first update.
                if (ReferenceEquals(_session!.Route, this))
                {
                    _session.Route = null;
                }

                (_session, _mirror, _updates) = (null, null, 0);
                Start(again);
                break;
            case JoinRefused refused:
                Fail(refused.Reason);
                break;
            case ViewUpdate update when _mirror is not null:
                Apply(update);
                break;
            case CommandRefused refused:
                CommandRefused?.Invoke(refused.Error);
                break;
            case LobbyUpdate lobby when _session is null:
                (Lobby, LobbySeat) = (lobby.Lobby, lobby.Seat);
                LobbyChanged?.Invoke();
                break;
            case ChatLine line:
                ChatReceived?.Invoke(line.From, line.Text);
                break;
            case RecordSent sent when _records.Remove(sent.Request, out var answered):
                answered(sent.Record, sent.Record is null ? sent.Error ?? "The host sent no record." : null);
                break;
            case GamePaused paused when _session is not null:
                Paused = paused.Reason;
                PauseChanged?.Invoke();
                break;
            default:
                Fail($"The client did not expect {message.GetType().Name} now.");
                break;
        }
    }

    private void Start(JoinAccepted accepted)
    {
        try
        {
            var players = accepted.Players.Select(p => new Player(p.Id, p.Name, p.IsHuman, p.Team)).ToList();
            var state = new GameState(MapConverter.ToMap(accepted.Ground), players, [], content: content,
                options: new StateCodec(EmptyCatalogs(content)).Read<GameOptions>(accepted.Options));
            if (new StateCodec(state).Read<ScenarioRules>(accepted.Rules) is { } rules)
            {
                state.SetRules(rules);
            }

            var bus = new EventBus();
            _session = GameSession.FollowingHost(state, bus);
            _session.Route = this;
            _mirror = new ViewMirror(state, accepted.Seat, bus);
            (Seat, Ground) = (accepted.Seat, accepted.Ground);
        }
        catch (Exception e) when (e is InvalidDataException or InvalidOperationException or ArgumentException or KeyNotFoundException)
        {
            Fail($"This computer cannot show the host's game (another version or other content tables): {e.Message}");
        }
    }

    private void Apply(ViewUpdate update)
    {
        if (update.Sequence != _updates + 1)
        {
            Fail($"The host sent update {update.Sequence}, but this computer has {_updates}.");
            return;
        }

        try
        {
            _mirror!.Apply(update);
        }
        catch (Exception e) when (e is InvalidDataException or InvalidOperationException or ArgumentException or KeyNotFoundException)
        {
            Fail($"This computer could not follow the host's update {update.Sequence}: {e.Message}");
            return;
        }

        if (++_updates == 1)
        {
            Joined?.Invoke();
        }
    }

    /// <summary>A game with nothing in it, whose catalogs read the options (which name no content).</summary>
    private static GameState EmptyCatalogs(ContentCatalogs? content) =>
        new(new Domain.Map.AdventureMap(1, 1), [new Player(new PlayerId(0), "", true)], [], content: content);

    private void Fail(string reason)
    {
        Failure = reason;
        connection.Close(reason);
        Disconnected?.Invoke(reason);
    }
}
