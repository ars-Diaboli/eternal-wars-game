using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Towns;
using static EternalWars.Core.Domain.Battles.BattleSide;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Battles;

/// <summary>Army morale (ADR-075, CMB-2): mixed factions and a town's Brotherhood; creatures without morale (ADR-071, CMB-7).</summary>
public class ArmyMoraleTests
{
    private const string Skeleton = "necropolis.skeleton";

    /// <summary>A town at (0, 0) with its entrance at (3, 6); hero A just below it.</summary>
    private static readonly string[] Rows =
    [
        .. Enumerable.Repeat("............", 7),
        "...A........",
        "............",
        "...........B",
    ];

    [Fact]
    public void EveryFactionBeyondTheFirst_CostsOneMorale_NeutralsCountAsOne()
    {
        var creatures = CreatureCatalog.Default;
        CreatureDefinition[] Of(params string[] ids) => ids.Select(creatures.Get).ToArray();

        Assert.Equal(0, MoraleAndLuck.MixedFactions([]));
        Assert.Equal(0, MoraleAndLuck.MixedFactions(Of(BuiltInCreatures.Footman, BuiltInCreatures.Footman)));
        Assert.Equal(-1, MoraleAndLuck.MixedFactions(Of(BuiltInCreatures.Footman, Skeleton)));
        Assert.Equal(-2, MoraleAndLuck.MixedFactions(Of(BuiltInCreatures.Footman, Skeleton, BuiltInCreatures.Wolf, BuiltInCreatures.DireWolf)));
    }

    [Fact]
    public void Undead_HaveNoMorale_WhateverTheHeroGives()
    {
        var battle = TestBattle.Create(attackerBonuses: HeroBattleBonuses.None with { Morale = 3 });
        var footmen = battle.Add(Attacker, BuiltInCreatures.Footman, 1, 0, 0);
        var skeletons = battle.Add(Attacker, Skeleton, 1, 0, 4);

        Assert.True(CreatureCatalog.Default.Get(Skeleton).HasTag(CreatureTags.Undead));
        Assert.Equal(3, battle.State.MoraleOf(footmen));
        Assert.Equal(0, battle.State.MoraleOf(skeletons));
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(MoraleAndLuck.BrotherhoodLevel, MoraleAndLuck.BrotherhoodDefenderMorale)]
    public void AMixedArmy_AttackingAGarrison_AndTheTownsBrotherhood(int tavern, int defenders)
    {
        var state = Create(Rows);
        var town = AddTown(state, TownIds.Haven, 0, 0, Red);
        town.SetLevel(BuiltInBuildings.Tavern, tavern);
        town.Garrison.Set(0, new CreatureStack(BuiltInCreatures.Footman, 5));
        state.GetHero(A).Army.Set(1, new CreatureStack(Skeleton, 5));
        state.GetHero(A).Army.Set(2, new CreatureStack(BuiltInCreatures.Wolf, 5));
        var session = Start(state);

        session.Step(A, Direction.North);

        var battle = Assert.IsType<BattleState>(session.State.Battle);
        Assert.Equal(town.Id, battle.Defender.Guard);
        Assert.Equal(-2, battle.Attacker.ArmyMorale);
        Assert.Equal(defenders, battle.Defender.ArmyMorale);
    }

    [Fact]
    public void AnOwnHeroAtTheTownsEntrance_DefendsWithTheBrotherhoodsMorale()
    {
        var state = Create(Rows);
        var town = AddTown(state, TownIds.Haven, 0, 0, Red);
        town.SetLevel(BuiltInBuildings.Tavern, MoraleAndLuck.BrotherhoodLevel);
        state.GetHero(B).Position = town.VisitPosition;
        var session = Start(state);

        session.Step(A, Direction.North);

        var battle = Assert.IsType<BattleState>(session.State.Battle);
        Assert.Equal(B, battle.Defender.Hero);
        Assert.Equal((0, MoraleAndLuck.BrotherhoodDefenderMorale), (battle.Attacker.ArmyMorale, battle.Defender.ArmyMorale));
    }
}
