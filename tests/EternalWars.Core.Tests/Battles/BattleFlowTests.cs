using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Battles;

/// <summary>Battles started from the adventure map and their consequences.</summary>
public class BattleFlowTests
{
    [Fact]
    public void AttackingAnEnemyHero_FreezesTheMapUntilTheBattleIsOver()
    {
        var state = Create("A.B", "...");
        state.GetHero(A).Army.Set(0, new CreatureStack(BuiltInCreatures.Champion, 10));
        var session = Start(state);
        var events = Record(session);

        var outcome = session.MoveTo(A, new MapPosition(2, 0));

        Assert.Equal(MoveResult.BattleStarted, outcome.StopReason);
        Assert.IsType<BattleDecision>(state.PendingDecision);
        Assert.Equal(BattlePhase.Fighting, state.Battle!.Phase);
        Assert.Single(events.OfType<BattleStarted>());
        Assert.Throws<InvalidOperationException>(session.EndTurn);

        session.AutoResolve();

        Assert.Null(state.Battle);
        Assert.Null(state.PendingDecision);
        var ended = Assert.Single(events.OfType<BattleEnded>());
        Assert.Equal((BattleSide.Attacker, BattleEndReason.Defeated), (ended.Winner, ended.Reason));
        Assert.Contains(new HeroDefeated(B, Red, Retreated: false), events);
        Assert.DoesNotContain(state.Heroes, h => h.Id == B);
        Assert.Equal(10 * 14 + BattleService.HeroDefeatExperience, state.GetHero(A).Experience); // 10 footmen × 14 HP + hero bonus
        Assert.Contains(events, e => e is ArmyChanged { Hero: var h } && h == A);
        session.EndTurn();
    }

    [Fact]
    public void TheDefender_CommandsItsOwnSide_DuringTheAttackersTurn()
    {
        var state = Create("A.B", "...");
        state.GetHero(B).Army.Set(0, new CreatureStack(BuiltInCreatures.Familiar, 5)); // initiative 14: acts first
        var session = Start(state);

        session.MoveTo(A, new MapPosition(2, 0));

        Assert.Equal(Blue, state.CurrentPlayer.Id);
        Assert.Equal(Red, session.ActivePlayer());
        Assert.False(session.Execute(new BattleDefendCommand(Blue)).IsAccepted);
        Assert.True(session.Execute(new BattleDefendCommand(Red)).IsAccepted);
        Assert.Equal(Blue, session.ActivePlayer());
    }

    [Fact]
    public void Retreat_LosesTheHero_AndGivesNoHeroBonus()
    {
        var session = Start(Create("A.B", "..."));
        var events = Record(session);
        session.MoveTo(A, new MapPosition(2, 0));

        Assert.True(session.Execute(new BattleRetreatCommand(Blue)).IsAccepted);

        var ended = Assert.Single(events.OfType<BattleEnded>());
        Assert.Equal((BattleSide.Defender, BattleEndReason.Retreated, 0), (ended.Winner, ended.Reason, ended.Experience));
        Assert.Contains(new HeroDefeated(A, Blue, Retreated: true), events);
        Assert.DoesNotContain(events, e => e is HeroExperienceGained);
        Assert.Equal(10, session.State.GetHero(B).Army[0]!.Count);
    }

    [Fact]
    public void HeroWithoutArmy_CannotAttack()
    {
        var state = Create("AB.", "...");
        state.GetHero(A).Army.Set(0, null);
        var session = Start(state);

        Assert.Equal(MoveResult.NoArmy, session.Step(A, Direction.East));
        Assert.Null(state.Battle);
    }

    [Fact]
    public void DefenderWithoutArmy_IsOverrunWithoutABattlefield()
    {
        var state = Create("AB.", "...");
        state.GetHero(B).Army.Set(0, null);
        var session = Start(state);
        var events = Record(session);

        Assert.Equal(MoveResult.BattleStarted, session.Step(A, Direction.East));

        Assert.Null(state.Battle);
        Assert.Null(state.PendingDecision);
        Assert.DoesNotContain(events, e => e is BattleStarted);
        Assert.DoesNotContain(state.Heroes, h => h.Id == B);
        Assert.Equal(BattleService.HeroDefeatExperience, state.GetHero(A).Experience);
    }

    [Fact]
    public void ComputerPlayers_FightOutTheirBattleImmediately()
    {
        var state = Create("A.B", "...");
        foreach (var player in state.Players)
        {
            player.IsHuman = false;
        }

        var session = Start(state);

        var outcome = session.MoveTo(A, new MapPosition(2, 0));

        Assert.Equal(MoveResult.BattleStarted, outcome.StopReason);
        Assert.Null(state.Battle);
        Assert.Null(state.PendingDecision);
        Assert.Single(state.Heroes);
    }

    [Fact]
    public void BetterTactics_ChoosesTheBattlefieldWidth()
    {
        var state = Create("A.B", "...");
        state.GetHero(A).Tactics = SkillLevel.Expert;
        state.GetHero(B).Tactics = SkillLevel.Basic; // effective: Advanced for the attacker
        var session = Start(state);

        session.MoveTo(A, new MapPosition(2, 0));

        var battle = state.Battle!;
        Assert.Equal(BattlePhase.ChoosingWidth, battle.Phase);
        Assert.Equal([28, 30, 32], battle.WidthOptions);
        Assert.False(session.Execute(new ChooseBattleWidthCommand(Red, 28)).IsAccepted);
        Assert.False(session.Execute(new ChooseBattleWidthCommand(Blue, 34)).IsAccepted);
        Assert.True(session.Execute(new ChooseBattleWidthCommand(Blue, 32)).IsAccepted);
        Assert.Equal((BattlePhase.Fighting, 32), (battle.Phase, battle.Width));
        Assert.All(battle.Units.Where(u => u.Side == BattleSide.Defender), u => Assert.Equal(32, u.Rect.Right));
    }

    [Fact]
    public void Survivors_GoBackToTheirSlots_AndLosersSlotsEmpty()
    {
        var state = Create("A.B", "...");
        var army = state.GetHero(A).Army;
        army.Set(0, null);
        army.Set(3, new CreatureStack(BuiltInCreatures.Pikeman, 1)); // fodder that should die
        army.Set(5, new CreatureStack(BuiltInCreatures.Seraph, 3));
        state.GetHero(B).Army.Set(0, new CreatureStack(BuiltInCreatures.Archer, 20));
        var session = Start(state);

        session.MoveTo(A, new MapPosition(2, 0));
        session.AutoResolve();

        Assert.Contains(state.Heroes, h => h.Id == A);
        Assert.Equal(BuiltInCreatures.Seraph, army[5]!.Creature);
        Assert.InRange(army[5]!.Count, 1, 3);
        Assert.Equal([5], army.Stacks.Where(s => s.Stack.Creature == BuiltInCreatures.Seraph).Select(s => s.Slot));
    }

    [Fact]
    public void Ambushes_AreFlaggedOnTheBattle()
    {
        var state = Create(new GameOptions(FogMode.ShroudWithRefog), "AB......");
        state.GetHero(A).ScoutingRadius = 0; // A sees only its own tile, so B next to it is unseen
        state.GetHero(B).ScoutingRadius = 0;
        var session = Start(state);
        var events = Record(session);

        Assert.False(state.CanSee(Blue, state.GetHero(B)));
        session.Step(A, Direction.East);

        Assert.Contains(events, e => e is BattleStarted { IsAmbush: true });
    }

    private static List<IEvent> Record(GameSession session)
    {
        var events = new List<IEvent>();
        session.Events.Subscribe<IEvent>(events.Add);
        return events;
    }
}
