using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Magic;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Tests.Data;

/// <summary>Seed + map + command log reproduce a game exactly (ADR-036).</summary>
public class ReplayTests
{
    [Fact]
    public void RandomGenerator_IsReproducible_AndStaysInRange()
    {
        var a = new GameRandom(42);
        var b = new GameRandom(42);
        var values = Enumerable.Range(0, 1000).Select(_ => a.NextInclusive(3, 7)).ToList();

        Assert.Equal(values, Enumerable.Range(0, 1000).Select(_ => b.NextInclusive(3, 7)));
        Assert.All(values, v => Assert.InRange(v, 3, 7));
        Assert.Equal([3, 4, 5, 6, 7], values.Distinct().Order());
        Assert.Equal(a.State, b.State);
    }

    [Fact]
    public void RefusedCommands_AreNotLogged()
    {
        var session = new GameSession(DemoScenario.Create());
        session.Start();

        Assert.False(session.Execute(new EndTurnCommand(new PlayerId(1))).IsAccepted); // not Red's turn
        Assert.False(session.Execute(new BattleWaitCommand(new PlayerId(0))).IsAccepted); // no battle
        Assert.True(session.Execute(new EndTurnCommand(new PlayerId(0))).IsAccepted);

        Assert.Equal([new EndTurnCommand(new PlayerId(0))], session.CommandLog);
    }

    [Fact]
    public void RecordedGame_ReplaysToTheSameState_ThroughJson()
    {
        var map = MapConverter.ToDocument(DemoScenario.Create());
        var options = new GameOptions(Seed: 2024);
        var session = new GameSession(MapConverter.ToGameState(map, options: options));
        session.Start();
        PlayScriptedDays(session, days: 4);

        var json = GameRecordSerializer.Serialize(GameReplay.Record(map, session));
        var replayed = GameReplay.Replay(GameRecordSerializer.Deserialize(json));

        Assert.Contains("\"type\": \"move\"", json);
        Assert.True(session.CommandLog.Count > 8);
        Assert.Equal(GameStateDigest.Describe(session.State), GameStateDigest.Describe(replayed.State));
    }

    [Fact]
    public void ASiege_ReplaysExactly_WithTheAimedEngine_ThroughJson()
    {
        var state = TestWorld.Create([.. Enumerable.Repeat("..........", 7), "...A......", "..........", ".........B"]);
        var town = TestWorld.AddTown(state, TownIds.Haven, 0, 0, TestWorld.Red);
        town.SetLevel(BuiltInBuildings.Fort, 3);
        town.Garrison.Set(0, new CreatureStack(BuiltInCreatures.Archer, 6));
        state.GetHero(TestWorld.A).Army.Set(0, new CreatureStack(BuiltInCreatures.Footman, 100)); // lasts through the towers' shots
        state.GetHero(TestWorld.A).Skills.Set(BuiltInSkills.Ballistics, SkillLevel.Basic);
        var map = MapConverter.ToDocument(state);
        var session = new GameSession(MapConverter.ToGameState(map, options: new GameOptions(Seed: 31)));
        session.Start();

        session.Execute(new StepHeroCommand(TestWorld.Blue, TestWorld.A, Direction.North)).ThrowIfRejected();
        var aimed = 0;
        for (var turn = 0; turn < 12 && session.State.Battle is { Siege: { } siege } battle; turn++)
        {
            var active = battle.ActiveUnit!;
            var player = battle.Participant(active.Side).Owner!.Value;
            GameCommand command = active.IsMachine && siege.Find(5) is { IsStanding: true }
                ? new BattleShootStructureCommand(player, 5)
                : new BattleDefendCommand(player);
            aimed += command is BattleShootStructureCommand ? 1 : 0;
            session.Execute(command).ThrowIfRejected();
        }

        var json = GameRecordSerializer.Serialize(GameReplay.Record(map, session));
        var replayed = GameReplay.Replay(GameRecordSerializer.Deserialize(json));

        Assert.True(aimed > 0);
        Assert.Contains("\"type\": \"battleShootStructure\"", json);
        Assert.Contains("siege 5 Gate", GameStateDigest.Describe(session.State));
        Assert.Equal(GameStateDigest.Describe(session.State), GameStateDigest.Describe(replayed.State));
    }

    [Fact]
    public void ABoughtSpellBook_TheGuildsSpells_AndACast_ReplayExactly_ThroughJson()
    {
        var state = TestWorld.Create([.. Enumerable.Repeat("..........", 7), "...A......", "..........", ".........B"]);
        var town = TestWorld.AddTown(state, TownIds.Haven, 0, 0, TestWorld.Blue);
        town.SetLevel(BuiltInBuildings.MageGuild, 1);
        state.GetHero(TestWorld.A).Skills.Set(MagicRules.MagicSkill(TownIds.Haven), SkillLevel.Basic);
        state.GetHero(TestWorld.A).Army.Set(0, new CreatureStack(BuiltInCreatures.Footman, 100));
        var monster = TestWorld.Add<MonsterGuard>(state, BuiltInMapObjects.Monster, 3, 9);
        (monster.Creature, monster.Count, monster.Disposition) = (BuiltInCreatures.Troll, 4, GuardDisposition.Savage);
        var map = MapConverter.ToDocument(state);
        var session = new GameSession(MapConverter.ToGameState(map, options: new GameOptions(Seed: 41)));
        session.Start();
        var blue = TestWorld.Blue;
        var placed = session.State.ObjectsOf<Town>().Single();

        session.Execute(new StepHeroCommand(blue, TestWorld.A, Direction.North)).ThrowIfRejected();
        session.Execute(new BuySpellBookCommand(blue, placed.Id)).ThrowIfRejected();
        session.Execute(new StepHeroCommand(blue, TestWorld.A, Direction.South)).ThrowIfRejected();
        session.Execute(new StepHeroCommand(blue, TestWorld.A, Direction.South)).ThrowIfRejected();
        var battle = session.State.Battle!;
        var buff = battle.Attacker.Caster!.Spells.First(s => s.Spell.Effect is { Kind: SpellEffectKind.Buff, Reach: SpellReach.Stack, Filter: SpellFilter.None });
        session.Execute(new BattleCastCommand(blue, buff.Spell.Id, battle.ActiveUnit!.Id)).ThrowIfRejected(); // the trolls are the AI's
        session.Execute(new AutoResolveBattleCommand(blue)).ThrowIfRejected();

        var json = GameRecordSerializer.Serialize(GameReplay.Record(map, session));
        var replayed = GameReplay.Replay(GameRecordSerializer.Deserialize(json));

        Assert.Contains("\"type\": \"buySpellBook\"", json);
        Assert.Contains("\"type\": \"battleCast\"", json);
        Assert.Contains(" book light.", GameStateDigest.Describe(session.State));
        Assert.Equal(GameStateDigest.Describe(session.State), GameStateDigest.Describe(replayed.State));
    }

    [Fact]
    public void BattleCommands_ReplayExactly()
    {
        var state = TestWorld.Create(".A.....", ".......", "......B");
        state.GetHero(TestWorld.A).Army.Set(1, new CreatureStack(BuiltInCreatures.Archer, 15));
        var monster = TestWorld.Add<MonsterGuard>(state, BuiltInMapObjects.Monster, 2, 1);
        (monster.Creature, monster.Count, monster.Disposition) = (BuiltInCreatures.Troll, 4, GuardDisposition.Savage);
        var map = MapConverter.ToDocument(state);
        var session = new GameSession(MapConverter.ToGameState(map, options: new GameOptions(Seed: 77)));
        session.Start();
        var blue = new PlayerId(0);

        session.Execute(new StepHeroCommand(blue, TestWorld.A, Direction.SouthEast)).ThrowIfRejected();
        for (var turn = 0; turn < 3 && session.State.Battle is { } battle; turn++)
        {
            var active = battle.ActiveUnit!;
            var enemy = battle.LivingUnitsOf(BattleSide.Defender).First();
            GameCommand command = BattleGrid.CanShoot(battle, active, enemy, out _)
                ? new BattleShootCommand(blue, enemy.Id)
                : new BattleDefendCommand(blue);
            session.Execute(command).ThrowIfRejected();
        }

        if (session.State.Battle is not null)
        {
            session.Execute(new AutoResolveBattleCommand(blue)).ThrowIfRejected();
        }

        var replayed = GameReplay.Replay(GameRecordSerializer.Deserialize(GameRecordSerializer.Serialize(GameReplay.Record(map, session))));

        Assert.Contains(session.CommandLog, c => c is BattleShootCommand);
        Assert.Equal(GameStateDigest.Describe(session.State), GameStateDigest.Describe(replayed.State));
    }

    [Fact]
    public void Replay_DetectsADifferentOutcome()
    {
        var map = MapConverter.ToDocument(DemoScenario.Create());
        var session = new GameSession(MapConverter.ToGameState(map));
        session.Start();
        PlayScriptedDays(session, days: 2);
        var record = GameReplay.Record(map, session) with { FinalDigest = "0000000000000000" };

        Assert.Throws<InvalidOperationException>(() => GameReplay.Replay(record));
    }

    /// <summary>Every hero walks towards the map centre each day; decisions and battles are resolved by commands.</summary>
    private static void PlayScriptedDays(GameSession session, int days)
    {
        var state = session.State;
        var centre = new MapPosition(state.Map.Width / 2, (state.Map.Height / 2) + 1);
        for (var turn = 0; turn < days * state.Players.Count; turn++)
        {
            var player = state.CurrentPlayer.Id;
            foreach (var hero in state.HeroesOf(player).ToList())
            {
                if (state.Heroes.Contains(hero))
                {
                    session.Execute(new MoveHeroCommand(player, hero.Id, centre));
                    Settle(session);
                }
            }

            Settle(session);
            session.Execute(new EndTurnCommand(state.CurrentPlayer.Id)).ThrowIfRejected();
        }
    }

    private static void Settle(GameSession session)
    {
        var state = session.State;
        for (var guard = 0; guard < 10 && state.PendingDecision is { } decision; guard++)
        {
            var player = state.CurrentPlayer.Id;
            GameCommand command = decision switch
            {
                EternalWars.Core.Domain.Decisions.TreasureChestDecision => new ResolveTreasureChestCommand(player, EternalWars.Core.Domain.Decisions.TreasureChestReward.Gold),
                EternalWars.Core.Domain.Decisions.HeroExchangeDecision => new CompleteExchangeCommand(player),
                EternalWars.Core.Domain.Decisions.GuardEncounterDecision => new RespondToGuardCommand(player, Accept: false),
                _ => new AutoResolveBattleCommand(state.Battle!.Attacker.Owner ?? player),
            };
            session.Execute(command).ThrowIfRejected();
        }
    }
}
