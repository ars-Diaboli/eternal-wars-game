using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Battles;

/// <summary>
/// The combat log of a battle: one line of text per battle event, in the order the events come. It is fed every event
/// in order, from <see cref="BattleStarted"/> on, and keeps what later lines name: the sides (heroes, a town's garrison, a
/// guard), the stacks on the field (those of <see cref="BattleStarted"/> and those that join later: summons, copies, raised
/// skeletons, split parts; ADR-120, ADR-152) and a siege's structures (ADR-106). Spell and creature names come from the
/// game's catalogs. Events without a log line give null. Engine-free, so the 3D battle view and headless tools tell a
/// battle the same way.
/// </summary>
public sealed class BattleNarrator(Func<GameState> state)
{
    private readonly Dictionary<BattleSide, string> _sides = [];
    private readonly Dictionary<int, string> _units = [];
    private readonly Dictionary<int, SiegeStructureKind> _structures = [];

    /// <summary>The side's name as the log tells it: its hero, a town's garrison, a guard; the side itself before a battle started.</summary>
    public string SideName(BattleSide side) => _sides.GetValueOrDefault(side, side.ToString());

    /// <summary>The creature name of a stack on the field, or <c>#id</c> for one the log does not know.</summary>
    public string UnitName(int unit) => _units.TryGetValue(unit, out var name) ? name : $"#{unit}";

    /// <summary>
    /// The log line of <paramref name="e"/>, or null for an event the log does not tell. <see cref="BattleStarted"/> starts a new
    /// battle's log (the game's battle must be set up already) and gives its opening line.
    /// </summary>
    public string? Line(IEvent e) => e switch
    {
        BattleStarted started => Start(started),
        BattleRoundStarted round => $"Round {round.Round}.",
        BattleUnitTeleported teleport => $"{SpellName(teleport.Spell)} takes {UnitName(teleport.Unit)} across the field.",
        BattleUnitTurnRegained regained =>
            $"{SpellName(regained.Spell)}: {UnitName(regained.Unit)} may act once more this round{(regained.InPlace ? ", without moving" : string.Empty)}.",
        BattleDeploymentStarted deployment =>
            $"{SideName(deployment.Side)} deploys within {deployment.Columns} columns: drag a stack to where it should stand (or click it, then the cell); then start the battle.",
        BattleDeploymentEnded => "The battle begins.",
        BattleUnitAttacked attack => Attacked(attack),
        BattleUnitHealed healed => $"The healing tent restores {healed.Healed} health to {UnitName(healed.Unit)}.",
        SiegeStructureShot shot => StructureShot(shot),
        SiegeStructureDamaged damaged => StructureDamaged(damaged),
        BattleUnitSiegeDamaged siege => siege.Source == SiegeDamageSource.Moat
            ? $"{UnitName(siege.Unit)} wade into the moat: {siege.Damage} damage, {siege.Killed} killed."
            : $"The {StructureName(siege.Structure is { } id && _structures.TryGetValue(id, out var kind) ? kind : SiegeStructureKind.Tower)} shoots {UnitName(siege.Unit)}: {siege.Damage} damage, {siege.Killed} killed.",
        BattleUnitSpellDamaged spell => $"{SpellName(spell.Spell)} hits {UnitName(spell.Unit)}: {spell.Damage} damage, {spell.Killed} killed.",
        BattleMoraleTriggered morale => Morale(morale),
        BattleUnitSummoned summoned => Summoned(summoned),
        BattleUnitCopied copied => Copied(copied),
        BattleCopyVanished vanished => $"{SpellName(vanished.Spell)}: the copy of {UnitName(vanished.Unit)} vanishes.",
        BattleUnitRaised raised => Raised(raised),
        BattleUnitSplit split => Split(split),
        BattleUnitShielded shielded =>
            $"{SpellName(shielded.Spell)}: {UnitName(shielded.Martyr)} take {shielded.Damage} of the blow meant for {UnitName(shielded.Unit)}, {shielded.Killed} killed.",
        BattleManaGained mana => $"{SpellName(mana.Spell)}: {SideName(mana.Side)} gains {mana.Gained} mana ({mana.Mana} now).",
        BattleManaStolen stolen => $"{UnitName(stolen.Unit)} steal {stolen.Stolen} mana from {SideName(stolen.From)} ({stolen.FromMana} left).",
        BattleSpellPowerSapped sapped => $"{SpellName(sapped.Spell)}: {SideName(sapped.Side)} loses {sapped.Lost} Spell Power for the battle.",
        BattleRubbleFell rubble => $"{SpellName(rubble.Spell)} leaves rubble on {rubble.Rubble.Count} cell(s).",
        BattleUnitActsAgain again => $"{SpellName(again.Spell)}: {UnitName(again.Unit)} acts again.",
        BattleUnitReborn reborn => $"{UnitName(reborn.Unit)} rise from the ashes: {reborn.Count} of them.",
        BattleUnitRevived revived => $"{SpellName(revived.Spell)}: {UnitName(revived.Unit)} regain {revived.Health} health, {revived.Count} of them now.",
        BattleSpellDispelled dispelled => $"{SpellName(dispelled.By)} takes {SpellName(dispelled.Spell)} off {UnitName(dispelled.Unit)}.",
        BattleUnitSpellbound spellbound => $"{SpellName(spellbound.Spell)}: {UnitName(spellbound.Unit)} cannot act.",
        BattleUnitStunned stunned => $"{SpellName(stunned.Spell)}: {UnitName(stunned.Unit)} will lose its next action.",
        BattleCreatureCast cast => $"{UnitName(cast.Unit)} cast {SpellName(cast.Spell)} ({cast.CastsLeft} cast(s) left).",
        BattleUnitLeftKeep left => $"The keep falls: {UnitName(left.Unit)} come out of it.",
        BattleSpellCast cast => $"{SideName(cast.Side)} casts {SpellName(cast.Spell)} ({cast.Cost} mana, {cast.Mana} left).",
        BattleZoneCreated zone => $"{SpellName(zone.Spell)} covers the field until round {zone.UntilRound}.",
        BattleZoneExpired zone => $"{SpellName(zone.Spell)} fades.",
        BattleSpellApplied applied => applied.Turns is { } turns
            ? $"{SpellName(applied.Spell)} on {UnitName(applied.Unit)} for its next {(turns == 1 ? "turn" : $"{turns} turns")}."
            : $"{SpellName(applied.Spell)} on {UnitName(applied.Unit)} until round {applied.UntilRound}.",
        BattleUnitSideChanged changed => $"{UnitName(changed.Unit)} now fights for {SideName(changed.Side)}.",
        BattleSpellResisted resisted => $"{UnitName(resisted.Unit)} is untouched by {SpellName(resisted.Spell)}.",
        BattleSpellExpired expired => $"{SpellName(expired.Spell)} on {UnitName(expired.Unit)} wears off.",
        BattleRageGained rage => $"Blood Rage: {UnitName(rage.Unit)} has +{rage.Attack} Attack.",
        BattleWarcryUsed warcry => WarcryLine(warcry),
        BattleUnitTerrified terrified => $"Terror: {UnitName(terrified.Unit)} loses its action.",
        BattleUnitWaited waited => $"{UnitName(waited.Unit)} waits.",
        BattleUnitDefended defended => $"{UnitName(defended.Unit)} defends.",
        BattleUnitDied died => $"{UnitName(died.Unit)} perish.",
        BattleEnded ended => ended.Reason switch
        {
            BattleEndReason.Retreated => $"{SideName(ended.Winner.Opponent())} retreats. {SideName(ended.Winner)} wins.",
            BattleEndReason.Surrendered => $"{SideName(ended.Winner.Opponent())} surrenders. {SideName(ended.Winner)} wins.",
            _ => $"{SideName(ended.Winner)} wins.",
        },
        _ => null,
    };

    /// <summary>A siege structure's name in the log.</summary>
    public static string StructureName(SiegeStructureKind kind) => kind switch
    {
        SiegeStructureKind.Wall => "wall section",
        SiegeStructureKind.Gate => "gate",
        SiegeStructureKind.Keep => "keep",
        _ => "tower",
    };

    private string Start(BattleStarted e)
    {
        var game = state();
        var battle = game.Battle!;
        _sides.Clear();
        _units.Clear();
        _structures.Clear();
        foreach (var side in new[] { BattleSide.Attacker, BattleSide.Defender })
        {
            _sides[side] = NameOf(game, battle.Participant(side));
        }

        foreach (var info in e.Units)
        {
            _units[info.Unit] = battle.GetUnit(info.Unit).Creature.Name;
        }

        foreach (var structure in e.Siege?.Structures ?? [])
        {
            _structures[structure.Structure] = structure.Kind;
        }

        var sides = $"{SideName(BattleSide.Attacker)} against {SideName(BattleSide.Defender)}.";
        return e.IsAmbush ? $"Ambush! {sides}" : sides;
    }

    private string Attacked(BattleUnitAttacked e)
    {
        var verb = e.Kind switch
        {
            BattleAttackKind.Ranged => "shoots",
            BattleAttackKind.Retaliation => "strikes back at",
            BattleAttackKind.RangedRetaliation => "shoots back at",
            BattleAttackKind.Area => "also hits",
            _ => "attacks",
        };
        var luck = e.Luck switch
        {
            HitLuck.Lucky => " Lucky strike!",
            HitLuck.Unlucky => " Bad luck!",
            _ => string.Empty,
        };
        return $"{UnitName(e.Attacker)} {verb} {UnitName(e.Target)}: {e.Damage} damage, {e.Killed} killed.{luck}";
    }

    private string StructureShot(SiegeStructureShot e)
    {
        var machine = UnitName(e.Machine);
        var name = StructureName(_structures.GetValueOrDefault(e.Structure, SiegeStructureKind.Tower));
        return !e.Hit ? $"The {machine} misses the {name}."
            : e.Health == 0 ? $"The {machine} destroys the {name}."
            : $"The {machine} hits the {name}: {e.Health} hit point(s) left.";
    }

    private string StructureDamaged(SiegeStructureDamaged e)
    {
        var name = StructureName(_structures.GetValueOrDefault(e.Structure, SiegeStructureKind.Tower));
        var who = e.Unit is { } unit ? UnitName(unit) : SpellName(e.Spell ?? string.Empty);
        return e.Health == 0 ? $"{who} destroy the {name}." : $"{who} hit the {name}: {e.Damage} damage, {e.Health} hit point(s) left.";
    }

    private string Morale(BattleMoraleTriggered e) => e.Effect switch
    {
        MoraleEffect.ExtraAction => $"High morale: {UnitName(e.Unit)} acts again.",
        MoraleEffect.Haste => $"High morale: {UnitName(e.Unit)} is eager this round (+50 % initiative).",
        MoraleEffect.SkipTurn => $"Low morale: {UnitName(e.Unit)} freezes and loses its turn.",
        MoraleEffect.Slow => $"Low morale: {UnitName(e.Unit)} hesitates this round (−50 % initiative).",
        _ => string.Empty,
    };

    /// <summary>The log line of a warcry (ADR-067).</summary>
    private string WarcryLine(BattleWarcryUsed e) => e.Warcry switch
    {
        Warcry.Rally => $"{SideName(e.Side)} rallies: +{PrimalFury.RallyMorale} morale for {PrimalFury.RallyRounds} rounds.",
        Warcry.Intimidate => $"{SideName(e.Side)} intimidates the enemy: −1 morale, −20 % initiative for {PrimalFury.IntimidateRounds} rounds.",
        _ => $"{SideName(e.Side)} roars a terror cry: {e.Affected.Count} enemy stack(s) will lose their next action.",
    };

    private string Summoned(BattleUnitSummoned e)
    {
        Join(e.Unit);
        return $"{SpellName(e.Spell)} calls {e.Added} {UnitName(e.Unit.Unit)} for the battle.";
    }

    private string Copied(BattleUnitCopied e)
    {
        Join(e.Unit);
        return $"{SpellName(e.Spell)}: a copy of {UnitName(e.Original)} fights until round {e.UntilRound}, or until it is hit.";
    }

    private string Raised(BattleUnitRaised e)
    {
        Join(e.Unit);
        return $"{SpellName(e.Spell)}: {e.Unit.Count} {state().Creatures.Get(e.Unit.Creature).Name} rise where {UnitName(e.From)} fell.";
    }

    private string Split(BattleUnitSplit e)
    {
        Join(e.Unit);
        return $"{SpellName(e.Spell)} takes {e.Unit.Count} of {UnitName(e.From)}; {e.FromCount} stay on their side.";
    }

    /// <summary>A stack that came onto the field during the battle; one already known (a summoned stack that grew) keeps its name.</summary>
    private void Join(BattleUnitInfo info) => _units.TryAdd(info.Unit, state().Creatures.Get(info.Creature).Name);

    private string SpellName(string spell) => state().Spells.TryGet(spell, out var definition) ? definition.Name : spell;

    private static string NameOf(GameState state, BattleParticipant participant)
    {
        if (participant.Hero is { } hero && state.Heroes.Concat(state.GarrisonHeroes).FirstOrDefault(h => h.Id == hero) is { } found)
        {
            return found.Name;
        }

        return participant.Guard is { } guard
            ? state.FindObject(guard) switch
            {
                Town town => $"{town.Name} garrison",
                MonsterGuard monster => state.Creatures.TryGet(monster.Creature, out var creature) ? $"Neutral {creature.Name}" : "Neutral monsters",
                Mine mine => $"{mine.Definition.Name} guard",
                CreatureDwelling dwelling => $"{dwelling.Definition.Name} guard",
                CreatureBank bank => $"{bank.Definition.Name} guard",
                PandorasBox => "Pandora's box guard",
                _ => "Neutral",
            }
            : participant.Side.ToString();
    }
}
