using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.AI;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Core.Domain.Towns;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The adventure AI (ADR-059): it plays through commands only, deterministically, and never gets refused.</summary>
public class AdventureAiTests
{
    private static AdventureAi Ai(GameSession session) => new(session, controlHumanSlots: true);

    private static MonsterGuard Monster(GameState state, int x, int y, string creature, int count, GuardDisposition disposition = GuardDisposition.Savage)
    {
        var monster = Add<MonsterGuard>(state, BuiltInMapObjects.Monster, x, y);
        (monster.Creature, monster.Count, monster.Disposition) = (creature, count, disposition);
        return monster;
    }

    private static GameState TownMap(out Town town)
    {
        var state = Create("..........", "..........", "..........", "..........", "..........", "..........", "..........", "...A.....B");
        town = AddTown(state, TownIds.Haven, 0, 0);
        return state;
    }

    [Fact]
    public void ThePlayerTurn_EndsWithAnEndTurnCommand_AndPassesTheTurn()
    {
        var session = Start(Create("A.B"));
        var ai = Ai(session);

        Assert.True(ai.PlayTurn());

        Assert.IsType<EndTurnCommand>(session.CommandLog[^1]);
        Assert.Equal(Red, session.State.CurrentPlayer.Id);
        Assert.Equal(0, ai.Refused);
    }

    [Fact]
    public void ItDoesNotPlayHumanSlots_UnlessToldTo()
    {
        var session = Start(Create("A.B"));

        Assert.False(new AdventureAi(session).PlayTurn());
        Assert.False(AdventureAi.IsComputerTurn(session.State));
        session.State.GetPlayer(Blue).IsHuman = false;
        Assert.True(AdventureAi.IsComputerTurn(session.State));
        Assert.True(new AdventureAi(session).PlayTurn());
    }

    [Fact]
    public void ItCollectsAPickup()
    {
        var state = Create("A.......B");
        Add<ResourcePile>(state, BuiltInMapObjects.PileId(ResourceType.Gold), 3, 0);
        var session = Start(state);

        Ai(session).PlayTurn();

        Assert.Equal(750, state.GetPlayer(Blue).Resources.Gold);
        Assert.DoesNotContain(state.Objects, o => o is ResourcePile);
    }

    [Fact]
    public void ItCapturesAnUndefendedTown()
    {
        var state = TownMap(out var town);
        var session = Start(state);

        Ai(session).PlayTurn();

        Assert.Equal(Blue, town.Owner);
    }

    [Fact]
    public void ItAttacksAMonsterItCanBeat_AndLeavesOneItCannot()
    {
        var weak = Create("A..W");
        Monster(weak, 3, 0, BuiltInCreatures.Wolf, 3);
        weak.GetHero(A).Army.Set(0, new CreatureStack(BuiltInCreatures.Champion, 10));
        var weakSession = Start(weak);
        Ai(weakSession).PlayTurn();
        Assert.DoesNotContain(weak.Objects, o => o is MonsterGuard);

        var strong = Create("A..W");
        var dragon = Monster(strong, 3, 0, BuiltInCreatures.Champion, 40);
        var session = Start(strong);
        var ai = Ai(session);
        for (var i = 0; i < 6; i++)
        {
            ai.PlayTurn();
        }

        Assert.Contains(strong.Objects, o => o == dragon);
        Assert.Contains(strong.Heroes, h => h.Id == A);
    }

    [Fact]
    public void ItTakesFreeTroopsFromAFriendlyMonster()
    {
        var state = Create("A..W");
        Monster(state, 3, 0, BuiltInCreatures.Wolf, 5, GuardDisposition.Compliant);
        var session = Start(state);

        Ai(session).PlayTurn();

        Assert.Contains(state.GetHero(A).Army.Stacks, s => s.Stack.Creature == BuiltInCreatures.Wolf);
        Assert.Null(state.PendingDecision);
    }

    [Fact]
    public void ItAnswersAChest_WithoutLeavingADecisionPending()
    {
        var state = Create("A..");
        Add<TreasureChest>(state, BuiltInMapObjects.TreasureChest, 1, 0);
        var session = Start(state);

        Assert.True(Ai(session).PlayTurn());

        Assert.Null(state.PendingDecision);
        Assert.DoesNotContain(state.Objects, o => o is TreasureChest);
    }

    [Fact]
    public void ItBuildsAndRecruitsInItsTown()
    {
        var state = TownsScenario.Create();
        var session = Start(state);
        var gold = state.GetPlayer(Blue).Resources.Gold;
        var ai = Ai(session);

        ai.PlayTurn();

        Assert.Contains(session.CommandLog, c => c is BuildDwellingCommand);
        Assert.Contains(session.CommandLog, c => c is RecruitCommand);
        Assert.True(state.GetPlayer(Blue).Resources.Gold < gold || gold == 0);
        Assert.Equal(0, ai.Refused);
    }

    [Fact]
    public void ItNeverAttacksWhenItWouldLose()
    {
        // Equal armies: the fight is not safe, so the heroes keep away from each other.
        var state = Create("A.B");
        var session = Start(state);
        var ai = Ai(session);

        for (var i = 0; i < 6; i++)
        {
            ai.PlayTurn();
        }

        Assert.DoesNotContain(session.CommandLog, c => c is AutoResolveBattleCommand);
        Assert.Equal(2, state.Heroes.Count);
    }

    [Fact]
    public void ItAttacksAnEnemyHeroItClearlyBeats()
    {
        var state = Create("A.B");
        state.GetHero(A).Army.Set(0, new CreatureStack(BuiltInCreatures.Champion, 10));
        var session = Start(state);

        Ai(session).PlayTurn();

        Assert.DoesNotContain(state.Heroes, h => h.Id == B);
        Assert.True(state.IsOver);
    }

    [Fact]
    public void ItWaitsForAHumanWhoCommandsTheOtherSideOfABattle()
    {
        var state = Create("A.B");
        state.GetHero(A).Army.Set(0, new CreatureStack(BuiltInCreatures.Champion, 10));
        state.GetPlayer(Blue).IsHuman = false; // Red stays human
        var session = Start(state);
        var ai = new AdventureAi(session);

        Assert.False(ai.PlayTurn());
        Assert.NotNull(session.State.Battle);

        session.AutoResolve(); // the human has its say
        Assert.Null(session.State.Battle);
        Assert.True(ai.PlayTurn() || session.State.IsOver);
    }

    [Fact]
    public void AfterTheGameEnds_ItPlaysNothing()
    {
        var state = Create("A.B");
        state.GetHero(A).Army.Set(0, new CreatureStack(BuiltInCreatures.Champion, 10));
        var session = Start(state);
        var ai = Ai(session);

        ai.PlayTurn();

        Assert.True(state.IsOver);
        Assert.False(ai.IsMyTurn);
        Assert.False(ai.PlayTurn());
    }

    [Theory]
    [InlineData(FogMode.PermanentReveal)]
    [InlineData(FogMode.ShroudWithRefog)]
    public void TwoAisPlayAWholeScenario_WithoutAnyRefusedCommand_AndTheGameReplays(FogMode fog)
    {
        var options = new GameOptions(FogMode: fog, Seed: 5);
        var document = MapConverter.ToDocument(TownsScenario.Create());
        var session = new GameSession(MapConverter.ToGameState(document, options: options));
        var ai = Ai(session);
        session.Start();

        while (!session.State.IsOver && session.State.Date.AbsoluteDay <= 30)
        {
            Assert.True(ai.PlayTurn(), $"The AI got stuck on {session.State.Date}.");
        }

        Assert.Equal(0, ai.Refused);
        Assert.True(ai.Commands > 20);
        var replayed = GameReplay.Replay(GameRecordSerializer.Deserialize(GameRecordSerializer.Serialize(GameReplay.Record(document, session))));
        Assert.Equal(GameStateDigest.Compute(session.State), GameStateDigest.Compute(replayed.State));
    }

    [Fact]
    public void TheSameGame_PlaysTheSameWay()
    {
        static string Play()
        {
            var session = new GameSession(TownsScenario.Create());
            var ai = Ai(session);
            session.Start();
            while (!session.State.IsOver && session.State.Date.AbsoluteDay <= 20)
            {
                ai.PlayTurn();
            }

            return GameStateDigest.Compute(session.State);
        }

        Assert.Equal(Play(), Play());
    }

    [Fact]
    public void PlayTurns_PlaysUntilItIsAHumansTurn()
    {
        var state = Create("A.B");
        state.GetPlayer(Red).IsHuman = false;
        state.GetPlayer(Blue).IsHuman = false;
        var session = Start(state);

        Assert.Equal(4, new AdventureAi(session).PlayTurns(limit: 4));
        Assert.Equal(3, session.State.Date.AbsoluteDay);

        state.GetPlayer(Red).IsHuman = true;
        var turns = new AdventureAi(session).PlayTurns();
        Assert.Equal(1, turns); // Blue plays, then it is Red's turn
        Assert.Equal(Red, session.State.CurrentPlayer.Id);
    }
}
