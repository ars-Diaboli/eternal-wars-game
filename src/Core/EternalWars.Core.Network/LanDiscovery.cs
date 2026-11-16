using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace EternalWars.Core.Network;

/// <summary>
/// What a LAN host tells the computers looking for games (ADR-194): its version, its player, the game, how many colours wait for a player, the TCP
/// port to join on and whether the game has started (a dropped player joins it again).
/// </summary>
public sealed record LanGameInfo(int Protocol, string Host, string Game, int OpenColours, int Port, bool Started);

/// <summary>A game found on the LAN: the address to join (<c>192.168.1.20:47820</c>) and what its host said.</summary>
public sealed record LanGameFound(string Address, LanGameInfo Info);

/// <summary>
/// The host's side of LAN discovery (ADR-194): listens on a UDP port (<see cref="DefaultPort"/>) for the probes of <see cref="LanBrowser"/>s and
/// answers each with <see cref="Info"/>. It answers on a background thread; <see cref="Info"/> is a whole record, replaced at once.
/// </summary>
public sealed class LanAnnouncer : IDisposable
{
    /// <summary>The discovery port: the game's TCP port plus one.</summary>
    public const int DefaultPort = TcpGameListener.DefaultPort + 1;

    /// <summary>What a browser sends.</summary>
    internal static readonly byte[] Probe = "EternalWars?"u8.ToArray();

    private readonly UdpClient _udp;
    private volatile LanGameInfo _info;

    /// <param name="info">What to answer.</param>
    /// <param name="port">The discovery port; 0 takes any free one (tests).</param>
    public LanAnnouncer(LanGameInfo info, int port = DefaultPort)
    {
        _info = info;
        _udp = new UdpClient(AddressFamily.InterNetwork);
        _udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true); // two hosts on one computer
        _udp.Client.Bind(new IPEndPoint(IPAddress.Any, port));
        Port = ((IPEndPoint)_udp.Client.LocalEndPoint!).Port;
        _ = Task.Run(AnswerAsync);
    }

    public int Port { get; }

    /// <summary>What the host answers now.</summary>
    public LanGameInfo Info
    {
        get => _info;
        set => _info = value;
    }

    /// <summary>This computer's IPv4 addresses on its networks (what a player types to join it), loopback left out.</summary>
    public static IReadOnlyList<string> LocalAddresses()
    {
        try
        {
            return
            [
                .. System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up)
                    .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                    .Select(a => a.Address)
                    .Where(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a))
                    .Select(a => a.ToString())
                    .Distinct(),
            ];
        }
        catch (System.Net.NetworkInformation.NetworkInformationException)
        {
            return [];
        }
    }

    public void Dispose() => _udp.Dispose();

    private async Task AnswerAsync()
    {
        while (true)
        {
            try
            {
                var probe = await _udp.ReceiveAsync();
                if (probe.Buffer.AsSpan().SequenceEqual(Probe))
                {
                    await _udp.SendAsync(JsonSerializer.SerializeToUtf8Bytes(_info), probe.RemoteEndPoint);
                }
            }
            catch (SocketException e) when (e.SocketErrorCode == SocketError.ConnectionReset)
            {
                // A browser that has gone away (Windows reports it on the next receive).
            }
            catch (Exception e) when (e is SocketException or ObjectDisposedException)
            {
                return; // closed
            }
        }
    }
}

/// <summary>
/// Looks for LAN games (ADR-194): <see cref="Probe"/> asks every host on the network (a broadcast) and on this computer; the answers arrive in the
/// background and <see cref="Poll"/> takes them in on the caller's thread. A game that has not answered the last two probes is dropped.
/// </summary>
public sealed class LanBrowser : IDisposable
{
    private readonly UdpClient _udp;
    private readonly int _port;
    private readonly ConcurrentQueue<(IPEndPoint From, LanGameInfo Info)> _answers = new();
    private readonly Dictionary<string, (LanGameFound Game, int Round)> _found = [];
    private int _round;

    /// <param name="port">The hosts' discovery port.</param>
    public LanBrowser(int port = LanAnnouncer.DefaultPort)
    {
        _port = port;
        _udp = new UdpClient(AddressFamily.InterNetwork) { EnableBroadcast = true };
        _udp.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
        _ = Task.Run(ListenAsync);
    }

    /// <summary>The games found, by address.</summary>
    public IReadOnlyList<LanGameFound> Games => [.. _found.Values.Select(f => f.Game).OrderBy(g => g.Info.Host).ThenBy(g => g.Address)];

    /// <summary>Asks the hosts for their games again.</summary>
    public void Probe()
    {
        _round++;
        Send(IPAddress.Broadcast);
        Send(IPAddress.Loopback);
    }

    /// <summary>Takes in the answers that have arrived and drops the games that stopped answering; true when <see cref="Games"/> changed.</summary>
    public bool Poll()
    {
        var changed = false;
        while (_answers.TryDequeue(out var answer))
        {
            var address = $"{answer.From.Address}:{answer.Info.Port}";
            var game = new LanGameFound(address, answer.Info);

            // A host on this computer answers both the broadcast and the loopback probe: it is listed once, by its network address.
            var loopback = IPAddress.IsLoopback(answer.From.Address);
            var twins = _found.Where(f => f.Key != address && f.Value.Game.Info.Port == answer.Info.Port && f.Value.Game.Info.Host == answer.Info.Host
                                          && (loopback || f.Value.Game.Address.StartsWith("127.", StringComparison.Ordinal))).ToList();
            if (loopback && twins.Count > 0)
            {
                continue;
            }

            foreach (var twin in twins)
            {
                _found.Remove(twin.Key);
            }

            changed |= !_found.TryGetValue(address, out var known) || known.Game != game || twins.Count > 0;
            _found[address] = (game, _round);
        }

        foreach (var stale in _found.Where(f => f.Value.Round < _round - 2).Select(f => f.Key).ToList())
        {
            _found.Remove(stale);
            changed = true;
        }

        return changed;
    }

    public void Dispose() => _udp.Dispose();

    private void Send(IPAddress to)
    {
        try
        {
            _udp.Send(LanAnnouncer.Probe, LanAnnouncer.Probe.Length, new IPEndPoint(to, _port));
        }
        catch (SocketException)
        {
            // No network to broadcast on: this computer's hosts still answer the loopback probe.
        }
    }

    private async Task ListenAsync()
    {
        while (true)
        {
            try
            {
                var answer = await _udp.ReceiveAsync();
                if (JsonSerializer.Deserialize<LanGameInfo>(answer.Buffer) is { } info)
                {
                    _answers.Enqueue((answer.RemoteEndPoint, info));
                }
            }
            catch (Exception e) when (e is JsonException or NotSupportedException)
            {
                // Not a host's answer.
            }
            catch (SocketException e) when (e.SocketErrorCode == SocketError.ConnectionReset)
            {
                // A probe nobody listened to (Windows reports it on the next receive).
            }
            catch (Exception e) when (e is SocketException or ObjectDisposedException)
            {
                return; // closed
            }
        }
    }
}
