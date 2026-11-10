using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Magic;
using EternalWars.Core.Events;
using static EternalWars.Core.Domain.Battles.BattleSide;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Battles;

/// <summary>Summoned creatures, creature casters and the Phoenix's rebirth (ADR-106, ADR-120).</summary>
public class SummonAndCasterTests
{
    private static BattleCaster Caster(int spellPower, int mana, params string[] spells) =>
        new(spellPower, 1, spells.Select(s => new KnownSpell(SpellCatalog.Default.Get(s), 1, SpellCatalog.Default.Get(s).Mana)).ToList(), null, mana);

    [Fact]
    public void EverySummon_CallsTownCreaturesOrConfluxElementals_ThatExist()
    {
        var summons = SpellCatalog.Default.All.Where(s => s.HasTag("summon")).ToList();
        Assert.Equal(5, summons.Count);
        Assert.All(summons, s => Assert.Equal(SpellEffectKind.Summon, s.Effect!.Kind));
        foreach (var id in summons.SelectMany(s => s.Effect!.Summons))
        {
            var creature = CreatureCatalog.Default.Get(id);
            Assert.False(string.IsNullOrEmpty(creature.Town)); // never neutrals (ADR-106)
        }

        Assert.Equal(["sylvan.pegasus"], SpellCatalog.Default.Get("nature.summon-pack").Effect!.Summons);
        Assert.True(SpellCatalog.Default.Get("nature.awaken-grove").Effect!.Rooted);
        Assert.Equal(4, SpellCatalog.Default.Get("arcana.bind-elemental").Effect!.Summons.Count);
        Assert.Throws<FormatException>(() => SpellEffect.Parse("summon sylvan.pegasus"));
    }

    [Fact]
    public void ASummon_CallsCreaturesWorthItsHealth_AtTheOwnEdge_AndASecondCastJoinsThem()
    {
        var battle = TestBattle.Create(attackerCaster: Caster(spellPower: 10, mana: 50, "chaos.summon-demons"));
        var own = battle.Add(Attacker, TestBattle.Fixed("own", 1), 1, 0, 0);
        battle.Add(Defender, TestBattle.Fixed("enemy", 1), 1, 25, 10);
        battle.StartWith(own);
        var demon = CreatureCatalog.Default.Get("inferno.horned-demon");

        Assert.Null(battle.Engine.Cast(Attacker, "chaos.summon-demons", null, null));

        var summoned = Assert.Single(battle.State.Units, u => u.IsSummoned);
        Assert.Equal((Attacker, demon.Id, 500 / demon.Health), (summoned.Side, summoned.Creature.Id, summoned.Count)); // 250 × (1 + 10 / 10)
        Assert.Equal(0, summoned.Position.X);
        Assert.Equal(summoned.Id, Assert.Single(battle.Events.OfType<BattleUnitSummoned>()).Unit.Unit);
        Assert.Equal(summoned.Count, battle.Events.OfType<BattleUnitSummoned>().Single().Added);

        battle.State.CastRounds.Remove(Attacker); // as in the next round
        Assert.Null(battle.Engine.Cast(Attacker, "chaos.summon-demons", null, null));
        Assert.Single(battle.State.Units, u => u.IsSummoned);
        Assert.Equal(2 * (500 / demon.Health), summoned.Count);
    }

    [Fact]
    public void BindElemental_CallsAnotherKindEachTime_AndTreantsAreRooted()
    {
        var battle = TestBattle.Create(attackerCaster: Caster(spellPower: 0, mana: 100, "arcana.bind-elemental", "nature.awaken-grove"));
        var own = battle.Add(Attacker, TestBattle.Fixed("own", 1), 1, 0, 0);
        battle.Add(Defender, TestBattle.Fixed("enemy", 1), 1, 25, 10);
        battle.StartWith(own);

        Assert.Null(battle.Engine.Cast(Attacker, "arcana.bind-elemental", null, null));
        battle.State.CastRounds.Remove(Attacker);
        Assert.Null(battle.Engine.Cast(Attacker, "arcana.bind-elemental", null, null));
        battle.State.CastRounds.Remove(Attacker);
        Assert.Null(battle.Engine.Cast(Attacker, "nature.awaken-grove", null, null));

        var called = battle.State.Units.Where(u => u.IsSummoned).ToList();
        Assert.Equal(["conflux.air-elemental", "conflux.earth-elemental", "sylvan.treant"], called.Select(u => u.Creature.Id));
        var treant = called[2];
        Assert.True(treant.IsRooted);
        Assert.Equal([treant.Position], BattleGrid.Reachable(battle.State, treant).Keys);
    }

    [Fact]
    public void SummonedCreatures_VanishAfterTheBattle_AreNobodysLoss_AndWorthNoExperience()
    {
        var state = Create("A.B", "...");
        state.GetHero(A).Army.Set(0, new CreatureStack(BuiltInCreatures.Champion, 30));
        var red = state.GetHero(B);
        red.HasSpellBook = true;
        red.Learn("chaos.summon-demons");
        red.Knowledge = 5;
        var session = Start(state);
        var events = new List<IEvent>();
        session.Events.Subscribe<IEvent>(events.Add);

        session.MoveTo(A, new MapPosition(2, 0));
        session.AutoResolve();

        Assert.Single(events.OfType<BattleUnitSummoned>()); // the battle AI calls them for Red
        var ended = Assert.Single(events.OfType<BattleEnded>());
        Assert.Equal(Attacker, ended.Winner);
        Assert.DoesNotContain(ended.Casualties, c => c.Creature == "inferno.horned-demon");
        var footman = state.Creatures.Get(BuiltInCreatures.Footman);
        Assert.Equal((10 * footman.Health) + BattleService.HeroDefeatExperience, state.GetHero(A).Experience);
        Assert.DoesNotContain(state.GetHero(A).Army.Stacks, s => s.Stack.Creature == "inferno.horned-demon");
    }

    [Fact]
    public void ACreatureCaster_CastsItsSpellAsItsAction_WithHalfItsCountAsSpellPower_AsOftenAsItsCasts()
    {
        var mage = CreatureCatalog.Default.Get("academy.mage");
        Assert.Equal(1, mage.SpellCasts["chaos.fireball"]);
        var battle = TestBattle.Create();
        var casters = battle.Add(Attacker, mage, 10, 0, 0);
        var enemy = battle.Add(Defender, TestBattle.Fixed("enemy", 1, health: 1000), 1, 20, 10);
        battle.StartWith(casters);

        Assert.Null(battle.Engine.CreatureCast(null, new CellPosition(20, 10)));

        Assert.Equal(1000 - (35 * (10 + 5) / 10), enemy.TopHealth); // Fireball 35 at Spell Power 10 / 2
        Assert.Equal(0, casters.CastsLeft["chaos.fireball"]);
        Assert.NotEqual(casters, battle.State.ActiveUnit); // it was the stack's action
        Assert.Contains(battle.Events, e => e is BattleCreatureCast { Spell: "chaos.fireball", CastsLeft: 0 });

        battle.StartWith(casters);
        Assert.NotNull(battle.Engine.CreatureCast(null, new CellPosition(20, 10))); // no cast left
    }

    [Fact]
    public void ACreatureCaster_CastsTheSpellFromTheGamesOwnSpellTable()
    {
        var spells = new SpellCatalog(SpellCatalog.Default.All.Select(s => s.Id == "chaos.fireball" ? s with { Effect = s.Effect! with { Amount = 100 } } : s));
        var battle = TestBattle.Create(spells: spells);
        var casters = battle.Add(Attacker, CreatureCatalog.Default.Get("academy.mage"), 10, 0, 0);
        var enemy = battle.Add(Defender, TestBattle.Fixed("enemy", 1, health: 1000), 1, 20, 10);
        battle.StartWith(casters);

        Assert.Null(battle.Engine.CreatureCast(null, new CellPosition(20, 10)));

        Assert.Equal(1000 - (100 * (10 + 5) / 10), enemy.TopHealth); // the table's Fireball, 100, not the built-in 35
    }

    [Fact]
    public void TheBattleAi_CastsACreatureSpell_WhenItCannotHitAnythingItself()
    {
        var caster = TestBattle.Fixed("caster", 1, capabilities: new CreatureCapability(CapabilityKind.Caster, 1, "nature.call-lightning"));
        var battle = TestBattle.Create();
        var unit = battle.Add(Attacker, caster, 10, 0, 0);
        var enemy = battle.Add(Defender, TestBattle.Fixed("enemy", 1, health: 1000), 1, 25, 20);
        battle.StartWith(unit);

        BattleAi.Act(battle.State, battle.Engine);

        Assert.Equal(1000 - (60 * 15 / 10), enemy.TopHealth); // Call Lightning 60 at Spell Power 5
    }

    [Fact]
    public void APhoenix_RisesOnceWithAThirdOfItsStack()
    {
        var phoenix = CreatureCatalog.Default.Get("sylvan.phoenix");
        var battle = TestBattle.Create();
        var killer = battle.Add(Attacker, TestBattle.Fixed("killer", 100_000, health: 1_000_000, attack: 50), 1, 10, 10);
        var flock = battle.Add(Defender, phoenix, 10, 14, 10);
        battle.StartWith(killer);

        Assert.Null(battle.Engine.Attack(flock.Id, null));
        Assert.Equal(3, flock.Count);
        Assert.Contains(new BattleUnitReborn(flock.Id, 3), battle.Events);
        Assert.DoesNotContain(battle.Events, e => e is BattleUnitDied d && d.Unit == flock.Id);

        battle.StartWith(killer);
        Assert.Null(battle.Engine.Attack(flock.Id, null));
        Assert.False(flock.IsAlive); // only once
    }
}
