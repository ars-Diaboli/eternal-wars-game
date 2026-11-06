using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Map;
using static EternalWars.Core.Domain.Battles.BattleSide;

namespace EternalWars.Core.Tests.Battles;

/// <summary>Cliffs that split the battlefield partly (ADR-118 CMB-12, ADR-146).</summary>
public class DividingCliffTests
{
    private static readonly CreatureDefinition Large = TestBattle.Fixed("large", 3, size: CreatureSize.Large);

    /// <summary>A battle on <paramref name="terrain"/> with a large stack against a small one, deployed by the rules.</summary>
    private static TestBattle Deployed(TerrainType terrain, ulong seed, SiegeSpec? siege = null)
    {
        var battle = TestBattle.Create(seed: seed, terrain: terrain, siege: siege);
        BattleSetup.Deploy(battle.State, [new BattleStackSpec(Large, 5, 0)], [new BattleStackSpec(TestBattle.Fixed("small", 3), 5, 0)]);
        return battle;
    }

    private static List<BattleObstacle> Cliffs(TestBattle battle) => battle.State.Obstacles.Where(o => o.Class == ObstacleClass.Cliff).ToList();

    [Theory]
    [InlineData(TerrainType.Highlands)]
    [InlineData(TerrainType.Rough)]
    [InlineData(TerrainType.RockyGround)]
    [InlineData(TerrainType.Subterranean)]
    public void OnCliffGround_AboutOneBattleInThree_HasACliff(TerrainType terrain)
    {
        var withCliffs = Enumerable.Range(1, 300).Count(seed => Cliffs(Deployed(terrain, (ulong)seed)).Count > 0);
        Assert.InRange(withCliffs, 70, 130);
    }

    [Theory]
    [InlineData(TerrainType.Grass)]
    [InlineData(TerrainType.Snow)]
    [InlineData(TerrainType.Swamp)]
    public void OtherGround_NeverHasACliff(TerrainType terrain) =>
        Assert.All(Enumerable.Range(1, 100), seed => Assert.Empty(Cliffs(Deployed(terrain, (ulong)seed))));

    [Fact]
    public void ASiege_NeverHasACliff() =>
        Assert.All(Enumerable.Range(1, 100), seed => Assert.Empty(Cliffs(Deployed(TerrainType.Highlands, (ulong)seed, new SiegeSpec(1, 5)))));

    [Fact]
    public void ABattleThatForbidsCliffs_HasNone()
    {
        Assert.All(Enumerable.Range(1, 100), seed =>
        {
            var battle = TestBattle.Create(seed: (ulong)seed, terrain: TerrainType.Highlands);
            battle.State.CliffsAllowed = false; // a creature bank's battle
            BattleSetup.Deploy(battle.State, [new BattleStackSpec(Large, 5, 0)], [new BattleStackSpec(Large, 5, 0)]);
            Assert.Empty(Cliffs(battle));
        });
    }

    [Fact]
    public void EveryCliff_Is6To12CellsLong_LeavesTwoGaps_AndALargeStackCanWalkRoundIt()
    {
        var seen = 0;
        foreach (var seed in Enumerable.Range(1, 300))
        {
            var battle = Deployed(TerrainType.Highlands, (ulong)seed);
            var cliffs = Cliffs(battle);
            if (cliffs.Count == 0)
            {
                continue;
            }

            seen++;
            Assert.All(cliffs, c => Assert.InRange(c.Length, 6, 12));
            var gaps = BattleSetup.CliffGaps(cliffs.OrderBy(c => c.Position.Y).Select(c => (c.Position.Y, c.Length)).ToList());
            Assert.NotNull(gaps);
            Assert.True(gaps.Count(g => g >= BattleSetup.CliffGap) >= 2, $"seed {seed}: gaps {string.Join(", ", gaps)}");

            // Between the armies, and the large stack finds a way to the enemy.
            var (large, enemy) = (battle.State.Units.Single(u => u.Side == Attacker), battle.State.Units.Single(u => u.Side == Defender));
            Assert.All(cliffs, c => Assert.True(c.Rect.X > large.Rect.Right && c.Rect.Right < enemy.Position.X));
            Assert.True(BattleGrid.ApproachSteps(battle.State, large, [enemy]).ContainsKey(large.Position), $"seed {seed}: no way round");
        }

        Assert.True(seen > 50);
    }

    [Fact]
    public void ACliff_GivesCoverLikeACrag()
    {
        var battle = TestBattle.Create();
        var shooter = battle.Add(Attacker, TestBattle.Fixed("archer", 1, shots: 10), 1, 2, 10);
        var target = battle.Add(Defender, TestBattle.Fixed("target", 1), 1, 20, 10);
        battle.AddCliff(12, 4, 12);

        Assert.Equal(ObstacleClass.Big.CoverEighths(), BattleGrid.CoverEighths(battle.State, shooter, target));
    }

    [Fact]
    public void AWalkerGoesRound_AFlyerPassesOver()
    {
        var battle = TestBattle.Create();
        battle.AddCliff(10, 0, 12);
        battle.AddCliff(10, 16, 8); // the only gap: rows 12–15
        var walker = battle.Add(Attacker, TestBattle.Fixed("walker", 1, speed: 8), 1, 6, 4);
        var flyer = battle.Add(Attacker, TestBattle.Fixed("flyer", 1, speed: 8, capabilities: new CreatureCapability(CapabilityKind.Flying)), 1, 6, 8);

        Assert.DoesNotContain(new CellPosition(13, 4), BattleGrid.Reachable(battle.State, walker).Keys); // 7 cells straight, but the cliff is in the way
        Assert.Contains(new CellPosition(13, 8), BattleGrid.Reachable(battle.State, flyer).Keys);
    }

    [Fact]
    public void TheBattleAi_WalksTowardsTheGap_NotIntoTheCliffsFace()
    {
        var battle = TestBattle.Create();
        battle.AddCliff(10, 0, 12);
        battle.AddCliff(10, 16, 8); // the only gap: rows 12–15
        var walker = battle.Add(Attacker, TestBattle.Fixed("walker", 1, speed: 4), 1, 6, 2);
        battle.Add(Defender, TestBattle.Fixed("enemy", 1, speed: 1), 1, 14, 2);

        battle.StartWith(walker);
        BattleAi.Act(battle.State, battle.Engine);

        Assert.True(walker.Position.Y >= 5, $"walked to {walker.Position}"); // down towards the gap, not along row 2 to the cliff
    }
}
