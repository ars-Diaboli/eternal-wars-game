using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Events;
using static EternalWars.Core.Domain.Battles.BattleSide;

namespace EternalWars.Core.Tests.Battles;

/// <summary>The shared helpers of the battle engine: unit ids, the health of a number of creatures, top-creature healing.</summary>
public sealed class BattleWoundsTests
{
    [Fact]
    public void NextUnitIdIsOneAboveTheHighest()
    {
        var battle = TestBattle.Create();
        battle.Add(Attacker, TestBattle.Fixed("a", 1), 5, 0, 0);
        battle.Add(Defender, TestBattle.Fixed("b", 1), 5, 10, 0);

        Assert.Equal(3, battle.State.NextUnitId());
    }

    [Fact]
    public void HealthOfCountsTheWoundedTopCreatureFirst()
    {
        var battle = TestBattle.Create();
        var unit = battle.Add(Attacker, TestBattle.Fixed("a", 1, health: 10), 5, 0, 0);
        unit.TopHealth = 4;

        Assert.Equal(4, unit.HealthOf(1));
        Assert.Equal(24, unit.HealthOf(3));
        Assert.Equal(unit.TotalHealth, unit.HealthOf(unit.Count));
    }

    [Fact]
    public void HealTopNeverGoesAboveTheMaximumAndReportsNothingWhenFull()
    {
        var battle = TestBattle.Create();
        var unit = battle.Add(Attacker, TestBattle.Fixed("a", 1, health: 10), 5, 0, 0);
        var wounds = new BattleWounds(battle.State, battle.Bus);
        unit.TopHealth = 7;

        wounds.HealTop(unit, 25);
        wounds.HealTop(unit, 5);

        Assert.Equal(10, unit.TopHealth);
        Assert.Equal(3, Assert.Single(battle.Events.OfType<BattleUnitHealed>()).Healed);
    }
}
