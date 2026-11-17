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

    /// <summary>How long a client waits between two <see cref="RecordRequest"/>s: each makes the host write the game's whole record.</summary>
    public static readonly TimeSpan RecordInterval = TimeSpan.FromSeconds(5);

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

    private static void Refuse(INetConnection connection, string reason)
    {
        connection.Send(new JoinRefused(reason));
        connection.Close(reason);
    }
}
