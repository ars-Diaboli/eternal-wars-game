using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Magic;
using static EternalWars.Core.Domain.Battles.BattleSide;

namespace EternalWars.Core.Tests.Battles;

/// <summary>ADR-149: the field-zone spells (Fire Wall, Thornwall, Bone Wall, Consecrate, Darkness), their grammar, movement, shots and rounds.</summary>
public class FieldZoneTests
{
    private static readonly CreatureCapability Flying = new(CapabilityKind.Flying);

    // The table --------------------------------------------------------------------------------

    [Theory]
    [InlineData("light.consecrate")]
    [InlineData("nature.thornwall")]
    [InlineData("death.bone-wall")]
    [InlineData("chaos.fire-wall")]
    [InlineData("shadow.darkness")]
    public void TheFiveZoneSpells_CanBeCastInBattle_AtEveryRank(string id)
    {
        var spell = SpellCatalog.Default.Get(id);
        Assert.All(new[] { 1, 2, 3 }, rank => Assert.Equal(SpellEffectKind.Zone, spell.EffectAt(rank)!.Kind));
    }

    [Fact]
    public void ALine_RunsDownTheField_CentredOnTheAimedCell()
    {
        var line = SpellEffect.Parse("zone line 5 wide 3 enter 30 rounds 3");

        Assert.Equal(new CellRect(9, 8, 3, 5), BattleMagic.Area(line, new CellPosition(10, 10)));
        Assert.Equal(30, line.Zone!.Enter);
    }

    [Theory]
    [InlineData("zone line 5 enter 30")] // no rounds
    [InlineData("zone enter 30 rounds 3")] // no shape
    [InlineData("zone area 3 line 5 rounds 3")] // two shapes
    [InlineData("zone area 3 attack 2 rounds 3")] // only Defense
    [InlineData("damage 30 line 5")] // a line is for zones
    [InlineData("damage 30 blocks")]
    public void TheZoneGrammar_RefusesWhatDoesNotFit(string text) =>
        Assert.Throws<FormatException>(() => SpellEffect.Parse(text));

    [Fact]
    public void AZone_IsClippedToTheField_AndLastsItsRounds()
    {
        var battle = Battle(("chaos.fire-wall", 1));
        var own = battle.Add(Attacker, Sturdy("own"), 1, 0, 20);
        battle.Add(Defender, Sturdy("enemy"), 1, 29, 20);
        battle.StartWith(own);

        Assert.Null(battle.Engine.Cast(Attacker, "chaos.fire-wall", null, new CellPosition(10, 0)));

        var zone = Assert.Single(battle.State.Zones);
        Assert.Equal(new CellRect(10, 0, 1, 3), zone.Rect);
        Assert.Equal(4, zone.UntilRound); // the rest of round 1 and three more
        Assert.Contains(battle.Events, e => e is BattleZoneCreated { Spell: "chaos.fire-wall", UntilRound: 4 });

        for (var round = 0; round < 4; round++)
        {
            EndRound(battle);
        }

        Assert.Empty(battle.State.Zones);
        Assert.Contains(battle.Events, e => e is BattleZoneExpired { Spell: "chaos.fire-wall" });
    }

    // Fire Wall --------------------------------------------------------------------------------

    [Fact]
    public void FireWall_BurnsAWalkerCrossingIt_NotAFlyerPassingOver()
    {
        var battle = Battle(("chaos.fire-wall", 1));
        var walker = battle.Add(Attacker, Sturdy("walker"), 1, 7, 10);
        var flyer = battle.Add(Attacker, TestBattle.Fixed("flyer", 10, health: 1000, capabilities: Flying), 1, 7, 2);
        battle.Add(Defender, Sturdy("enemy"), 1, 29, 20);
        battle.StartWith(walker);
        Assert.Null(battle.Engine.Cast(Attacker, "chaos.fire-wall", null, new CellPosition(10, 10)));
        battle.State.MutableZones.Add(battle.State.Zones[0] with { Id = 2, Rect = new CellRect(10, 0, 1, 5) }); // a second wall in the flyer's way

        Assert.Null(battle.Engine.Move(new CellPosition(12, 10)));
        battle.State.ActiveUnit = flyer;
        Assert.Null(battle.Engine.Move(new CellPosition(12, 2)));

        Assert.Equal(970, walker.TopHealth);
        Assert.Equal(1000, flyer.TopHealth);
    }

    [Fact]
    public void FireWall_BurnsAFlyerLandingInIt_AndAStackStandingInItAsARoundStarts()
    {
        var battle = Battle(("chaos.fire-wall", 1));
        var flyer = battle.Add(Attacker, TestBattle.Fixed("flyer", 10, health: 1000, capabilities: Flying), 1, 7, 10);
        var enemy = battle.Add(Defender, Sturdy("enemy"), 1, 20, 10);
        battle.StartWith(flyer);
        Assert.Null(battle.Engine.Cast(Attacker, "chaos.fire-wall", null, new CellPosition(10, 10)));
        battle.State.MutableZones.Add(battle.State.Zones[0] with { Id = 2, Rect = new CellRect(20, 10, 1, 1) }); // under the enemy

        Assert.Null(battle.Engine.Move(new CellPosition(10, 10)));
        Assert.Equal(970, flyer.TopHealth);

        EndRound(battle);
        Assert.Equal(940, flyer.TopHealth); // it stood in the fire as round 2 started
        Assert.Equal(970, enemy.TopHealth);
    }

    [Fact]
    public void FireWall_SparesAFireImmuneStack()
    {
        var battle = Battle(("chaos.fire-wall", 1));
        var immune = battle.Add(Attacker, TestBattle.Fixed("salamander", 10, health: 1000, capabilities: new CreatureCapability(CapabilityKind.SpellImmunity, 0, "fire")), 1, 7, 10);
        battle.Add(Defender, Sturdy("enemy"), 1, 29, 20);
        battle.StartWith(immune);
        Assert.Null(battle.Engine.Cast(Attacker, "chaos.fire-wall", null, new CellPosition(10, 10)));

        Assert.Null(battle.Engine.Move(new CellPosition(12, 10)));

        Assert.Equal(1000, immune.TopHealth);
    }

    [Fact]
    public void FireWall_GrowsWithSpellPower()
    {
        var battle = Battle(10, ("chaos.fire-wall", 1));
        var walker = battle.Add(Attacker, Sturdy("walker"), 1, 7, 10);
        battle.Add(Defender, Sturdy("enemy"), 1, 29, 20);
        battle.StartWith(walker);
        Assert.Null(battle.Engine.Cast(Attacker, "chaos.fire-wall", null, new CellPosition(10, 10)));

        Assert.Equal(7, battle.State.Zones[0].UntilRound); // 3 rounds × 2
        Assert.Null(battle.Engine.Move(new CellPosition(12, 10)));
        Assert.Equal(940, walker.TopHealth); // 30 × 2
    }

    // Thornwall and Bone Wall ------------------------------------------------------------------

    [Fact]
    public void Thornwall_KeepsWalkersOut_ButNotFlyers()
    {
        var battle = Battle(("nature.thornwall", 1));
        var walker = battle.Add(Attacker, TestBattle.Fixed("walker", 10, health: 1000, speed: 4), 1, 7, 10);
        var flyer = battle.Add(Attacker, TestBattle.Fixed("flyer", 10, health: 1000, speed: 4, capabilities: Flying), 1, 7, 2);
        battle.Add(Defender, Sturdy("enemy"), 1, 29, 20);
        battle.StartWith(walker);
        Assert.Null(battle.Engine.Cast(Attacker, "nature.thornwall", null, new CellPosition(10, 10)));
        battle.State.MutableZones.Add(battle.State.Zones[0] with { Id = 2, Rect = new CellRect(10, 0, 1, 5) });

        var walks = BattleGrid.Reachable(battle.State, walker);
        Assert.DoesNotContain(new CellPosition(9, 10), walks.Keys);
        Assert.DoesNotContain(new CellPosition(11, 10), walks.Keys); // the long way round is more than 4 cells
        Assert.Contains(new CellPosition(11, 2), BattleGrid.Reachable(battle.State, flyer).Keys);
    }

    [Fact]
    public void Thornwall_TearsAStackWalkingOutOfIt()
    {
        var battle = Battle(("nature.thornwall", 1));
        var caught = battle.Add(Attacker, Sturdy("caught"), 1, 10, 10);
        battle.Add(Defender, Sturdy("enemy"), 1, 29, 20);
        battle.StartWith(caught);
        Assert.Null(battle.Engine.Cast(Attacker, "nature.thornwall", null, new CellPosition(10, 10)));

        Assert.Null(battle.Engine.Move(new CellPosition(13, 10)));

        Assert.Equal(980, caught.TopHealth);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(3, false)]
    public void Thornwall_BlocksShotsOnlyAtRankIII(int rank, bool canShoot)
    {
        var battle = Battle(("nature.thornwall", rank));
        var archer = battle.Add(Attacker, TestBattle.Fixed("archer", 10, health: 1000, shots: 10), 1, 5, 10);
        var enemy = battle.Add(Defender, Sturdy("enemy"), 1, 15, 10);
        battle.StartWith(archer);
        Assert.Null(battle.Engine.Cast(Attacker, "nature.thornwall", null, new CellPosition(10, 10)));

        Assert.Equal(canShoot, BattleGrid.CanShoot(battle.State, archer, enemy, out _));
    }

    [Fact]
    public void BoneWall_BlocksShots_AndLetsOnlyTheCastersUndeadThrough()
    {
        var battle = Battle(("death.bone-wall", 1));
        var undead = TestBattle.Fixed("ghoul", 10, health: 1000, speed: 2) with { Tags = [CreatureTags.Undead] };
        var own = battle.Add(Attacker, undead, 1, 7, 10);
        var archer = battle.Add(Defender, TestBattle.Fixed("archer", 10, health: 1000, shots: 10), 1, 15, 10);
        var enemyUndead = battle.Add(Defender, undead with { Id = "enemy-ghoul" }, 1, 11, 12);
        battle.StartWith(own);
        Assert.Null(battle.Engine.Cast(Attacker, "death.bone-wall", null, new CellPosition(10, 10)));

        Assert.Contains(new CellPosition(9, 10), BattleGrid.Reachable(battle.State, own).Keys);
        Assert.DoesNotContain(new CellPosition(10, 12), BattleGrid.Reachable(battle.State, enemyUndead).Keys);
        Assert.False(BattleGrid.CanShoot(battle.State, archer, own, out _));
    }

    [Fact]
    public void BoneWallIII_HurtsLivingEnemiesNextToIt_AsARoundStarts()
    {
        var battle = Battle(("death.bone-wall", 3));
        var own = battle.Add(Attacker, Sturdy("own"), 1, 8, 10);
        var enemy = battle.Add(Defender, Sturdy("enemy"), 1, 11, 10);
        var undead = battle.Add(Defender, Sturdy("ghoul") with { Tags = [CreatureTags.Undead] }, 1, 11, 8);
        battle.StartWith(own);
        Assert.Null(battle.Engine.Cast(Attacker, "death.bone-wall", null, new CellPosition(10, 10)));

        EndRound(battle);

        Assert.Equal((1000, 980, 1000), (own.TopHealth, enemy.TopHealth, undead.TopHealth));
    }

    // Consecrate -------------------------------------------------------------------------------

    [Fact]
    public void Consecrate_HurtsEnemyUndeadAndDemonsInIt_NotTheLivingOrOwn()
    {
        var battle = Battle(("light.consecrate", 1));
        var ownUndead = battle.Add(Attacker, Sturdy("own-ghoul") with { Tags = [CreatureTags.Undead] }, 1, 8, 8);
        var demon = battle.Add(Defender, Sturdy("imp") with { Tags = [CreatureTags.Demon] }, 1, 10, 10);
        var living = battle.Add(Defender, Sturdy("man"), 1, 10, 8);
        battle.StartWith(ownUndead);
        Assert.Null(battle.Engine.Cast(Attacker, "light.consecrate", null, new CellPosition(10, 10)));

        EndRound(battle);

        Assert.Equal((1000, 985, 1000), (ownUndead.TopHealth, demon.TopHealth, living.TopHealth));
    }

    [Fact]
    public void ConsecrateIII_HealsOwnStacksInIt()
    {
        var battle = Battle(("light.consecrate", 3));
        var own = battle.Add(Attacker, Sturdy("own"), 1, 10, 10);
        battle.Add(Defender, Sturdy("enemy"), 1, 29, 20);
        battle.StartWith(own);
        own.TopHealth = 900;
        Assert.Null(battle.Engine.Cast(Attacker, "light.consecrate", null, new CellPosition(10, 10)));

        EndRound(battle);

        Assert.Equal(910, own.TopHealth);
    }

    // Darkness ---------------------------------------------------------------------------------

    [Fact]
    public void Darkness_HalvesShotsIntoOrOutOfIt()
    {
        var battle = Battle(("shadow.darkness", 1));
        var archer = battle.Add(Attacker, TestBattle.Fixed("archer", 10, shots: 10), 10, 0, 10);
        var enemy = battle.Add(Defender, Sturdy("enemy"), 1, 20, 10);
        battle.StartWith(archer);
        var hit = new HitModifiers(10, IsRanged: true);
        var before = DamageCalculator.Damage(battle.State, archer, enemy, hit);

        Assert.Null(battle.Engine.Cast(Attacker, "shadow.darkness", null, new CellPosition(20, 10)));

        Assert.Equal(before / 2, DamageCalculator.Damage(battle.State, archer, enemy, hit));
        Assert.Equal(before, DamageCalculator.Damage(battle.State, archer, enemy, hit with { IsRanged = false })); // melee is not darkened
    }

    [Fact]
    public void DarknessIII_GivesOwnStacksInIt2Defense()
    {
        var battle = Battle(("shadow.darkness", 3));
        var own = battle.Add(Attacker, Sturdy("own"), 1, 10, 10);
        var enemy = battle.Add(Defender, TestBattle.Fixed("enemy", 10), 100, 12, 10);
        battle.StartWith(own);
        var blow = new HitModifiers(10, IsRanged: false);
        var before = DamageCalculator.Damage(battle.State, enemy, own, blow);
        var back = DamageCalculator.Damage(battle.State, own, enemy, blow);

        Assert.Null(battle.Engine.Cast(Attacker, "shadow.darkness", null, new CellPosition(10, 10)));

        Assert.Equal(before * 950 / 1000, DamageCalculator.Damage(battle.State, enemy, own, blow)); // −2.5 % a point of Defense
        Assert.Equal(back, DamageCalculator.Damage(battle.State, own, enemy, blow)); // not the enemy's
    }

    // The AI -----------------------------------------------------------------------------------

    [Fact]
    public void TheBattleAi_CastsFireWallOnEnemyStacks()
    {
        var battle = Battle(("chaos.fire-wall", 1));
        var own = battle.Add(Attacker, Sturdy("own"), 1, 0, 0);
        var enemy = battle.Add(Defender, Sturdy("enemy"), 1, 20, 10);
        battle.StartWith(own);

        BattleAi.Act(battle.State, battle.Engine);

        Assert.True(Assert.Single(battle.State.Zones).Rect.Intersects(enemy.Rect));
    }

    private static CreatureDefinition Sturdy(string id) => TestBattle.Fixed(id, 10, health: 1000);

    private static TestBattle Battle(params (string Spell, int Rank)[] spells) => Battle(0, spells);

    private static TestBattle Battle(int spellPower, params (string Spell, int Rank)[] spells) => TestBattle.Create(attackerCaster: new BattleCaster(
        spellPower, 1, spells.Select(s => new KnownSpell(SpellCatalog.Default.Get(s.Spell), s.Rank, SpellCatalog.Default.Get(s.Spell).Mana)).ToList(), null, 100));

    /// <summary>Every stack still to act this round defends, so the next round starts.</summary>
    private static void EndRound(TestBattle battle)
    {
        var round = battle.State.Round;
        while (battle.State.Round == round && battle.State.ActiveUnit is not null)
        {
            Assert.Null(battle.Engine.Defend());
        }
    }
}
