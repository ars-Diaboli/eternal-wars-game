using System.Diagnostics;
using EternalWars.Core.Domain.AI;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Players;
using EternalWars.Core.Events;
using EternalWars.Core.Data.Templates;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Generation;
using EternalWars.Core.Network;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Network;

/// <summary>
/// LAN games (ADR-192, ADR-193): a host runs the game, a client sends its own colour's commands and keeps only its player's view, which the host
/// sends as it changes: nothing under the fog, events only where the player sees them.
/// </summary>
public class LanGameTests
{
    /// <summary>Wide enough that a hero at one end does not see the other end (sight 5, refog margin 2).</summary>
    private const string Wide = "A..................B";

    private static readonly GameOptions Refog = new(FogMode.ShroudWithRefog);

    [Fact]
    public void AClientJoins_WithItsOwnHeroesAndResources_AndNothingUnderItsFog()
    {
        var (session, host) = HostGame(Create(Refog, Wide), remote: [Red]);
        var client = Join(host);
        var view = client.Session!.State;

        Assert.Equal(Red, client.Seat);
        Assert.Equal(session.State.Date, view.Date);
        Assert.Equal(session.State.CurrentPlayer.Id, view.CurrentPlayer.Id);
        Assert.Equal(new MapPosition(19, 0), view.GetHero(B).Position);
        Assert.DoesNotContain(view.Heroes, h => h.Id == A);
        Assert.Equal(session.State.GetPlayer(Red).Resources, view.GetPlayer(Red).Resources);
        Assert.Equal(default, view.GetPlayer(Blue).Resources);
        Assert.False(view.Fog.IsExplored(Red, new MapPosition(0, 0)));
        Assert.True(view.Fog.IsExplored(Red, new MapPosition(19, 0)));
        Assert.Equal(0ul, view.Options.Seed);
    }

    [Fact]
    public void AClientsCommand_RunsOnTheHost_AndItsViewFollows()
    {
        var (session, host) = HostGame(Create("A...B"), remote: [Blue]);
        var client = Join(host);
        var view = client.Session!;

        var result = view.Execute(new StepHeroCommand(Blue, A, Direction.East));

        // Sent, not run: the view waits for the host.
        Assert.True(result.IsSent);
        Assert.Equal(new MapPosition(0, 0), view.State.GetHero(A).Position);

        var moved = Record<HeroMoved>(view);
        var figure = view.State.GetHero(A);
        Pump(host, client);

        // The hero the views hold is the one that moved: the view takes the host's values in place.
        Assert.Same(figure, view.State.GetHero(A));

        Assert.Equal(new MapPosition(1, 0), session.State.GetHero(A).Position);
        Assert.Equal(new MapPosition(1, 0), view.State.GetHero(A).Position);
        Assert.Equal(view.State.GetHero(A).MovementPoints, session.State.GetHero(A).MovementPoints);
        Assert.Equal([new HeroMoved(A, new MapPosition(0, 0), new MapPosition(1, 0), session.State.GetHero(A).MovementPoints)], moved);
    }

    [Fact]
    public void AHeroMovedByTheHost_IsFoundOnItsNewTile_InTheClientsView()
    {
        var (session, host) = HostGame(Create("A...B"), remote: [Blue]);
        var client = Join(host);
        var view = client.Session!.State;

        Assert.True(client.Session.Execute(new StepHeroCommand(Blue, A, Direction.East)).IsSent);
        host.Poll();
        Assert.Equal(new MapPosition(1, 0), session.State.GetHero(A).Position);

        // The client looks before the host's update reaches it, so its lookup is built after the host's move but from the old places.
        Assert.Equal(A, view.HeroAt(new MapPosition(0, 0))?.Id);
        Pump(host, client);

        Assert.Equal(new MapPosition(1, 0), view.GetHero(A).Position);
        Assert.Equal(A, view.HeroAt(new MapPosition(1, 0))?.Id);
        Assert.Null(view.HeroAt(new MapPosition(0, 0)));
    }

    [Fact]
    public void AnEnemyHero_ComesIntoTheView_WhenSeen_AndLeavesItWhenOutOfSight()
    {
        var (session, host) = HostGame(Create(Refog, "A...........B"), remote: [Red]);
        var client = Join(host);
        var view = client.Session!;
        var placed = Record<HeroPlaced>(view);
        var removed = Record<HeroRemoved>(view);
        var moves = Record<HeroMoved>(view);

        Assert.DoesNotContain(view.State.Heroes, h => h.Id == A);
        for (var i = 0; i < 7; i++)
        {
            session.Step(A, Direction.East);
        }

        Pump(host, client);
        Assert.Equal(new MapPosition(7, 0), view.State.GetHero(A).Position);
        Assert.Equal([A], placed.Select(e => e.Hero));
        Assert.All(moves, m => Assert.True(m.To.X >= 7 || m.From.X >= 7)); // only the steps in Red's sight (B at 12, sight 5)

        for (var i = 0; i < 7; i++)
        {
            session.Step(A, Direction.West);
        }

        Pump(host, client);
        Assert.DoesNotContain(view.State.Heroes, h => h.Id == A);
        Assert.Equal([A], removed.Select(e => e.Hero));
    }

    [Fact]
    public void AnObjectOutOfSight_StaysAsLastSeen_UntilThePlayerSeesItsPlaceAgain()
    {
        // Red's hero B stands by a pile, walks away from it, and Blue's hero takes it meanwhile.
        var state = Create(Refog, "A..........B........");
        var pile = Add<ResourcePile>(state, BuiltInMapObjects.PileId(ResourceType.Gold), 9, 0);
        var (session, host) = HostGame(state, remote: [Red]);
        var client = Join(host);
        var view = client.Session!;
        Assert.NotNull(view.State.FindObject(pile.Id));

        session.EndTurn();
        Pump(host, client);
        for (var i = 0; i < 8; i++)
        {
            view.Execute(new StepHeroCommand(Red, B, Direction.East)).ThrowIfRejected();
            Pump(host, client);
        }

        view.Execute(new EndTurnCommand(Red)).ThrowIfRejected();
        Pump(host, client);
        var collected = Record<ResourcePileCollected>(view);
        for (var i = 0; i < 9; i++)
        {
            session.Step(A, Direction.East);
        }

        Pump(host, client);
        Assert.Null(session.State.FindObject(pile.Id));
        Assert.NotNull(view.State.FindObject(pile.Id)); // as last seen
        Assert.NotNull(view.State.Memory.Get(Red, pile.Id));
        Assert.Empty(collected);
        Assert.Equal(default, view.State.GetPlayer(Blue).Resources);

        session.EndTurn();
        Pump(host, client);
        for (var i = 0; i < 8; i++)
        {
            view.Execute(new StepHeroCommand(Red, B, Direction.West)).ThrowIfRejected();
            Pump(host, client);
        }

        Assert.Null(view.State.FindObject(pile.Id));
        Assert.Null(view.State.Memory.Get(Red, pile.Id));
    }

    [Fact]
    public void AnotherPlayersPrivateEvents_StayOnTheHost_AndTheTurnOrderReachesEveryone()
    {
        var (session, host) = HostGame(Create(Refog, Wide), remote: [Red]);
        var client = Join(host);
        var view = client.Session!;
        var resources = Record<ResourcesChanged>(view);
        var turns = Record<TurnStarted>(view);
        var executed = Record<CommandExecuted>(view);

        session.Step(A, Direction.East);
        session.EndTurn();
        Pump(host, client);
        view.Execute(new EndTurnCommand(Red)).ThrowIfRejected();
        Pump(host, client);

        Assert.Equal([Red, Blue], turns.Select(t => t.Player));
        Assert.All(resources, r => Assert.Equal(Red, r.Player));
        Assert.Equal([new EndTurnCommand(Red)], executed.Select(e => e.Command));
        Assert.Equal(2, view.State.Date.AbsoluteDay);
    }

    [Fact]
    public void BothSidesOfABattle_SeeAllOfIt_AndAThirdPlayerOnlyItsResult()
    {
        var green = new PlayerId(2);
        var two = Create("A.B..C");
        var heroes = two.Heroes.ToList();
        two.GetHero(A).Army.Set(0, new CreatureStack(BuiltInCreatures.Champion, 10));
        var state = new GameState(two.Map,
            [new Player(Blue, "Blue", true), new Player(Red, "Red", true), new Player(green, "Green", true)],
            [heroes[0], heroes[1], new Hero(C, "Charlie", green, heroes[2].Position, DefaultArmy())]);
        var (session, host) = HostGame(state, remote: [Blue, Red, green]);
        var blue = Join(host, "Ann", Blue);
        var red = Join(host, "Bob", Red);
        var third = Join(host, "Cid", green);
        var redBattle = Record<BattleStarted>(red.Session!);
        var thirdBattle = Record<BattleStarted>(third.Session!);
        var thirdDefeats = Record<HeroDefeated>(third.Session!);

        blue.Session!.Execute(new MoveHeroCommand(Blue, A, new MapPosition(2, 0))).ThrowIfRejected();
        Pump(host, blue, red, third);
        Assert.IsType<BattleDecision>(blue.Session.State.PendingDecision);
        Assert.NotNull(blue.Session.State.Battle);
        Assert.NotNull(red.Session!.State.Battle);
        Assert.Null(third.Session!.State.Battle);
        Assert.Null(third.Session.State.PendingDecision);
        Assert.Single(redBattle);
        Assert.Empty(thirdBattle);

        // Two players on two computers fight by hand: no quick combat.
        var refusals = new List<string>();
        blue.CommandRefused += refusals.Add;
        blue.Session.Execute(new AutoResolveBattleCommand(Blue)).ThrowIfRejected();
        Pump(host, blue, red, third);
        Assert.StartsWith("A battle between players on two computers is fought by hand", Assert.Single(refusals));
        Assert.NotNull(session.State.Battle);

        red.Session.Execute(new BattleRetreatCommand(Red)).ThrowIfRejected();
        Pump(host, blue, red, third);

        Assert.Null(session.State.Battle);
        Assert.Null(blue.Session.State.Battle);
        Assert.Null(red.Session.State.Battle);
        Assert.DoesNotContain(red.Session.State.Heroes, h => h.Id == B);
        Assert.DoesNotContain(third.Session.State.Heroes, h => h.Id == B);
        Assert.Equal([B], thirdDefeats.Select(d => d.Hero));
        Assert.Equal(session.State.GetHero(A).Army.ToString(), blue.Session.State.GetHero(A).Army.ToString());
    }

    [Fact]
    public void AnotherColoursCommand_IsRefused_OnTheClient_OnTheHostsScreen_AndByTheHost()
    {
        var (session, host) = HostGame(Create("A...B"), remote: [Red]);
        var (hostEnd, clientEnd) = LoopbackConnection.Pair();
        host.Accept(hostEnd);
        var client = new GameClient(clientEnd);
        client.Join("Bob");
        Pump(host, client);
        session.EndTurn(); // Red's turn
        Pump(host, client);

        // The client's view refuses another colour's command; the host's own screen refuses the remote colour's.
        Assert.False(client.Session!.Execute(new EndTurnCommand(Blue)).IsAccepted);
        var onTheHost = session.Execute(new EndTurnCommand(Red));
        Assert.False(onTheHost.IsAccepted);
        Assert.Contains("another computer", onTheHost.Error);

        // A client that sends another colour's command anyway is refused by the host.
        var refusals = new List<string>();
        client.CommandRefused += refusals.Add;
        clientEnd.Send(new SendCommand(7, new StepHeroCommand(Blue, A, Direction.East)));
        Pump(host, client);

        Assert.Equal(["You play Red."], refusals);
        Assert.Equal(new MapPosition(0, 0), session.State.GetHero(A).Position);
    }

    [Fact]
    public void ARuleRefusal_ComesBackToTheClient_AndChangesNothing()
    {
        var (session, host) = HostGame(Create("A...B"), remote: [Blue]);
        var client = Join(host);
        var refusals = new List<string>();
        client.CommandRefused += refusals.Add;

        // The client runs no rules, so it sends what the host's rules refuse: buying in a town that does not exist.
        Assert.True(client.Session!.Execute(new BuySpellBookCommand(Blue, new MapObjectId(99))).IsSent);
        Pump(host, client);

        Assert.Single(refusals);
        Assert.Empty(session.CommandLog);
    }

    [Fact]
    public void TheHostPlaysTheComputerPlayers_AndTheClientFollowsTheTurns()
    {
        var state = Create(Refog, Wide);
        state.GetPlayer(Blue).IsHuman = false;
        var (session, host) = HostGame(state, remote: [Red]);
        var client = Join(host);

        new AdventureAi(session).PlayTurns(limit: 1);
        Pump(host, client);
        Assert.Equal(Red, client.Session!.State.CurrentPlayer.Id);
        client.Session.Execute(new EndTurnCommand(Red)).ThrowIfRejected();
        Pump(host, client);

        Assert.Equal(2, client.Session.State.Date.AbsoluteDay);
        Assert.Equal(Blue, client.Session.State.CurrentPlayer.Id);
    }

    [Fact]
    public void AComputerGameOnAGeneratedMap_TheClientsViewIsAlwaysWhatItsPlayerSees()
    {
        var (state, _) = MapGenerator.Build(BuiltInTemplates.Find("duel")!, 72, 5,
            [new PlayerSetup(0, "Blue", false, "haven"), new PlayerSetup(1, "Red", true, "haven")], Refog);
        var (session, host) = HostGame(state, remote: [Red]);
        var client = Join(host);
        var view = client.Session!.State;
        var ai = new AdventureAi(session);
        var red = new PlayerId(1);

        for (var day = 0; day < 6 && !session.State.IsOver; day++)
        {
            ai.PlayTurns(limit: 1);
            Pump(host, client);
            Check();
            client.Session!.Execute(new EndTurnCommand(red));
            Pump(host, client);
            Check();
        }

        void Check()
        {
            var host = session.State;
            Assert.Equal(host.Date, view.Date);
            Assert.Equal(host.CurrentPlayer.Id, view.CurrentPlayer.Id);
            Assert.Equal(host.GetPlayer(red).Resources, view.GetPlayer(red).Resources);
            Assert.Equal(host.Heroes.Where(h => host.CanSee(red, h)).Select(h => h.Id.Value).Order(), view.Heroes.Select(h => h.Id.Value).Order());
            foreach (var hero in view.Heroes)
            {
                Assert.Equal(host.GetHero(hero.Id).Position, hero.Position);
                Assert.Equal(host.GetHero(hero.Id).Army.ToString(), hero.Army.ToString());
            }

            Assert.All(view.Objects, o => Assert.NotNull(host.Memory.Get(red, o.Id)));
            Assert.Equal(host.Memory.Of(red).Select(k => k.Id.Value).Order(), view.Memory.Of(red).Select(k => k.Id.Value).Order());
            Assert.All(host.Memory.Of(red), k => Assert.Equal(k, view.Memory.Get(red, k.Id)));
        }
    }

    [Fact]
    public void AJoin_IsRefused_ForAnotherProtocol_OrWhenNoColourIsFree()
    {
        var (_, host) = HostGame(Create("A...B"), remote: [Red]);
        Join(host);

        var full = Connect(host, out var fullEnd);
        full.Join("Late");
        Pump(host, full);
        Assert.Equal("Every colour of this game is taken.", full.Failure);
        Assert.True(fullEnd.IsClosed);

        var (hostEnd, clientEnd) = LoopbackConnection.Pair();
        host.Accept(hostEnd);
        clientEnd.Send(new JoinRequest(NetSerializer.ProtocolVersion + 1, "Future"));
        host.Poll();
        Assert.True(clientEnd.TryReceive(out var refusal));
        Assert.Contains("protocol", Assert.IsType<JoinRefused>(refusal).Reason);
    }

    [Fact]
    public void APlayerWhoLeaves_FreesTheColour_AndJoinsAgainWithTheGameAsItStandsThen()
    {
        var (session, host) = HostGame(Create("A...B"), remote: [Red]);
        var left = new List<(PlayerId, string)>();
        host.Left += (seat, name, _) => left.Add((seat, name));
        var first = Join(host, "Bob");

        first.Dispose();
        session.Step(A, Direction.East);
        host.Poll();
        Assert.Equal([(Red, "Bob")], left);
        Assert.Null(host.PlayerAt(Red));

        var again = Join(host, "Bob", Red);
        Assert.Equal(new MapPosition(1, 0), again.Session!.State.GetHero(A).Position);
    }

    [Fact]
    public void AnIdleHost_SendsNoUpdates_UntilTheGameChanges()
    {
        var (session, host) = HostGame(Create("A...B"), remote: [Red]);
        var (hostEnd, clientEnd) = LoopbackConnection.Pair();
        host.Accept(hostEnd);
        var client = new GameClient(clientEnd);
        client.Join("Bob");
        Pump(host, client);

        for (var frame = 0; frame < 5; frame++)
        {
            host.Poll();
        }

        Assert.False(clientEnd.TryReceive(out var idle), $"sent {idle}");

        session.Execute(new StepHeroCommand(Blue, A, Direction.East)).ThrowIfRejected(); // the host's own player
        host.Poll();

        Assert.True(clientEnd.TryReceive(out var update));
        Assert.IsType<ViewUpdate>(update);
    }

    [Fact]
    public void AClientThatMissesAnUpdate_StopsInsteadOfShowingAnotherGame()
    {
        var (_, host) = HostGame(Create("A...B"), remote: [Red]);
        var (hostEnd, clientEnd) = LoopbackConnection.Pair();
        host.Accept(hostEnd);
        var client = new GameClient(clientEnd);
        client.Join("Bob");
        Pump(host, client);

        hostEnd.Send(new ViewUpdate(5, [], [], null, []));
        client.Poll();

        Assert.Contains("this computer has 1", client.Failure);
        Assert.False(client.Session!.Execute(new EndTurnCommand(Red)).IsAccepted);
    }

    [Fact]
    public void OverTcp_AClientOnThisComputer_PlaysWithTheHost()
    {
        var (session, host) = HostGame(Create("A...B"), remote: [Red]);
        using var listener = new TcpGameListener(port: 0, loopbackOnly: true);
        using var connection = TcpConnection.Connect("127.0.0.1", listener.Port, TimeSpan.FromSeconds(5));
        var client = new GameClient(connection);
        client.Join("Bob");

        Until(() =>
        {
            while (listener.TryAccept(out var accepted))
            {
                host.Accept(accepted);
            }

            Pump(host, client);
            return client.Session is not null;
        });
        session.EndTurn();
        Until(() =>
        {
            Pump(host, client);
            return client.Session!.State.CurrentPlayer.Id == Red;
        });
        client.Session!.Execute(new StepHeroCommand(Red, B, Direction.West));
        Until(() =>
        {
            Pump(host, client);
            return client.Session.State.GetHero(B).Position == new MapPosition(3, 0);
        });

        Assert.Equal(new MapPosition(3, 0), session.State.GetHero(B).Position);
    }

    /// <summary>Starts the game the way the game app does, and hosts it.</summary>
    private static (GameSession Session, GameHost Host) HostGame(GameState state, PlayerId[] remote)
    {
        GameSession.CheckRefusals = true;
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

    private static GameClient Connect(GameHost host, out LoopbackConnection clientEnd)
    {
        var (hostEnd, end) = LoopbackConnection.Pair();
        host.Accept(hostEnd);
        clientEnd = end;
        return new GameClient(end);
    }

    private static GameClient Join(GameHost host, string name = "Bob", PlayerId? seat = null)
    {
        var client = Connect(host, out _);
        client.Join(name, seat);
        Pump(host, client);
        Assert.True(client.Failure is null, client.Failure);
        Assert.NotNull(client.Session);
        return client;
    }

    /// <summary>Lets the messages go to and fro until nothing is left in flight (loopback links deliver at once).</summary>
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

    private static void Until(Func<bool> condition)
    {
        var clock = Stopwatch.StartNew();
        while (!condition())
        {
            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(10), "The messages did not arrive in time.");
            Thread.Sleep(5);
        }
    }
}
