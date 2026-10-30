using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Magic;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Towns;
using static EternalWars.Core.Domain.Battles.BattleSide;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The artifact re-draft's new effect kinds (ADR-111): sight, mana, creature health, growth, Necromancy, immunities, no retreat, the magic ones.</summary>
public class ArtifactEffectTests
{
    private static Hero Outfit(GameState state, HeroId id, params string[] worn)
    {
        var hero = state.GetHero(id);
        hero.Artifacts.ReplaceWith(worn, [], state.Artifacts);
        state.RefreshArtifacts(hero);
        return hero;
    }

    [Fact]
    public void TheRedraftedTable_HasTheNewRows_AndTheRenamedIds()
    {
        var catalog = ArtifactCatalog.Default;

        foreach (var id in new[] { "badge-of-courage", "lizard-warriors-flail", "buckler-of-the-lizard-king", "dragon-scale-armor", "unwritten-codex", "orb-of-silt" })
        {
            Assert.True(catalog.Contains(id), id);
        }

        foreach (var id in new[] { "ash-sigil", "greater-gnolls-flail", "buckler-of-the-gnoll-king", "scale-of-the-elder-dragon" })
        {
            Assert.False(catalog.Contains(id), id);
        }

        Assert.Equal("Ashen Court Regalia", catalog.GetSet("ash-court-regalia").Name);
        Assert.Equal(4, catalog.PiecesOf("firstborn-regalia").Count());
    }

    [Fact]
    public void ManaArtifacts_RaiseTheDailyManaAndThePool_SightArtifactsTheScoutingRadius()
    {
        var state = Create("A.B");
        var hero = state.GetHero(A);
        var (daily, max, radius) = (ManaRules.DailyMana(hero), ManaRules.MaxMana(hero), hero.ScoutingRadius);

        Outfit(state, A, "ring-of-the-wellspring", "mystic-orb-of-mana", "speculum", "spyglass");

        Assert.Equal(daily + 5, ManaRules.DailyMana(hero));
        Assert.Equal(max + 10, ManaRules.MaxMana(hero));
        Assert.Equal(radius + 3, hero.ScoutingRadius);
    }

    [Fact]
    public void GrowthArtifacts_OfTheOwnersHeroes_AddToTheWeeklyGrowthOfItsTowns()
    {
        var state = Create("............", "............", "............", "............", "............", "............", "............", "..........A.", "...........B");
        var town = AddTown(state, TownIds.Haven, 0, 0, Blue);
        var line = state.Creatures.Get(BuiltInCreatures.Footman);
        Outfit(state, A, "head-of-legion", "torso-of-legion");

        Assert.Equal(10, TownRules.ArtifactGrowthPercent(state, town));
        Assert.Equal(
            line.WeeklyGrowth + (line.WeeklyGrowth * (TownRules.GrowthBonusPercent(town) + 10) / 100),
            TownRules.WeeklyGrowth(state, town, line, TownRules.ArtifactGrowthPercent(state, town)));
    }

    [Fact]
    public void HealthArtifacts_GiveTheArmysCreaturesMoreHealth_InBattle()
    {
        var state = Create("A.B", "...");
        state.GetHero(A).Army.Set(0, new CreatureStack(BuiltInCreatures.Footman, 10));
        state.GetHero(B).Army.Set(0, new CreatureStack(BuiltInCreatures.Footman, 10));
        Outfit(state, A, "vial-of-lifeblood", "ring-of-vitality"); // +15 %
        var session = Start(state);

        session.MoveTo(A, new MapPosition(2, 0));

        var health = state.Creatures.Get(BuiltInCreatures.Footman).Health;
        var battle = state.Battle!;
        Assert.Contains(battle.Units, u => u.Side == Attacker && !u.IsMachine);
        Assert.Contains(battle.Units, u => u.Side == Defender && !u.IsMachine);
        Assert.All(battle.Units.Where(u => u.Side == Attacker && !u.IsMachine), u => Assert.Equal(health * 115 / 100, u.Creature.Health));
        Assert.All(battle.Units.Where(u => u.Side == Defender && !u.IsMachine), u => Assert.Equal(health, u.Creature.Health));
    }

    [Fact]
    public void NecromancyPoints_AddToTheShare_OnlyWithTheSkill()
    {
        Assert.Equal(0, NecromancyRules.Percent(SkillLevel.None, amplified: false, artifactPercent: 10));
        Assert.Equal(5 + 10, NecromancyRules.Percent(SkillLevel.Basic, amplified: false, artifactPercent: 10));
    }

    [Fact]
    public void ArmyImmunities_StopTheEnemysHostileSpellsWithTheTag()
    {
        var immune = HeroBattleBonuses.None with { Immunities = new HashSet<string> { "mind" } };
        var battle = TestBattle.Create(attackerCaster: Caster(30, "death.sorrow", "chaos.firebolt"), defenderBonuses: immune);
        var unit = battle.Add(Attacker, TestBattle.Fixed("own", 1), 1, 0, 0);
        var enemy = battle.Add(Defender, TestBattle.Fixed("enemy", 1, health: 1000), 1, 20, 0);
        battle.StartWith(unit);

        Assert.Equal("Sorrow has no effect on enemy.", battle.Engine.Cast(Attacker, "death.sorrow", enemy.Id, null));
        Assert.Null(battle.Engine.Cast(Attacker, "chaos.firebolt", enemy.Id, null));
    }

    [Fact]
    public void TheRecantersCloak_ForbidsSpellsAboveLevelTwo_ToBothSides()
    {
        var cloak = HeroBattleBonuses.None with { SpellLevelCap = 2 };
        var battle = TestBattle.Create(attackerCaster: Caster(30, "light.smite", "chaos.firebolt"), defenderBonuses: cloak);
        var unit = battle.Add(Attacker, TestBattle.Fixed("own", 1), 1, 0, 0);
        var enemy = battle.Add(Defender, TestBattle.Fixed("enemy", 1, health: 1000), 1, 20, 0);
        battle.StartWith(unit);

        Assert.Equal("No spell above level 2 can be cast in this battle.", battle.Engine.Cast(Attacker, "light.smite", enemy.Id, null));
        Assert.Null(battle.Engine.Cast(Attacker, "chaos.firebolt", enemy.Id, null));
    }

    [Fact]
    public void NoRetreat_KeepsBothSidesOnTheField()
    {
        var battle = TestBattle.Create(attackerBonuses: HeroBattleBonuses.None with { NoRetreat = true });
        battle.Add(Attacker, TestBattle.Fixed("own", 1), 1, 0, 0);
        battle.Add(Defender, TestBattle.Fixed("enemy", 1), 100, 20, 0);

        Assert.Equal(SurrenderRules.LockedRefusal, SurrenderRules.CheckRetreat(battle.State, Attacker));
        Assert.Equal(SurrenderRules.LockedRefusal, SurrenderRules.CheckRetreat(battle.State, Defender));
        Assert.False(SurrenderRules.ShouldRetreat(battle.State, Attacker));
    }

    [Fact]
    public void ElementalOrbs_RaiseTheirElementsSpells_OfAnySchool()
    {
        var caster = Caster(30, "chaos.firebolt", "nature.call-lightning") with
        {
            ElementPercent = new Dictionary<SpellElement, int> { [SpellElement.Fire] = 25 },
        };
        var battle = TestBattle.Create(attackerCaster: caster);
        var unit = battle.Add(Attacker, TestBattle.Fixed("own", 1), 1, 0, 0);
        battle.Add(Defender, TestBattle.Fixed("enemy", 1, health: 1000), 1, 20, 0);
        battle.StartWith(unit);

        Assert.Equal(45 * 125 / 100, BattleMagic.Amount(battle.State, Attacker, caster.Find("chaos.firebolt")!));
        Assert.Equal(60, BattleMagic.Amount(battle.State, Attacker, caster.Find("nature.call-lightning")!));
    }

    [Fact]
    public void TheOrbsAndTheCodex_ReachTheHerosBattleCaster_OnlyWithASpellBook()
    {
        var state = Create("A.B");
        var hero = Outfit(state, A, "unwritten-codex", "orb-of-tempestuous-fire");

        hero.HasSpellBook = false;
        Assert.Null(BattleCaster.For(hero, state.Spells));

        hero.HasSpellBook = true;
        var caster = BattleCaster.For(hero, state.Spells)!;
        Assert.Equal(state.Spells.All.Where(s => s.Level <= 2).Select(s => s.Id).Order(StringComparer.Ordinal), caster.Spells.Select(s => s.Spell.Id));
        Assert.All(caster.Spells, s => Assert.Equal(1, s.Rank));
        Assert.Equal(25, caster.ElementBonus(state.Spells.Get("chaos.firebolt")));
    }

    private static BattleCaster Caster(int mana, params string[] spells) =>
        new(0, 1, spells.Select(s => new KnownSpell(SpellCatalog.Default.Get(s), 1, SpellCatalog.Default.Get(s).Mana)).ToList(), null, mana);
}
