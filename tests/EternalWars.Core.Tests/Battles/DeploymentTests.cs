using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Battles;

/// <summary>The deployment step (ADR-072): the side with the higher Tactics places its stacks within 4 / 5 / 6 columns of its edge.</summary>
public class DeploymentTests
{
    /// <summary>Hero A attacks hero B next to it; a width choice, if any, keeps the default.</summary>
    private static (GameSession Session, BattleState Battle, List<IEvent> Events) Fight(
        SkillLevel attacker, SkillLevel defender = SkillLevel.None, bool defenderIsComputer = false, Action<GameState>? setup = null)
    {
        var state = Create("A.B", "...");
        state.GetHero(A).Tactics = attacker;
        state.GetHero(B).Tactics = defender;
        state.GetPlayer(Red).IsHuman = !defenderIsComputer;
        setup?.Invoke(state);
        var session = Start(state);
        var events = new List<IEvent>();
        session.Events.Subscribe<IEvent>(events.Add);

        session.MoveTo(A, new MapPosition(2, 0));
        var battle = session.State.Battle!;
        if (battle is { Phase: BattlePhase.ChoosingWidth, WidthChooser: { } chooser })
        {
            session.Execute(new ChooseBattleWidthCommand(session.Battles.Commander(chooser)!.Value, BattleState.DefaultWidth)).ThrowIfRejected();
        }

        return (session, battle, events);
    }

    [Theory]
    [InlineData(SkillLevel.Basic, 4)]
    [InlineData(SkillLevel.Advanced, 5)]
    [InlineData(SkillLevel.Expert, 6)]
    public void TheBetterTactics_Deploys_WithinFourFiveOrSixColumns_FreeOfObstacles(SkillLevel tactics, int columns)
    {
        var (_, battle, events) = Fight(tactics);

        Assert.Equal((BattlePhase.Deploying, BattleSide.Attacker, columns), (battle.Phase, battle.Deployer, battle.DeploymentColumns));
        Assert.Equal(new BattleDeploymentStarted(BattleSide.Attacker, columns), Assert.Single(events.OfType<BattleDeploymentStarted>()));
        Assert.All(battle.Obstacles, o => Assert.True(o.Rect.X > columns, $"obstacle at {o.Position}"));
        Assert.Null(battle.ActiveUnit); // no round yet
    }

    [Fact]
    public void ATie_OrAComputerSide_DoesNotDeploy_AndADefenderDeploysAtItsOwnEdge()
    {
        Assert.Equal(BattlePhase.Fighting, Fight(SkillLevel.Basic, SkillLevel.Basic).Battle.Phase);
        Assert.Equal(BattlePhase.Fighting, Fight(SkillLevel.None, SkillLevel.Expert, defenderIsComputer: true).Battle.Phase);

        var (_, battle, _) = Fight(SkillLevel.None, SkillLevel.Advanced);
        Assert.Equal((BattleSide.Defender, 5), (battle.Deployer, battle.DeploymentColumns));
        Assert.True(battle.IsInDeploymentZone(new CellRect(battle.Width - 5, 0, 5, 2)));
        Assert.False(battle.IsInDeploymentZone(new CellRect(battle.Width - 6, 0, 2, 2)));
    }

    [Fact]
    public void TheDeployer_PlacesOwnStacksInTheZone_ThenStartsTheBattle()
    {
        var (session, battle, events) = Fight(SkillLevel.Basic, setup: state =>
        {
            state.GetHero(A).Army.Set(1, new CreatureStack(BuiltInCreatures.Archer, 5));
            state.GetHero(A).AddWarMachine(WarMachineSlot.Tent);
        });
        var footmen = battle.Units.Single(u => u.Side == BattleSide.Attacker && u.Creature.Id == BuiltInCreatures.Footman);
        var archers = battle.Units.Single(u => u.Side == BattleSide.Attacker && u.Creature.Id == BuiltInCreatures.Archer);
        var tent = battle.Units.Single(u => u.Machine == WarMachineKind.HealingTent);
        var from = footmen.Position;

        // Within 4 columns (the tent's column included): a footman (2 × 2) may stand at x = 1 or 2, not 3.
        session.Execute(new BattleDeployCommand(Blue, footmen.Id, new CellPosition(2, 0))).ThrowIfRejected();
        Assert.Equal(new CellPosition(2, 0), footmen.Position);
        Assert.Equal(new BattleUnitDeployed(footmen.Id, from, new CellPosition(2, 0)), Assert.Single(events.OfType<BattleUnitDeployed>()));

        Assert.Contains("first 4 columns", session.Execute(new BattleDeployCommand(Blue, footmen.Id, new CellPosition(3, 0))).Error);
        Assert.Contains("does not fit", session.Execute(new BattleDeployCommand(Blue, footmen.Id, archers.Position)).Error);
        Assert.Contains("keep their column", session.Execute(new BattleDeployCommand(Blue, tent.Id, new CellPosition(0, 8))).Error);
        Assert.False(session.Execute(new BattleDeployCommand(Red, footmen.Id, new CellPosition(1, 8))).IsAccepted);
        Assert.False(session.Execute(new BattleWaitCommand(Blue)).IsAccepted); // nobody acts before the battle starts

        session.Execute(new BattleStartCommand(Blue)).ThrowIfRejected();

        Assert.Equal(BattlePhase.Fighting, battle.Phase);
        Assert.Equal(new BattleDeploymentEnded(BattleSide.Attacker), Assert.Single(events.OfType<BattleDeploymentEnded>()));
        Assert.False(session.Execute(new BattleDeployCommand(Blue, footmen.Id, new CellPosition(1, 0))).IsAccepted);
    }

    [Fact]
    public void QuickCombat_SkipsTheDeployment()
    {
        var (session, battle, events) = Fight(SkillLevel.Expert);

        session.Execute(new AutoResolveBattleCommand(Blue)).ThrowIfRejected();

        Assert.Equal(BattlePhase.Finished, battle.Phase);
        Assert.Single(events.OfType<BattleDeploymentEnded>());
        Assert.Null(session.State.Battle);
    }

    [Fact]
    public void ABattleWithADeployment_ReplaysExactly()
    {
        var state = Create("A.B", "...");
        state.GetHero(A).Tactics = SkillLevel.Advanced;
        var map = MapConverter.ToDocument(state);
        var session = new GameSession(MapConverter.ToGameState(map, options: new GameOptions(Seed: 5)));
        session.Start();
        session.Execute(new MoveHeroCommand(Blue, A, new MapPosition(2, 0))).ThrowIfRejected();
        if (session.State.Battle is { Phase: BattlePhase.ChoosingWidth })
        {
            session.Execute(new ChooseBattleWidthCommand(Blue, 32)).ThrowIfRejected();
        }

        var footmen = session.State.Battle!.LivingUnitsOf(BattleSide.Attacker).First();
        session.Execute(new BattleDeployCommand(Blue, footmen.Id, new CellPosition(3, 10))).ThrowIfRejected();
        session.Execute(new BattleStartCommand(Blue)).ThrowIfRejected();
        if (session.State.Battle is not null)
        {
            session.Execute(new AutoResolveBattleCommand(Blue)).ThrowIfRejected();
        }

        var json = GameRecordSerializer.Serialize(GameReplay.Record(map, session));
        var replayed = GameReplay.Replay(GameRecordSerializer.Deserialize(json));

        Assert.Contains("\"type\": \"battleDeploy\"", json);
        Assert.Equal(GameStateDigest.Describe(session.State), GameStateDigest.Describe(replayed.State));
    }

    [Fact]
    public void ThePlacementUnderThePointer_CentresTheFootprint_InsideTheField()
    {
        var (_, battle, _) = Fight(SkillLevel.Basic);
        var footmen = battle.LivingUnitsOf(BattleSide.Attacker).First();

        Assert.Equal(new CellPosition(1, 5), BattleTargeting.DeploymentPlacement(battle, footmen, 2.1f, 6.2f));
        Assert.Equal(new CellPosition(0, 0), BattleTargeting.DeploymentPlacement(battle, footmen, 0.2f, 0.1f));
        Assert.Equal(new CellPosition(battle.Width - 2, BattleState.Height - 2), BattleTargeting.DeploymentPlacement(battle, footmen, 99f, 99f));
    }
}
