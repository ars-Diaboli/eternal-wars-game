using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Scenarios;
using static EternalWars.Core.Domain.Battles.BattleSide;

namespace EternalWars.Core.Tests.Battles;

/// <summary>Morale and luck (ADR-053). Rates are checked over many seeded rounds, so the tests are exact and repeatable.</summary>
public class MoraleAndLuckTests
{
    private const int Seeds = 10;

    [Fact]
    public void MoraleAndLuck_AddCreatureAndHero_WithinFive()
    {
        var battle = TestBattle.Create(attackerBonuses: HeroBattleBonuses.None with { Morale = 3, Luck = -4 });
        var unit = battle.Add(Attacker, TestBattle.Fixed("brave", 1) with { Morale = 4, Luck = -3 }, 1, 0, 0);
        var neutral = battle.Add(Defender, TestBattle.Fixed("calm", 1), 1, 20, 0);

        Assert.Equal(5, battle.State.MoraleOf(unit));
        Assert.Equal(-5, battle.State.LuckOf(unit));
        Assert.Equal(0, battle.State.MoraleOf(neutral));
    }

    [Fact]
    public void GoodMorale_GivesExtraActions_AndHaste_AtTheDraftRates()
    {
        var (events, turns) = PlayRounds(morale: 5);
        var extra = events.Count(e => e.Effect == MoraleEffect.ExtraAction);
        var haste = events.Count(e => e.Effect == MoraleEffect.Haste);

        // 1000 rounds: 2 % × 5 = 10 % extra actions; otherwise 4 % × 5 = 20 % haste (18 % overall).
        Assert.InRange(extra, 70, 130);
        Assert.InRange(haste, 140, 220);
        Assert.DoesNotContain(events, e => e.Effect is MoraleEffect.SkipTurn or MoraleEffect.Slow);
        Assert.Equal(1000 + extra, turns); // every extra action is one more turn
    }

    [Fact]
    public void BadMorale_CostsTurns_AndSlows_AtTheDraftRates()
    {
        var (events, turns) = PlayRounds(morale: -5);
        var skipped = events.Count(e => e.Effect == MoraleEffect.SkipTurn);
        var slow = events.Count(e => e.Effect == MoraleEffect.Slow);

        Assert.InRange(skipped, 70, 130);
        Assert.InRange(slow, 140, 220);
        Assert.Equal(1000 - skipped, turns);
    }

    [Fact]
    public void ExtraAction_FollowsTheUnitsOwnAction_NotDefend()
    {
        var battle = TestBattle.Create(seed: 3);
        var unit = battle.Add(Attacker, TestBattle.Fixed("bold", 1) with { Morale = 5 }, 1, 0, 0);
        battle.Add(Defender, TestBattle.Fixed("idle", 1), 1, 26, 20);
        battle.StartWith(unit);
        unit.RoundMorale = MoraleEffect.ExtraAction;

        Assert.Null(battle.Engine.Move(new CellPosition(2, 0)));
        Assert.Same(unit, battle.State.ActiveUnit); // acts again at once
        Assert.Contains(new BattleMoraleTriggered(unit.Id, MoraleEffect.ExtraAction), battle.Events);
        Assert.Null(battle.Engine.Move(new CellPosition(4, 0)));
        Assert.NotSame(unit, battle.State.ActiveUnit); // only once

        var defender = TestBattle.Create(seed: 3);
        var guard = defender.Add(Attacker, TestBattle.Fixed("bold", 1) with { Morale = 5 }, 1, 0, 0);
        defender.Add(Defender, TestBattle.Fixed("idle", 1), 1, 26, 20);
        defender.StartWith(guard);
        guard.RoundMorale = MoraleEffect.ExtraAction;
        Assert.Null(defender.Engine.Defend());
        Assert.NotSame(guard, defender.State.ActiveUnit);
    }

    [Fact]
    public void Haste_AndSlow_ChangeTheRoundsInitiative()
    {
        Assert.Equal(15, MoraleAndLuck.Initiative(10, MoraleEffect.Haste));
        Assert.Equal(5, MoraleAndLuck.Initiative(10, MoraleEffect.Slow));
        Assert.Equal(1, MoraleAndLuck.Initiative(1, MoraleEffect.Slow));
        Assert.Equal(10, MoraleAndLuck.Initiative(10, MoraleEffect.ExtraAction));
    }

    [Theory]
    [InlineData(HitLuck.None, 100)]
    [InlineData(HitLuck.Lucky, 175)]
    [InlineData(HitLuck.Unlucky, 50)]
    public void Luck_ScalesTheHit(HitLuck luck, int damage)
    {
        var battle = TestBattle.Create();
        var attacker = battle.Add(Attacker, TestBattle.Fixed("a", 10), 10, 0, 0);
        var defender = battle.Add(Defender, TestBattle.Fixed("d", 1), 10, 2, 0);

        Assert.Equal(damage, DamageCalculator.Damage(battle.State, attacker, defender, new HitModifiers(10, IsRanged: false, Luck: luck)));
    }

    [Fact]
    public void AttackersLuck_TriggersAtTheDraftRate()
    {
        var hits = Strikes(attackerLuck: 5, defenderLuck: 0);

        // 4 % × 5 = 20 % of 1000 blows.
        Assert.InRange(hits.Count(h => h == HitLuck.Lucky), 150, 250);
        Assert.DoesNotContain(HitLuck.Unlucky, hits);
    }

    [Fact]
    public void BadLuck_AndTheDefendersLuck_FavourTheDefender_AndBothRollsCancel()
    {
        var bad = Strikes(attackerLuck: -5, defenderLuck: 0);
        Assert.InRange(bad.Count(h => h == HitLuck.Unlucky), 150, 250);
        Assert.DoesNotContain(HitLuck.Lucky, bad);

        // Both lucky: each 20 %; only one of the two counts (16 % each way), both or neither is nothing.
        var both = Strikes(attackerLuck: 5, defenderLuck: 5);
        Assert.InRange(both.Count(h => h == HitLuck.Lucky), 110, 210);
        Assert.InRange(both.Count(h => h == HitLuck.Unlucky), 110, 210);
        Assert.InRange(both.Count(h => h == HitLuck.None), 600, 760);
    }

    /// <summary>100 rounds per seed of one unit with <paramref name="morale"/> that moves on its turns; the other side defends.</summary>
    private static (List<BattleMoraleTriggered> Events, int Turns) PlayRounds(int morale)
    {
        var triggered = new List<BattleMoraleTriggered>();
        var turns = 0;
        for (var seed = 1UL; seed <= Seeds; seed++)
        {
            var battle = TestBattle.Create(seed: seed);
            var unit = battle.Add(Attacker, TestBattle.Fixed("moody", 1, speed: 1) with { Morale = morale }, 1, 0, 0);
            battle.Add(Defender, TestBattle.Fixed("idle", 1), 1, 26, 20);
            battle.Engine.StartFighting();
            while (battle.State.Phase == BattlePhase.Fighting)
            {
                var active = battle.State.ActiveUnit!;
                Assert.Null(active == unit
                    ? battle.Engine.Move(new CellPosition(unit.Position.X == 0 ? 1 : 0, 0))
                    : battle.Engine.Defend());
            }

            triggered.AddRange(battle.Events.OfType<BattleMoraleTriggered>());
            turns += battle.Events.OfType<BattleTurnStarted>().Count(t => t.Unit == unit.Id);
        }

        return (triggered, turns);
    }

    /// <summary>One blow per round for 100 rounds per seed, against a target that cannot retaliate or die.</summary>
    private static List<HitLuck> Strikes(int attackerLuck, int defenderLuck)
    {
        var hits = new List<HitLuck>();
        for (var seed = 1UL; seed <= Seeds; seed++)
        {
            var battle = TestBattle.Create(seed: seed);
            var striker = battle.Add(Attacker, TestBattle.Fixed("striker", 1, capabilities: new CreatureCapability(CapabilityKind.NoEnemyRetaliation)) with { Luck = attackerLuck }, 1, 0, 0);
            battle.Add(Defender, TestBattle.Fixed("wall", 1, health: 1_000_000) with { Luck = defenderLuck }, 1, 2, 0);
            battle.Engine.StartFighting();
            while (battle.State.Phase == BattlePhase.Fighting)
            {
                var active = battle.State.ActiveUnit!;
                Assert.Null(active == striker ? battle.Engine.Attack(2, striker.Position) : battle.Engine.Defend());
            }

            hits.AddRange(battle.Events.OfType<BattleUnitAttacked>().Where(a => a.Attacker == striker.Id).Select(a => a.Luck));
        }

        return hits;
    }
}

/// <summary>Where morale and luck come from: the creature table, map heroes (format v7) and the editor.</summary>
public class MoraleAndLuckDataTests
{
    [Fact]
    public void CreatureTable_ReadsOptionalMoraleAndLuck_AndRejectsMoreThanFive()
    {
        const string header = "id,name,tier,size,attack,defense,min_damage,max_damage,health,speed,map_speed,initiative,morale,luck";
        var creature = CreatureTable.Parse($"{header}\nx.brave,Brave,1,Small,1,1,1,1,1,1,1,1,2,-1\n").Single();

        Assert.Equal((2, -1), (creature.Morale, creature.Luck));
        Assert.Contains(",2,-1,", CreatureTable.Format([creature]));
        Assert.Throws<FormatException>(() => CreatureTable.Parse($"{header}\nx.mad,Mad,1,Small,1,1,1,1,1,1,1,1,6,0\n"));
        Assert.Equal(0, CreatureCatalog.Default.Get(BuiltInCreatures.Footman).Morale); // omitted = 0
    }

    [Fact]
    public void HeroMoraleAndLuck_RoundTripThroughTheMapFormat_AndAreOmittedWhenZero()
    {
        var editor = new MapEditorSession(BlankScenario.Create(12, 10));
        editor.PlaceHero(new PlayerId(0), new MapPosition(2, 2), "Orrin", out var brave);
        editor.PlaceHero(new PlayerId(0), new MapPosition(4, 2), "Plain", out _);
        Assert.Equal(EditResult.Ok, editor.SetHeroMoraleAndLuck(brave, 3, -2));

        var json = MapSerializer.Serialize(MapConverter.ToDocument(editor.Scenario));
        var restored = MapConverter.ToGameState(MapSerializer.Deserialize(json));

        Assert.Equal((3, -2), (restored.GetHero(brave).Morale, restored.GetHero(brave).Luck));
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(json, "\"morale\""));
        Assert.Contains($"\"formatVersion\": {MapDocument.CurrentFormatVersion}", json);
    }

    [Fact]
    public void Editor_SetsMoraleAndLuck_Undoably_WithinFive()
    {
        var editor = new MapEditorSession(BlankScenario.Create(12, 10));
        editor.PlaceHero(new PlayerId(0), new MapPosition(2, 2), "Orrin", out var id);
        var hero = editor.Scenario.GetHero(id);

        Assert.Equal(EditResult.InvalidValue, editor.SetHeroMoraleAndLuck(id, 6, 0));
        Assert.Equal(EditResult.Ok, editor.SetHeroMoraleAndLuck(id, -5, 5));
        Assert.Equal((-5, 5), (hero.Morale, hero.Luck));
        Assert.Equal((-5, 5), (HeroBattleBonuses.For(hero, CreatureCatalog.Default).Morale, HeroBattleBonuses.For(hero, CreatureCatalog.Default).Luck));

        editor.Undo();
        Assert.Equal((0, 0), (hero.Morale, hero.Luck));
    }
}
