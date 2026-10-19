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
using EternalWars.Core.Domain.Heroes;
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
        var pile = Add<ResourcePile>(state, BuiltInMapObjects.PileId(ResourceType.Gold), 3, 0);
        var session = Start(state);

        Ai(session).PlayTurn();

        Assert.Equal(pile.Amount, state.GetPlayer(Blue).Resources.Gold);
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
    public void ItFlagsAMapDwelling_AndBuysCreaturesThere()
    {
        var state = Create("A.......B", ".........", ".........", ".........");
        var dwelling = Add<CreatureDwelling>(state, BuiltInMapObjects.Dwelling, 3, 0); // entrance (4, 1)
        dwelling.Creature = BuiltInCreatures.Footman;
        var session = Start(state);
        state.GetPlayer(Blue).Resources = new ResourceSet(Gold: 20_000);
        var ai = Ai(session);

        for (var turn = 0; turn < 8 && dwelling.Owner is null; turn++)
        {
            ai.PlayTurn(); // Blue and Red in turn
        }

        Assert.Equal(Blue, dwelling.Owner);
        ai.PlayTurn();
        Assert.Contains(session.CommandLog, c => c is RecruitAtDwellingCommand);
        Assert.True(state.GetHero(A).Army.Stacks.Sum(s => s.Stack.Count) > 10);
        Assert.Equal(0, ai.Refused);
    }

    [Fact]
    public void ATownWithACaravan_HiresFromOwnedDwellings_AndSendsTroopsToAnEmptyTown()
    {
        var state = Create(WideMap());
        var home = AddTown(state, TownIds.Haven, 0, 0, Blue);
        var outpost = AddTown(state, TownIds.Haven, 12, 0, Blue);
        home.SetLevel(BuiltInBuildings.Caravan, 1);
        home.SetLevel(BuiltInBuildings.Tavern, 0); // no hero is hired to take the garrison along
        outpost.SetLevel(BuiltInBuildings.Tavern, 0);
        outpost.LastBuilt = state.Date; // it builds nothing today, so it stays empty
        home.Garrison.Set(0, new CreatureStack(BuiltInCreatures.Footman, 5));
        home.Garrison.Set(1, new CreatureStack(BuiltInCreatures.Wolf, 3));
        var dwelling = Add<CreatureDwelling>(state, BuiltInMapObjects.Dwelling, 25, 3);
        dwelling.Creature = BuiltInCreatures.Footman;
        dwelling.Owner = Blue;
        var session = Start(state);
        state.GetPlayer(Blue).Resources = new ResourceSet(Gold: 50_000, Lumber: 50, Ore: 50);
        state.GetHero(A).Position = new MapPosition(38, 8); // far from both towns: the towns act, not the hero

        var ai = Ai(session);
        ai.PlayTurn();

        Assert.Contains(session.CommandLog, c => c is OrderCaravanCommand);
        var sent = Assert.Single(session.CommandLog.OfType<SendTroopsCommand>());
        Assert.Equal((home.Id, outpost.Id), (sent.FromTown, sent.ToTown));
        Assert.Equal(0, ai.Refused);
    }

    [Fact]
    public void ItBuildsACaravan_OnceItOwnsADwelling()
    {
        var state = Create(WideMap());
        var town = AddTown(state, TownIds.Haven, 0, 0, Blue);
        foreach (var building in new[] { BuiltInBuildings.Blacksmith, BuiltInBuildings.Marketplace, BuiltInBuildings.MageGuild })
        {
            town.SetLevel(building, 1);
        }

        town.SetLevel(BuiltInBuildings.TownHall, 2);
        foreach (var tier in new[] { 1, 2, 3, 4, 5 })
        {
            var line = LinesOf(state, town).First(l => l.Tier == tier);
            town.SetDwelling(line.Id, 1);
        }

        var dwelling = Add<CreatureDwelling>(state, BuiltInMapObjects.Dwelling, 25, 3);
        dwelling.Owner = Blue;
        var session = Start(state);
        state.GetPlayer(Blue).Resources = new ResourceSet(Gold: 100_000, Lumber: 100, Ore: 100, Crystal: 50, Mercury: 50, Sulfur: 50);

        var ai = Ai(session);
        for (var turn = 0; turn < 16 && town.Level(BuiltInBuildings.Caravan) == 0; turn++)
        {
            ai.PlayTurn();
        }

        Assert.Equal(1, town.Level(BuiltInBuildings.Caravan));
    }

    /// <summary>40 × 12 grass: Alpha at (3, 7), Bravo in the far corner.</summary>
    private static string[] WideMap() =>
        Enumerable.Range(0, 12).Select(y => string.Concat(Enumerable.Range(0, 40).Select(x => x == 3 && y == 7 ? 'A' : x == 39 && y == 11 ? 'B' : '.'))).ToArray();

    private static IReadOnlyList<CreatureDefinition> LinesOf(GameState state, Town town) => TownRules.Lines(state, town);

    private static string Shooter => CreatureCatalog.Default.All.First(c => c.Town == TownIds.Haven && c.Shots > 0 && c.UpgradesTo is not null).Id;

    [Fact]
    public void ItBuysATentAndACartForShooters_AndItsOffensiveMachineBackOnlyWithAdvancedBallistics()
    {
        foreach (var ballistics in new[] { SkillLevel.None, SkillLevel.Advanced })
        {
            var state = Create("..........", "..........", "..........", "..........", "..........", "..........", "...A......", ".........B");
            var town = AddTown(state, TownIds.Haven, 0, 0, Blue);
            town.SetLevel(BuiltInBuildings.Blacksmith, 1);
            town.LastBuilt = state.Date;
            var hero = state.GetHero(A);
            hero.Army.Set(1, new CreatureStack(Shooter, 10));
            hero.SetWarMachines([]); // lost its offensive machine
            hero.Skills.Set(BuiltInSkills.Ballistics, ballistics);
            var session = Start(state);
            state.GetPlayer(Blue).Resources = new ResourceSet(Gold: 20_000);

            var ai = Ai(session);
            ai.PlayTurn();

            Assert.Contains(WarMachineSlot.Tent, hero.WarMachines);
            Assert.Contains(WarMachineSlot.Cart, hero.WarMachines);
            Assert.Equal(ballistics == SkillLevel.Advanced, hero.WarMachines.Contains(WarMachineSlot.Offensive));
            Assert.Equal(0, ai.Refused);
        }
    }

    [Fact]
    public void AHeroWithStewardship_IsAppointedGovernorOfAnUngovernedTown()
    {
        var state = Create("..........", "..........", "..........", "..........", "..........", "..........", "..........", "A....C...B");
        var town = AddTown(state, TownIds.Haven, 0, 0, Blue);
        state.GetHero(C).Skills.Set(BuiltInSkills.Stewardship, SkillLevel.Basic);
        var session = Start(state);

        var ai = Ai(session);
        ai.PlayTurn();

        Assert.Equal(C, town.Governor);
        Assert.Contains(session.CommandLog, c => c is AppointGovernorCommand);
        Assert.Equal(0, ai.Refused);
    }

    /// <summary>A town at the left with its entrance at (3, 6); Blue's hero A at (12, 6); Red's hero B at (16, 6), a day's march from the town.</summary>
    private static (GameState State, Town Town) Threatened(int heroFootmen, int garrisonFootmen, int enemyFootmen)
    {
        var state = Create(
            "....................", "....................", "....................", "....................", "....................", "....................",
            "............A...B...", "....................");
        var town = AddTown(state, TownIds.Haven, 0, 0, Blue);
        town.SetLevel(BuiltInBuildings.Tavern, 0);
        town.LastBuilt = state.Date;
        town.Garrison.Set(0, new CreatureStack(BuiltInCreatures.Footman, garrisonFootmen));
        state.GetHero(A).Army.Set(0, new CreatureStack(BuiltInCreatures.Footman, heroFootmen));
        state.GetHero(B).Army.Set(0, new CreatureStack(BuiltInCreatures.Footman, enemyFootmen));
        return (state, town);
    }

    [Fact]
    public void AHero_GoesHomeToATownAnEnemyThreatens_AndStaysThere()
    {
        // The garrison (40) alone cannot hold against 100, the hero (50) cannot beat them alone, together they can.
        var (state, town) = Threatened(heroFootmen: 50, garrisonFootmen: 40, enemyFootmen: 100);
        Add<ResourcePile>(state, BuiltInMapObjects.PileId(ResourceType.Gold), 9, 2);
        var session = Start(state);
        var ai = Ai(session);

        ai.PlayTurn();
        Assert.Equal(town.VisitPosition, state.GetHero(A).Position);

        session.Execute(new EndTurnCommand(Red)).ThrowIfRejected();
        ai.PlayTurn();
        Assert.Equal(town.VisitPosition, state.GetHero(A).Position); // the pile can wait
        Assert.Contains(state.Objects, o => o is ResourcePile);
        Assert.Equal(0, ai.Refused);
    }

    [Fact]
    public void AHero_StaysOutOfReachOfAStrongerEnemy()
    {
        var state = Create("A....p.....B");
        Add<ResourcePile>(state, BuiltInMapObjects.PileId(ResourceType.Gold), 5, 0);
        state.GetHero(B).Army.Set(0, new CreatureStack(BuiltInCreatures.Champion, 30));
        var session = Start(state);

        Ai(session).PlayTurn();

        Assert.Contains(state.Objects, o => o is ResourcePile); // the pile is in the champions' reach
        Assert.Equal(new MapPosition(0, 0), state.GetHero(A).Position);
    }

    [Fact]
    public void ASecondaryHero_BringsItsTroopsToTheMainHero()
    {
        var state = Create("A.....C..#B");
        state.GetHero(A).Army.Set(0, new CreatureStack(BuiltInCreatures.Champion, 10));
        var footmen = (int)Math.Max(30, 1500 / CreatureCatalog.Default.Get(BuiltInCreatures.Footman).Value);
        state.GetHero(C).Army.Set(0, new CreatureStack(BuiltInCreatures.Footman, footmen));
        var session = Start(state);

        var ai = Ai(session);
        ai.PlayTurn();

        Assert.Contains(state.GetHero(A).Army.Stacks, s => s.Stack.Creature == BuiltInCreatures.Footman && s.Stack.Count == footmen - 1);
        Assert.Equal(1, state.GetHero(C).Army.Stacks.Sum(s => s.Stack.Count)); // it keeps one creature
        Assert.Null(state.PendingDecision);
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
