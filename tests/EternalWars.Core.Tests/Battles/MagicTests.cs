using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
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

/// <summary>Magic, first slice (ADR-110): the spell table, guild spells, spell books, learning, and the hero's casting in battle.</summary>
public class MagicTests
{
    // The spell table ---------------------------------------------------------------------------

    [Fact]
    public void TheSpellTable_HasSeventeenSpellsASchool_FiveFiveFourThreeByLevel()
    {
        var spells = SpellCatalog.Default;

        Assert.Equal(102, spells.All.Count());
        foreach (var school in BuiltInSkills.Schools.Keys)
        {
            Assert.Equal([5, 5, 4, 3], Enumerable.Range(1, 4).Select(level => spells.Of(school, level).Count));
        }
    }

    [Fact]
    public void EverySpellSpecialty_NamesASpellOfTheTable() =>
        Assert.All(
            HeroCatalog.Default.All.Where(h => h.Specialty is { Kind: SpecialtyKind.Spell }),
            h => Assert.True(SpellCatalog.Default.Contains(h.Specialty!.Target), $"{h.Id}: {h.Specialty.Target}"));

    [Theory]
    [InlineData("damage 35 area 3", SpellEffectKind.Damage, 35, SpellReach.Area, 3, null)]
    [InlineData("curse morale -2 rounds battle", SpellEffectKind.Curse, 0, SpellReach.Stack, 0, null)]
    [InlineData("buff attack 3 rounds 3", SpellEffectKind.Buff, 0, SpellReach.Stack, 0, 3)]
    [InlineData("damage 30 all living", SpellEffectKind.Damage, 30, SpellReach.All, 0, null)]
    public void BattleEffects_AreParsed(string text, SpellEffectKind kind, int amount, SpellReach reach, int area, int? rounds)
    {
        var effect = SpellEffect.Parse(text);

        Assert.Equal((kind, amount, reach, area, rounds), (effect.Kind, effect.Amount, effect.Reach, effect.AreaSize, effect.Rounds));
    }

    [Theory]
    [InlineData("buff attack 3")]
    [InlineData("curse rounds 2")]
    [InlineData("damage 10 area")]
    [InlineData("heal")]
    [InlineData("damage 10 sparkle 3")]
    public void BadBattleEffects_AreRefused(string text) => Assert.Throws<FormatException>(() => SpellEffect.Parse(text));

    // Spell books and guilds ----------------------------------------------------------------------

    [Fact]
    public void MagicTownMages_StartWithABookAndTheirSpecialtySpell_WarriorsAndPrimalFuryMagesWithout()
    {
        Hero Make(string id) => Hero.Create(new HeroId(9), Blue, new MapPosition(0, 0), null, HeroCatalog.Default.Get(id));

        var mage = Make("haven.ansel");
        Assert.True(mage.HasSpellBook);
        Assert.Equal(["light.bless"], mage.Spells.Keys);
        Assert.False(Make("haven.aldric").HasSpellBook);
        Assert.False(Make("stronghold.ugra").HasSpellBook);
    }

    [Fact]
    public void Guilds_DrawThreeThreeTwoTwoSpellsOfTheTownsSchool_AtGameStart_AndNoneForPrimalFuryTowns()
    {
        var state = Create(WideMap);
        var haven = AddTown(state, TownIds.Haven, 0, 0, Blue);
        var stronghold = AddTown(state, TownIds.Stronghold, 8, 0);
        Start(state);

        Assert.Equal([1, 2, 3, 4], haven.GuildSpells.Keys.Order());
        Assert.Equal([3, 3, 2, 2], haven.GuildSpells.OrderBy(l => l.Key).Select(l => l.Value.Count));
        foreach (var (level, spells) in haven.GuildSpells)
        {
            Assert.Equal(spells.Count, spells.Distinct().Count());
            Assert.All(spells, s => Assert.Equal((TownIds.Haven, level), (state.Spells.Get(s).School, state.Spells.Get(s).Level)));
        }

        Assert.Empty(stronghold.GuildSpells);
    }

    [Fact]
    public void TheGuildDraw_FollowsTheGameSeed()
    {
        IReadOnlyDictionary<int, IReadOnlyList<string>> Draw(ulong seed)
        {
            var state = Create(GameOptionsWithSeed(seed), WideMap);
            var town = AddTown(state, TownIds.Academy, 0, 0, Blue);
            Start(state);
            return town.GuildSpells;
        }

        Assert.Equal(Draw(11).SelectMany(l => l.Value), Draw(11).SelectMany(l => l.Value));
        Assert.Contains(Enumerable.Range(12, 10), s => !Draw((ulong)s).SelectMany(l => l.Value).SequenceEqual(Draw(11).SelectMany(l => l.Value)));
    }

    [Fact]
    public void AHeroEnteringATownWithAGuild_LearnsTheSpellsItsMagicSkillAllows_ButKeepsItsMana()
    {
        var (session, town, hero) = HeroBeforeTown();
        town.SetLevel(BuiltInBuildings.MageGuild, 2);
        hero.HasSpellBook = true;
        hero.Skills.Set(MagicRules.MagicSkill(TownIds.Haven), SkillLevel.Basic);
        hero.Mana = 1;
        var learned = new List<SpellsLearned>();
        session.Events.Subscribe<SpellsLearned>(learned.Add);

        session.Step(A, Direction.North);

        Assert.Equal(town.GuildSpells[1], Assert.Single(learned).Spells);
        Assert.Equal(town.GuildSpells[1].Order(StringComparer.Ordinal), hero.Spells.Keys); // level 2 needs Advanced
        Assert.Equal(1, hero.Mana); // only a night there refills it
    }

    [Fact]
    public void ANightInATownWithAGuild_RefillsTheMana_BookOrNot()
    {
        var (session, town, hero) = HeroBeforeTown();
        town.SetLevel(BuiltInBuildings.MageGuild, 1);
        hero.Skills.Set(MagicRules.MagicSkill(TownIds.Haven), SkillLevel.Master);
        session.Step(A, Direction.North);
        hero.Mana = 0;

        session.EndTurn(); // Blue
        session.EndTurn(); // Red; Blue's next day starts with the hero in the town

        Assert.Empty(hero.Spells);
        Assert.Equal(hero.MaxMana, hero.Mana);
    }

    [Fact]
    public void ANightOutsideTheTown_OnlyGivesTheDailyMana()
    {
        var (session, town, hero) = HeroBeforeTown();
        town.SetLevel(BuiltInBuildings.MageGuild, 1);
        hero.Mana = 0;

        session.EndTurn();
        session.EndTurn();

        Assert.Equal(ManaRules.DailyMana(hero), hero.Mana);
    }

    [Fact]
    public void ASpellBook_CostsFiveHundredGoldInAGuild_AndTheHeroLearnsRightAway()
    {
        var (session, town, hero) = HeroBeforeTown();
        var state = session.State;
        state.GetPlayer(Blue).Resources = new(Gold: 600);
        hero.Skills.Set(MagicRules.MagicSkill(TownIds.Haven), SkillLevel.Basic);
        session.Step(A, Direction.North);

        Assert.Equal($"{town.Name} has no mage guild.", MagicRules.CheckBuySpellBook(state, town));
        town.SetLevel(BuiltInBuildings.MageGuild, 1);
        session.Execute(new BuySpellBookCommand(Blue, town.Id)).ThrowIfRejected();

        Assert.True(hero.HasSpellBook);
        Assert.Equal(100, state.GetPlayer(Blue).Resources.Gold);
        Assert.Equal(town.GuildSpells[1].Order(StringComparer.Ordinal), hero.Spells.Keys);
        Assert.False(session.Execute(new BuySpellBookCommand(Blue, town.Id)).IsAccepted);
    }

    // Casting in battle ---------------------------------------------------------------------------

    [Fact]
    public void MagicArrow_DoesBaseTimesSpellPower_CostsMana_OnceARound_AndTheUnitKeepsItsTurn()
    {
        var battle = TestBattle.Create(attackerCaster: Caster(spellPower: 5, mana: 20, "arcana.magic-arrow"));
        var unit = battle.Add(Attacker, TestBattle.Fixed("caster-army", 1), 1, 0, 0);
        var enemy = battle.Add(Defender, TestBattle.Fixed("target", 1, health: 1000), 1, 20, 0);
        battle.StartWith(unit);

        Assert.Null(battle.Engine.Cast(Attacker, "arcana.magic-arrow", enemy.Id, null));

        Assert.Equal(1000 - 60, enemy.TopHealth); // 40 × (10 + 5) / 10
        Assert.Equal(15, battle.State.ManaOf(Attacker));
        Assert.Same(unit, battle.State.ActiveUnit);
        Assert.Equal("The hero has already cast a spell this round.", battle.Engine.Cast(Attacker, "arcana.magic-arrow", enemy.Id, null));
        Assert.Single(battle.Events.OfType<BattleSpellCast>());
    }

    [Fact]
    public void Casting_IsRefused_WithoutABook_AnUnknownSpell_TooLittleMana_OrTheWrongTarget()
    {
        var battle = TestBattle.Create(attackerCaster: Caster(spellPower: 0, mana: 5, "arcana.magic-arrow", "light.bless", "arcana.slow"));
        var unit = battle.Add(Attacker, TestBattle.Fixed("own", 1), 1, 0, 0);
        var enemy = battle.Add(Defender, TestBattle.Fixed("enemy", 1), 1, 20, 0);
        battle.StartWith(unit);

        Assert.Equal("The hero has no spell book.", BattleMagic.CastRefusal(battle.State, Defender, "arcana.magic-arrow", unit.Id, null));
        Assert.StartsWith("The hero does not know", battle.Engine.Cast(Attacker, "chaos.fireball", null, new CellPosition(20, 0)));
        Assert.StartsWith("Not enough mana", battle.Engine.Cast(Attacker, "arcana.slow", enemy.Id, null));
        Assert.Equal("Magic Arrow is cast on enemy stacks.", battle.Engine.Cast(Attacker, "arcana.magic-arrow", unit.Id, null));
        Assert.Equal("Bless is cast on own stacks.", battle.Engine.Cast(Attacker, "light.bless", enemy.Id, null));
        Assert.Empty(battle.Events.OfType<BattleSpellCast>());
    }

    [Fact]
    public void SpellResistanceAndInterference_CutTheDamage_AndRaiseTheCost()
    {
        var bonuses = HeroBattleBonuses.None with { SpellResistancePercent = 20, EnemySpellPowerPenalty = 4, EnemySpellCostPenalty = 2 };
        var battle = TestBattle.Create(attackerCaster: Caster(spellPower: 10, mana: 20, "arcana.magic-arrow"), defenderBonuses: bonuses);
        var unit = battle.Add(Attacker, TestBattle.Fixed("own", 1), 1, 0, 0);
        var enemy = battle.Add(Defender, TestBattle.Fixed("enemy", 1, health: 1000), 1, 20, 0);
        battle.StartWith(unit);

        Assert.Null(battle.Engine.Cast(Attacker, "arcana.magic-arrow", enemy.Id, null));

        Assert.Equal(1000 - 51, enemy.TopHealth); // 40 × (10 + 6) / 10 = 64, less 20 %
        Assert.Equal(20 - 7, battle.State.ManaOf(Attacker));
    }

    [Fact]
    public void HolyWord_HitsOnlyUndeadAndDemons_AtDoubleDamage()
    {
        var battle = TestBattle.Create(attackerCaster: Caster(spellPower: 0, mana: 30, "light.holy-word"));
        var unit = battle.Add(Attacker, TestBattle.Fixed("own", 1), 1, 0, 0);
        var undead = battle.Add(Defender, TestBattle.Fixed("ghoul", 1, health: 1000) with { Tags = [CreatureTags.Undead] }, 1, 20, 0);
        var living = battle.Add(Defender, TestBattle.Fixed("farmer", 1, health: 1000), 1, 20, 5);
        battle.StartWith(unit);

        Assert.Null(battle.Engine.Cast(Attacker, "light.holy-word", null, null));

        Assert.Equal(1000 - 100, undead.TopHealth);
        Assert.Equal(1000, living.TopHealth);
        Assert.Equal(living.Id, Assert.Single(battle.Events.OfType<BattleSpellResisted>()).Unit);
    }

    [Fact]
    public void ImmuneAndResistantCreatures_AreNotTouched()
    {
        var battle = TestBattle.Create(attackerCaster: Caster(spellPower: 0, mana: 30, "chaos.fireball", "arcana.magic-arrow"));
        var unit = battle.Add(Attacker, TestBattle.Fixed("own", 1), 1, 0, 0);
        var immune = battle.Add(Defender, TestBattle.Fixed("salamander", 1, 50, 10, 10, CreatureSize.Small, 10, 10, 0,
            new CreatureCapability(CapabilityKind.SpellImmunity, Parameter: SpellTags.Fire)), 1, 20, 10);
        var resistant = battle.Add(Defender, TestBattle.Fixed("warded", 1, 50, 10, 10, CreatureSize.Small, 10, 10, 0,
            new CreatureCapability(CapabilityKind.SpellResistance, 1)), 1, 20, 20);
        battle.StartWith(unit);

        Assert.Equal("Magic Arrow has no effect on warded.", BattleMagic.CastRefusal(battle.State, Attacker, "arcana.magic-arrow", resistant.Id, null));
        Assert.Null(battle.Engine.Cast(Attacker, "chaos.fireball", null, new CellPosition(20, 10)));
        Assert.Equal(50, immune.TopHealth);
    }

    [Fact]
    public void AnAreaSpell_HitsEveryStackWithACellInTheArea_OwnOnesToo()
    {
        var battle = TestBattle.Create(attackerCaster: Caster(spellPower: 0, mana: 30, "chaos.fireball"));
        var unit = battle.Add(Attacker, TestBattle.Fixed("own", 1), 1, 0, 0);
        var near = battle.Add(Attacker, TestBattle.Fixed("near", 1, health: 100), 1, 10, 9);
        var enemy = battle.Add(Defender, TestBattle.Fixed("enemy", 1, health: 100), 1, 12, 11);
        var far = battle.Add(Defender, TestBattle.Fixed("far", 1, health: 100), 1, 20, 20);
        battle.StartWith(unit);

        Assert.Null(battle.Engine.Cast(Attacker, "chaos.fireball", null, new CellPosition(11, 10)));

        Assert.Equal((65, 65, 100), (near.TopHealth, enemy.TopHealth, far.TopHealth));
    }

    [Fact]
    public void ABuff_ChangesTheStack_ForItsRounds_ThenExpires()
    {
        var battle = TestBattle.Create(attackerCaster: Caster(spellPower: 0, mana: 30, "light.righteous-might"));
        var unit = battle.Add(Attacker, TestBattle.Fixed("own", 10, attack: 10), 1, 0, 0);
        var enemy = battle.Add(Defender, TestBattle.Fixed("enemy", 1, health: 1000, defense: 10), 1, 20, 0);
        battle.StartWith(unit);
        var before = DamageCalculator.Damage(battle.State, unit, enemy, new HitModifiers(10, false));

        Assert.Null(battle.Engine.Cast(Attacker, "light.righteous-might", unit.Id, null));

        Assert.Equal(3, unit.SpellModifiers.Attack);
        Assert.Equal(before * 115 / 100, DamageCalculator.Damage(battle.State, unit, enemy, new HitModifiers(10, false)));
        Assert.Equal(1 + 3, Assert.Single(unit.Spells).UntilRound);
        for (var round = 0; round < 3; round++)
        {
            Assert.Null(battle.Engine.Defend());
            Assert.Null(battle.Engine.Defend());
        }

        Assert.Equal(4, battle.State.Round);
        Assert.Single(unit.Spells);
        Assert.Null(battle.Engine.Defend());
        Assert.Null(battle.Engine.Defend());
        Assert.Empty(unit.Spells);
        Assert.Equal(new BattleSpellExpired(unit.Id, "light.righteous-might"), Assert.Single(battle.Events.OfType<BattleSpellExpired>()));
    }

    [Fact]
    public void Bless_RollsTheMaximum_AndCurse_TheMinimum()
    {
        var spread = TestBattle.Fixed("spread", 1, health: 1000) with { MinDamage = 1, MaxDamage = 9 };
        var battle = TestBattle.Create(attackerCaster: Caster(spellPower: 0, mana: 30, "light.bless"), defenderCaster: Caster(spellPower: 0, mana: 30, "death.curse"));
        var blessed = battle.Add(Attacker, spread, 1, 0, 0);
        var cursed = battle.Add(Attacker, spread, 1, 0, 4);
        battle.Add(Defender, spread, 1, 20, 0);
        battle.StartWith(blessed);

        Assert.Null(battle.Engine.Cast(Attacker, "light.bless", blessed.Id, null));
        Assert.Null(battle.Engine.Cast(Defender, "death.curse", cursed.Id, null));

        Assert.Equal((9, 1, 5), (DamageCalculator.Roll(blessed, 5), DamageCalculator.Roll(cursed, 5), DamageCalculator.Roll(battle.State.Units[2], 5)));
    }

    [Fact]
    public void TheBattleAi_CastsItsDamageSpellWorthMost()
    {
        var battle = TestBattle.Create(attackerCaster: Caster(spellPower: 0, mana: 30, "arcana.magic-arrow", "chaos.fireball"));
        var unit = battle.Add(Attacker, TestBattle.Fixed("own", 1), 1, 0, 0);
        battle.Add(Defender, TestBattle.Fixed("a", 1, health: 10), 10, 20, 10);
        battle.Add(Defender, TestBattle.Fixed("b", 1, health: 10), 10, 22, 12);
        battle.StartWith(unit);

        BattleAi.Act(battle.State, battle.Engine);

        Assert.Equal("chaos.fireball", Assert.Single(battle.Events.OfType<BattleSpellCast>()).Spell);
    }

    [Fact]
    public void TheManaSpentInABattle_IsGoneAfterIt()
    {
        var state = Create("AB", "..");
        var hero = state.GetHero(A);
        hero.HasSpellBook = true;
        hero.Learn("arcana.magic-arrow");
        hero.Knowledge = 2;
        var session = Start(state);
        session.Step(A, Direction.East);
        var battle = session.State.Battle!;
        while (battle.ActiveUnit!.Side != Attacker)
        {
            session.Execute(new BattleDefendCommand(Red)).ThrowIfRejected();
        }

        var before = hero.Mana;
        var casts = new List<BattleSpellCast>();
        var spent = new List<HeroManaSpent>();
        session.Events.Subscribe<BattleSpellCast>(casts.Add);
        session.Events.Subscribe<HeroManaSpent>(spent.Add);
        session.Execute(new BattleCastCommand(Blue, "arcana.magic-arrow", battle.Units.First(u => u.Side == Defender).Id)).ThrowIfRejected();
        Assert.Equal(before - 5, battle.ManaOf(Attacker));
        session.AutoResolve(); // the AI may cast again for the hero

        var left = casts.Where(c => c.Side == Attacker).Last().Mana;
        Assert.Equal(left, hero.Mana);
        Assert.Equal(new HeroManaSpent(A, before - left, left, hero.MaxMana), Assert.Single(spent));
    }

    private static readonly string[] WideMap = [.. Enumerable.Repeat("....................", 8), "A..................B"];

    private static BattleCaster Caster(int spellPower, int mana, params string[] spells) =>
        new(spellPower, 1, spells.Select(s => new KnownSpell(SpellCatalog.Default.Get(s), 1, SpellCatalog.Default.Get(s).Mana)).ToList(), null, mana);

    private static GameOptions GameOptionsWithSeed(ulong seed) => GameOptions.Default with { Seed = seed };

    /// <summary>Blue's hero A one step south of an own Haven town's entrance.</summary>
    private static (GameSession Session, Town Town, Hero Hero) HeroBeforeTown()
    {
        var state = Create("............", "............", "............", "............", "............", "............", "............", "...A........", "...........B");
        var town = AddTown(state, TownIds.Haven, 0, 0, Blue);
        var session = Start(state);
        return (session, town, state.GetHero(A));
    }
}
