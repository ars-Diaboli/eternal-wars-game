using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>Neutral monsters, guarded mines, dispositions and the two guard modes (ADR-040).</summary>
public class GuardTests
{
    [Fact]
    public void SteppingOntoTheMonster_StartsTheEncounterWithoutMoving()
    {
        var state = Create(".A.....", ".......", "......B");
        Monster(state, 2, 1, BuiltInCreatures.Troll, 20, GuardDisposition.Savage);
        var session = Start(state);

        Assert.Equal(MoveResult.GuardEncountered, session.Step(A, Direction.SouthEast));

        Assert.Equal(new MapPosition(1, 0), state.GetHero(A).Position);
        Assert.IsType<BattleDecision>(state.PendingDecision);
        Assert.NotNull(state.Battle);
    }

    [Fact]
    public void EnteringAGuardedTile_IsIntercepted()
    {
        var state = Create("A......", ".......", "......B");
        Monster(state, 3, 1, BuiltInCreatures.Troll, 20, GuardDisposition.Savage);
        var session = Start(state);

        Assert.Equal(MoveResult.Moved, session.Step(A, Direction.East)); // (1,0) is not guarded
        Assert.Equal(MoveResult.GuardEncountered, session.Step(A, Direction.East)); // (2,0) is

        Assert.Equal(new MapPosition(1, 0), state.GetHero(A).Position);
        Assert.NotNull(state.Battle);
    }

    [Fact]
    public void ClassicMode_PickupsInTheGuardedArea_CanBeStolenFromOutside()
    {
        var state = Create("A......", ".......", "......B");
        Monster(state, 3, 1, BuiltInCreatures.Troll, 20, GuardDisposition.Savage);
        var pile = Add<ResourcePile>(state, BuiltInMapObjects.PileId(ResourceType.Gold), 2, 0);
        var session = Start(state);
        session.Step(A, Direction.East);

        Assert.Equal(MoveResult.CollectedFromAdjacent, session.Step(A, Direction.East));

        Assert.Equal(new MapPosition(1, 0), state.GetHero(A).Position);
        Assert.Equal(pile.Amount, state.GetPlayer(Blue).Resources.Gold);
        Assert.Null(state.Battle);
        Assert.True(state.GetHero(A).MovementPoints < 1400); // the grab still costs the step
    }

    [Fact]
    public void AreaMode_GuardsPickupsToo()
    {
        var state = Create("A......", ".......", "......B");
        Monster(state, 3, 1, BuiltInCreatures.Troll, 20, GuardDisposition.Savage).Mode = GuardMode.Area;
        Add<ResourcePile>(state, BuiltInMapObjects.PileId(ResourceType.Gold), 2, 0);
        var session = Start(state);
        session.Step(A, Direction.East);

        Assert.Equal(MoveResult.GuardEncountered, session.Step(A, Direction.East));

        Assert.Equal(0, state.GetPlayer(Blue).Resources.Gold);
        Assert.NotNull(state.Battle);
    }

    [Fact]
    public void ClassicAndAreaStacks_OnTheSameMap_GuardTheirOwnPickupsDifferently()
    {
        var state = Create("A.........", "..........", "..........", "..........", ".........B");
        Monster(state, 3, 1, BuiltInCreatures.Troll, 20, GuardDisposition.Savage); // Classic by default
        var treasureGuard = Monster(state, 3, 3, BuiltInCreatures.Troll, 20, GuardDisposition.Savage);
        treasureGuard.Mode = GuardMode.Area;
        var stolen = Add<ResourcePile>(state, BuiltInMapObjects.PileId(ResourceType.Gold), 2, 0);
        Add<ResourcePile>(state, BuiltInMapObjects.PileId(ResourceType.Gold), 2, 4);
        var session = Start(state);
        session.Step(A, Direction.East);

        Assert.Equal(MoveResult.CollectedFromAdjacent, session.Step(A, Direction.East)); // next to the Classic stack
        Assert.Equal(stolen.Amount, state.GetPlayer(Blue).Resources.Gold);

        Assert.Equal(MoveResult.Moved, session.Step(A, Direction.South)); // (1,1)
        Assert.Equal(MoveResult.Moved, session.Step(A, Direction.South)); // (1,2)
        Assert.Equal(MoveResult.Moved, session.Step(A, Direction.South)); // (1,3)
        Assert.Equal(MoveResult.GuardEncountered, session.Step(A, Direction.SouthEast)); // the pile at (2,4) next to the Area stack

        Assert.Equal(stolen.Amount, state.GetPlayer(Blue).Resources.Gold);
        Assert.Equal(treasureGuard.Id, state.Battle!.Defender.Guard);
    }

    [Fact]
    public void APickupGuardedByBothModes_IsDefendedByTheAreaStack()
    {
        var state = Create("A......", ".......", "......B");
        Monster(state, 3, 1, BuiltInCreatures.Wolf, 5, GuardDisposition.Savage); // Classic, lower id
        var areaGuard = Monster(state, 3, 0, BuiltInCreatures.Troll, 20, GuardDisposition.Savage);
        areaGuard.Mode = GuardMode.Area;
        Add<ResourcePile>(state, BuiltInMapObjects.PileId(ResourceType.Gold), 2, 1);
        var session = Start(state);
        session.Step(A, Direction.East); // (1,0)

        Assert.Equal(MoveResult.GuardEncountered, session.Step(A, Direction.SouthEast));

        Assert.Equal(0, state.GetPlayer(Blue).Resources.Gold);
        Assert.Equal(areaGuard.Id, state.Battle!.Defender.Guard);
    }

    [Fact]
    public void PathFinder_EndsRoutesAtGuardedTiles_AndRoutesAroundThem()
    {
        var corridor = Create("A......", ".......", "......B");
        Monster(corridor, 3, 1, BuiltInCreatures.Wolf, 5, GuardDisposition.Savage);
        var session = Start(corridor);

        Assert.Null(session.Movement.FindPath(A, new MapPosition(6, 0))); // every way east crosses the guarded column
        Assert.Equal(new MapPosition(2, 0), session.Movement.FindPath(A, new MapPosition(2, 0))!.Destination);

        var open = Create("A......", ".......", ".......", ".......", "......B");
        Monster(open, 3, 1, BuiltInCreatures.Wolf, 5, GuardDisposition.Savage);
        var path = Start(open).Movement.FindPath(A, new MapPosition(6, 0))!;

        Assert.DoesNotContain(path.Steps, s => open.IsGuarded(s.Position) && s.Position != path.Destination);
    }

    [Fact]
    public void RouteToAMonster_EndsOnIt_AndTheFirstGuardedTileStartsTheEncounter()
    {
        var state = Create("A......", ".......", ".......", "......B");
        var monster = Monster(state, 5, 1, BuiltInCreatures.Troll, 20, GuardDisposition.Savage);
        var session = Start(state);

        var path = session.Movement.FindPath(A, monster.VisitPosition);
        var outcome = session.MoveTo(A, monster.VisitPosition);

        Assert.Equal(monster.VisitPosition, path!.Destination);
        Assert.Equal(MoveResult.GuardEncountered, outcome.StopReason);
        Assert.Equal(new MapPosition(3, 0), state.GetHero(A).Position); // stopped before the guarded column
        Assert.Equal(monster.Id, state.Battle!.Defender.Guard);
    }

    [Theory]
    [InlineData(GuardDisposition.Compliant, 100, GuardOffer.Join)] // joins whatever the ratio
    [InlineData(GuardDisposition.Friendly, 10, GuardOffer.Join)] // R = 1
    [InlineData(GuardDisposition.Aggressive, 10, null)] // R = 1 < 2: neither join nor flee → battle
    [InlineData(GuardDisposition.Aggressive, 4, GuardOffer.Join)] // R = 2.5 ≥ 2
    [InlineData(GuardDisposition.Savage, 3, GuardOffer.Flee)] // never joins; R = 3.3 ≥ 3 → flees
    public void Disposition_DecidesTheOffer(GuardDisposition disposition, int wolves, GuardOffer? expected)
    {
        var state = Create(".A.....", ".......", "......B");
        state.GetHero(A).Army.Set(0, new CreatureStack(BuiltInCreatures.Wolf, 10));
        Monster(state, 2, 1, BuiltInCreatures.Wolf, wolves, disposition);
        var session = Start(state);

        session.Step(A, Direction.SouthEast);

        if (expected is { } offer)
        {
            var decision = Assert.IsType<GuardEncounterDecision>(state.PendingDecision);
            Assert.Equal((offer, BuiltInCreatures.Wolf, wolves), (decision.Offer, decision.Creature, decision.Count));
            Assert.Null(state.Battle);
        }
        else
        {
            Assert.IsType<BattleDecision>(state.PendingDecision);
        }
    }

    [Fact]
    public void AcceptingAJoinOffer_AddsTheStack_AndRemovesTheMonster()
    {
        var state = Create(".A.....", ".......", "......B");
        state.GetHero(A).Army.Set(0, new CreatureStack(BuiltInCreatures.Wolf, 10));
        var monster = Monster(state, 2, 1, BuiltInCreatures.Wolf, 6, GuardDisposition.Compliant);
        var session = Start(state);
        var events = Record(session);
        session.Step(A, Direction.SouthEast);

        session.RespondToGuard(accept: true);

        Assert.Equal(new CreatureStack(BuiltInCreatures.Wolf, 16), state.GetHero(A).Army[0]);
        Assert.DoesNotContain(state.Objects, o => o.Id == monster.Id);
        Assert.Contains(new GuardJoined(A, monster.Id, BuiltInCreatures.Wolf, 6), events);
        Assert.Null(state.PendingDecision);
    }

    [Fact]
    public void LettingFleeingGuardsGo_RemovesThemWithoutExperience_DecliningFights()
    {
        var state = Create(".A.....", ".......", "......B");
        state.GetHero(A).Army.Set(0, new CreatureStack(BuiltInCreatures.Seraph, 10));
        var first = Monster(state, 2, 1, BuiltInCreatures.Wolf, 5, GuardDisposition.Savage);
        var session = Start(state);
        var events = Record(session);
        session.Step(A, Direction.SouthEast);

        session.RespondToGuard(accept: true);

        Assert.Contains(new GuardFled(A, first.Id), events);
        Assert.Equal(0, state.GetHero(A).Experience);

        Monster(state, 2, 1, BuiltInCreatures.Wolf, 5, GuardDisposition.Savage);
        session.Step(A, Direction.SouthEast);
        session.RespondToGuard(accept: false);

        Assert.NotNull(state.Battle);
    }

    [Fact]
    public void NeverFlees_AndAFullArmy_MeanBattle()
    {
        var state = Create(".A.....", ".......", "......B");
        var army = state.GetHero(A).Army;
        string[] seven = [BuiltInCreatures.Seraph, BuiltInCreatures.Footman, BuiltInCreatures.Pikeman, BuiltInCreatures.Zealot,
            BuiltInCreatures.Archer, BuiltInCreatures.Monk, BuiltInCreatures.Griffin];
        for (var slot = 0; slot < Army.SlotCount; slot++)
        {
            army.Set(slot, new CreatureStack(seven[slot], 10));
        }

        var monster = Monster(state, 2, 1, BuiltInCreatures.Wolf, 5, GuardDisposition.Compliant);
        monster.NeverFlees = true;
        var session = Start(state);

        session.Step(A, Direction.SouthEast);

        Assert.IsType<BattleDecision>(state.PendingDecision); // no room to join, may not flee
    }

    [Fact]
    public void BeatingAMonster_RemovesIt_AndLosingLeavesTheSurvivors()
    {
        var strong = Create(".A.....", ".......", "......B");
        strong.GetHero(A).Army.Set(0, new CreatureStack(BuiltInCreatures.Seraph, 5));
        var prey = Monster(strong, 2, 1, BuiltInCreatures.Wolf, 30, GuardDisposition.Savage);
        prey.NeverFlees = true;
        var session = Start(strong);
        session.Step(A, Direction.SouthEast);
        session.AutoResolve();

        Assert.DoesNotContain(strong.Objects, o => o.Id == prey.Id);
        Assert.True(strong.GetHero(A).Experience > 0);

        var weak = Create(".A.....", ".......", "......B");
        weak.GetHero(A).Army.Set(0, new CreatureStack(BuiltInCreatures.Imp, 1));
        var hunter = Monster(weak, 2, 1, BuiltInCreatures.Troll, 10, GuardDisposition.Savage);
        var lost = Start(weak);
        lost.Step(A, Direction.SouthEast);
        lost.AutoResolve();

        Assert.DoesNotContain(weak.Heroes, h => h.Id == A);
        Assert.Equal(10, hunter.Count);
    }

    [Fact]
    public void GuardedMine_MustBeBeaten_ThenItIsFlagged()
    {
        var state = Create("A......", ".......", "......B");
        state.GetHero(A).Army.Set(0, new CreatureStack(BuiltInCreatures.Seraph, 5));
        var mine = Add<Mine>(state, BuiltInMapObjects.MineId(ResourceType.Gold), 2, 0);
        mine.Guard = new GuardStack(BuiltInCreatures.Wolf, 10);
        var session = Start(state);
        var events = Record(session);

        session.MoveTo(A, mine.VisitPosition);

        Assert.Null(mine.Owner);
        Assert.NotNull(state.Battle);

        session.AutoResolve();

        Assert.Null(mine.Guard);
        Assert.Equal(Blue, mine.Owner);
        Assert.Contains(new MineGuardDefeated(mine.Id), events);
    }

    [Fact]
    public void UnsetGuardCounts_AreRolledAtStart_FromTheSeed()
    {
        static (int? Monster, int? Mine) Roll(ulong seed)
        {
            var state = Create(new GameOptions(Seed: seed), "A......", ".......", "......B");
            var monster = Monster(state, 6, 0, BuiltInCreatures.Troll, null, GuardDisposition.Hostile);
            var mine = Add<Mine>(state, BuiltInMapObjects.MineId(ResourceType.Ore), 1, 1);
            mine.Guard = new GuardStack(BuiltInCreatures.RedDragon);
            Start(state);
            return (monster.Count, mine.Guard.Count);
        }

        var rolled = Roll(5);

        Assert.InRange(rolled.Monster!.Value, 8, 16); // tier 3
        Assert.InRange(rolled.Mine!.Value, 1, 4); // tier 5
        Assert.Equal(rolled, Roll(5));
    }

    [Fact]
    public void UnfixedChest_RollsFromTheTable_FixedChestDoesNot()
    {
        var state = Create("A..", "..B");
        Add<TreasureChest>(state, BuiltInMapObjects.TreasureChest, 1, 0);
        var session = Start(state);

        session.Step(A, Direction.East);

        var decision = Assert.IsType<TreasureChestDecision>(state.PendingDecision);
        Assert.Contains(new ChestValues(decision.Gold, decision.Experience), TreasureChest.RollTable);
    }

    private static MonsterGuard Monster(GameState state, int x, int y, string creature, int? count, GuardDisposition disposition)
    {
        var monster = Add<MonsterGuard>(state, BuiltInMapObjects.Monster, x, y);
        (monster.Creature, monster.Count, monster.Disposition) = (creature, count, disposition);
        return monster;
    }

    private static List<IEvent> Record(GameSession session)
    {
        var events = new List<IEvent>();
        session.Events.Subscribe<IEvent>(events.Add);
        return events;
    }
}
