using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;

namespace EternalWars.Core.Network;

/// <summary>
/// When a LAN game is paused (ADR-195): while a remote colour without a player holds the game up, every command is refused until a player joins
/// that colour or the host lets the computer play it. It tells the seated clients and the host's screen when the pause starts or ends.
/// </summary>
internal sealed class PausePolicy(SeatManager seats)
{
    /// <summary>The pause as the clients were last told it.</summary>
    private string? _sent;

    /// <summary>The game was paused or goes on again.</summary>
    public event Action? Changed;

    /// <summary>Set while the host hands a colour to the computer: that command goes through the pause.</summary>
    public bool IsHandingOver { get; private set; }

    /// <summary>
    /// The remote colours that hold the game up, with why: those whose player left, and one nobody has joined whose turn it is or that fights the
    /// battle. Nothing holds up a game not started or over.
    /// </summary>
    public IReadOnlyList<(PlayerId Seat, string Reason)> Waiting(GameState state)
    {
        if (!state.IsStarted || state.IsOver)
        {
            return [];
        }

        var sides = state.Battle is { } battle
            ? new[] { battle.Attacker, battle.Defender }.Where(p => !p.IsComputer).Select(p => p.Owner).ToList()
            : [];
        var waiting = new List<(PlayerId, string)>();
        foreach (var seat in seats.RemoteSeats.Where(s => seats.PlayerAt(s) is null && !state.GetPlayer(s).IsDefeated))
        {
            if (seats.LeftReason(seat) is { } left)
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

    /// <summary>Why the game is paused, in words, as the players see it; null while it goes on.</summary>
    public string? Reason(GameState state) => Waiting(state) is { Count: > 0 } waiting
        ? $"{string.Join(" ", waiting.Select(w => w.Reason))} The game waits for a player to join "
          + $"{string.Join(", ", waiting.Select(w => state.GetPlayer(w.Seat).Name))}, or for the host to let the computer play it."
        : null;

    /// <summary>The refusal of every command while the game is paused; null while it goes on.</summary>
    public CommandResult? Refusal(GameState state) => Waiting(state) is { Count: > 0 } waiting
        ? CommandResult.Rejected($"The game is paused: waiting for {string.Join(", ", waiting.Select(w => state.GetPlayer(w.Seat).Name))}.")
        : null;

    /// <summary>Runs the host's command that lets the computer play a colour, through the pause (<see cref="IsHandingOver"/>).</summary>
    public CommandResult HandOver(Func<CommandResult> run)
    {
        IsHandingOver = true;
        try
        {
            return run();
        }
        finally
        {
            IsHandingOver = false;
        }
    }

    /// <summary>Tells a client that has just got the game that it is paused, if it is; the next <see cref="Send"/> tells it when the join ends it.</summary>
    public void TellJoined(HostPeer peer)
    {
        if (_sent is { } paused)
        {
            peer.Connection.Send(new GamePaused(paused));
        }
    }

    /// <summary>Tells the seated clients, and the host's screen, when the game is paused or goes on again since they were last told.</summary>
    public void Send(GameState state)
    {
        var reason = Reason(state);
        if (reason == _sent)
        {
            return;
        }

        _sent = reason;
        foreach (var peer in seats.Seated)
        {
            peer.Connection.Send(new GamePaused(reason));
        }

        Changed?.Invoke();
    }
}
