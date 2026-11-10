using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Battles;

/// <summary>
/// Tactical battle rules on a <see cref="BattleState"/>: initiative order, the active unit's actions,
/// retaliation and area attacks, end of battle. Validates first, then mutates, then publishes.
/// Every action method returns null on success or the reason it was refused (nothing changed then).
/// </summary>
/// <remarks>
/// Each round every living unit acts exactly once (ADR-009), by descending Initiative; ties go to the
/// attacker's side, then the higher Speed, then a roll made at round start (ADR-021). Waiting units act
/// after everyone else in reverse order. Defend lasts until the unit's next turn.
/// Retaliation is once per round (unless unlimited), in melee and, for shooters with ranged retaliation, against
/// shots; first strike moves it before the attacker's blow (ADR-042).
/// Morale is rolled at round start (extra action, lost turn, ±50 % initiative) and luck on every hit (ADR-053); the
/// extra action of good morale is the one exception to "exactly once" per round.
/// War machines (ADR-056) never move, fight in melee or retaliate; only the offensive one takes turns, the tent heals
/// when a round starts, and a side whose creatures are all gone has lost even if machines still stand.
/// In a siege (ADR-106) the attacker's offensive machine fires at the fortifications, the keep and towers shoot when a round starts, and an
/// attacker entering the moat takes its damage.
/// The turn order lives in <see cref="BattleRounds"/>, spells in <see cref="BattleSpellResolver"/>, damage and healing in <see cref="BattleWounds"/>;
/// one engine (one action, or a test's battle) shares one of each.
/// </remarks>
internal sealed class BattleEngine
{
    /// <summary>Safety net: a battle still undecided after this many rounds is won by the defender.</summary>
    public const int MaxRounds = 100;

    private readonly BattleState battle;
    private readonly IEventPublisher events;
    private readonly BattleWounds wounds;
    private readonly BattleSpellResolver spells;
    private readonly BattleRounds rounds;

    public BattleEngine(BattleState battle, IEventPublisher events)
    {
        this.battle = battle;
        this.events = events;
        wounds = new BattleWounds(battle, events);
        spells = new BattleSpellResolver(battle, events, wounds);
        rounds = new BattleRounds(battle, events, wounds, spells);
    }

    public void StartFighting()
    {
        battle.Phase = BattlePhase.Fighting;
        rounds.NextTurn();
    }

    public string? Move(CellPosition to)
    {
        var unit = battle.ActiveUnit!;
        if (to == unit.Position || !BattleGrid.Reachable(battle, unit).ContainsKey(to))
        {
            return $"{unit.Creature.Name} cannot move to {to}.";
        }

        MoveUnit(unit, to);
        rounds.EndTurn();
        return null;
    }

    public string? Attack(int targetId, CellPosition? from)
    {
        var unit = battle.ActiveUnit!;
        if (battle.FindUnit(targetId) is not { IsAlive: true } target || !BattleGrid.IsFoe(unit, target))
        {
            return "Not a living enemy.";
        }

        if (unit.IsMachine)
        {
            return $"The {unit.Creature.Name} cannot fight in melee.";
        }

        var positions = BattleGrid.AttackPositions(battle, unit, target);
        var position = from ?? (positions.Count > 0 ? positions[0] : (CellPosition?)null);
        if (position is not { } stand || !positions.Contains(stand))
        {
            return $"{unit.Creature.Name} cannot reach {target.Creature.Name}" + (from is null ? "." : $" from {from}.");
        }

        if (stand != unit.Position)
        {
            MoveUnit(unit, stand);
        }

        // First strike: the defender hits back before the blow lands, unless the attacker has first strike too.
        var retaliated = false;
        if (target.Creature.Has(CapabilityKind.FirstStrike) && !unit.Creature.Has(CapabilityKind.FirstStrike) && CanRetaliate(target, unit))
        {
            Retaliate(target, unit);
            retaliated = true;
        }

        if (unit.IsAlive)
        {
            Strike(unit, target, BattleAttackKind.Melee);
        }

        if (!retaliated && CanRetaliate(target, unit))
        {
            Retaliate(target, unit);
        }

        if (unit.IsAlive && target.IsAlive && unit.Creature.Has(CapabilityKind.DoubleStrike))
        {
            Strike(unit, target, BattleAttackKind.Melee);
        }

        rounds.EndTurn();
        return null;
    }

    public string? Shoot(int targetId)
    {
        var unit = battle.ActiveUnit!;
        if (battle.FindUnit(targetId) is not { } target || !BattleGrid.CanShoot(battle, unit, target, out var penalty))
        {
            return $"{unit.Creature.Name} cannot shoot that target.";
        }

        Fire(unit, target, penalty);
        if (CanShootBack(target, unit, out var returnPenalty))
        {
            target.HasRetaliated = true;
            UseShot(target);
            Strike(target, unit, BattleAttackKind.RangedRetaliation, returnPenalty);
        }

        if (unit.IsAlive && target.IsAlive && unit.Creature.Has(CapabilityKind.ShootsTwice) && BattleGrid.CanShoot(battle, unit, target, out penalty))
        {
            Fire(unit, target, penalty);
        }

        rounds.EndTurn();
        return null;
    }

    /// <summary>
    /// The siege engine fires at a structure (ADR-063, ADR-106): 2 hits in 3 (all from Expert), 1–2 hit points a hit (2–3 at Master); from Advanced
    /// a shot that destroys a structure is followed by one more at the next automatic target.
    /// </summary>
    public string? ShootStructure(int structureId)
    {
        var unit = battle.ActiveUnit!;
        if (SiegeRules.ShotRefusal(battle, unit, structureId) is { } refusal)
        {
            return refusal;
        }

        var ballistics = battle.Participant(unit.Side).Bonus.Ballistics;
        if (FireAt(unit, battle.Siege!.Find(structureId)!, ballistics) && WarMachines.FiresAgainAfterKill(ballistics) && battle.Siege.AutoTarget() is { } next)
        {
            FireAt(unit, next, ballistics);
        }

        rounds.EndTurn();
        return null;
    }

    /// <returns>Whether the shot destroyed the structure.</returns>
    private bool FireAt(BattleUnit machine, SiegeStructure structure, Heroes.SkillLevel ballistics)
    {
        var hit = WarMachines.HitsFortification(ballistics, battle.Random);
        var damage = !hit ? 0
            : structure.Kind == SiegeStructureKind.Gate ? structure.Health // a war machine breaks the gate in one hit (ADR-120)
            : Math.Min(structure.Health, WarMachines.FortificationDamage(ballistics, battle.Random));
        structure.Health -= damage;
        events.Publish(new SiegeStructureShot(machine.Id, structure.Id, hit, damage, structure.Health));
        Fell(structure);
        return !structure.IsStanding;
    }

    /// <summary>
    /// The active stack hits the gate (ADR-120): it walks next to it if needed (the closest placement, or <paramref name="from"/>), then takes
    /// <see cref="SiegeRules.GateDamage"/> off it. Nothing hits back.
    /// </summary>
    public string? AttackStructure(int structureId, CellPosition? from)
    {
        var unit = battle.ActiveUnit!;
        if (SiegeRules.StructureAttackRefusal(battle, unit, structureId) is { } refusal)
        {
            return refusal;
        }

        var structure = battle.Siege!.Find(structureId)!;
        var positions = StructureAttackPositions(unit, structure);
        var position = from ?? (positions.Count > 0 ? positions[0] : (CellPosition?)null);
        if (position is not { } stand || !positions.Contains(stand))
        {
            return $"{unit.Creature.Name} cannot reach the gate" + (from is null ? "." : $" from {from}.");
        }

        if (stand != unit.Position)
        {
            MoveUnit(unit, stand);
        }

        if (unit.IsAlive)
        {
            var damage = Math.Min(structure.Health, SiegeRules.GateDamage(battle, unit, RollDamage(unit)));
            structure.Health -= damage;
            events.Publish(new SiegeStructureDamaged(structure.Id, unit.Id, null, damage, structure.Health));
        }

        if (!rounds.CheckFinished())
        {
            rounds.EndTurn();
        }

        return null;
    }

    /// <summary>The stack's damage roll for a blow; Bless and Curse fix it (ADR-110), and nothing is drawn then.</summary>
    private int RollDamage(BattleUnit unit) =>
        unit.SpellModifiers.Roll != 0 ? DamageCalculator.Roll(unit, 0) : battle.Random.NextInclusive(unit.Creature.MinDamage, unit.Creature.MaxDamage);

    /// <summary>Placements from which the unit can hit the structure this turn, closest first (then top-to-bottom, left-to-right).</summary>
    internal IReadOnlyList<CellPosition> StructureAttackPositions(BattleUnit unit, SiegeStructure structure) => BattleGrid.StructureAttackPositions(battle, unit, structure);

    /// <summary>A structure that fell: when it is the keep, its shooting stack comes out and is an ordinary stack (ADR-120).</summary>
    private void Fell(SiegeStructure structure)
    {
        if (structure.IsStanding || structure.Kind != SiegeStructureKind.Keep)
        {
            return;
        }

        foreach (var unit in battle.Units.Where(u => u.InKeep).ToList())
        {
            unit.InKeep = false;
            events.Publish(new BattleUnitLeftKeep(unit.Id));
        }
    }

    private void Fire(BattleUnit shooter, BattleUnit target, int penalty)
    {
        UseShot(shooter);
        Strike(shooter, target, BattleAttackKind.Ranged, penalty);
    }

    /// <summary>War machines and shooters supplied by an ammo cart with Advanced Ammo supply never run out (ADR-056).</summary>
    private void UseShot(BattleUnit shooter)
    {
        if (!battle.HasUnlimitedShots(shooter))
        {
            shooter.ShotsLeft--;
        }
    }

    /// <summary>Ranged retaliation: a shooter that was shot at fires back if it could shoot the attacker now.</summary>
    private bool CanShootBack(BattleUnit defender, BattleUnit shooter, out int penalty)
    {
        penalty = 0;
        return defender.IsAlive && shooter.IsAlive &&
               defender.Creature.Has(CapabilityKind.RangedRetaliation) &&
               !shooter.Creature.Has(CapabilityKind.NoEnemyRetaliation) &&
               !defender.Has(Magic.SpellStatus.NoRetaliation | Magic.SpellStatus.Blind) &&
               (!defender.HasRetaliated || defender.Creature.Has(CapabilityKind.UnlimitedRetaliation)) &&
               BattleGrid.CanShoot(battle, defender, shooter, out penalty);
    }

    private void Retaliate(BattleUnit defender, BattleUnit attacker)
    {
        defender.HasRetaliated = true;
        Strike(defender, attacker, BattleAttackKind.Retaliation);
    }

    public string? Wait()
    {
        var unit = battle.ActiveUnit!;
        if (!battle.WaitedThisRound.Add(unit.Id))
        {
            return $"{unit.Creature.Name} has already waited this round.";
        }

        battle.MutableWaiting.Add(unit.Id);
        events.Publish(new BattleUnitWaited(unit.Id));
        battle.ActiveUnit = null;
        rounds.NextTurn();
        return null;
    }

    public string? Defend()
    {
        var unit = battle.ActiveUnit!;
        unit.IsDefending = true;
        events.Publish(new BattleUnitDefended(unit.Id));
        rounds.EndTurn(moraleMayAct: false); // defending is not an action good morale repeats
        return null;
    }

    /// <summary>The side's hero leaves the field; the other side wins.</summary>
    public void Retreat(BattleSide side) => rounds.Finish(side.Opponent(), BattleEndReason.Retreated);

    /// <summary>The side's hero leaves the field with its army, paid for by <c>BattleService.Surrender</c>; the other side wins.</summary>
    public void Surrender(BattleSide side) => rounds.Finish(side.Opponent(), BattleEndReason.Surrendered);

    private void MoveUnit(BattleUnit unit, CellPosition to)
    {
        var from = unit.Position;
        var hits = battle.Zones.Count > 0 ? BattleZones.MoveDamage(battle, unit, BattleGrid.Path(battle, unit, to)) : [];
        unit.Position = to;
        events.Publish(new BattleUnitMoved(unit.Id, from, to));
        wounds.Landed(unit, from, hits);
    }

    /// <summary>Night Terrors and Stasis stop retaliation; Vigil makes it unlimited (ADR-123).</summary>
    private bool CanRetaliate(BattleUnit defender, BattleUnit attacker) =>
        defender.IsAlive && attacker.IsAlive && !defender.IsMachine &&
        defender.Rect.IsAdjacentTo(attacker.Rect) &&
        !attacker.Creature.Has(CapabilityKind.NoEnemyRetaliation) &&
        !defender.Has(Magic.SpellStatus.NoRetaliation | Magic.SpellStatus.Stasis) &&
        (!defender.HasRetaliated || defender.Creature.Has(CapabilityKind.UnlimitedRetaliation) || defender.Has(Magic.SpellStatus.Vigil));

    /// <summary>One hit plus, for a melee strike, the attacker's area ability with the same roll.</summary>
    private void Strike(BattleUnit attacker, BattleUnit target, BattleAttackKind kind, int penalty = 0)
    {
        var roll = RollDamage(attacker);
        var secondary = kind == BattleAttackKind.Melee ? AreaTargets(attacker, target).Where(u => BattleGrid.CanBeAttacked(attacker, u, melee: true)).ToList() : [];
        var hit = new HitModifiers(roll, kind is BattleAttackKind.Ranged or BattleAttackKind.RangedRetaliation, penalty);

        // Luck: the attacker rolls once per strike, every target for itself (ADR-053).
        var attackerLuck = RollLuck(attacker);
        var primaryHit = hit with { Luck = CombineLuck(attackerLuck, target) };

        // Damage for every target is computed before any of it is applied, so the order of victims does not matter.
        var damages = secondary.Select(s =>
        {
            var areaHit = hit with { Luck = CombineLuck(attackerLuck, s) };
            return (Unit: s, Damage: DamageCalculator.Damage(battle, attacker, s, areaHit), areaHit.Luck);
        }).ToList();
        var killed = Apply(attacker, target, kind, DamageCalculator.Damage(battle, attacker, target, primaryHit), primaryHit.Luck);
        foreach (var (unit, damage, luck) in damages)
        {
            killed += Apply(attacker, unit, BattleAttackKind.Area, damage, luck);
        }

        GainRage(attacker, killed);
    }

    /// <summary>Blood Rage (ADR-067): a stack that killed creatures gains +1 Attack, up to the level's limit, until the battle ends.</summary>
    private void GainRage(BattleUnit attacker, int killed)
    {
        if (killed <= 0 || attacker.IsMachine || attacker.RageAttack >= battle.Participant(attacker.Side).Bonus.BloodRageMaxAttack)
        {
            return;
        }

        attacker.RageAttack++;
        events.Publish(new BattleRageGained(attacker.Id, attacker.RageAttack));
    }

    /// <summary>
    /// A hero's warcry (ADR-067): no mana, no action of the active unit. A side uses at most one warcry a round and each warcry once per
    /// battle; the Warcry skill level says which it knows. Null on success, else why it was refused.
    /// </summary>
    public string? UseWarcry(BattleSide side, Warcry warcry)
    {
        if (battle.WarcryRefusal(side, warcry) is { } refusal)
        {
            return refusal;
        }

        var affected = new List<int>();
        var strength = battle.Participant(side).Bonus.WarcryPercent; // a Warcry specialist's are stronger (ADR-114)
        switch (warcry)
        {
            case Warcry.Rally:
                battle.SideEffects.Add(new BattleSideEffect(side, PrimalFury.RallyMorale, 0, battle.Round + PrimalFury.RallyRounds));
                affected.AddRange(battle.LivingArmyOf(side).Select(u => u.Id));
                break;
            case Warcry.Intimidate:
                battle.SideEffects.Add(new BattleSideEffect(
                    side.Opponent(), PrimalFury.IntimidateMorale, PrimalFury.IntimidateInitiativePercent * strength / 100, battle.Round + PrimalFury.IntimidateRounds));
                affected.AddRange(battle.LivingArmyOf(side.Opponent()).Select(u => u.Id));
                break;
            default:
                foreach (var unit in battle.LivingArmyOf(side.Opponent()).OrderBy(u => u.Id))
                {
                    if (unit.Has(Magic.SpellStatus.Fearless))
                    {
                        continue; // Steadfast (ADR-123): it cannot be made to flee
                    }

                    if (battle.Random.Next(100) < Math.Min(PrimalFury.MaxTerrorChancePercent, PrimalFury.TerrorChancePercent * strength / 100))
                    {
                        unit.LosesNextAction = true;
                        affected.Add(unit.Id);
                    }
                }

                break;
        }

        battle.UsedWarcries.Add((side, warcry));
        battle.WarcryRounds[side] = battle.Round;
        events.Publish(new BattleWarcryUsed(side, warcry, affected));
        return null;
    }

    /// <summary>
    /// The side's hero casts a spell (ADR-110, <see cref="BattleSpellResolver.Cast"/>). The active unit keeps its action, unless the spell killed it.
    /// </summary>
    public string? Cast(BattleSide side, string spellId, int? targetId, CellPosition? cell, int? second = null)
    {
        if (spells.Cast(side, spellId, targetId, cell, second) is { } refusal)
        {
            return refusal;
        }

        if (!rounds.CheckFinished() && battle.ActiveUnit is { IsAlive: false })
        {
            rounds.EndTurn(moraleMayAct: false);
        }

        return null;
    }

    /// <summary>The active creature caster casts its spell (ADR-120, <see cref="BattleSpellResolver.CreatureCast"/>): its action for the turn.</summary>
    public string? CreatureCast(int? targetId, CellPosition? cell)
    {
        if (spells.CreatureCast(targetId, cell) is { } refusal)
        {
            return refusal;
        }

        if (!rounds.CheckFinished())
        {
            rounds.EndTurn();
        }

        return null;
    }

    /// <summary>+1 good luck, −1 bad luck, 0 nothing; rolls only for a unit with luck.</summary>
    private int RollLuck(BattleUnit unit)
    {
        var luck = battle.LuckOf(unit);
        return luck != 0 && battle.Random.Next(100) < MoraleAndLuck.LuckPercent * Math.Abs(luck) ? Math.Sign(luck) : 0;
    }

    /// <summary>The attacker's roll against the defender's: rolls favouring each side cancel out.</summary>
    private HitLuck CombineLuck(int attackerLuck, BattleUnit defender)
    {
        var favour = attackerLuck - RollLuck(defender);
        return favour > 0 ? HitLuck.Lucky : favour < 0 ? HitLuck.Unlucky : HitLuck.None;
    }

    /// <summary>Breath: the target-sized area behind the target (friendly fire). Cleave / multi-head: enemies only.</summary>
    private List<BattleUnit> AreaTargets(BattleUnit attacker, BattleUnit target)
    {
        var creature = attacker.Creature;
        if (creature.Has(CapabilityKind.Breath))
        {
            var (ax, ay) = attacker.Rect.Center2;
            var (tx, ty) = target.Rect.Center2;
            var (dx, dy) = (Math.Sign(tx - ax), Math.Sign(ty - ay));
            var area = new CellRect(target.Position.X + (dx * target.Width), target.Position.Y + (dy * target.Height), target.Width, target.Height);
            return battle.LivingUnits.Where(u => u != attacker && u != target && u.Rect.Intersects(area)).ToList();
        }

        if (creature.Has(CapabilityKind.MultiHead))
        {
            return BattleGrid.AdjacentEnemies(battle, attacker).Where(u => u != target).ToList();
        }

        if (creature.Has(CapabilityKind.Cleave))
        {
            return BattleGrid.AdjacentEnemies(battle, attacker)
                .Where(u => u != target && u.Rect.IsAdjacentTo(target.Rect))
                .Take(2)
                .ToList();
        }

        return [];
    }

    /// <returns>How many creatures the hit killed.</returns>
    private int Apply(BattleUnit attacker, BattleUnit target, BattleAttackKind kind, int damage, HitLuck luck)
    {
        // A blind or terrified stack, or one under a half-retaliation spell, hits back at half (ADR-123).
        if (kind is BattleAttackKind.Retaliation or BattleAttackKind.RangedRetaliation && attacker.Has(Magic.SpellStatus.Blind | Magic.SpellStatus.NoAttack))
        {
            damage = Math.Max(1, damage / 2);
        }

        var dealt = Math.Min(damage, target.TotalHealth);
        var killed = wounds.Take(target, damage);
        target.HitsThisRound++;
        attacker.DamageDealt += dealt;
        events.Publish(new BattleUnitAttacked(attacker.Id, target.Id, kind, damage, killed, target.Count, target.TopHealth, luck));
        wounds.Hit(target);
        wounds.Bury(target);

        // Vampirism (ADR-123): part of the damage dealt to a living stack heals the attacker and revives its dead, for good.
        var spells = attacker.SpellModifiers;
        if (spells.LifestealPercent > 0 && attacker.IsAlive && !target.Creature.HasTag(CreatureTags.Undead) && !target.Creature.HasTag(CreatureTags.Construct))
        {
            var source = attacker.Spells.First(s => s.Modifiers.LifestealPercent > 0).Spell;
            wounds.Revive(attacker, source, dealt * spells.LifestealPercent / 100, lasting: true);
        }

        // Barkskin III (ADR-123): a melee attacker takes part of its damage back.
        if (kind == BattleAttackKind.Melee && target.Spells.FirstOrDefault(s => s.Modifiers.ThornsPercent > 0) is { } thorns && attacker.IsAlive && dealt > 0)
        {
            wounds.SpellDamage(attacker, thorns.Spell, Math.Max(1, dealt * thorns.Modifiers.ThornsPercent / 100));
        }

        return killed;
    }

}
