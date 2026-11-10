using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Battles;

/// <summary>The combat log lines <see cref="BattleNarrator"/> tells for battle events.</summary>
public class BattleNarratorTests
{
    private const string Bless = "light.bless";

    [Fact]
    public void TheOpeningLine_NamesBothHeroes_AndAnAmbush()
    {
        var (narrator, started, _, _) = Fight();

        Assert.Equal("Alpha against Bravo.", narrator.Line(started));
        Assert.Equal("Ambush! Alpha against Bravo.", narrator.Line(started with { IsAmbush = true }));
        Assert.Equal("Alpha", narrator.SideName(BattleSide.Attacker));
        Assert.Equal("Bravo", narrator.SideName(BattleSide.Defender));
    }

    [Fact]
    public void BeforeABattle_SidesAreNamedByTheirRole()
    {
        var narrator = new BattleNarrator(() => Create("A.B"));

        Assert.Equal("Defender", narrator.SideName(BattleSide.Defender));
        Assert.Equal("#4", narrator.UnitName(4));
    }

    [Fact]
    public void AGuard_IsNamedAfterItsCreature()
    {
        var state = Create("A..", "...");
        state.AddObject(new MonsterGuard(state.NextObjectId(), MapObjectCatalog.Default.Get(BuiltInMapObjects.Monster), new MapPosition(2, 0), BuiltInCreatures.Wolf, 5));
        var session = Start(state);
        BattleStarted? started = null;
        session.Events.Subscribe<BattleStarted>(e => started = e);
        session.MoveTo(A, new MapPosition(2, 0));
        var narrator = new BattleNarrator(() => state);

        Assert.Equal("Alpha against Neutral Timber Wolf.", narrator.Line(started!));
    }

    [Fact]
    public void Attacks_TellTheKindAndLuck()
    {
        var (narrator, started, footmen, familiars) = Fight();
        narrator.Line(started);

        Assert.Equal("Footman attacks Familiar: 12 damage, 2 killed.", narrator.Line(new BattleUnitAttacked(footmen, familiars, BattleAttackKind.Melee, 12, 2, 3, 5)));
        Assert.Equal(
            "Familiar strikes back at Footman: 4 damage, 0 killed. Lucky strike!",
            narrator.Line(new BattleUnitAttacked(familiars, footmen, BattleAttackKind.Retaliation, 4, 0, 10, 9, HitLuck.Lucky)));
        Assert.Equal("Footman shoots Familiar: 1 damage, 0 killed. Bad luck!", narrator.Line(new BattleUnitAttacked(footmen, familiars, BattleAttackKind.Ranged, 1, 0, 5, 9, HitLuck.Unlucky)));
        Assert.Equal("Footman shoots back at Familiar: 1 damage, 0 killed.", narrator.Line(new BattleUnitAttacked(footmen, familiars, BattleAttackKind.RangedRetaliation, 1, 0, 5, 9)));
        Assert.Equal("Footman also hits Familiar: 1 damage, 0 killed.", narrator.Line(new BattleUnitAttacked(footmen, familiars, BattleAttackKind.Area, 1, 0, 5, 9)));
        Assert.Equal("Footman attacks #99: 1 damage, 0 killed.", narrator.Line(new BattleUnitAttacked(footmen, 99, BattleAttackKind.Melee, 1, 0, 5, 9)));
    }

    [Fact]
    public void TurnsMoraleAndTheEnd_HaveTheirLines()
    {
        var (narrator, started, footmen, familiars) = Fight();
        narrator.Line(started);

        Assert.Equal("Round 2.", narrator.Line(new BattleRoundStarted(2)));
        Assert.Null(narrator.Line(new BattleTurnStarted(footmen, 2)));
        Assert.Null(narrator.Line(new BattleUnitMoved(footmen, new CellPosition(0, 0), new CellPosition(1, 0))));
        Assert.Equal("Footman waits.", narrator.Line(new BattleUnitWaited(footmen)));
        Assert.Equal("Familiar defends.", narrator.Line(new BattleUnitDefended(familiars)));
        Assert.Equal("Familiar perish.", narrator.Line(new BattleUnitDied(familiars)));
        Assert.Equal("The healing tent restores 7 health to Footman.", narrator.Line(new BattleUnitHealed(footmen, 7, 10)));
        Assert.Equal("High morale: Footman acts again.", narrator.Line(new BattleMoraleTriggered(footmen, MoraleEffect.ExtraAction)));
        Assert.Equal("High morale: Footman is eager this round (+50 % initiative).", narrator.Line(new BattleMoraleTriggered(footmen, MoraleEffect.Haste)));
        Assert.Equal("Low morale: Footman freezes and loses its turn.", narrator.Line(new BattleMoraleTriggered(footmen, MoraleEffect.SkipTurn)));
        Assert.Equal("Low morale: Footman hesitates this round (−50 % initiative).", narrator.Line(new BattleMoraleTriggered(footmen, MoraleEffect.Slow)));
        Assert.Equal("Blood Rage: Footman has +3 Attack.", narrator.Line(new BattleRageGained(footmen, 3)));
        Assert.Equal("Terror: Familiar loses its action.", narrator.Line(new BattleUnitTerrified(familiars)));
        Assert.Equal($"Alpha rallies: +{PrimalFury.RallyMorale} morale for {PrimalFury.RallyRounds} rounds.", narrator.Line(new BattleWarcryUsed(BattleSide.Attacker, Warcry.Rally, [])));
        Assert.Equal(
            $"Bravo intimidates the enemy: −1 morale, −20 % initiative for {PrimalFury.IntimidateRounds} rounds.",
            narrator.Line(new BattleWarcryUsed(BattleSide.Defender, Warcry.Intimidate, [])));
        Assert.Equal("Alpha roars a terror cry: 1 enemy stack(s) will lose their next action.", narrator.Line(new BattleWarcryUsed(BattleSide.Attacker, Warcry.Terror, [familiars])));
        Assert.Equal(
            "Bravo deploys within 3 columns: drag a stack to where it should stand (or click it, then the cell); then start the battle.",
            narrator.Line(new BattleDeploymentStarted(BattleSide.Defender, 3)));
        Assert.Equal("The battle begins.", narrator.Line(new BattleDeploymentEnded(BattleSide.Defender)));
        Assert.Equal("Alpha wins.", narrator.Line(new BattleEnded(BattleSide.Attacker, BattleEndReason.Defeated, [], 0)));
        Assert.Equal("Alpha retreats. Bravo wins.", narrator.Line(new BattleEnded(BattleSide.Defender, BattleEndReason.Retreated, [], 0)));
        Assert.Equal("Bravo surrenders. Alpha wins.", narrator.Line(new BattleEnded(BattleSide.Attacker, BattleEndReason.Surrendered, [], 0)));
    }

    [Fact]
    public void Spells_AreNamedFromTheCatalog()
    {
        var (narrator, started, footmen, familiars) = Fight();
        narrator.Line(started);

        Assert.Equal("Alpha casts Bless (5 mana, 15 left).", narrator.Line(new BattleSpellCast(BattleSide.Attacker, Bless, footmen, null, 5, 15)));
        Assert.Equal("Bless on Footman until round 3.", narrator.Line(new BattleSpellApplied(footmen, Bless, 3)));
        Assert.Equal("Bless on Footman for its next turn.", narrator.Line(new BattleSpellApplied(footmen, Bless, 3, 1)));
        Assert.Equal("Bless on Footman for its next 2 turns.", narrator.Line(new BattleSpellApplied(footmen, Bless, 3, 2)));
        Assert.Equal("Bless on Footman wears off.", narrator.Line(new BattleSpellExpired(footmen, Bless)));
        Assert.Equal("Familiar is untouched by Bless.", narrator.Line(new BattleSpellResisted(familiars, Bless)));
        Assert.Equal("unknown.spell hits Familiar: 30 damage, 3 killed.", narrator.Line(new BattleUnitSpellDamaged(familiars, "unknown.spell", 30, 3, 2, 5)));
        Assert.Equal("Bless takes Bless off Familiar.", narrator.Line(new BattleSpellDispelled(familiars, Bless, Bless)));
        Assert.Equal("Bless: Familiar cannot act.", narrator.Line(new BattleUnitSpellbound(familiars, Bless)));
        Assert.Equal("Bless: Familiar will lose its next action.", narrator.Line(new BattleUnitStunned(familiars, Bless)));
        Assert.Equal("Familiar cast Bless (2 cast(s) left).", narrator.Line(new BattleCreatureCast(familiars, Bless, null, null, 2)));
        Assert.Equal("Bless covers the field until round 4.", narrator.Line(new BattleZoneCreated(1, Bless, BattleSide.Attacker, new CellRect(0, 0, 1, 1), 4)));
        Assert.Equal("Bless fades.", narrator.Line(new BattleZoneExpired(1, Bless)));
        Assert.Equal("Bless takes Footman across the field.", narrator.Line(new BattleUnitTeleported(footmen, new CellPosition(0, 0), new CellPosition(5, 5), Bless)));
        Assert.Equal("Bless: Footman may act once more this round.", narrator.Line(new BattleUnitTurnRegained(footmen, Bless, false)));
        Assert.Equal("Bless: Footman may act once more this round, without moving.", narrator.Line(new BattleUnitTurnRegained(footmen, Bless, true)));
        Assert.Equal("Footman now fights for Bravo.", narrator.Line(new BattleUnitSideChanged(footmen, BattleSide.Defender)));
        Assert.Equal("Bless: Bravo gains 4 mana (9 now).", narrator.Line(new BattleManaGained(BattleSide.Defender, Bless, 4, 9)));
        Assert.Equal("Bless: Footman acts again.", narrator.Line(new BattleUnitActsAgain(footmen, Bless)));
        Assert.Equal("Bless: Footman take 6 of the blow meant for Familiar, 1 killed.", narrator.Line(new BattleUnitShielded(footmen, familiars, Bless, 6, 1, 9, 10)));
        Assert.Equal("Familiar rise from the ashes: 3 of them.", narrator.Line(new BattleUnitReborn(familiars, 3)));
        Assert.Equal("Bless: Familiar regain 20 health, 4 of them now.", narrator.Line(new BattleUnitRevived(familiars, Bless, 20, 4, 9)));
        Assert.Equal("Bless: the copy of Familiar vanishes.", narrator.Line(new BattleCopyVanished(familiars, Bless)));
    }

    [Fact]
    public void StacksJoiningTheField_AreNamedInLaterLines()
    {
        var (narrator, started, footmen, familiars) = Fight();
        narrator.Line(started);

        Assert.Equal("Bless calls 3 Familiar for the battle.", narrator.Line(new BattleUnitSummoned(Info(50, BuiltInCreatures.Familiar, 3), Bless, 3)));
        Assert.Equal("Bless calls 2 Familiar for the battle.", narrator.Line(new BattleUnitSummoned(Info(50, BuiltInCreatures.Footman, 5), Bless, 2))); // grew: keeps its name
        Assert.Equal("Bless: a copy of Footman fights until round 4, or until it is hit.", narrator.Line(new BattleUnitCopied(Info(51, BuiltInCreatures.Footman, 10), footmen, Bless, 4)));
        Assert.Equal("Bless: 4 Footman rise where Familiar fell.", narrator.Line(new BattleUnitRaised(Info(52, BuiltInCreatures.Footman, 4), familiars, Bless)));
        Assert.Equal("Bless takes 2 of Familiar; 3 stay on their side.", narrator.Line(new BattleUnitSplit(familiars, 3, Info(53, BuiltInCreatures.Familiar, 2), Bless)));
        Assert.Equal(["Familiar", "Footman", "Footman", "Familiar"], new[] { 50, 51, 52, 53 }.Select(narrator.UnitName));

        // A new battle forgets them.
        narrator.Line(started);
        Assert.Equal("#50", narrator.UnitName(50));
    }

    [Fact]
    public void SiegeLines_NameTheStructures()
    {
        var (narrator, started, footmen, _) = Fight();
        var gate = new SiegeStructureInfo(7, SiegeStructureKind.Gate, new CellRect(10, 10, 1, 2), 20);
        var keep = new SiegeStructureInfo(8, SiegeStructureKind.Keep, new CellRect(30, 10, 2, 2), 50);
        var wall = new SiegeStructureInfo(9, SiegeStructureKind.Wall, new CellRect(10, 0, 1, 4), 30);
        narrator.Line(started with { Siege = new SiegeLayout(1, [gate, keep, wall], []) });

        Assert.Equal("The Footman misses the gate.", narrator.Line(new SiegeStructureShot(footmen, 7, false, 0, 20)));
        Assert.Equal("The Footman hits the wall section: 5 hit point(s) left.", narrator.Line(new SiegeStructureShot(footmen, 9, true, 25, 5)));
        Assert.Equal("The Footman destroys the keep.", narrator.Line(new SiegeStructureShot(footmen, 8, true, 50, 0)));
        Assert.Equal("Footman hit the gate: 8 damage, 12 hit point(s) left.", narrator.Line(new SiegeStructureDamaged(7, footmen, null, 8, 12)));
        Assert.Equal("Bless destroy the wall section.", narrator.Line(new SiegeStructureDamaged(9, null, Bless, 5, 0)));
        Assert.Equal("The keep shoots Footman: 9 damage, 1 killed.", narrator.Line(new BattleUnitSiegeDamaged(footmen, SiegeDamageSource.Tower, 8, 9, 1, 9, 4)));
        Assert.Equal("The tower shoots Footman: 9 damage, 1 killed.", narrator.Line(new BattleUnitSiegeDamaged(footmen, SiegeDamageSource.Tower, 99, 9, 1, 9, 4)));
        Assert.Equal("Footman wade into the moat: 3 damage, 0 killed.", narrator.Line(new BattleUnitSiegeDamaged(footmen, SiegeDamageSource.Moat, null, 3, 0, 10, 7)));
        Assert.Equal("The keep falls: Footman come out of it.", narrator.Line(new BattleUnitLeftKeep(footmen)));
    }

    /// <summary>Alpha's footmen attack Bravo's familiars; the narrator has not seen <see cref="BattleStarted"/> yet.</summary>
    private static (BattleNarrator Narrator, BattleStarted Started, int Footmen, int Familiars) Fight()
    {
        var state = Create("A.B", "...");
        state.GetHero(B).Army.Set(0, new CreatureStack(BuiltInCreatures.Familiar, 5));
        var session = Start(state);
        BattleStarted? started = null;
        session.Events.Subscribe<BattleStarted>(e => started = e);
        session.MoveTo(A, new MapPosition(2, 0));
        Assert.NotNull(started);
        return (
            new BattleNarrator(() => state),
            started,
            started.Units.Single(u => u.Creature == BuiltInCreatures.Footman).Unit,
            started.Units.Single(u => u.Creature == BuiltInCreatures.Familiar).Unit);
    }

    private static BattleUnitInfo Info(int unit, string creature, int count) =>
        new(unit, BattleSide.Attacker, creature, count, new CellPosition(0, 0), 1, 1);
}
