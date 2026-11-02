using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Towns;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The rarer specialties (ADR-093, ADR-107) and the skill specialties built in ADR-114.</summary>
public class SpecialtyTests
{
    private static IEnumerable<HeroDefinition> Roster => HeroCatalog.Default.All;

    // The roster ----------------------------------------------------------------------------------

    [Fact]
    public void FourGoldSpecialists_NoTwoOfOneTown()
    {
        var gold = Roster.Where(h => h.Specialty is { Kind: SpecialtyKind.Resource, Target: "gold", Amount: 350 }).ToList();

        Assert.Equal(4, gold.Count);
        Assert.Equal(4, gold.Select(h => h.Town).Distinct().Count());
    }

    [Fact]
    public void SixRareResourceSpecialists_TwoOfEach_NoTwoOfOneTown_AndNoOreOrLumber()
    {
        var resources = Roster.Where(h => h.Specialty.Kind == SpecialtyKind.Resource).ToList();
        var rare = resources.Where(h => h.Specialty.Target != "gold").ToList();

        Assert.Equal(10, resources.Count);
        Assert.Equal(6, rare.Count);
        Assert.All(rare, h => Assert.Equal(1, h.Specialty.Amount));
        Assert.Equal(["crystal", "crystal", "mercury", "mercury", "sulfur", "sulfur"], rare.Select(h => h.Specialty.Target).Order(StringComparer.Ordinal));
        Assert.Equal(6, rare.Select(h => h.Town).Distinct().Count());
    }

    [Fact]
    public void TwoNecromancySpecialists_ANecropolisMageAndWarrior()
    {
        var necromancers = Roster.Where(h => h.Specialty is { Kind: SpecialtyKind.Skill, Amount: 5 } s && s.Target == NecromancyRules.Skill).ToList();

        Assert.Equal(2, necromancers.Count);
        Assert.All(necromancers, h => Assert.Equal(TownIds.Necropolis, h.Town));
        Assert.Equal([HeroClass.Warrior, HeroClass.Mage], necromancers.Select(h => h.Class).Order());
    }

    [Fact]
    public void TwoFlatLogisticsSpecialists_AndNoPerLevelOnes()
    {
        var logistics = Roster.Where(h => h.Specialty is { Kind: SpecialtyKind.Skill, Target: BuiltInSkills.Logistics }).ToList();

        Assert.Equal(2, logistics.Count);
        Assert.All(logistics, h => Assert.Equal(10, h.Specialty.Amount));
    }

    [Fact]
    public void EveryPerLevelSkillSpecialty_NamesASkillWhoseEffectItRaises()
    {
        string[] built =
        [
            BuiltInSkills.Melee, BuiltInSkills.Archery, BuiltInSkills.Armor, BuiltInSkills.Tactics, BuiltInSkills.WarTempo, BuiltInSkills.Treasury,
            BuiltInSkills.Scouting, BuiltInSkills.BloodRage, BuiltInSkills.Warcry, .. BuiltInSkills.Schools.Keys.Select(ManaRules.AttunementSkill),
        ];

        Assert.All(
            Roster.Where(h => h.Specialty is { Kind: SpecialtyKind.Skill, Amount: 0 }),
            h => Assert.True(built.Contains(h.Specialty.Target), $"{h.Id}: {h.Specialty}"));
    }

    [Fact]
    public void TheHeroesThatLostAResourceSpecialty_TookASkillTheyStartWith() =>
        Assert.All(
            Roster.Where(h => h.Specialty is { Kind: SpecialtyKind.Skill, Amount: 0 } s && s.Target is BuiltInSkills.Armor or BuiltInSkills.Treasury or BuiltInSkills.Scouting or BuiltInSkills.WarTempo),
            h => Assert.True(h.StartingSkills.ContainsKey(h.Specialty.Target), $"{h.Id}: {h.Specialty}"));

    [Theory]
    [InlineData("skill pathfinding.logistics 10", 10)]
    [InlineData("skill magic.necropolis.attunement 5", 5)]
    [InlineData("skill offense.melee", 0)]
    public void FlatSpecialties_ParseAndFormat(string text, int amount)
    {
        var specialty = HeroSpecialty.Parse(text);

        Assert.Equal(amount, specialty.Amount);
        Assert.Equal(text, specialty.ToString());
    }

    [Theory]
    [InlineData("skill offense.melee 5")]
    [InlineData("skill pathfinding.logistics 0")]
    [InlineData("skill nothing.at-all 5")]
    public void FlatSpecialties_OnlyForLogisticsAndDeathAttunement(string text) => Assert.Throws<FormatException>(() => HeroSpecialty.Parse(text));

    // Flat specialties ----------------------------------------------------------------------------

    [Fact]
    public void AFlatLogisticsSpecialist_MovesTenPercentFarther_EvenWithoutTheSkill_AndNotMoreWithLevels()
    {
        var specialist = Make(BuiltInSkills.Logistics, level: 20, amount: 10);
        var plain = Make(BuiltInSkills.Melee, level: 20);

        Assert.Equal(MovementAllowance.Daily(plain, CreatureCatalog) * 110 / 100, MovementAllowance.Daily(specialist, CreatureCatalog));

        specialist.Skills.Set(BuiltInSkills.Logistics, SkillLevel.Basic);
        plain.Skills.Set(BuiltInSkills.Logistics, SkillLevel.Basic);
        Assert.Equal(MovementAllowance.Daily(null, 20), MovementAllowance.Daily(specialist, CreatureCatalog)); // 10 + 10, no +5 % a level
        Assert.Equal(MovementAllowance.Daily(null, 10), MovementAllowance.Daily(plain, CreatureCatalog));
    }

    [Fact]
    public void ANecromancySpecialist_AddsFivePoints_OnlyWithTheSkill()
    {
        var specialist = Make(NecromancyRules.Skill, level: 12, amount: 5);

        Assert.Equal(5, NecromancyRules.HeroBonus(specialist));
        Assert.Equal(0, NecromancyRules.Percent(SkillLevel.None, amplified: false, NecromancyRules.HeroBonus(specialist)));
        Assert.Equal(10, NecromancyRules.Percent(SkillLevel.Basic, amplified: false, NecromancyRules.HeroBonus(specialist)));
        Assert.Equal(30, NecromancyRules.Percent(SkillLevel.Master, amplified: true, NecromancyRules.HeroBonus(specialist))); // with the Amplifier
        Assert.Equal(0, NecromancyRules.HeroBonus(Make(BuiltInSkills.Melee)));
    }

    [Fact]
    public void ANecromancySpecialist_KeepsItsDeathAttunementManaUnscaled()
    {
        var specialist = Make(NecromancyRules.Skill, level: 10, amount: 5);
        specialist.Skills.Set(NecromancyRules.Skill, SkillLevel.Basic);

        Assert.Equal(20, SkillEffects.WithSpecialty(specialist, NecromancyRules.Skill, 20));
    }

    // Skill specialties: +5 % per hero level ---------------------------------------------------------

    [Fact]
    public void Armor_WarTempo_Treasury_AndScouting_Scale_WithTheHerosLevel()
    {
        var armor = Make(BuiltInSkills.Armor, level: 10, BuiltInSkills.Armor, SkillLevel.Master);
        var tempo = Make(BuiltInSkills.WarTempo, level: 10, BuiltInSkills.WarTempo, SkillLevel.Master);
        var treasury = Make(BuiltInSkills.Treasury, level: 10, BuiltInSkills.Treasury, SkillLevel.Basic);
        var scouting = Make(BuiltInSkills.Scouting, level: 10, BuiltInSkills.Scouting, SkillLevel.Expert);

        Assert.Equal(30, HeroBattleBonuses.For(armor, CreatureCatalog).DamageTakenReductionPercent); // 20 × 1.5
        Assert.Equal(6, HeroBattleBonuses.For(tempo, CreatureCatalog).SpeedBonus); // 4 × 1.5
        Assert.Equal(375, SkillEffects.TreasuryGold(treasury)); // 250 × 1.5
        Assert.Equal(375, HeroIncome.Of(treasury).Gold);
        Assert.Equal(Hero.DefaultScoutingRadius + 9, scouting.ScoutingRadius); // 6 × 1.5
    }

    [Fact]
    public void BloodRage_RaisesTheAttackCap_WithTheHerosLevel()
    {
        var specialist = Make(BuiltInSkills.BloodRage, level: 10, BuiltInSkills.BloodRage, SkillLevel.Master);
        var plain = Make(BuiltInSkills.Melee, level: 10, BuiltInSkills.BloodRage, SkillLevel.Master);

        Assert.Equal(12, HeroBattleBonuses.For(specialist, CreatureCatalog).BloodRageMaxAttack);
        Assert.Equal(8, HeroBattleBonuses.For(plain, CreatureCatalog).BloodRageMaxAttack);
        Assert.Equal(4, (HeroBattleBonuses.None with { BloodRage = SkillLevel.Advanced }).BloodRageMaxAttack);
    }

    [Fact]
    public void Warcry_GetsStronger_WithTheHerosLevel()
    {
        var specialist = Make(BuiltInSkills.Warcry, level: 10, BuiltInSkills.Warcry, SkillLevel.Expert);

        Assert.Equal(150, HeroBattleBonuses.For(specialist, CreatureCatalog).WarcryPercent);
        Assert.Equal(100, HeroBattleBonuses.For(Make(BuiltInSkills.Melee, level: 10), CreatureCatalog).WarcryPercent);
    }

    [Fact]
    public void ATacticsSpecialist_DeploysOneColumnDeeper_TwoFromLevelTen_OnlyWithTheSkill()
    {
        Assert.Equal(0, SkillEffects.TacticsSpecialtyColumns(Make(BuiltInSkills.Tactics, level: 12)));
        Assert.Equal(1, SkillEffects.TacticsSpecialtyColumns(Make(BuiltInSkills.Tactics, level: 9, BuiltInSkills.Tactics, SkillLevel.Basic)));
        Assert.Equal(2, SkillEffects.TacticsSpecialtyColumns(Make(BuiltInSkills.Tactics, level: 10, BuiltInSkills.Tactics, SkillLevel.Basic)));
        Assert.Equal(0, SkillEffects.TacticsSpecialtyColumns(Make(BuiltInSkills.Melee, level: 10, BuiltInSkills.Tactics, SkillLevel.Expert)));
    }

    [Fact]
    public void ATacticsSpecialist_GetsTheWiderZone_WhenItDeploys()
    {
        var state = Create("A.B", "...");
        var hero = state.GetHero(A);
        hero.Specialty = new HeroSpecialty(SpecialtyKind.Skill, BuiltInSkills.Tactics);
        hero.Tactics = SkillLevel.Basic;
        hero.Level = 10;
        var session = Start(state);

        session.MoveTo(A, new MapPosition(2, 0));

        Assert.Equal(BattlePhase.Deploying, session.State.Battle?.Phase);
        Assert.Equal(BattleSetup.DeploymentColumns(SkillLevel.Basic) + 2, session.State.Battle!.DeploymentColumns);
    }

    private static EternalWars.Core.Domain.Creatures.CreatureCatalog CreatureCatalog => EternalWars.Core.Domain.Creatures.CreatureCatalog.Default;

    private static Hero Make(string specialty, int level = 1, string? skill = null, SkillLevel skillLevel = SkillLevel.None, int amount = 0)
    {
        var hero = Hero.Create(new HeroId(1), Blue, new MapPosition(0, 0), null, null);
        hero.Specialty = new HeroSpecialty(SpecialtyKind.Skill, specialty, amount);
        hero.Level = level;
        if (skill is not null)
        {
            hero.Skills.Set(skill, skillLevel);
        }

        return hero;
    }
}
