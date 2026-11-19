using EternalWars.Core.Domain.Common;
using EternalWars.Core.Network;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Network;

/// <summary>
/// A LAN game's rules for the front-end (ADR-192, ADR-194, ADR-231): an address typed to join, the games found on the network, a lobby's colours,
/// and where a hosting computer's new connection goes.
/// </summary>
public class LanRulesTests
{
    [Theory]
    [InlineData("192.168.1.20", "192.168.1.20", TcpGameListener.DefaultPort)]
    [InlineData("192.168.1.20:5000", "192.168.1.20", 5000)]
    [InlineData("box:port", "box:port", TcpGameListener.DefaultPort)]
    [InlineData(":5000", ":5000", TcpGameListener.DefaultPort)]
    public void AnAddress_IsANameAndAPort_TheGamesPortWhenNoneIsGiven(string typed, string host, int port) =>
        Assert.Equal((host, port), LanAddress.Parse(typed));

    [Fact]
    public void AGameFound_ReadsAsItsHostGameStateAndOpenColours_AndIsJoinableWithAColourOpen()
    {
        var info = new LanGameInfo(NetSerializer.ProtocolVersion, "Ann", "Two Towns", 2, 47_820, Started: false);
        var found = new LanGameFound("10.0.0.5:47820", info);

        Assert.Equal("Ann: Two Towns, in the lobby, 2 open colours (10.0.0.5:47820)", LanListing.Describe(found));
        Assert.True(LanListing.CanJoin(found));

        var full = found with { Info = info with { OpenColours = 0, Started = true } };
        Assert.Equal("Ann: Two Towns, playing, no open colour (10.0.0.5:47820)", LanListing.Describe(full));
        Assert.False(LanListing.CanJoin(full));
    }

    [Fact]
    public void AGameOfAnotherVersion_SaysSo_AndCannotBeJoined()
    {
        var found = new LanGameFound("10.0.0.5:47820", new LanGameInfo(NetSerializer.ProtocolVersion + 1, "Ann", "Two Towns", 1, 47_820, false));

        Assert.Equal("Ann: another version of the game (10.0.0.5:47820)", LanListing.Describe(found));
        Assert.False(LanListing.CanJoin(found));
    }

    [Fact]
    public void ALobbysColour_SaysWhoPlaysIt_AndOnlyAnOpenRemoteOneCanBeTaken()
    {
        var host = new LobbySeat(Blue, "Blue", SeatKind.Host, "Ann", "");
        var computer = new LobbySeat(Red, "Red", SeatKind.Computer, null, "");
        var open = new LobbySeat(new PlayerId(2), "Green", SeatKind.Remote, null, "");
        var taken = open with { Player = "Bob" };

        Assert.Equal(["Ann (host)", "computer", "open", "Bob"], new[] { host, computer, open, taken }.Select(LanListing.WhoPlays));
        Assert.Equal([false, false, true, false], new[] { host, computer, open, taken }.Select(LanListing.CanTake));
    }

    [Fact]
    public void ANewConnection_GoesToTheLobby_ElseTheGame_ElseIsTurnedAway()
    {
        using var lobby = new LobbyHost("Ann");
        var (toLobby, _) = LoopbackConnection.Pair();
        LanAccept.Route(toLobby, lobby, null);
        Assert.False(toLobby.IsClosed);

        var (nowhere, client) = LoopbackConnection.Pair();
        LanAccept.Route(nowhere, null, null);
        Assert.True(nowhere.IsClosed);
        Assert.True(client.TryReceive(out var message));
        Assert.Equal(new JoinRefused(LanAccept.NoGameYet), message);
    }
}
