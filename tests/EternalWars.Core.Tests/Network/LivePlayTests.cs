using EternalWars.Core.Domain.AI;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Players;
using EternalWars.Core.Events;
using EternalWars.Core.Network;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Network;

/// <summary>
/// Live play between computers (ADR-195): a battle between two players on two computers, each side commanded from its own; a player who drops
/// out pauses the game for everyone until a player joins the colour again or the host lets the computer play it.
/// </summary>
public class LivePlayTests
{
    private static readonly PlayerId Green = new(2);

    [Fact]
    public void ABattleBetweenTwoComputers_IsFoughtLive_EachSideByItsOwnPlayer()
    {
        var (session, host) = HostGame(Create("A.B"), remote: [Blue, Red]);
        var blue = Join(host, "Ann", Blue);
        var red = Join(host, "Bob", Red);
        var redRefusals = new List<string>();
        red.CommandRefused += redRefusals.Add;
        var redSaw = Record<BattleUnitDefended>(red.Session!);
        var blueSaw = Record<BattleUnitDefended>(blue.Session!);

        blue.Session!.Execute(new MoveHeroCommand(Blue, A, new MapPosition(2, 0))).ThrowIfRejected();
        Pump(host, blue, red);
        var battle = session.State.Battle!;
        Assert.Equal(BattlePhase.Fighting, battle.Phase);

        // Each side acts in its own turn, from its own computer; the other side's player cannot act for it.
        var acted = 0;
        while (session.State.Battle is { Round: 1, ActiveUnit: { } active } && acted < 20)
        {
            var (mine, other) = active.Side == BattleSide.Attacker ? (blue, red) : (red, blue);
            var otherColour = other.Seat!.Value;
            other.Session!.Execute(new BattleDefendCommand(otherColour)).ThrowIfRejected();
            Pump(host, blue, red);
            Assert.Same(active, session.State.Battle!.ActiveUnit);

            Assert.Equal(CommandResult.Sent(), mine.Session!.Execute(new BattleDefendCommand(mine.Seat!.Value)));
            Assert.False(mine.Session.Execute(new BattleDefendCommand(otherColour)).IsAccepted);
            Pump(host, blue, red);
            acted++;
        }

        Assert.True(acted >= 2);
        Assert.Equal(acted, redSaw.Count);
        Assert.Equal(acted, blueSaw.Count);
        Assert.Equal(session.State.Battle!.Round, red.Session!.State.Battle!.Round);
        Assert.Equal(session.State.Battle.ActiveUnit!.Id, blue.Session.State.Battle!.ActiveUnit!.Id);
        Assert.Contains(redRefusals, r => r.Length > 0); // Red tried to act in Blue's turn, at least once
    }

    [Fact]
    public void APlayerWhoDropsOut_PausesTheGameForEveryone_UntilAPlayerJoinsTheColourAgain()
    {
        var (session, host) = HostGame(ThreePlayers(), remote: [Red, Green]);
        var bob = Join(host, "Bob", Red);
        var cid = Join(host, "Cid", Green);
        var pauses = 0;
        host.PauseChanged += () => pauses++;

        bob.Dispose();
        Pump(host, cid);

        Assert.True(host.IsPaused);
        var (seat, reason) = Assert.Single(host.Waiting);
        Assert.Equal(Red, seat);
        Assert.StartsWith("Bob (Red) left the game", reason);
        Assert.Equal(host.PauseReason, cid.Paused);
        Assert.StartsWith("The game is paused", session.Execute(new StepHeroCommand(Blue, A, Direction.East)).Error);
        Assert.StartsWith("The game is paused", cid.Session!.Execute(new SetAutoCombatCommand(Green, AutoCombatMode.Always)).Error);

        var again = Join(host, "Dan");
        Pump(host, again, cid);

        Assert.Equal(Red, again.Seat);
        Assert.False(host.IsPaused);
        Assert.Null(cid.Paused);
        Assert.Null(again.Paused);
        Assert.Equal(2, pauses);
        session.Execute(new StepHeroCommand(Blue, A, Direction.East)).ThrowIfRejected();
    }

    [Fact]
    public void TheHost_LetsTheComputerPlayAColourWhosePlayerLeft_AndTheGameGoesOn()
    {
        var (session, host) = HostGame(ThreePlayers(), remote: [Red, Green]);
        var bob = Join(host, "Bob", Red);
        var cid = Join(host, "Cid", Green);
        var handed = Record<PlayerHandedToComputer>(cid.Session!);
        var lines = new List<string>();
        cid.ChatReceived += (_, text) => lines.Add(text);
        bob.Dispose();
        Pump(host, cid);

        Assert.False(host.HandToComputer(Green).IsAccepted); // Cid plays it
        host.HandToComputer(Red).ThrowIfRejected();
        Pump(host, cid);

        Assert.False(host.IsPaused);
        Assert.Null(cid.Paused);
        Assert.False(session.State.GetPlayer(Red).IsHuman);
        Assert.False(host.IsRemote(Red));
        Assert.False(cid.Session!.State.GetPlayer(Red).IsHuman);
        Assert.Equal([Red], handed.Select(h => h.Player));
        Assert.Contains("The computer plays Red.", lines);

        // Red is no longer open to join, and its turn is the computer's.
        var late = Connect(host);
        late.Join("Eve");
        Pump(host, late);
        Assert.Equal("Every colour of this game is taken.", late.Failure);

        session.EndTurn();
        Assert.Equal(Red, session.State.CurrentPlayer.Id);
        Assert.True(new AdventureAi(session).PlayTurn());
        Assert.Equal(Green, session.State.CurrentPlayer.Id);
    }

    [Fact]
    public void ThePlayerOfABattleSide_DropsOut_AndTheComputerFightsOnForIt()
    {
        var state = Create("A.B");
        state.GetHero(A).Army.Set(0, new CreatureStack(BuiltInCreatures.Champion, 10));
        var (session, host) = HostGame(state, remote: [Red]);
        var bob = Join(host, "Bob", Red);
        session.Execute(new MoveHeroCommand(Blue, A, new MapPosition(2, 0))).ThrowIfRejected();
        Pump(host, bob);
        Assert.NotNull(bob.Session!.State.Battle);

        bob.Dispose();
        host.Poll();
        Assert.StartsWith("The game is paused", session.Execute(new BattleDefendCommand(Blue)).Error);

        host.HandToComputer(Red).ThrowIfRejected();
        var battle = session.State.Battle!;
        Assert.True(battle.Defender.IsComputer);
        Assert.Null(session.Battles.Commander(BattleSide.Defender));

        // Blue's turns are Blue's; the computer plays Red's at once, until the battle is over.
        for (var i = 0; i < 200 && session.State.Battle is { } fighting; i++)
        {
            Assert.Equal(BattleSide.Attacker, fighting.ActiveUnit!.Side);
            session.Execute(new BattleDefendCommand(Blue)).ThrowIfRejected();
        }

        session.Execute(new AutoResolveBattleCommand(Blue)); // quick combat: one side is the computer's now
        Assert.Null(session.State.Battle);
    }

    [Fact]
    public void AColourNobodyJoined_PausesTheGameAtItsTurn_UntilTheComputerTakesItOver()
    {
        var (session, host) = HostGame(Create("A...B"), remote: [Red]);
        Assert.False(host.IsPaused);

        session.EndTurn();

        Assert.Equal("Nobody plays Red yet.", Assert.Single(host.Waiting).Reason);
        host.HandToComputer(Red).ThrowIfRejected();
        Assert.False(host.IsPaused);
        Assert.True(new AdventureAi(session).PlayTurn());
        Assert.Equal(Blue, session.State.CurrentPlayer.Id);
    }

    [Fact]
    public void TheComputer_DoesNotTakeOverTheLastPersonOrAComputerPlayer()
    {
        var state = Create("A...B");
        state.AssignControllers(new Dictionary<PlayerId, bool> { [Red] = false });
        var session = new GameSession(state);
        session.Start();

        Assert.Equal("The computer already plays Red.", session.Execute(new HandToComputerCommand(Red)).Error);
        Assert.Equal("A person must play at least one player.", session.Execute(new HandToComputerCommand(Blue)).Error);
    }

    [Fact]
    public void AClient_CannotHandItsOwnColourToTheComputer()
    {
        var (session, host) = HostGame(Create("A...B"), remote: [Red]);
        var bob = Join(host, "Bob", Red);
        var refusals = new List<string>();
        bob.CommandRefused += refusals.Add;

        bob.Session!.Execute(new HandToComputerCommand(Red));
        Pump(host, bob);

        Assert.Equal(["Only the host lets the computer play a colour."], refusals);
        Assert.True(session.State.GetPlayer(Red).IsHuman);
    }

    /// <summary>Blue's hero A, Red's B and Green's C in a row, every colour a person's.</summary>
    private static GameState ThreePlayers()
    {
        var two = Create("A.B..C");
        var heroes = two.Heroes.ToList();
        return new GameState(two.Map,
            [new Player(Blue, "Blue", true), new Player(Red, "Red", true), new Player(Green, "Green", true)],
            [heroes[0], heroes[1], new Hero(C, "Charlie", Green, heroes[2].Position, DefaultArmy())]);
    }

    private static (GameSession Session, GameHost Host) HostGame(GameState state, PlayerId[] remote)
    {
        var session = new GameSession(state);
        session.Start();
        return (session, new GameHost(session, remote));
    }

    private static List<T> Record<T>(GameSession session)
        where T : IEvent
    {
        var seen = new List<T>();
        session.Events.Subscribe<T>(seen.Add);
        return seen;
    }

    private static GameClient Connect(GameHost host)
    {
        var (hostEnd, clientEnd) = LoopbackConnection.Pair();
        host.Accept(hostEnd);
        return new GameClient(clientEnd);
    }

    private static GameClient Join(GameHost host, string name, PlayerId? seat = null)
    {
        var client = Connect(host);
        client.Join(name, seat);
        Pump(host, client);
        Assert.True(client.Failure is null, client.Failure);
        Assert.NotNull(client.Session);
        return client;
    }

    private static void Pump(GameHost host, params GameClient[] clients)
    {
        for (var i = 0; i < 3; i++)
        {
            host.Poll();
            foreach (var client in clients)
            {
                client.Poll();
            }
        }
    }
}
