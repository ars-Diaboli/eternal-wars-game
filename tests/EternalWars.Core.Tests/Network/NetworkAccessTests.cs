using System.Reflection;
using System.Runtime.CompilerServices;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Events;
using EternalWars.Core.Network;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Network;

/// <summary>
/// What the LAN code may do to a game (ADR-218): it sees the Domain's public surface only, a client changes its view through the session's
/// <see cref="IViewSink"/>, and a host and a client play together only when their views carry the same fields (<see cref="NetSerializer.Schema"/>).
/// </summary>
public class NetworkAccessTests
{
    [Fact]
    public void TheDomain_ShowsItsInternals_OnlyToTheTests()
    {
        var friends = typeof(GameState).Assembly.GetCustomAttributes<InternalsVisibleToAttribute>().Select(a => a.AssemblyName);

        Assert.Equal(["EternalWars.Core.Tests"], friends);
    }

    [Fact]
    public void OnlyASessionThatFollowsTheHost_HasAViewSink()
    {
        Assert.Null(new GameSession(Create("A...B")).ViewSink);
        Assert.NotNull(GameSession.FollowingHost(Create("A...B"), new EventBus()).ViewSink);
    }

    [Fact]
    public void TheSchema_ListsAnEnumsNamesAndNumbers_AsTheCodecSendsAnEnumAsItsNumber()
    {
        var line = SchemaLineOf(typeof(EternalWars.Core.Domain.Artifacts.ArtifactSlot));
        var expected = string.Concat(Enum.GetValues<EternalWars.Core.Domain.Artifacts.ArtifactSlot>().Select(v => $"{v}={(long)v};"));

        Assert.Equal($"{typeof(EternalWars.Core.Domain.Artifacts.ArtifactSlot).FullName}{{{expected}}}", line);
        Assert.DoesNotContain("value__", StateCodec.SchemaText(), StringComparison.Ordinal);
    }

    [Fact]
    public void TheSchema_LeavesOutTheRuntimesFields_NamingTheFirstBaseTypeOutsideTheDomainInstead()
    {
        // ActiveSpellList is a Collection<ActiveSpell>, CommandRefusedException an InvalidOperationException: what those keep inside is the
        // runtime's and differs between its versions, so two computers on different runtimes would not agree on the hash.
        var spells = SchemaLineOf(typeof(GameState).Assembly.GetType("EternalWars.Core.Domain.Battles.ActiveSpellList")!);
        var refused = SchemaLineOf(typeof(EternalWars.Core.Domain.Commands.CommandRefusedException));

        Assert.EndsWith(";:System.Collections.ObjectModel.Collection`1[EternalWars.Core.Domain.Battles.ActiveSpell]}", spells);
        Assert.DoesNotContain("items:", spells, StringComparison.Ordinal);
        Assert.EndsWith("{:System.InvalidOperationException}", refused);
    }

    private static string SchemaLineOf(Type type) =>
        StateCodec.SchemaText().Split('\n').Single(l => l.StartsWith(type.FullName + "{", StringComparison.Ordinal));

    [Fact]
    public void TheSchema_IsOneShortHash_ThatTheClientSendsWhenItJoins()
    {
        Assert.Matches("^[0-9A-F]{16}$", NetSerializer.Schema);
        Assert.Equal(NetSerializer.Schema, StateCodec.SchemaHash);

        var (session, host) = Hosted();
        var (hostEnd, clientEnd) = LoopbackConnection.Pair();
        host.Accept(hostEnd);
        var client = new GameClient(clientEnd);
        client.Join("Bob");
        for (var i = 0; i < 4; i++)
        {
            host.Poll();
            client.Poll();
        }

        Assert.Null(client.Failure);
        Assert.NotNull(client.Session);
        Assert.Same(session, host.Session);
    }

    [Theory]
    [InlineData("0000000000000000")]
    [InlineData(null)]
    public void AClientWhosePiecesDiffer_IsTurnedAway_EvenOnTheSameProtocol(string? schema)
    {
        var (_, host) = Hosted();
        var (hostEnd, clientEnd) = LoopbackConnection.Pair();
        host.Accept(hostEnd);

        clientEnd.Send(new JoinRequest(NetSerializer.ProtocolVersion, "Other", Schema: schema));
        host.Poll();

        Assert.True(clientEnd.TryReceive(out var answer));
        Assert.StartsWith("The host's game keeps its pieces differently", Assert.IsType<JoinRefused>(answer).Reason);
    }

    private static (GameSession Session, GameHost Host) Hosted()
    {
        var session = new GameSession(Create("A...B"));
        session.Start();
        return (session, new GameHost(session, [Red]));
    }
}
