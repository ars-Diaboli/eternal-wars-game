using EternalWars.Core.Data.Maps;
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
/// </summary>
/// <remarks>Everything happens on the caller's thread in <see cref="Poll"/>.</remarks>
public sealed class GameClient(INetConnection connection, ContentCatalogs? content = null) : ICommandRoute, IDisposable
{
    private int _sequence;
    private int _updates;
    private ViewMirror? _mirror;
    private GameSession? _session;

    /// <summary>The host took the client in and its view is in place: <see cref="Session"/> and <see cref="Seat"/> are set.</summary>
    public event Action? Joined;

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

    /// <summary>Asks the host for <paramref name="seat"/>, or for any free colour.</summary>
    public void Join(string name, PlayerId? seat = null) => connection.Send(new JoinRequest(NetSerializer.ProtocolVersion, name, seat));

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
            case JoinRefused refused:
                Fail(refused.Reason);
                break;
            case ViewUpdate update when _mirror is not null:
                Apply(update);
                break;
            case CommandRefused refused:
                CommandRefused?.Invoke(refused.Error);
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
