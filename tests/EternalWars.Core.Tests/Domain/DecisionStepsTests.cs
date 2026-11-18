using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.Domain.ObjectVisitWorld;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>
/// Every decision takes the same steps in the same order (ADR-215): an answer that is refused changes nothing and leaves its decision pending; an
/// accepted one closes its decision (<see cref="DecisionResolved"/>) before its effects, so a decision those effects open comes after.
/// </summary>
public class DecisionStepsTests
{
    [Fact]
    public void AChestsExperience_ResolvesTheChest_BeforeTheLevelUpItBringsIsAsked()
    {
        var state = Create("A...", "...B");
        var chest = Add<TreasureChest>(state, BuiltInMapObjects.TreasureChest, 2, 0);
        chest.FixedValues = new ChestValues(1500, 1000); // 1000 experience is level 2
        var session = Start(state);
        session.MoveTo(A, chest.VisitPosition);
        var events = Record(session);

        session.ResolveChest(TreasureChestReward.Experience);

        var resolved = events.FindIndex(e => e is DecisionResolved { Decision: TreasureChestDecision });
        Assert.InRange(resolved, 0, events.FindIndex(e => e is MapObjectRemoved) - 1);
        Assert.InRange(events.FindIndex(e => e is DecisionRequested { Decision: LevelUpDecision }), resolved + 1, events.Count - 1);
        Assert.IsType<LevelUpDecision>(state.PendingDecision);
    }

    [Fact]
    public void AChosenSkill_ResolvesTheLevelUp_BeforeTheHeroLevelsUp()
    {
        var state = Create("A...", "...B");
        var chest = Add<TreasureChest>(state, BuiltInMapObjects.TreasureChest, 2, 0);
        chest.FixedValues = new ChestValues(1500, 1000);
        var session = Start(state);
        session.MoveTo(A, chest.VisitPosition);
        session.ResolveChest(TreasureChestReward.Experience);
        var decision = Assert.IsType<LevelUpDecision>(state.PendingDecision);
        var events = Record(session);

        session.Execute(new ChooseSkillCommand(Blue, decision.Options[0])).ThrowIfRejected();

        Assert.InRange(events.FindIndex(e => e is DecisionResolved), 0, events.FindIndex(e => e is HeroLeveledUp) - 1);
        Assert.Null(state.PendingDecision);
    }

    [Fact]
    public void AJoiningGuard_ResolvesTheOffer_ThenLeavesTheMap_ThenJoins()
    {
        var state = Create(".A.....", ".......", "......B");
        state.GetHero(A).Army.Set(0, new CreatureStack(BuiltInCreatures.Wolf, 10));
        var monster = Add<MonsterGuard>(state, BuiltInMapObjects.Monster, 2, 1);
        (monster.Creature, monster.Count, monster.Disposition) = (BuiltInCreatures.Wolf, 6, GuardDisposition.Compliant);
        var session = Start(state);
        session.Step(A, Direction.SouthEast);
        var events = Record(session);

        session.RespondToGuard(accept: true);

        var order = events.Select(e => e.GetType().Name).Where(n => n is nameof(DecisionResolved) or nameof(MapObjectRemoved) or nameof(GuardJoined)).ToList();
        Assert.Equal([nameof(DecisionResolved), nameof(MapObjectRemoved), nameof(GuardJoined)], order);
    }

    [Fact]
    public void ARefusedWitchHutAnswer_LeavesTheDecisionPending_AndTheHeroAsItWas()
    {
        var (session, _) = World<WitchHut>(BuiltInMapObjects.WitchHut, (_, h) => h.Skills = [BuiltInSkills.Stewardship]);
        var hero = HeroA(session);
        session.Step(A, Direction.North);
        var decision = Assert.IsType<WitchHutDecision>(session.State.PendingDecision);

        // Other groups learned since the hut asked, up to the limit: the hut's skill no longer fits.
        var group = BuiltInSkills.Get(BuiltInSkills.Stewardship).Group;
        foreach (var other in BuiltInSkills.All.Where(s => s.Group != group).DistinctBy(s => s.Group))
        {
            if (hero.Skills.Groups.Count() < HeroSkills.MaxGroups)
            {
                hero.Skills.Set(other.Id, SkillLevel.Basic);
            }
        }

        var before = hero.Skills.ToString();
        var events = Record(session);

        var result = session.Execute(new ChooseWitchHutSkillCommand(Blue, BuiltInSkills.Stewardship));

        Assert.Contains("cannot be raised", result.Error, StringComparison.Ordinal);
        Assert.Same(decision, session.State.PendingDecision);
        Assert.Equal(before, hero.Skills.ToString());
        Assert.Null(hero.WitchHutLevel);
        Assert.DoesNotContain(events, e => e is DecisionResolved or WitchHutSkillLearned);
    }

    [Fact]
    public void AnAnswerToAnotherDecision_IsRefused_WithTheDecisionStillPending()
    {
        var state = Create("A...", "...B");
        var chest = Add<TreasureChest>(state, BuiltInMapObjects.TreasureChest, 2, 0);
        chest.FixedValues = new ChestValues(1500, 1000);
        var session = Start(state);
        session.MoveTo(A, chest.VisitPosition);
        var decision = Assert.IsType<TreasureChestDecision>(state.PendingDecision);

        var result = session.Execute(new ChooseWitchHutSkillCommand(Blue, null));

        Assert.Equal("No witch hut is waiting for an answer.", result.Error);
        Assert.Same(decision, state.PendingDecision);
    }

    private static List<IEvent> Record(GameSession session)
    {
        var events = new List<IEvent>();
        session.Events.Subscribe<IEvent>(events.Add);
        return events;
    }
}
