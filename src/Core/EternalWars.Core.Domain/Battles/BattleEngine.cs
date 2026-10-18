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
        if (battle.FindUnit(targetId) is not { } target || !BattleGrid.CanShoot(battle, unit, target, out var cover))
        {
            return $"{unit.Creature.Name} cannot shoot that target.";
        }

        Fire(unit, target, cover);
        if (CanShootBack(target, unit, out var returnCover))
        {
            target.HasRetaliated = true;
            UseShot(target);
            Strike(target, unit, BattleAttackKind.RangedRetaliation, returnCover);
        }

        if (unit.IsAlive && target.IsAlive && unit.Creature.Has(CapabilityKind.ShootsTwice) && BattleGrid.CanShoot(battle, unit, target, out cover))
        {
            Fire(unit, target, cover);
        }

        EndTurn();
        return null;
    }

    private void Fire(BattleUnit shooter, BattleUnit target, int cover)
    {
        UseShot(shooter);
        Strike(shooter, target, BattleAttackKind.Ranged, cover);
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
    private bool CanShootBack(BattleUnit defender, BattleUnit shooter, out int cover)
    {
        cover = 0;
        return defender.IsAlive && shooter.IsAlive &&
               defender.Creature.Has(CapabilityKind.RangedRetaliation) &&
               !shooter.Creature.Has(CapabilityKind.NoEnemyRetaliation) &&
               (!defender.HasRetaliated || defender.Creature.Has(CapabilityKind.UnlimitedRetaliation)) &&
               BattleGrid.CanShoot(battle, defender, shooter, out cover);
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

    private void MoveUnit(BattleUnit unit, CellPosition to)
    {
        var from = unit.Position;
        unit.Position = to;
        events.Publish(new BattleUnitMoved(unit.Id, from, to));
    }

    private bool CanRetaliate(BattleUnit defender, BattleUnit attacker) =>
        defender.IsAlive && attacker.IsAlive && !defender.IsMachine &&
        defender.Rect.IsAdjacentTo(attacker.Rect) &&
        !attacker.Creature.Has(CapabilityKind.NoEnemyRetaliation) &&
        (!defender.HasRetaliated || defender.Creature.Has(CapabilityKind.UnlimitedRetaliation));

    /// <summary>One hit plus, for a melee strike, the attacker's area ability with the same roll.</summary>
    private void Strike(BattleUnit attacker, BattleUnit target, BattleAttackKind kind, int cover = 0)
    {
        var roll = battle.Random.NextInclusive(attacker.Creature.MinDamage, attacker.Creature.MaxDamage);
        var secondary = kind == BattleAttackKind.Melee ? AreaTargets(attacker, target) : [];
        var hit = new HitModifiers(roll, kind is BattleAttackKind.Ranged or BattleAttackKind.RangedRetaliation, cover);

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
        if (killed <= 0 || attacker.IsMachine || attacker.RageAttack >= PrimalFury.BloodRageMaxAttack(battle.Participant(attacker.Side).Bonus.BloodRage))
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
        switch (warcry)
        {
            case Warcry.Rally:
                battle.SideEffects.Add(new BattleSideEffect(side, PrimalFury.RallyMorale, 0, battle.Round + PrimalFury.RallyRounds));
                affected.AddRange(battle.LivingArmyOf(side).Select(u => u.Id));
                break;
            case Warcry.Intimidate:
                battle.SideEffects.Add(new BattleSideEffect(
                    side.Opponent(), PrimalFury.IntimidateMorale, PrimalFury.IntimidateInitiativePercent, battle.Round + PrimalFury.IntimidateRounds));
                affected.AddRange(battle.LivingArmyOf(side.Opponent()).Select(u => u.Id));
                break;
            default:
                foreach (var unit in battle.LivingArmyOf(side.Opponent()).OrderBy(u => u.Id))
                {
                    if (battle.Random.Next(100) < PrimalFury.TerrorChancePercent)
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
            var area = new CellRect(target.Position.X + (dx * target.Size), target.Position.Y + (dy * target.Size), target.Size, target.Size);
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

        target.HitsThisRound++;
        events.Publish(new BattleUnitAttacked(attacker.Id, target.Id, kind, damage, before - target.Count, target.Count, luck));
        if (!target.IsAlive)
        {
            battle.MutableQueue.Remove(target.Id);
            battle.MutableWaiting.Remove(target.Id);
            events.Publish(new BattleUnitDied(target.Id));
        }

        return before - target.Count;
    }

    /// <summary>Ends the active unit's action; with good morale rolled this round it acts once more right away (ADR-053).</summary>
    private void EndTurn(bool moraleMayAct = true)
    {
        var unit = battle.ActiveUnit;
        battle.ActiveUnit = null;
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
        Heal(BattleSide.Attacker);
        Heal(BattleSide.Defender);
        foreach (var unit in battle.MutableQueue.Select(battle.GetUnit).Where(u => u.RoundMorale is MoraleEffect.Haste or MoraleEffect.Slow))
        {
            events.Publish(new BattleMoraleTriggered(unit.Id, unit.RoundMorale));
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
