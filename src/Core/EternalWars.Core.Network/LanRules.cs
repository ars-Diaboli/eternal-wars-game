namespace EternalWars.Core.Network;

/// <summary>An address typed to join a LAN game (ADR-192, ADR-231): <c>name</c> or <c>name:port</c>.</summary>
public static class LanAddress
{
    /// <summary>The host's name and port; the game's port (<see cref="TcpGameListener.DefaultPort"/>) when none is given or it is no number.</summary>
    public static (string Host, int Port) Parse(string address)
    {
        var colon = address.LastIndexOf(':');
        return colon > 0 && int.TryParse(address[(colon + 1)..], out var port)
            ? (address[..colon], port)
            : (address, TcpGameListener.DefaultPort);
    }
}

/// <summary>How the games found on the network and a lobby's colours read to a player looking for one (ADR-194, ADR-231).</summary>
public static class LanListing
{
    /// <summary>Whether a found game can be joined: the same version of the game, with a colour open.</summary>
    public static bool CanJoin(LanGameFound game) => game.Info.Protocol == NetSerializer.ProtocolVersion && game.Info.OpenColours > 0;

    /// <summary>A found game's line: its host, game, whether it is playing, its open colours and its address; or that it is another version.</summary>
    public static string Describe(LanGameFound game)
    {
        var info = game.Info;
        if (info.Protocol != NetSerializer.ProtocolVersion)
        {
            return $"{info.Host}: another version of the game ({game.Address})";
        }

        var state = info.Started ? "playing" : "in the lobby";
        var open = info.OpenColours switch
        {
            0 => "no open colour",
            1 => "1 open colour",
            var n => $"{n} open colours",
        };
        return $"{info.Host}: {info.Game}, {state}, {open} ({game.Address})";
    }

    /// <summary>Who plays a lobby's colour: the host's player, the computer, the player who took it, or "open".</summary>
    public static string WhoPlays(LobbySeat seat) => seat.Kind switch
    {
        SeatKind.Host => $"{seat.Player} (host)",
        SeatKind.Computer => "computer",
        _ => seat.Player ?? "open",
    };

    /// <summary>Whether a joined player may take a lobby's colour: one played on another computer that nobody has taken.</summary>
    public static bool CanTake(LobbySeat seat) => seat.Kind == SeatKind.Remote && seat.Player is null;
}

/// <summary>Where a hosting computer's new connection goes (ADR-192, ADR-231): its lobby before the game, then its game.</summary>
public static class LanAccept
{
    /// <summary>What a connection hears when the host has neither a lobby nor a game.</summary>
    public const string NoGameYet = "The host has not started a game yet.";

    /// <summary>Hands <paramref name="connection"/> to the lobby, else the game; with neither, it is turned away, told why.</summary>
    public static void Route(INetConnection connection, LobbyHost? lobby, GameHost? host)
    {
        if (lobby is not null)
        {
            lobby.Accept(connection);
        }
        else if (host is not null)
        {
            host.Accept(connection);
        }
        else
        {
            connection.Send(new JoinRefused(NoGameYet));
            connection.Close("No game yet.");
        }
    }
}
