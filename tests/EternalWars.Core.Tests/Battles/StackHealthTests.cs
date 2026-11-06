using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Events;

namespace EternalWars.Core.Tests.Battles;

public sealed class StackHealthTests
{
    [Theory]
    [InlineData(100, 100, HealthBand.Green)]
    [InlineData(51, 100, HealthBand.Green)]
    [InlineData(50, 100, HealthBand.Yellow)]
    [InlineData(21, 100, HealthBand.Yellow)]
    [InlineData(20, 100, HealthBand.Red)]
    [InlineData(1, 100, HealthBand.Red)]
    [InlineData(3, 5, HealthBand.Green)]
    [InlineData(1, 5, HealthBand.Red)]
    [InlineData(2, 5, HealthBand.Yellow)]
    public void Band_IsGreenOverHalf_YellowOverAFifth_RedBelow(int top, int full, HealthBand band) =>
        Assert.Equal(band, StackHealth.Band(top, full));

    [Fact]
    public void Fraction_IsTheTopCreaturesShare()
    {
        Assert.Equal(0.25f, StackHealth.Fraction(5, 20));
        Assert.Equal(1f, StackHealth.Fraction(20, 20));
        Assert.Equal(0f, StackHealth.Fraction(0, 20));
    }

    [Fact]
    public void AHit_ReportsTheTopCreaturesHealth()
    {
        var battle = TestBattle.Create();
        var attacker = battle.Add(BattleSide.Attacker, TestBattle.Fixed("a", 25), 1, 0, 0);
        var target = battle.Add(BattleSide.Defender, TestBattle.Fixed("t", 1, health: 10), 5, 2, 0);
        battle.StartWith(attacker);

        battle.Engine.Attack(target.Id, null);

        var hit = battle.Events.OfType<BattleUnitAttacked>().First(e => e.Target == target.Id);
        Assert.Equal((3, 5), (hit.RemainingCount, hit.TopHealth));
        Assert.Equal(HealthBand.Yellow, StackHealth.Band(hit.TopHealth, target.Creature.Health));
    }
}
