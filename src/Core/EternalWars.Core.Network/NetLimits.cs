namespace EternalWars.Core.Network;

/// <summary>
/// What a link to a LAN host may cost it (ADR-203): anyone on the network can connect, so a link that has not joined, or one that floods, must
/// not take the host's memory or its frames.
/// </summary>
public static class NetLimits
{
    /// <summary>
    /// The biggest message a host reads from a client (<see cref="TcpGameListener"/>): a client sends joins, colours, chat (cut to
    /// <see cref="ChatText.MaxLength"/>), commands and record requests, all far smaller. The client's end reads up to
    /// <see cref="TcpConnection.MaxMessageBytes"/>: the host sends the map and whole records.
    /// </summary>
    public const int MaxClientMessageBytes = 64 * 1024;

    /// <summary>The messages a client's link may have waiting for the host's next <c>Poll</c>; one more and the link is closed.</summary>
    public const int MaxClientMessagesWaiting = 256;

    /// <summary>The messages a host takes from one link in one <c>Poll</c>; the rest wait for the next.</summary>
    public const int MaxMessagesPerPoll = 16;

    /// <summary>The links that have not joined yet a host keeps at once; one more is turned away.</summary>
    public const int MaxUnjoined = 8;

    /// <summary>How long a new link has to send its <see cref="JoinRequest"/>.</summary>
    public static readonly TimeSpan JoinTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// How long a colour waits between two <see cref="RecordRequest"/>s: each makes the host write the game's whole record. It is the colour's, so
    /// joining it again on a new link does not start it over.
    /// </summary>
    public static readonly TimeSpan RecordInterval = TimeSpan.FromSeconds(5);

    /// <summary>The players in a lobby without a colour (none is open) at once; one more joining is turned away.</summary>
    public const int MaxWaitingForColour = 8;

    /// <summary>The chat lines a player may write in <see cref="ChatWindow"/>; the lines beyond are dropped, the writer told once.</summary>
    public const int ChatLines = 5;

    /// <summary>The span <see cref="ChatLines"/> counts over.</summary>
    public static readonly TimeSpan ChatWindow = TimeSpan.FromSeconds(10);

    /// <summary>
    /// How long a link may go without sending anything of what waits for it (<see cref="TcpConnection"/>): the other side stopped reading, and the
    /// link is closed.
    /// </summary>
    public static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(30);

    /// <summary>The bytes that may wait to be sent on a link (two of the biggest messages); one more closes it.</summary>
    public const long MaxBytesWaitingToSend = 2L * TcpConnection.MaxMessageBytes;

    /// <summary>How long a closed link still sends what waits (a refusal's reason) before its socket goes.</summary>
    public static readonly TimeSpan CloseLinger = TimeSpan.FromSeconds(2);

    /// <summary>Takes a new link unless <paramref name="unjoined"/> links already wait to join; turns it away (told why) otherwise.</summary>
    internal static bool AdmitNew(INetConnection connection, int unjoined)
    {
        if (unjoined < MaxUnjoined)
        {
            return true;
        }

        Refuse(connection, "Too many players are joining this host at once: try again in a moment.");
        return false;
    }

    /// <summary>Turns away a link that has not joined within <see cref="JoinTimeout"/> of <paramref name="arrived"/>.</summary>
    internal static void CloseIfLate(INetConnection connection, DateTimeOffset arrived, DateTimeOffset now)
    {
        if (now - arrived > JoinTimeout)
        {
            Refuse(connection, $"No join request came within {JoinTimeout.TotalSeconds:0} seconds.");
        }
    }

    internal static void Refuse(INetConnection connection, string reason)
    {
        connection.Send(new JoinRefused(reason));
        connection.Close(reason);
    }
}

/// <summary>
/// How much one link may write in the chat (<see cref="NetLimits.ChatLines"/> in <see cref="NetLimits.ChatWindow"/>), in the lobby and in the
/// game; a line beyond it is dropped and its writer told, once until a line goes through again.
/// </summary>
internal sealed class ChatAllowance
{
    public const string TooFast = "You write too fast: wait a moment.";

    private readonly Queue<DateTimeOffset> _written = new();
    private bool _told;

    /// <summary>Whether a line written at <paramref name="now"/> goes to the chat; when not, <paramref name="writer"/> is told (once).</summary>
    public bool Take(DateTimeOffset now, INetConnection writer)
    {
        while (_written.Count > 0 && now - _written.Peek() >= NetLimits.ChatWindow)
        {
            _written.Dequeue();
        }

        if (_written.Count < NetLimits.ChatLines)
        {
            _written.Enqueue(now);
            _told = false;
            return true;
        }

        if (!_told)
        {
            writer.Send(new ChatLine("", TooFast));
            _told = true;
        }

        return false;
    }
}
