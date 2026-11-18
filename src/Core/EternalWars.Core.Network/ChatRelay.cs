namespace EternalWars.Core.Network;

/// <summary>
/// The chat of a LAN game (ADR-192): each line goes to every client that has the game and to the host's screen. The players' lines are cleaned
/// (<see cref="ChatText"/>); the game's own lines have no writer.
/// </summary>
internal sealed class ChatRelay(SeatManager seats)
{
    /// <summary>A line of the chat (who wrote it, empty for the game's own lines; the text).</summary>
    public event Action<string, string>? Received;

    /// <summary>A player writes in the chat: the host's player or a seated client. Nothing is sent when no text is left once cleaned.</summary>
    public void Say(string from, string? text)
    {
        if (ChatText.Clean(text) is { } line)
        {
            Tell(from, line);
        }
    }

    /// <summary>The game writes a line in the chat (a join, a departure, a pause handed over, a day played again).</summary>
    public void Announce(string text) => Tell("", text);

    private void Tell(string from, string text)
    {
        foreach (var peer in seats.Peers.Where(p => p.Feed is not null))
        {
            peer.Connection.Send(new ChatLine(from, text));
        }

        Received?.Invoke(from, text);
    }
}
