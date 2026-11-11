using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Magic;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;
using static EternalWars.Core.Domain.Battles.BattleSide;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Battles;

/// <summary>Spell ranks II and III (ADR-122): the table's rank effects, how they work in battle, and Mastery raising spells in guilds (MAG-3).</summary>
public class SpellRankTests
{
    // The table --------------------------------------------------------------------------------

    [Fact]
    public void EveryRankEffect_KeepsItsSpellsKind_AndOnlyBattleSpellsHaveThem()
    {
        foreach (var spell in SpellCatalog.Default.All)
        {
            if (spell.Effect is null)
            {
                Assert.True(spell.Rank2 is null && spell.Rank3 is null, spell.Id);
                continue;
            }

            Assert.All(new[] { spell.EffectAt(2), spell.EffectAt(3) }, e => Assert.Equal(spell.Effect.Kind, e!.Kind));
        }

        Assert.True(SpellCatalog.Default.All.Count(s => s.Rank2 is not null || s.Rank3 is not null) >= 40);
    }

    [Fact]
    public void ARankWithoutItsOwnEffect_UsesTheRankBelow()
    {
        // Every shipped battle spell has its own rank III now (ADR-177); a designer's table may still leave a rank empty.
        var mended = new SpellDefinition("x.mend", "Mend", "haven", 1, 5, SpellElement.Water, [], false, SpellEffect.Parse("heal 40"), Rank2: SpellEffect.Parse("heal 60"));
        Assert.Same(mended.EffectAt(2), mended.EffectAt(3));
        Assert.Equal((40, 60, 60), (mended.EffectAt(1)!.Amount, mended.EffectAt(2)!.Amount, mended.EffectAt(3)!.Amount));

        var darkBolt = SpellCatalog.Default.Get("shadow.dark-bolt"); // rank II burns (ADR-123)
        Assert.Equal((0, 20, 20), (darkBolt.EffectAt(1)!.Modifiers.Burn, darkBolt.EffectAt(2)!.Modifiers.Burn, darkBolt.EffectAt(3)!.Modifiers.Burn));
        Assert.Equal(2, darkBolt.EffectAt(3)!.Targets);
    }

    [Fact]
    public void Fireball_GrowsToFiveByFive_ThenSparesOwnStacks()
    {
        var fireball = SpellCatalog.Default.Get("chaos.fireball");

        Assert.Equal((3, false), (fireball.EffectAt(1)!.AreaSize, fireball.EffectAt(1)!.SpareOwn));
        Assert.Equal((5, false), (fireball.EffectAt(2)!.AreaSize, fireball.EffectAt(2)!.SpareOwn));
        Assert.Equal((5, true), (fireball.EffectAt(3)!.AreaSize, fireball.EffectAt(3)!.SpareOwn));
    }

    [Theory]
    [InlineData("damage 40 targets 3", 3, false, SpellFilter.None)]
    [InlineData("damage 35 area 5 spare-own", 1, true, SpellFilter.None)]
    [InlineData("buff melee% 30 rounds 3 allies melee", 1, false, SpellFilter.Melee)]
    [InlineData("damage 40 all ground initiative% -20 rounds 2", 1, false, SpellFilter.Ground)]
    public void TheRankWords_AreParsed(string text, int targets, bool spareOwn, SpellFilter filter)
    {
        var effect = SpellEffect.Parse(text);

        Assert.Equal((targets, spareOwn, filter), (effect.Targets, effect.SpareOwn, effect.Filter));
    }

    [Theory]
    [InlineData("damage 35 area 3 targets 2")]
    [InlineData("damage 40 targets 0")]
    [InlineData("buff attack 3 rounds 3 spare-own")]
    [InlineData("damage 40 spare-own")]
    public void BadRankWords_AreRefused(string text) => Assert.Throws<FormatException>(() => SpellEffect.Parse(text));

    [Fact]
    public void ATableRowWithARankButNoRankOneEffect_OrAnotherKind_IsRefused()
    {
        const string Header = "id,name,school,level,mana,element,tags,adventure,battle,battle2,battle3,notes\n";

        Assert.Throws<FormatException>(() => SpellTable.Parse(Header + "x.a,A,haven,1,5,fire,,,,damage 10,,\n"));
        Assert.Throws<FormatException>(() => SpellTable.Parse(Header + "x.a,A,haven,1,5,fire,,,damage 10,heal 10,,\n"));
    }

    // In battle --------------------------------------------------------------------------------

    [Fact]
    public void MagicArrow_RankTwo_HitsHarder_AndRankThree_AlsoHitsTheEnemyNearestTheTarget()
    {
        var battle = TestBattle.Create(attackerCaster: Caster(("arcana.magic-arrow", 3)));
        var unit = battle.Add(Attacker, TestBattle.Fixed("own", 1), 1, 0, 0);
        var target = battle.Add(Defender, TestBattle.Fixed("target", 1, health: 1000), 1, 20, 5);
        var near = battle.Add(Defender, TestBattle.Fixed("near", 1, health: 1000), 1, 22, 5);
        var far = battle.Add(Defender, TestBattle.Fixed("far", 1, health: 1000), 1, 28, 18);
        battle.StartWith(unit);

        Assert.Null(battle.Engine.Cast(Attacker, "arcana.magic-arrow", target.Id, null));

        Assert.Equal((940, 940, 1000), (target.TopHealth, near.TopHealth, far.TopHealth));
    }

    [Fact]
    public void ASecondTarget_MustBeOneTheSpellWorksOn()
    {
        var battle = TestBattle.Create(attackerCaster: Caster(("arcana.magic-arrow", 3)));
        var unit = battle.Add(Attacker, TestBattle.Fixed("own", 1), 1, 0, 0);
        var target = battle.Add(Defender, TestBattle.Fixed("target", 1, health: 1000), 1, 20, 5);
        var warded = battle.Add(Defender, TestBattle.Fixed("warded", 1, health: 1000, capabilities: new CreatureCapability(CapabilityKind.SpellResistance, 2)), 1, 21, 5);
        var next = battle.Add(Defender, TestBattle.Fixed("next", 1, health: 1000), 1, 24, 5);
        battle.StartWith(unit);

        Assert.Null(battle.Engine.Cast(Attacker, "arcana.magic-arrow", target.Id, null));

        Assert.Equal((940, 1000, 940), (target.TopHealth, warded.TopHealth, next.TopHealth));
    }

    [Fact]
    public void Fireball_RankThree_SparesTheCastersOwnStacksInTheArea()
    {
        var battle = TestBattle.Create(attackerCaster: Caster(("chaos.fireball", 3)));
        var unit = battle.Add(Attacker, TestBattle.Fixed("own", 1), 1, 0, 0);
        var near = battle.Add(Attacker, TestBattle.Fixed("near", 1, health: 100), 1, 9, 9);
        var enemy = battle.Add(Defender, TestBattle.Fixed("enemy", 1, health: 100), 1, 13, 12);
        battle.StartWith(unit);

        Assert.Null(battle.Engine.Cast(Attacker, "chaos.fireball", null, new CellPosition(11, 10)));

        Assert.Equal((100, 65), (near.TopHealth, enemy.TopHealth)); // 5 × 5 reaches (13, 12)
    }

    [Fact]
    public void Bless_RankTwo_BlessesEveryOwnStack()
    {
        var battle = TestBattle.Create(attackerCaster: Caster(("light.bless", 2)));
        var a = battle.Add(Attacker, TestBattle.Fixed("a", 1), 1, 0, 0);
        var b = battle.Add(Attacker, TestBattle.Fixed("b", 1), 1, 0, 10);
        var enemy = battle.Add(Defender, TestBattle.Fixed("enemy", 1), 1, 20, 0);
        battle.StartWith(a);

        Assert.Null(battle.Engine.Cast(Attacker, "light.bless", null, null));

        Assert.All(new[] { a, b }, u => Assert.Equal(1, u.SpellModifiers.Roll));
        Assert.Empty(enemy.Spells);
    }

    [Fact]
    public void BearsStrength_RankThree_StrengthensOnlyTheMeleeStacks()
    {
        var battle = TestBattle.Create(attackerCaster: Caster(("nature.bears-strength", 3)));
        var melee = battle.Add(Attacker, TestBattle.Fixed("melee", 1), 1, 0, 0);
        var shooter = battle.Add(Attacker, TestBattle.Fixed("shooter", 1, shots: 10), 1, 0, 10);
        battle.Add(Defender, TestBattle.Fixed("enemy", 1), 1, 20, 0);
        battle.StartWith(melee);

        Assert.Null(battle.Engine.Cast(Attacker, "nature.bears-strength", null, null));

        Assert.Equal((30, 0), (melee.SpellModifiers.MeleePercent, shooter.SpellModifiers.MeleePercent));
    }

    [Fact]
    public void Earthquake_RankTwo_SparesFlyers_AndRankThree_TheCastersOwnStacks()
    {
        int[] Quake(int rank)
        {
            var battle = TestBattle.Create(attackerCaster: Caster(("nature.earthquake", rank)));
            var own = battle.Add(Attacker, TestBattle.Fixed("own", 1, health: 100), 1, 0, 0);
            var walker = battle.Add(Defender, TestBattle.Fixed("walker", 1, health: 100), 1, 20, 0);
            var flyer = battle.Add(Defender, TestBattle.Fixed("flyer", 1, health: 100, capabilities: new CreatureCapability(CapabilityKind.Flying)), 1, 20, 10);
            battle.StartWith(own);
            Assert.Null(battle.Engine.Cast(Attacker, "nature.earthquake", null, null));
            return [own.TopHealth, walker.TopHealth, flyer.TopHealth];
        }

        Assert.Equal([60, 60, 60], Quake(1));
        Assert.Equal([60, 60, 100], Quake(2));
        Assert.Equal([100, 60, 100], Quake(3));
    }

    [Fact]
    public void TheSummons_RankThree_CallTheUpgradedCreature()
    {
        Assert.Equal(["sylvan.silver-pegasus"], SpellCatalog.Default.Get("nature.summon-pack").EffectAt(3)!.Summons);
        var grove = SpellCatalog.Default.Get("nature.awaken-grove");
        Assert.Equal((true, false), (grove.EffectAt(2)!.Rooted, grove.EffectAt(3)!.Rooted));
        Assert.All(
            SpellCatalog.Default.All.Select(s => s.EffectAt(3)).OfType<SpellEffect>().SelectMany(e => e.Summons),
            id => Assert.True(CreatureCatalog.Default.TryGet(id, out _), id));
    }

    [Fact]
    public void RankThree_CountsOneLevelHigherAgainstCreatureSpellResistance()
    {
        var resistant = TestBattle.Fixed("warded", 1, health: 1000, capabilities: new CreatureCapability(CapabilityKind.SpellResistance, 1));

        string? Refusal(int rank)
        {
            var battle = TestBattle.Create(attackerCaster: Caster(("arcana.magic-arrow", rank)));
            var unit = battle.Add(Attacker, TestBattle.Fixed("own", 1), 1, 0, 0);
            var target = battle.Add(Defender, resistant, 1, 20, 0);
            battle.StartWith(unit);
            return BattleMagic.CastRefusal(battle.State, Attacker, "arcana.magic-arrow", target.Id, null);
        }

        Assert.NotNull(Refusal(2));
        Assert.Null(Refusal(3)); // a level 1 spell at rank III counts as level 2
    }

    // Mastery ----------------------------------------------------------------------------------

    [Theory]
    [InlineData(SkillLevel.None, 1)]
    [InlineData(SkillLevel.Basic, 2)]
    [InlineData(SkillLevel.Advanced, 3)]
    public void Mastery_AllowsRankTwoAtBasic_AndThreeAtAdvanced(SkillLevel mastery, int rank)
    {
        var hero = Hero.Create(new HeroId(9), Blue, new MapPosition(0, 0), null, HeroCatalog.Default.Get("haven.ansel"));
        hero.Skills.Set(MagicRules.MasterySkill(TownIds.Haven), mastery);

        Assert.Equal(rank, MagicRules.MaxRank(hero, SpellCatalog.Default.Get("light.bless")));
        Assert.Equal(1, MagicRules.MaxRank(hero, SpellCatalog.Default.Get("arcana.magic-arrow"))); // another school's Mastery
    }

    [Fact]
    public void AHeroWithMastery_VisitingAGuildThatOffersAKnownSpell_RaisesIt_Free()
    {
        var (session, town, hero) = HeroBeforeTown();
        town.SetLevel(BuiltInBuildings.MageGuild, 1);
        var spell = town.GuildSpells[1][0];
        hero.HasSpellBook = true;
        hero.Learn(spell);
        hero.Skills.Set(MagicRules.MasterySkill(TownIds.Haven), SkillLevel.Advanced);
        var gold = session.State.GetPlayer(Blue).Resources;
        var raised = new List<SpellsRaised>();
        session.Events.Subscribe<SpellsRaised>(raised.Add);

        session.Step(A, Direction.North);

        Assert.Equal(3, hero.Spells[spell]);
        Assert.Equal(new SpellRank(spell, 3), Assert.Single(Assert.Single(raised).Spells));
        Assert.Equal(gold, session.State.GetPlayer(Blue).Resources);
    }

    [Fact]
    public void SpellsLearnedInAGuild_RiseThereAtOnce_ButNotWithoutMastery_OrSpellsTheGuildDoesNotOffer()
    {
        var (session, town, hero) = HeroBeforeTown();
        town.SetLevel(BuiltInBuildings.MageGuild, 1);
        hero.HasSpellBook = true;
        hero.Learn("arcana.magic-arrow"); // not offered in a Haven guild
        hero.Skills.Set(MagicRules.MagicSkill(TownIds.Haven), SkillLevel.Basic);
        hero.Skills.Set(MagicRules.MasterySkill(TownIds.Haven), SkillLevel.Basic);
        hero.Skills.Set(MagicRules.MasterySkill(TownIds.Academy), SkillLevel.Advanced);

        session.Step(A, Direction.North);

        Assert.All(town.GuildSpells[1], s => Assert.Equal(2, hero.Spells[s]));
        Assert.Equal(1, hero.Spells["arcana.magic-arrow"]);
    }

    [Fact]
    public void WithoutMastery_NothingRises()
    {
        var (session, town, hero) = HeroBeforeTown();
        town.SetLevel(BuiltInBuildings.MageGuild, 1);
        hero.HasSpellBook = true;
        hero.Skills.Set(MagicRules.MagicSkill(TownIds.Haven), SkillLevel.Basic);
        var raised = new List<SpellsRaised>();
        session.Events.Subscribe<SpellsRaised>(raised.Add);

        session.Step(A, Direction.North);

        Assert.All(hero.Spells.Values, r => Assert.Equal(1, r));
        Assert.Empty(raised);
    }

    [Fact]
    public void TheBattleCaster_CarriesTheRank_AndCastsItsEffect()
    {
        var hero = Hero.Create(new HeroId(9), Blue, new MapPosition(0, 0), null, HeroCatalog.Default.Get("haven.ansel"));
        hero.RaiseSpell("light.bless", 2);

        var known = BattleCaster.For(hero, SpellCatalog.Default)!.Find("light.bless")!;

        Assert.Equal((2, SpellReach.Side), (known.Rank, known.Effect!.Reach));
    }

    private static BattleCaster Caster(params (string Spell, int Rank)[] spells) =>
        new(0, 1, spells.Select(s => new KnownSpell(SpellCatalog.Default.Get(s.Spell), s.Rank, SpellCatalog.Default.Get(s.Spell).Mana)).ToList(), null, 100);

    /// <summary>Blue's hero A one step south of an own Haven town's entrance.</summary>
    private static (GameSession Session, Town Town, Hero Hero) HeroBeforeTown()
    {
        var state = Create("............", "............", "............", "............", "............", "............", "............", "...A........", "...........B");
        var town = AddTown(state, TownIds.Haven, 0, 0, Blue);
        var session = Start(state);
        return (session, town, state.GetHero(A));
    }
}
