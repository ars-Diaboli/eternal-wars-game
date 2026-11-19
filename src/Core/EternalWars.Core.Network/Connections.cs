using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;

namespace EternalWars.Core.Network;

/// <summary>
/// One end of a link between a host and a client (ADR-192). Messages arrive on a background thread and wait in a queue until the game's thread
/// takes them (<see cref="TryReceive"/>), so the game itself never runs on two threads.
/// </summary>
public interface INetConnection : IDisposable
{
    /// <summary>The other end, for messages ("192.168.1.20:47820").</summary>
    string Remote { get; }

    /// <summary>Whether the link is down. Messages that arrived before stay in the queue.</summary>
    bool IsClosed { get; }

    /// <summary>Why the link went down; null while it is up.</summary>
    string? CloseReason { get; }

    /// <summary>Sends a message without waiting for the other side; nothing happens once the link is down.</summary>
    void Send(NetMessage message);

    /// <summary>The next message that has arrived, if any.</summary>
    bool TryReceive(out NetMessage message);

    /// <summary>Takes the link down for both ends; what was sent before still goes out, for a moment.</summary>
    void Close(string reason);
}

/// <summary>
/// A link over TCP: each message is its length (4 bytes, big-endian) and its UTF-8 JSON (<see cref="NetSerializer"/>).
/// </summary>
/// <remarks>
/// <see cref="Send"/> never waits for the other side (ADR-203): the message waits in a queue that a background task writes. A link whose other side
/// stops reading is closed once nothing of the queue has gone out for <see cref="NetLimits.SendTimeout"/>, or at once when the queue holds more
/// than <see cref="NetLimits.MaxBytesWaitingToSend"/>. A closed link still writes what waits for <see cref="NetLimits.CloseLinger"/> (a refusal's
/// reason), then drops the socket.
/// </remarks>
public sealed class TcpConnection : INetConnection
{
    /// <summary>The biggest message read; a whole game's record on a big map is a few megabytes.</summary>
    public const int MaxMessageBytes = 64 * 1024 * 1024;

    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    /// <summary>How much of a message one write hands the socket: the send timeout counts from the last piece that went out.</summary>
    private const int WritePiece = 64 * 1024;

    private readonly ConcurrentQueue<NetMessage> _received = new();
    private readonly Channel<byte[]> _outgoing = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = true });
    private readonly int _maxMessageBytes;
    private readonly int _maxWaiting;
    private readonly TimeSpan _sendTimeout;
    private readonly long _maxBytesWaitingToSend;
    private long _bytesWaitingToSend;
    private string? _closeReason;

    /// <param name="client">The connected socket.</param>
    /// <param name="maxMessageBytes">The biggest message read; a longer one closes the link before anything is allocated for it.</param>
    /// <param name="maxWaiting">The messages that may wait for <see cref="TryReceive"/>; one more closes the link.</param>
    /// <param name="sendTimeout">How long nothing may go out of what waits to be sent (<see cref="NetLimits.SendTimeout"/> unless set).</param>
    /// <param name="maxBytesWaitingToSend">The bytes that may wait to be sent; one more closes the link.</param>
    public TcpConnection(
        TcpClient client, int maxMessageBytes = MaxMessageBytes, int maxWaiting = int.MaxValue, TimeSpan? sendTimeout = null,
        long maxBytesWaitingToSend = NetLimits.MaxBytesWaitingToSend)
    {
        _maxMessageBytes = Math.Clamp(maxMessageBytes, 1, MaxMessageBytes);
        _maxWaiting = maxWaiting;
        _sendTimeout = sendTimeout ?? NetLimits.SendTimeout;
        _maxBytesWaitingToSend = maxBytesWaitingToSend;
        _client = client;
        _client.NoDelay = true;
        _stream = client.GetStream();
        Remote = client.Client.RemoteEndPoint?.ToString() ?? "?";
        _ = Task.Run(ReadAsync);
        _ = Task.Run(WriteAsync);
    }

    public string Remote { get; }

    public bool IsClosed => Volatile.Read(ref _closeReason) is not null;

    public string? CloseReason => Volatile.Read(ref _closeReason);

    /// <summary>Connects to a host; throws <see cref="SocketException"/> or <see cref="TimeoutException"/> when no host answers.</summary>
    public static TcpConnection Connect(string host, int port, TimeSpan timeout)
    {
        var client = new TcpClient();
        try
        {
            if (!client.ConnectAsync(host, port).Wait(timeout))
            {
                throw new TimeoutException($"No host answered at {host}:{port} within {timeout.TotalSeconds:0} seconds.");
            }
        }
        catch (AggregateException e) when (e.InnerException is SocketException socket)
        {
            client.Dispose();
            throw socket;
        }
        catch
        {
            client.Dispose();
            throw;
        }

        return new TcpConnection(client);
    }

    public void Send(NetMessage message)
    {
        if (IsClosed)
        {
            return;
        }

        var body = NetSerializer.Serialize(message);
        var frame = new byte[4 + body.Length];
        BinaryPrimitives.WriteInt32BigEndian(frame, body.Length);
        body.CopyTo(frame.AsSpan(4));
        if (Interlocked.Add(ref _bytesWaitingToSend, frame.Length) > _maxBytesWaitingToSend)
        {
            Drop($"More than {_maxBytesWaitingToSend} bytes wait to be sent: the other side stopped reading.");
            return;
        }

        _outgoing.Writer.TryWrite(frame);
    }

    public bool TryReceive(out NetMessage message) => _received.TryDequeue(out message!);

    public void Close(string reason)
    {
        if (!MarkClosed(reason))
        {
            return;
        }

        // What waits still goes out (the writer drops the socket once it has), but not for longer than the linger.
        _outgoing.Writer.TryComplete();
        _ = Task.Delay(NetLimits.CloseLinger).ContinueWith(_ => _client.Dispose(), TaskScheduler.Default);
    }

    public void Dispose() => Close("Closed.");

    /// <summary>Takes the link down at once, dropping what waits to be sent.</summary>
    private void Drop(string reason)
    {
        MarkClosed(reason);
        _outgoing.Writer.TryComplete();
        _client.Dispose();
    }

    private bool MarkClosed(string reason) => Interlocked.CompareExchange(ref _closeReason, reason, null) is null;

    private async Task WriteAsync()
    {
        using var stalled = new CancellationTokenSource();
        try
        {
            await foreach (var frame in _outgoing.Reader.ReadAllAsync())
            {
                for (var at = 0; at < frame.Length; at += WritePiece)
                {
                    stalled.CancelAfter(_sendTimeout);
                    await _stream.WriteAsync(frame.AsMemory(at, Math.Min(WritePiece, frame.Length - at)), stalled.Token);
                }

                Interlocked.Add(ref _bytesWaitingToSend, -frame.Length);
            }

            _client.Dispose();
        }
        catch (OperationCanceledException)
        {
            Drop($"Nothing could be sent for {_sendTimeout.TotalSeconds:0.#} seconds: the other side stopped reading.");
        }
        catch (Exception e)
        {
            Drop(e.Message);
        }
    }

    private async Task ReadAsync()
    {
        var header = new byte[4];
        try
        {
            while (!IsClosed)
            {
                await _stream.ReadExactlyAsync(header);
                var length = BinaryPrimitives.ReadInt32BigEndian(header);
                if (length <= 0 || length > _maxMessageBytes)
                {
                    throw new InvalidDataException($"A message of {length} bytes (at most {_maxMessageBytes}).");
                }

                var body = new byte[length];
                await _stream.ReadExactlyAsync(body);
                _received.Enqueue(NetSerializer.Deserialize(body));
                if (_received.Count > _maxWaiting)
                {
                    throw new InvalidDataException($"More than {_maxWaiting} messages waiting.");
                }
            }
        }
        catch (EndOfStreamException)
        {
            Close("The other side closed the connection.");
        }
        catch (Exception e)
        {
            // Whatever a message did (a bad length, bad JSON, a record that refused its values), the link goes down: a reader that stopped
            // with the link still up would leave its seat taken by nobody.
            Close(e.Message);
        }
    }
}

/// <summary>
/// Takes clients' connections on a TCP port (<see cref="DefaultPort"/> unless told otherwise) in the background; the host takes them from
/// <see cref="TryAccept"/>. A client's link reads small messages only (<see cref="NetLimits"/>).
/// </summary>
public sealed class TcpGameListener : IDisposable
{
    public const int DefaultPort = 47_820;

    private readonly TcpListener _listener;
    private readonly ConcurrentQueue<INetConnection> _accepted = new();

    /// <param name="port">The port; 0 takes any free one (<see cref="Port"/> says which).</param>
    /// <param name="loopbackOnly">Only this computer may connect (tests).</param>
    public TcpGameListener(int port = DefaultPort, bool loopbackOnly = false)
    {
        _listener = new TcpListener(loopbackOnly ? IPAddress.Loopback : IPAddress.Any, port);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _ = Task.Run(AcceptAsync);
    }

    public int Port { get; }

    public bool TryAccept(out INetConnection connection) => _accepted.TryDequeue(out connection!);

    public void Dispose() => _listener.Stop();

    private async Task AcceptAsync()
    {
        try
        {
            while (true)
            {
                var client = await _listener.AcceptTcpClientAsync();
                _accepted.Enqueue(new TcpConnection(client, NetLimits.MaxClientMessageBytes, NetLimits.MaxClientMessagesWaiting));
            }
        }
        catch (Exception e) when (e is SocketException or ObjectDisposedException or InvalidOperationException)
        {
            // Stopped.
        }
    }
}

/// <summary>
/// Two ends joined in memory, for tests and a host's own seat: each message still goes through <see cref="NetSerializer"/>, as over TCP.
/// </summary>
public sealed class LoopbackConnection : INetConnection
{
    private readonly ConcurrentQueue<NetMessage> _received = new();
    private LoopbackConnection _other = null!;
    private volatile string? _closeReason;

    private LoopbackConnection(string remote) => Remote = remote;

    public string Remote { get; }

    public bool IsClosed => _closeReason is not null;

    public string? CloseReason => _closeReason;

    /// <summary>A host's end and a client's end.</summary>
    public static (LoopbackConnection Host, LoopbackConnection Client) Pair()
    {
        var host = new LoopbackConnection("client");
        var client = new LoopbackConnection("host");
        host._other = client;
        client._other = host;
        return (host, client);
    }

    public void Send(NetMessage message)
    {
        if (!IsClosed)
        {
            _other._received.Enqueue(NetSerializer.Deserialize(NetSerializer.Serialize(message)));
        }
    }

    public bool TryReceive(out NetMessage message) => _received.TryDequeue(out message!);

    public void Close(string reason)
    {
        _closeReason ??= reason;
        _other._closeReason ??= reason;
    }

    public void Dispose() => Close("Closed.");
}
