using EternalWars.Core.Domain.Events;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Battles;

/// <summary>
/// The turn order of a battle (ADR-009, ADR-021, ADR-053): who acts next, what a round's start does (spells and zones running out, morale, the
/// order, the healing tent, burning and regrowth, the towers' shots, Raise Dead), the end of a turn (good morale's extra action, Clockwork
/// Precision) and the end of the battle.
/// </summary>
internal sealed class BattleRounds(BattleState battle, IEventPublisher events, BattleWounds wounds, BattleSpellResolver spells)
{
    /// <summary>Ends the active unit's action; with good morale rolled this round it acts once more right away (ADR-053).</summary>
    public void EndTurn(bool moraleMayAct = true)
    {
        wounds.WakeHit();
        var unit = battle.ActiveUnit;
        battle.ActiveUnit = null;
        if (unit is not null)
        {
            unit.StaysPut = false; // Shadow Step III's turn in place is over (ADR-151)
        }

        // Unseen lasts until the stack acts (ADR-123).
        foreach (var hidden in unit?.Spells.Where(s => s.Modifiers.Has(Magic.SpellStatus.Unseen)).ToList() ?? [])
        {
            unit!.MutableSpells.Remove(hidden);
            events.Publish(new BattleSpellExpired(unit.Id, hidden.Spell));
        }

        if (unit is not null)
        {
            UseTurn(unit);
        }

        if (moraleMayAct && unit is { IsAlive: true, RoundMorale: MoraleEffect.ExtraAction } && !CheckFinished())
        {
            unit.RoundMorale = MoraleEffect.None;
            battle.ActiveUnit = unit;
            events.Publish(new BattleMoraleTriggered(unit.Id, MoraleEffect.ExtraAction));
            events.Publish(new BattleTurnStarted(unit.Id, battle.Round));
            return;
        }

        // Clockwork Precision (ADR-152): a second turn right after the first, once a round.
        if (moraleMayAct && unit is { IsAlive: true, TookSecondTurn: false } && unit.Spells.FirstOrDefault(s => s.Modifiers.Has(Magic.SpellStatus.Twice)) is { } twice
            && !CheckFinished())
        {
            unit.TookSecondTurn = true;
            battle.ActiveUnit = unit;
            events.Publish(new BattleUnitActsAgain(unit.Id, twice.Spell));
            events.Publish(new BattleTurnStarted(unit.Id, battle.Round));
            return;
        }

        NextTurn();
    }

    public void NextTurn()
    {
        while (true)
        {
            if (CheckFinished())
            {
                return;
            }

            if (battle.MutableQueue.Count == 0 && battle.MutableWaiting.Count == 0)
            {
                if (battle.Round >= BattleEngine.MaxRounds)
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
                UseTurn(unit);
                continue;
            }

            // Blind and Stasis hold the stack: its turn passes (ADR-123).
            if (unit.Spells.FirstOrDefault(s => s.Modifiers.Has(Magic.SpellStatus.Blind | Magic.SpellStatus.Stasis)) is { } held)
            {
                events.Publish(new BattleUnitSpellbound(unit.Id, held.Spell));
                UseTurn(unit);
                continue;
            }

            if (unit.RoundMorale == MoraleEffect.SkipTurn)
            {
                // Bad morale: the turn passes without the unit acting.
                unit.RoundMorale = MoraleEffect.None;
                events.Publish(new BattleMoraleTriggered(unit.Id, MoraleEffect.SkipTurn));
                UseTurn(unit);
                continue;
            }

            battle.ActiveUnit = unit;
            events.Publish(new BattleTurnStarted(unit.Id, battle.Round));
            return;
        }
    }

    /// <summary>
    /// One of the stack's turns is over, played or passed (ADR-150): each of its spells that lasts turns has one fewer; one with none left wears off.
    /// </summary>
    private void UseTurn(BattleUnit unit)
    {
        if (!unit.Spells.Any(s => s.TurnsLeft is not null))
        {
            return;
        }

        var before = unit.Side;
        foreach (var spell in unit.Spells.Where(s => s.TurnsLeft is not null).ToList())
        {
            var index = unit.MutableSpells.IndexOf(spell);
            if (spell.TurnsLeft > 1)
            {
                unit.MutableSpells[index] = spell with { TurnsLeft = spell.TurnsLeft - 1 };
                continue;
            }

            unit.MutableSpells.RemoveAt(index);
            events.Publish(new BattleSpellExpired(unit.Id, spell.Spell));
        }

        wounds.SideChanged(unit, before);
    }

    private void StartRound()
    {
        battle.Round++;
        battle.WaitedThisRound.Clear();
        battle.TieRolls.Clear();

        // Copies whose rounds are over vanish (ADR-152).
        foreach (var copy in battle.LivingUnits.Where(u => u.IsCopy && u.CopyUntilRound < battle.Round).OrderBy(u => u.Id).ToList())
        {
            wounds.Vanish(copy);
            wounds.BuryOnly(copy);
        }

        // Spells that ran out go before this round's morale and order are decided (ADR-110).
        var expired = new List<(int Unit, string Spell)>();
        var freed = new List<BattleUnit>();
        foreach (var unit in battle.LivingUnits.Where(u => u.Spells.Count > 0).OrderBy(u => u.Id))
        {
            var before = unit.Side;
            expired.AddRange(unit.MutableSpells.Where(s => s.UntilRound < battle.Round).Select(s => (unit.Id, s.Spell)));
            unit.MutableSpells.RemoveAll(s => s.UntilRound < battle.Round);
            if (unit.Side != before)
            {
                freed.Add(unit);
            }
        }

        var gone = battle.Zones.Where(z => z.UntilRound < battle.Round).ToList();
        battle.MutableZones.RemoveAll(gone.Contains);

        foreach (var unit in battle.LivingUnits)
        {
            unit.StaysPut = false;
            unit.TookSecondTurn = false;
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

        foreach (var unit in freed)
        {
            events.Publish(new BattleUnitSideChanged(unit.Id, unit.Side));
        }

        foreach (var zone in gone)
        {
            events.Publish(new BattleZoneExpired(zone.Id, zone.Spell));
        }

        Heal(BattleSide.Attacker);
        Heal(BattleSide.Defender);
        Tick();
        TickZones();
        TowersFire();
        wounds.WakeHit();
        spells.Rise(BattleSide.Attacker);
        spells.Rise(BattleSide.Defender);
        foreach (var unit in battle.MutableQueue.Select(battle.GetUnit).Where(u => u.RoundMorale is MoraleEffect.Haste or MoraleEffect.Slow))
        {
            events.Publish(new BattleMoraleTriggered(unit.Id, unit.RoundMorale));
        }
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
                    wounds.SpellDamage(unit, spell.Spell, m.Burn);
                }

                if (m.Regen > 0 && unit.IsAlive)
                {
                    wounds.HealTop(unit, m.Regen);
                }

                if (m.DecayPercent > 0 && unit.IsAlive)
                {
                    wounds.KillCreatures(unit, spell.Spell, Math.Min(unit.Count, Math.Max(1, unit.Count * m.DecayPercent / 100)));
                }
            }
        }
    }

    /// <summary>
    /// Field zones as a round starts (ADR-149), zone by zone in id order, stacks in id order: damage to the stacks in it (<c>start</c>) and next
    /// to it (<c>beside</c>) that it hurts, healing for own stacks in it.
    /// </summary>
    private void TickZones()
    {
        foreach (var zone in battle.Zones.ToList())
        {
            foreach (var unit in battle.LivingUnits.OrderBy(u => u.Id).ToList())
            {
                var inside = zone.Rect.Intersects(unit.Rect);
                var amount = inside ? zone.Amounts.Start : unit.Rect.IsAdjacentTo(zone.Rect) ? zone.Amounts.Beside : 0;
                if (amount > 0 && BattleZones.Hurts(battle, zone, unit))
                {
                    wounds.SpellDamage(unit, zone.Spell, BattleZones.Damage(battle, zone, unit, amount));
                }

                wounds.HealTop(unit, BattleZones.Healing(zone, unit));
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
            wounds.HealTop(unit, amount);
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

            var killed = wounds.Take(target, damage);
            events.Publish(new BattleUnitSiegeDamaged(target.Id, SiegeDamageSource.Tower, tower.Id, damage, killed, target.Count, target.TopHealth));
            wounds.Hit(target);
            wounds.Bury(target);
        }
    }

    private (int Initiative, int Side, int Speed, int Roll) Key(int unitId) => Key(battle.GetUnit(unitId));

    private (int Initiative, int Side, int Speed, int Roll) Key(BattleUnit unit) =>
        (battle.InitiativeOf(unit), unit.Side == BattleSide.Attacker ? 1 : 0, battle.SpeedOf(unit), battle.TieRolls.GetValueOrDefault(unit.Id));

    /// <summary>Whether the battle is over; ends it when a side has no army left.</summary>
    public bool CheckFinished()
    {
        if (battle.Phase == BattlePhase.Finished)
        {
            return true;
        }

        // War machines alone do not keep a side in the battle (ADR-056); a stack the enemy controls still does (ADR-150).
        var attackers = battle.OwnedArmyOf(BattleSide.Attacker).Any();
        var defenders = battle.OwnedArmyOf(BattleSide.Defender).Any();
        if (attackers && defenders)
        {
            return false;
        }

        Finish(attackers ? BattleSide.Attacker : BattleSide.Defender, BattleEndReason.Defeated);
        return true;
    }

    public void Finish(BattleSide winner, BattleEndReason reason)
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
