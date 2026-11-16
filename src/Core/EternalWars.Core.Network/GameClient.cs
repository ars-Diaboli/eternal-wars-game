using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;

namespace EternalWars.Core.Network;

/// <summary>
/// A computer that joins a LAN game (ADR-192). It keeps a copy of the game (<see cref="Session"/>), built from the record the host sends on joining
/// and moved on only by the commands the host has run, in the host's order. Its own player's commands go to the host
/// (<see cref="CommandResult.Sent"/>) instead of running here; another colour's are refused here.
/// </summary>
/// <remarks>
/// The copy is the whole game, so the client sees what its fog hides if it looks; sending each client only its own view is the roadmap's next
/// step (MP-3, ADR-099). Everything happens on the caller's thread in <see cref="Poll"/>.
/// </remarks>
public sealed class GameClient(INetConnection connection, ContentCatalogs? content = null) : ICommandRoute, IDisposable
{
    private int _sequence;

    /// <summary>Set while a command from the host runs: the route lets it through.</summary>
    private bool _applying;

    /// <summary>The host took the client in: <see cref="Session"/> and <see cref="Seat"/> are set.</summary>
    public event Action? Joined;

    /// <summary>The host refused one of this client's commands (why); nothing changed.</summary>
    public event Action<string>? CommandRefused;

    /// <summary>The link went down or the copy could not follow the host (why); the client is done.</summary>
    public event Action<string>? Disconnected;

    /// <summary>The copy of the game; null until joined.</summary>
    public GameSession? Session { get; private set; }

    /// <summary>The colour this computer plays; null until joined.</summary>
    public PlayerId? Seat { get; private set; }

    /// <summary>The map the game started from; null until joined.</summary>
    public MapDocument? InitialMap { get; private set; }

    /// <summary>How the map was generated, when it was.</summary>
    public GenerationInfo? Generated { get; private set; }

    /// <summary>Why the client is done; null while it plays.</summary>
    public string? Failure { get; private set; }

    /// <summary>Asks the host for <paramref name="seat"/>, or for any free colour.</summary>
    public void Join(string name, PlayerId? seat = null) => connection.Send(new JoinRequest(NetSerializer.ProtocolVersion, name, seat));

    /// <summary>Handles what the host has sent: the seat and the game, commands run, refusals.</summary>
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
        if (_applying)
        {
            return null;
        }

        if (Failure is not null)
        {
            return CommandResult.Rejected($"The game is cut off from the host: {Failure}");
        }

        if (command.Player != Seat)
        {
            return CommandResult.Rejected($"{Session!.State.GetPlayer(command.Player).Name} plays on another computer.");
        }

        connection.Send(new SendCommand(++_sequence, command));
        return CommandResult.Sent();
    }

    public void Dispose()
    {
        if (Session is not null && ReferenceEquals(Session.Route, this))
        {
            Session.Route = null;
        }

        connection.Close("The player has left the game.");
    }

    private void Handle(NetMessage message)
    {
        switch (message)
        {
            case JoinAccepted accepted when Session is null:
                Start(accepted);
                break;
            case JoinRefused refused:
                Fail(refused.Reason);
                break;
            case CommandApplied applied when Session is not null:
                Apply(applied);
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
        GameSession session;
        try
        {
            session = GameReplay.Replay(accepted.Game, content);
        }
        catch (InvalidOperationException e)
        {
            Fail($"This computer's game does not replay the host's (another version or other content tables): {e.Message}");
            return;
        }

        session.Route = this;
        (Session, Seat, InitialMap, Generated) = (session, accepted.Seat, accepted.Game.Map, accepted.Game.Generated);
        Joined?.Invoke();
    }

    private void Apply(CommandApplied applied)
    {
        var session = Session!;
        if (applied.Index != session.CommandLog.Count)
        {
            Fail($"The host sent command {applied.Index}, but this copy has {session.CommandLog.Count}.");
            return;
        }

        _applying = true;
        try
        {
            if (session.Execute(applied.Command) is { IsAccepted: false } result)
            {
                Fail($"This copy refused the host's command {applied.Index} ({applied.Command}): {result.Error}");
            }
        }
        finally
        {
            _applying = false;
        }
    }

    private void Fail(string reason)
    {
        Failure = reason;
        connection.Close(reason);
        Disconnected?.Invoke(reason);
    }
}
