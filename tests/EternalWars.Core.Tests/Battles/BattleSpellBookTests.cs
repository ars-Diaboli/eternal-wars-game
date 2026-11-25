using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Magic;
using static EternalWars.Core.Domain.Battles.BattleSide;

namespace EternalWars.Core.Tests.Battles;

/// <summary>The battle's spell book (ADR-314): the hero's battle spells by school with cost and refusal, and its warcries as their own category.</summary>
public class BattleSpellBookTests
{
    private static KnownSpell Known(string id, int rank = 1) => new(SpellCatalog.Default.Get(id), rank, SpellCatalog.Default.Get(id).Mana);

    [Fact]
    public void TheSpells_StandBySchool_ThenLevel_WithoutAdventureSpells_AndTheInterferenceCost()
    {
        var spells = new[] { Known("arcana.magic-arrow"), Known("light.smite"), Known("light.distant-vision"), Known("light.heal"), Known("light.bless") };
        var battle = TestBattle.Create(
            attackerCaster: new BattleCaster(0, 1, spells, null, 15), defenderBonuses: HeroBattleBonuses.None with { EnemySpellCostPenalty = 2 });

        var book = BattleSpellBook.Of(battle.State, Attacker)!;

        Assert.Equal(15, book.Mana);
        Assert.Equal(
            ["light.bless", "light.heal", "light.smite", "arcana.magic-arrow"], // Light before Arcana, the schools' order; Distant Vision is not a battle spell
            book.Spells.Select(s => s.Known.Spell.Id.Value));
        Assert.Equal(["Light", "Light", "Light", "Arcana"], book.Spells.Select(s => s.SchoolName));
        Assert.Equal([7, 11, 18, 7], book.Spells.Select(s => s.Cost)); // +2 the enemy's Interference
        Assert.Equal([null, null, "Not enough mana for Smite.", null], book.Spells.Select(s => s.Refusal));
        Assert.Empty(book.Warcries);
    }

    [Fact]
    public void AfterASpellThisRound_EverySpellIsGreyed_WithTheEnginesReason()
    {
        var battle = TestBattle.Create(attackerCaster: new BattleCaster(0, 1, [Known("light.bless"), Known("arcana.magic-arrow")], null, 20));
        var own = battle.Add(Attacker, TestBattle.Fixed("own", 1), 1, 0, 0);
        var foe = battle.Add(Defender, TestBattle.Fixed("foe", 1), 1, 20, 0);
        battle.StartWith(own);
        Assert.Null(battle.Engine.Cast(Attacker, "light.bless", own.Id, null));

        var book = BattleSpellBook.Of(battle.State, Attacker)!;

        Assert.All(book.Spells, s => Assert.Equal("The hero has already cast a spell this round.", s.Refusal));
        Assert.Equal(book.Spells[1].Refusal, BattleMagic.CastRefusal(battle.State, Attacker, "arcana.magic-arrow", foe.Id, null));
    }

    [Fact]
    public void TheWarcries_AreTheOnesTheHeroKnows_GreyedOnceUsed_OrOneThisRound()
    {
        var battle = TestBattle.Create(attackerBonuses: HeroBattleBonuses.None with { Warcry = SkillLevel.Advanced });
        var own = battle.Add(Attacker, TestBattle.Fixed("own", 1), 1, 0, 0);
        battle.Add(Defender, TestBattle.Fixed("foe", 1), 1, 20, 0);
        battle.StartWith(own);

        var book = BattleSpellBook.Of(battle.State, Attacker)!; // a hero without a spell book but with warcries has one
        Assert.Empty(book.Spells);
        Assert.Equal([new SpellBookWarcry(Warcry.Rally, null), new SpellBookWarcry(Warcry.Intimidate, null)], book.Warcries);

        Assert.Null(battle.Engine.UseWarcry(Attacker, Warcry.Rally));

        Assert.Equal(
            [new SpellBookWarcry(Warcry.Rally, "Rally was already used in this battle."), new SpellBookWarcry(Warcry.Intimidate, "Only one warcry a round.")],
            BattleSpellBook.Of(battle.State, Attacker)!.Warcries);
    }

    [Fact]
    public void ASpellAboveTheBattlesLevelCap_IsGreyed()
    {
        var battle = TestBattle.Create(
            attackerCaster: new BattleCaster(0, 1, [Known("light.bless"), Known("light.smite")], null, 50),
            defenderBonuses: HeroBattleBonuses.None with { SpellLevelCap = 2 });

        Assert.Equal(
            [null, "No spell above level 2 can be cast in this battle."],
            BattleSpellBook.Of(battle.State, Attacker)!.Spells.Select(s => s.Refusal));
    }

    [Fact]
    public void ASideWithNeitherSpellsNorWarcries_HasNoBook()
    {
        var battle = TestBattle.Create();

        Assert.Null(BattleSpellBook.Of(battle.State, Defender));
    }
}
