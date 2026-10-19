using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Battles;

/// <summary>Native terrain in battle (ADR-068): +1 Attack, +1 Defense and +1 Speed for creatures of a town native to the battlefield.</summary>
public class NativeTerrainTests
{
    [Fact]
    public void ANativeCreature_HitsHarder_TakesLess_AndIsFaster()
    {
        var home = TestBattle.Create(nativeTowns: ["haven"]);
        var away = TestBattle.Create();
        var target = TestBattle.Fixed("target", 5, attack: 10, defense: 10);
        var homeFootman = home.Add(BattleSide.Attacker, "haven.footman", 10, 0, 0);
        var homeTarget = home.Add(BattleSide.Defender, target, 10, 1, 0);
        var awayFootman = away.Add(BattleSide.Attacker, "haven.footman", 10, 0, 0);
        var awayTarget = away.Add(BattleSide.Defender, target, 10, 1, 0);

        Assert.True(home.State.IsNative(homeFootman));
        Assert.False(home.State.IsNative(homeTarget)); // a creature without a town has no native terrain
        Assert.Equal(away.State.SpeedOf(awayFootman) + BattleState.NativeSpeed, home.State.SpeedOf(homeFootman));

        var hit = new HitModifiers(BaseRoll: 4, IsRanged: false);
        Assert.True(DamageCalculator.Damage(home.State, homeFootman, homeTarget, hit) > DamageCalculator.Damage(away.State, awayFootman, awayTarget, hit));
        Assert.True(DamageCalculator.Damage(home.State, homeTarget, homeFootman, hit) < DamageCalculator.Damage(away.State, awayTarget, awayFootman, hit));
    }

    [Fact]
    public void TheBattlefieldTerrain_DecidesWhoIsNative_SoTroglodytesAreAtHomeInAnAbandonedGoldMine()
    {
        var state = Create(".........", ".........", "...A.....", ".........", "C.......B");
        state.GetHero(A).Army.Set(0, new CreatureStack("haven.footman", 20));
        state.Map.SetTerrain(new MapPosition(3, 1), TerrainType.Grass);
        Add<Mine>(state, BuiltInMapObjects.AbandonedMineId(ResourceType.Gold), 2, 0);
        var session = Start(state);

        session.Step(A, Direction.North);

        var battle = session.State.Battle!;
        Assert.Equal(TerrainType.Subterranean, battle.Terrain);
        Assert.Contains("dungeon", battle.NativeTowns);
        Assert.Contains(battle.Units, u => u.Side == BattleSide.Defender);
        Assert.All(battle.Units.Where(u => u.Side == BattleSide.Defender), u => Assert.True(battle.IsNative(u)));
        Assert.All(battle.Units.Where(u => u.Side == BattleSide.Attacker && !u.IsMachine), u => Assert.False(battle.IsNative(u)));
    }
}
