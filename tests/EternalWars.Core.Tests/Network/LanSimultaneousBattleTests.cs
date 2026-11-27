using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Events;
using EternalWars.Core.Network;
using EternalWars.Core.Tests.Domain;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Network;

/// <summary>
/// A LAN game's simultaneous days with battles at the same time (ADR-366): each person fights on its own computer while the other plays on,
/// each client is sent only its own battle and question, and a conflict while battles are open has the host play the day again without them.
/// </summary>
public class LanSimultaneousBattleTests
{
    private static readonly GameOptions Options = new(FogMode.ShroudWithRefog) { SimultaneousFirstWeek = true, HostPlayer = Blue };

    [Fact]
    public void EachPerson_FightsItsBattleOnItsOwnComputer_AtTheSameTime_AndIsSentOnlyItsOwn()
    {
        var state = SimultaneousBattleTests.Fights(Options);
        var initial = MapConverter.ToDocument(state);
        var session = new GameSession(state);
        session.Start();
        var host = new GameHost(session, [Red]);
        host.RecordOf = name => GameReplay.Record(initial, host.Session, name);
        var red = Join(host, "Bob", Red);
        var redHeard = new List<IEvent>();
        red.Session!.Events.Subscribe<BattleStarted>(redHeard.Add);
        red.Session.Events.Subscribe<BattleEnded>(redHeard.Add);
        red.Session.Events.Subscribe<DecisionRequested>(redHeard.Add);

        // Blue fights on the host: Red's computer is shown nothing of it, and Red plays on.
        session.Execute(new StepHeroCommand(Blue, A, Direction.East)).ThrowIfRejected();
        Pump(host, red);
        Assert.NotNull(state.BattleOf(Blue));
        Assert.Null(red.Session.State.Battle);
        Assert.Empty(redHeard);

        red.Session.Execute(new StepHeroCommand(Red, B, Direction.West)).ThrowIfRejected();
        Pump(host, red);
        var reds = state.BattleOf(Red);
        Assert.NotNull(reds);
        Assert.Same(state.BattleOf(Blue), state.Battle); // the host shows its own
        Assert.NotNull(red.Session.State.Battle); // and the client its own
        Assert.Equal(reds.Units.Count, red.Session.State.Battle.Units.Count);
        Assert.IsType<BattleDecision>(red.Session.State.PendingDecision);
        Assert.Equal(2, redHeard.Count); // its own battle and its question, not Blue's

        // Each side's commands, from each computer, in any order.
        red.Session.Execute(new BattleDefendCommand(Red)).ThrowIfRejected();
        Pump(host, red);
        session.Execute(new BattleDefendCommand(Blue)).ThrowIfRejected();
        red.Session.Execute(new AutoResolveBattleCommand(Red)).ThrowIfRejected();
        Pump(host, red);
        Assert.Null(state.BattleOf(Red));
        Assert.Null(red.Session.State.Battle);
        Assert.NotNull(state.BattleOf(Blue));
        Assert.Single(redHeard.OfType<BattleEnded>());

        AnswerLevelUps(host, red);
        red.Session.Execute(new EndTurnCommand(Red)).ThrowIfRejected();
        Pump(host, red);
        Assert.Equal([Blue], state.OpenTurns);

        session.Execute(new AutoResolveBattleCommand(Blue)).ThrowIfRejected();
        SimultaneousBattleTests.AnswerLevelUps(session, Blue);
        session.Execute(new EndTurnCommand(Blue)).ThrowIfRejected();
        Pump(host, red);
        Assert.Equal(2, red.Session.State.Date.AbsoluteDay);

        // The host's record replays to its state.
        var replayed = GameReplay.Replay(host.RecordOf(null));
        Assert.Equal(GameStateDigest.Describe(state), GameStateDigest.Describe(replayed.State));
    }

    [Fact]
    public void AConflictWhileABattleIsOpen_HasTheHostPlayTheDayAgain_WithoutTheBattle()
    {
        var state = SimultaneousBattleTests.Fights(Options, zoneWidth: 8, gap: 1);
        state.RemoveObject(state.Objects.OfType<MonsterGuard>().Single(m => m.Position.X > 1)); // Red only walks
        var monster = state.Objects.OfType<MonsterGuard>().Single().Id;
        var initial = MapConverter.ToDocument(state);
        var session = new GameSession(state);
        session.Start();
        var host = new GameHost(session, [Red]);
        host.RecordOf = name => GameReplay.Record(initial, host.Session, name);
        GameSession? replaced = null;
        host.SessionReplaced += s => replaced = s;
        var red = Join(host, "Bob", Red);

        session.Execute(new StepHeroCommand(Blue, A, Direction.East)).ThrowIfRejected();
        session.Execute(new BattleDefendCommand(Blue)).ThrowIfRejected();
        Assert.NotNull(state.BattleOf(Blue));
        for (var i = 0; i < 12 && replaced is null; i++)
        {
            red.Session!.Execute(new StepHeroCommand(Red, B, Direction.West)); // towards Blue's zone, while Blue fights: a conflict
            Pump(host, red);
        }

        Assert.NotNull(replaced);
        var again = replaced!.State;
        Assert.False(again.IsSimultaneous);
        Assert.Empty(again.OpenBattles); // the battle went with the day
        Assert.NotNull(again.FindObject(monster));
        Assert.Equal(Blue, again.CurrentPlayer.Id);
        Assert.Equal(new MapPosition(0, 0), again.GetHero(A).Position);

        // The day goes on turn by turn: Blue fights its battle again in its own turn.
        replaced.Execute(new StepHeroCommand(Blue, A, Direction.East)).ThrowIfRejected();
        Assert.NotNull(again.Battle);
        Assert.StartsWith("It is not", red.Session!.Execute(new StepHeroCommand(Red, B, Direction.West)).Error);
    }

    /// <summary>The client answers each level-up it is asked with the first skill.</summary>
    internal static void AnswerLevelUps(GameHost host, GameClient client)
    {
        var player = client.Seat!.Value;
        for (var i = 0; i < 8 && client.Session!.State.PendingDecision is LevelUpDecision levelUp; i++)
        {
            client.Session.Execute(new ChooseSkillCommand(player, levelUp.Options[0])).ThrowIfRejected();
            Pump(host, client);
        }
    }

    internal static GameClient Join(GameHost host, string name, PlayerId seat)
    {
        var (hostEnd, clientEnd) = LoopbackConnection.Pair();
        host.Accept(hostEnd);
        var client = new GameClient(clientEnd);
        client.Join(name, seat);
        Pump(host, client);
        Assert.True(client.Failure is null, client.Failure);
        return client;
    }

    internal static void Pump(GameHost host, params GameClient[] clients)
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
