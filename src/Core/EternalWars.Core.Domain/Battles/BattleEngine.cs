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
/// </remarks>
internal sealed class BattleEngine(BattleState battle, IEventPublisher events)
{
    /// <summary>Safety net: a battle still undecided after this many rounds is won by the defender.</summary>
    public const int MaxRounds = 100;

    public void StartFighting()
    {
        battle.Phase = BattlePhase.Fighting;
        NextTurn();
    }

    public string? Move(CellPosition to)
    {
        var unit = battle.ActiveUnit!;
        if (to == unit.Position || !BattleGrid.Reachable(battle, unit).ContainsKey(to))
        {
            return $"{unit.Creature.Name} cannot move to {to}.";
        }

        MoveUnit(unit, to);
        EndTurn();
        return null;
    }

    public string? Attack(int targetId, CellPosition? from)
    {
        var unit = battle.ActiveUnit!;
        if (battle.FindUnit(targetId) is not { IsAlive: true } target || target.Side == unit.Side)
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

        EndTurn();
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

        EndTurn();
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

        EndTurn();
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
            var roll = unit.SpellModifiers.Roll != 0 ? DamageCalculator.Roll(unit, 0) : battle.Random.NextInclusive(unit.Creature.MinDamage, unit.Creature.MaxDamage);
            var damage = Math.Min(structure.Health, SiegeRules.GateDamage(battle, unit, roll));
            structure.Health -= damage;
            events.Publish(new SiegeStructureDamaged(structure.Id, unit.Id, null, damage, structure.Health));
        }

        if (!CheckFinished())
        {
            EndTurn();
        }

        return null;
    }

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

    /// <summary>Earthquake (ADR-120): <paramref name="sections"/> standing wall sections, drawn at random, take 1 damage each.</summary>
    private void Quake(string spell, int sections)
    {
        if (battle.Siege is not { } siege)
        {
            return;
        }

        var walls = siege.Structures.Where(s => s.IsStanding && s.Kind == SiegeStructureKind.Wall).OrderBy(s => s.Id).ToList();
        for (var i = 0; i < sections && walls.Count > 0; i++)
        {
            var wall = walls[battle.Random.Next(walls.Count)];
            walls.Remove(wall);
            wall.Health -= 1;
            events.Publish(new SiegeStructureDamaged(wall.Id, null, spell, 1, wall.Health));
        }
    }

    /// <summary>The keep and the towers each shoot once a round, as it starts, at the attacking stack worth most to hit (ADR-106; timing assumed).</summary>
    private void TowersFire()
    {
        if (battle.Siege is not { } siege)
        {
            return;
        }

        foreach (var tower in siege.Structures.Where(s => s.IsStanding && s.Shoots))
        {
            var damage = siege.ShotDamage(tower);
            var target = battle.LivingArmyOf(BattleSide.Attacker)
                .Where(u => !u.Has(Magic.SpellStatus.Stasis | Magic.SpellStatus.Unseen))
                .OrderByDescending(u => (long)Math.Min(damage, u.TotalHealth) * u.Creature.Value / u.Creature.Health)
                .ThenBy(u => u.Id)
                .FirstOrDefault();
            if (target is null)
            {
                return;
            }

            var killed = Wound(target, damage);
            events.Publish(new BattleUnitSiegeDamaged(target.Id, SiegeDamageSource.Tower, tower.Id, damage, killed, target.Count));
            Hit(target);
            Bury(target);
        }
    }

    /// <summary>An attacker entering the moat stops there (<see cref="BattleGrid.Reachable"/>) and takes its damage (ADR-106).</summary>
    private void EnterMoat(BattleUnit unit, CellPosition from)
    {
        if (!SiegeRules.FearsMoat(battle, unit.Side) || battle.Siege!.InMoat(unit.RectAt(from)) || !battle.Siege.InMoat(unit.Rect))
        {
            return;
        }

        var damage = battle.Siege.MoatDamage;
        var killed = Wound(unit, damage);
        events.Publish(new BattleUnitSiegeDamaged(unit.Id, SiegeDamageSource.Moat, null, damage, killed, unit.Count));
        Bury(unit);
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
        NextTurn();
        return null;
    }

    public string? Defend()
    {
        var unit = battle.ActiveUnit!;
        unit.IsDefending = true;
        events.Publish(new BattleUnitDefended(unit.Id));
        EndTurn(moraleMayAct: false); // defending is not an action good morale repeats
        return null;
    }

    /// <summary>The side's hero leaves the field; the other side wins.</summary>
    public void Retreat(BattleSide side) => Finish(side.Opponent(), BattleEndReason.Retreated);

    /// <summary>The side's hero leaves the field with its army, paid for by <c>BattleService.Surrender</c>; the other side wins.</summary>
    public void Surrender(BattleSide side) => Finish(side.Opponent(), BattleEndReason.Surrendered);

    private void MoveUnit(BattleUnit unit, CellPosition to)
    {
        var from = unit.Position;
        unit.Position = to;
        events.Publish(new BattleUnitMoved(unit.Id, from, to));
        EnterMoat(unit, from);
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
        // Bless and Curse fix the roll (ADR-110); nothing is drawn then.
        var roll = attacker.SpellModifiers.Roll != 0
            ? DamageCalculator.Roll(attacker, 0)
            : battle.Random.NextInclusive(attacker.Creature.MinDamage, attacker.Creature.MaxDamage);
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
    /// The side's hero casts a spell (ADR-110): it pays the mana, then the spell works on every stack in its reach it affects, by id; a stack it
    /// does not affect is reported as resisting. The active unit keeps its action, unless the spell killed it.
    /// </summary>
    public string? Cast(BattleSide side, string spellId, int? targetId, CellPosition? cell)
    {
        if (BattleMagic.CastRefusal(battle, side, spellId, targetId, cell) is { } refusal)
        {
            return refusal;
        }

        var known = battle.Participant(side).Caster!.Find(spellId)!;
        var cost = BattleMagic.Cost(battle, side, known);
        battle.ManaLeft[side] -= cost;
        battle.CastRounds[side] = battle.Round;
        events.Publish(new BattleSpellCast(side, spellId, targetId, cell, cost, battle.ManaOf(side)));
        Work(side, known, targetId, cell, null);

        if (!CheckFinished() && battle.ActiveUnit is { IsAlive: false })
        {
            EndTurn(moraleMayAct: false);
        }

        return null;
    }

    /// <summary>
    /// The active creature caster casts its spell (ADR-120): its action for the turn, no mana, with its own Spell Power
    /// (<see cref="BattleMagic.CreatureSpellPower"/>); one of its casts is used up.
    /// </summary>
    public string? CreatureCast(int? targetId, CellPosition? cell)
    {
        var unit = battle.ActiveUnit!;
        if (BattleMagic.CreatureCastRefusal(battle, unit, targetId, cell) is { } refusal)
        {
            return refusal;
        }

        var known = BattleMagic.CreatureSpell(unit)!;
        var caster = BattleMagic.CasterSpells.First(c => c.Value == known.Spell.Id && unit.CastsLeft.GetValueOrDefault(c.Key) > 0).Key;
        unit.MutableCastsLeft[caster]--;
        events.Publish(new BattleCreatureCast(unit.Id, known.Spell.Id, targetId, cell, unit.CastsLeft[caster]));
        Work(unit.Side, known, targetId, cell, BattleMagic.CreatureSpellPower(unit));
        if (!CheckFinished())
        {
            EndTurn();
        }

        return null;
    }

    /// <summary>What a cast does, the hero's or a creature's: the spell works on every stack in its reach it affects, by id (a stack it does not affect is reported as resisting); a summon calls its creatures; Earthquake shakes the walls.</summary>
    private void Work(BattleSide side, KnownSpell known, int? targetId, CellPosition? cell, int? creaturePower)
    {
        var effect = known.Effect!;
        var spellId = known.Spell.Id;
        if (effect.Kind == Magic.SpellEffectKind.Summon)
        {
            Summon(side, known, creaturePower);
            return;
        }

        var reach = BattleMagic.Reach(battle, side, known, targetId, cell);
        foreach (var unit in reach.Where(u => !BattleMagic.Affects(battle, side, known, u)))
        {
            events.Publish(new BattleSpellResisted(unit.Id, spellId));
        }

        if (effect.Kind == Magic.SpellEffectKind.Damage)
        {
            // Splash and chain hits come after the stacks in reach (ADR-123); every damage is worked out before it is dealt.
            foreach (var (unit, damage) in BattleMagic.DamagePlan(battle, side, known, targetId, cell, creaturePower))
            {
                SpellDamage(unit, spellId, damage);
                if (!unit.IsAlive)
                {
                    continue;
                }

                if (!effect.Modifiers.IsEmpty)
                {
                    Enchant(unit, side, known, creaturePower);
                }

                if (effect.StunPercent > 0 && battle.Random.Next(100) < effect.StunPercent)
                {
                    unit.LosesNextAction = true;
                    events.Publish(new BattleUnitStunned(unit.Id, spellId));
                }
            }
        }
        else
        {
            foreach (var unit in reach.Where(u => BattleMagic.Affects(battle, side, known, u)))
            {
                switch (effect.Kind)
                {
                    case Magic.SpellEffectKind.Heal:
                        var healed = Math.Min(BattleMagic.Amount(battle, side, known, creaturePower), unit.Creature.Health - unit.TopHealth);
                        if (healed > 0)
                        {
                            unit.TopHealth += healed;
                            events.Publish(new BattleUnitHealed(unit.Id, healed, unit.TopHealth));
                        }

                        break;
                    case Magic.SpellEffectKind.Revive:
                        Revive(unit, spellId, BattleMagic.Amount(battle, side, known, creaturePower), effect.Lasting);
                        break;
                    case Magic.SpellEffectKind.Dispel:
                        foreach (var removed in BattleMagic.Dispelled(side, effect, unit))
                        {
                            unit.MutableSpells.Remove(removed);
                            events.Publish(new BattleSpellDispelled(unit.Id, removed.Spell, spellId));
                        }

                        break;
                    case Magic.SpellEffectKind.Slay:
                        var slain = Math.Min(unit.Count, BattleMagic.Slain(effect, unit));
                        SpellDamage(unit, spellId, unit.TotalHealth - ((unit.Count - slain) * unit.Creature.Health)); // the wounded top creature goes first
                        break;
                    default:
                        Enchant(unit, side, known, creaturePower);
                        break;
                }
            }
        }

        if (effect.Walls > 0)
        {
            Quake(spellId, effect.Walls);
        }

        WakeHit();
    }

    /// <summary>A spell's damage to a stack (a cast, a burning spell, thorns): wounded, reported, buried; a blind stack hit wakes (ADR-123).</summary>
    private void SpellDamage(BattleUnit unit, string spell, int damage)
    {
        var killed = Wound(unit, damage);
        events.Publish(new BattleUnitSpellDamaged(unit.Id, spell, damage, killed, unit.Count));
        Hit(unit);
        Bury(unit);
    }

    /// <summary>
    /// Health back to the stack (ADR-123): its top creature first, then its dead up to its count when the battle started. Creatures revived
    /// without <paramref name="lasting"/> are lost again when the battle ends. A stack destroyed earlier stands again and acts from the next round.
    /// </summary>
    private void Revive(BattleUnit unit, string spell, int health, bool lasting)
    {
        var total = (long)Math.Min(BattleMagic.Missing(unit), health);
        if (total <= 0)
        {
            return;
        }

        var before = unit.Count;
        var now = unit.TotalHealth + total;
        var creature = unit.Creature.Health;
        unit.Count = (int)((now + creature - 1) / creature);
        unit.TopHealth = (int)(now - ((long)(unit.Count - 1) * creature));
        if (!lasting)
        {
            unit.Revived += unit.Count - before;
        }

        events.Publish(new BattleUnitRevived(unit.Id, spell, (int)total, unit.Count, unit.TopHealth));
    }

    /// <summary>Stacks hit while blind this action; they wake once the blow or the spell is over, so a blind stack still retaliates at half.</summary>
    private readonly HashSet<BattleUnit> _hitWhileBlind = [];

    private void Hit(BattleUnit unit)
    {
        if (unit.Has(Magic.SpellStatus.Blind))
        {
            _hitWhileBlind.Add(unit);
        }
    }

    /// <summary>Blind ends on the stacks hit (ADR-123).</summary>
    private void WakeHit()
    {
        foreach (var unit in _hitWhileBlind.OrderBy(u => u.Id))
        {
            foreach (var spell in unit.Spells.Where(s => s.Modifiers.Has(Magic.SpellStatus.Blind)).ToList())
            {
                unit.MutableSpells.Remove(spell);
                events.Publish(new BattleSpellExpired(unit.Id, spell.Spell));
            }
        }

        _hitWhileBlind.Clear();
    }

    /// <summary>
    /// A summon (ADR-106, ADR-120): its creatures join an own summoned stack of theirs or stand on the free placement nearest the own edge; they act
    /// from the next round, vanish when the battle ends and are worth no experience.
    /// </summary>
    private void Summon(BattleSide side, KnownSpell known, int? creaturePower)
    {
        if (BattleMagic.SummonPlacement(battle, side, known, creaturePower) is not { } placement)
        {
            return;
        }

        var (creature, count, joins, at) = placement;
        var unit = joins;
        if (unit is not null)
        {
            unit.Count += count;
        }
        else
        {
            unit = new BattleUnit(battle.Units.Max(u => u.Id) + 1, side, creature, count, -1, at)
            {
                IsSummoned = true,
                IsRooted = known.Effect!.Rooted,
            };
            battle.MutableUnits.Add(unit);
        }

        events.Publish(new BattleUnitSummoned(
            new BattleUnitInfo(unit.Id, unit.Side, unit.Creature.Id, unit.Count, unit.Position, unit.Width, unit.Height), known.Spell.Id, count));
    }

    /// <summary>A buff or curse takes hold on the stack, replacing the same spell's earlier effect.</summary>
    private void Enchant(BattleUnit unit, BattleSide side, KnownSpell known, int? creaturePower = null)
    {
        var until = BattleMagic.UntilRound(battle, side, known, unit, creaturePower);
        var modifiers = known.Effect!.Modifiers;
        if (modifiers.Burn != 0 || modifiers.Regen != 0)
        {
            // Burning and regrowth grow with Spell Power like damage, when cast (ADR-123).
            modifiers = modifiers with
            {
                Burn = modifiers.Burn == 0 ? 0 : BattleMagic.Scaled(battle, side, known, modifiers.Burn, creaturePower),
                Regen = modifiers.Regen == 0 ? 0 : BattleMagic.Scaled(battle, side, known, modifiers.Regen, creaturePower),
            };
        }

        unit.MutableSpells.RemoveAll(s => s.Spell == known.Spell.Id);
        unit.MutableSpells.Add(new ActiveSpell(known.Spell.Id, side, modifiers, until));
        events.Publish(new BattleSpellApplied(unit.Id, known.Spell.Id, until));
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

    /// <summary>
    /// This round's morale (ADR-053): per point, 2 % for an extra action (good) or a lost turn (bad), else 4 % for ±50 %
    /// initiative. Rolls only for a unit with morale.
    /// </summary>
    private MoraleEffect RollMorale(BattleUnit unit)
    {
        var morale = battle.MoraleOf(unit);
        if (morale == 0)
        {
            return MoraleEffect.None;
        }

        var points = Math.Abs(morale);
        if (battle.Random.Next(100) < MoraleAndLuck.ExtraActionPercent * points)
        {
            return morale > 0 ? MoraleEffect.ExtraAction : MoraleEffect.SkipTurn;
        }

        return battle.Random.Next(100) < MoraleAndLuck.InitiativePercent * points
            ? morale > 0 ? MoraleEffect.Haste : MoraleEffect.Slow
            : MoraleEffect.None;
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
        var killed = Wound(target, damage);
        target.HitsThisRound++;
        attacker.DamageDealt += dealt;
        events.Publish(new BattleUnitAttacked(attacker.Id, target.Id, kind, damage, killed, target.Count, luck));
        Hit(target);
        Bury(target);

        // Vampirism (ADR-123): part of the damage dealt to a living stack heals the attacker and revives its dead, for good.
        var spells = attacker.SpellModifiers;
        if (spells.LifestealPercent > 0 && attacker.IsAlive && !target.Creature.HasTag(CreatureTags.Undead) && !target.Creature.HasTag(CreatureTags.Construct))
        {
            var source = attacker.Spells.First(s => s.Modifiers.LifestealPercent > 0).Spell;
            Revive(attacker, source, dealt * spells.LifestealPercent / 100, lasting: true);
        }

        // Barkskin III (ADR-123): a melee attacker takes part of its damage back.
        if (kind == BattleAttackKind.Melee && target.Spells.FirstOrDefault(s => s.Modifiers.ThornsPercent > 0) is { } thorns && attacker.IsAlive && dealt > 0)
        {
            SpellDamage(attacker, thorns.Spell, Math.Max(1, dealt * thorns.Modifiers.ThornsPercent / 100));
        }

        return killed;
    }

    /// <summary>Takes the damage off the stack, top creature first; creatures revived only for the battle die first (ADR-123).</summary>
    /// <returns>How many creatures it killed.</returns>
    private static int Wound(BattleUnit target, int damage)
    {
        var killed = Kill(target, damage);
        target.Revived = Math.Max(0, target.Revived - killed);
        return killed;
    }

    private static int Kill(BattleUnit target, int damage)
    {
        var before = target.Count;
        var remaining = target.TotalHealth - damage;
        if (remaining <= 0)
        {
            target.Count = 0;
            target.TopHealth = 0;
        }
        else
        {
            var health = target.Creature.Health;
            target.Count = (remaining + health - 1) / health;
            target.TopHealth = remaining - ((target.Count - 1) * health);
        }

        return before - target.Count;
    }

    /// <summary>A stack that is gone leaves the round's order; a Phoenix with its rebirth left rises instead (ADR-120).</summary>
    private void Bury(BattleUnit target)
    {
        if (!target.IsAlive && target.CastsLeft.GetValueOrDefault(BattleMagic.Rebirth) > 0)
        {
            target.MutableCastsLeft[BattleMagic.Rebirth]--;
            target.Count = Math.Max(1, target.InitialCount * BattleMagic.RebirthPercent / 100);
            target.TopHealth = target.Creature.Health;
            events.Publish(new BattleUnitReborn(target.Id, target.Count));
            return;
        }

        if (!target.IsAlive)
        {
            target.MutableSpells.Clear(); // a stack revived later starts without them (ADR-123)
            target.Revived = 0;
            battle.MutableQueue.Remove(target.Id);
            battle.MutableWaiting.Remove(target.Id);
            events.Publish(new BattleUnitDied(target.Id));
        }
    }

    /// <summary>Ends the active unit's action; with good morale rolled this round it acts once more right away (ADR-053).</summary>
    private void EndTurn(bool moraleMayAct = true)
    {
        WakeHit();
        var unit = battle.ActiveUnit;
        battle.ActiveUnit = null;

        // Unseen lasts until the stack acts (ADR-123).
        foreach (var hidden in unit?.Spells.Where(s => s.Modifiers.Has(Magic.SpellStatus.Unseen)).ToList() ?? [])
        {
            unit!.MutableSpells.Remove(hidden);
            events.Publish(new BattleSpellExpired(unit.Id, hidden.Spell));
        }

        if (moraleMayAct && unit is { IsAlive: true, RoundMorale: MoraleEffect.ExtraAction } && !CheckFinished())
        {
            unit.RoundMorale = MoraleEffect.None;
            battle.ActiveUnit = unit;
            events.Publish(new BattleMoraleTriggered(unit.Id, MoraleEffect.ExtraAction));
            events.Publish(new BattleTurnStarted(unit.Id, battle.Round));
            return;
        }

        NextTurn();
    }

    private void NextTurn()
    {
        while (true)
        {
            if (CheckFinished())
            {
                return;
            }

            if (battle.MutableQueue.Count == 0 && battle.MutableWaiting.Count == 0)
            {
                if (battle.Round >= MaxRounds)
                {
                    Finish(BattleSide.Defender, BattleEndReason.Defeated);
                    return;
                }

                StartRound();
                if (CheckFinished())
                {
                    return; // the towers' shots ended it
                }
            }

            int next;
            if (battle.MutableQueue.Count > 0)
            {
                next = battle.MutableQueue[0];
                battle.MutableQueue.RemoveAt(0);
            }
            else
            {
                // Waiting units go in reverse acting order.
                next = battle.MutableWaiting.OrderByDescending(Key, ActingOrder.Instance).First();
                battle.MutableWaiting.Remove(next);
            }

            var unit = battle.GetUnit(next);
            if (!unit.IsAlive)
            {
                continue;
            }

            unit.IsDefending = false;
            if (unit.LosesNextAction)
            {
                // Terror (ADR-067): the turn passes without the unit acting.
                unit.LosesNextAction = false;
                events.Publish(new BattleUnitTerrified(unit.Id));
                continue;
            }

            // Blind and Stasis hold the stack: its turn passes (ADR-123).
            if (unit.Spells.FirstOrDefault(s => s.Modifiers.Has(Magic.SpellStatus.Blind | Magic.SpellStatus.Stasis)) is { } held)
            {
                events.Publish(new BattleUnitSpellbound(unit.Id, held.Spell));
                continue;
            }

            if (unit.RoundMorale == MoraleEffect.SkipTurn)
            {
                // Bad morale: the turn passes without the unit acting.
                unit.RoundMorale = MoraleEffect.None;
                events.Publish(new BattleMoraleTriggered(unit.Id, MoraleEffect.SkipTurn));
                continue;
            }

            battle.ActiveUnit = unit;
            events.Publish(new BattleTurnStarted(unit.Id, battle.Round));
            return;
        }
    }

    private void StartRound()
    {
        battle.Round++;
        battle.WaitedThisRound.Clear();
        battle.TieRolls.Clear();

        // Spells that ran out go before this round's morale and order are decided (ADR-110).
        var expired = new List<(int Unit, string Spell)>();
        foreach (var unit in battle.LivingUnits.Where(u => u.Spells.Count > 0).OrderBy(u => u.Id))
        {
            expired.AddRange(unit.MutableSpells.Where(s => s.UntilRound < battle.Round).Select(s => (unit.Id, s.Spell)));
            unit.MutableSpells.RemoveAll(s => s.UntilRound < battle.Round);
        }

        foreach (var unit in battle.LivingUnits)
        {
            unit.HasRetaliated = false;
            unit.HitsThisRound = 0;
            battle.TieRolls[unit.Id] = battle.Random.Next(1_000_000);
            unit.RoundMorale = RollMorale(unit);
        }

        battle.MutableQueue.Clear();
        battle.MutableQueue.AddRange(battle.LivingUnits
            .Where(u => u.Machine is not { } machine || WarMachines.TakesTurns(machine))
            .OrderBy(Key, ActingOrder.Instance)
            .Select(u => u.Id));
        events.Publish(new BattleRoundStarted(battle.Round));
        foreach (var (unit, spell) in expired)
        {
            events.Publish(new BattleSpellExpired(unit, spell));
        }

        Heal(BattleSide.Attacker);
        Heal(BattleSide.Defender);
        Tick();
        TowersFire();
        WakeHit();
        foreach (var unit in battle.MutableQueue.Select(battle.GetUnit).Where(u => u.RoundMorale is MoraleEffect.Haste or MoraleEffect.Slow))
        {
            events.Publish(new BattleMoraleTriggered(unit.Id, unit.RoundMorale));
        }
    }

    /// <summary>
    /// Spells that work each round (ADR-123), as it starts, stack by stack in id order: burning deals its damage, regrowth heals the top creature,
    /// decay (Plague, Pact of Ash) takes its percent of the creatures, at least 1.
    /// </summary>
    private void Tick()
    {
        foreach (var unit in battle.LivingUnits.Where(u => u.Spells.Count > 0).OrderBy(u => u.Id).ToList())
        {
            foreach (var spell in unit.Spells.ToList())
            {
                if (!unit.IsAlive)
                {
                    break;
                }

                var m = spell.Modifiers;
                if (m.Burn > 0)
                {
                    SpellDamage(unit, spell.Spell, m.Burn);
                }

                if (m.Regen > 0 && unit.IsAlive && unit.TopHealth < unit.Creature.Health)
                {
                    var healed = Math.Min(m.Regen, unit.Creature.Health - unit.TopHealth);
                    unit.TopHealth += healed;
                    events.Publish(new BattleUnitHealed(unit.Id, healed, unit.TopHealth));
                }

                if (m.DecayPercent > 0 && unit.IsAlive)
                {
                    var lost = Math.Min(unit.Count, Math.Max(1, unit.Count * m.DecayPercent / 100));
                    SpellDamage(unit, spell.Spell, unit.TotalHealth - ((unit.Count - lost) * unit.Creature.Health));
                }
            }
        }
    }

    /// <summary>The healing tent: the top creature of every own creature stack regains health, never above its maximum (ADR-056).</summary>
    private void Heal(BattleSide side)
    {
        if (battle.LivingMachine(side, WarMachineKind.HealingTent) is null)
        {
            return;
        }

        var amount = WarMachines.TentHealing(battle.Participant(side).Bonus.Healing);
        foreach (var unit in battle.LivingArmyOf(side).Where(u => u.TopHealth < u.Creature.Health).ToList())
        {
            var healed = Math.Min(amount, unit.Creature.Health - unit.TopHealth);
            unit.TopHealth += healed;
            events.Publish(new BattleUnitHealed(unit.Id, healed, unit.TopHealth));
        }
    }

    private (int Initiative, int Side, int Speed, int Roll) Key(int unitId) => Key(battle.GetUnit(unitId));

    private (int Initiative, int Side, int Speed, int Roll) Key(BattleUnit unit) =>
        (battle.InitiativeOf(unit), unit.Side == BattleSide.Attacker ? 1 : 0, battle.SpeedOf(unit), battle.TieRolls.GetValueOrDefault(unit.Id));

    private bool CheckFinished()
    {
        if (battle.Phase == BattlePhase.Finished)
        {
            return true;
        }

        // War machines alone do not keep a side in the battle (ADR-056).
        var attackers = battle.LivingArmyOf(BattleSide.Attacker).Any();
        var defenders = battle.LivingArmyOf(BattleSide.Defender).Any();
        if (attackers && defenders)
        {
            return false;
        }

        Finish(attackers ? BattleSide.Attacker : BattleSide.Defender, BattleEndReason.Defeated);
        return true;
    }

    private void Finish(BattleSide winner, BattleEndReason reason)
    {
        battle.Phase = BattlePhase.Finished;
        battle.Winner = winner;
        battle.EndReason = reason;
        battle.ActiveUnit = null;
        battle.MutableQueue.Clear();
        battle.MutableWaiting.Clear();
    }

    /// <summary>Acting order: higher initiative, attacker side, higher speed, higher tie roll first.</summary>
    private sealed class ActingOrder : IComparer<(int Initiative, int Side, int Speed, int Roll)>
    {
        public static readonly ActingOrder Instance = new();

        public int Compare((int Initiative, int Side, int Speed, int Roll) a, (int Initiative, int Side, int Speed, int Roll) b) =>
            b.CompareTo(a);
    }
}
