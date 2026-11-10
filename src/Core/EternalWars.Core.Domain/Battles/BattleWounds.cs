using EternalWars.Core.Domain.Events;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Battles;

/// <summary>
/// How stacks take damage and health back during one <see cref="BattleEngine"/> action: blows (with Martyrdom's shares), spell damage, deaths and
/// rebirths, healing and reviving, the moat and the field zones a move crosses. It keeps the action's blind stacks hit and martyrs' shares, so the
/// engine, <see cref="BattleSpellResolver"/> and <see cref="BattleRounds"/> share one instance.
/// </summary>
internal sealed class BattleWounds(BattleState battle, IEventPublisher events)
{
    /// <summary>Stacks hit while blind this action; they wake once the blow or the spell is over, so a blind stack still retaliates at half.</summary>
    private readonly HashSet<BattleUnit> _hitWhileBlind = [];

    /// <summary>Martyrs' shares of a blow (ADR-152), dealt once the blow's stack is buried.</summary>
    private readonly List<(BattleUnit Martyr, BattleUnit Guarded, string Spell, int Damage)> _shields = [];

    /// <summary>What the stack's move from <paramref name="from"/> costs it: the moat, then the field zones' damage on the way (<paramref name="hits"/>).</summary>
    public void Landed(BattleUnit unit, CellPosition from, IReadOnlyList<(BattleZone Zone, int Damage)> hits)
    {
        EnterMoat(unit, from);

        // Field zones on the way (ADR-149): Fire Wall burns a stack walking in, Thornwall tears one walking out.
        foreach (var (zone, damage) in hits)
        {
            if (unit.IsAlive)
            {
                SpellDamage(unit, zone.Spell, damage);
            }
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
        var killed = Take(unit, damage);
        events.Publish(new BattleUnitSiegeDamaged(unit.Id, SiegeDamageSource.Moat, null, damage, killed, unit.Count, unit.TopHealth));
        Bury(unit);
    }

    /// <summary>A spell's damage to a stack (a cast, a burning spell, thorns): wounded, reported, buried; a blind stack hit wakes (ADR-123).</summary>
    public void SpellDamage(BattleUnit unit, string spell, int damage, bool shieldable = true)
    {
        var killed = Take(unit, damage, shieldable);
        events.Publish(new BattleUnitSpellDamaged(unit.Id, spell, damage, killed, unit.Count, unit.TopHealth));
        Hit(unit);
        Bury(unit);
    }

    /// <summary>
    /// A spell takes <paramref name="creatures"/> of the stack's creatures (Slay, decay), the wounded top creature first; it is not a blow, so no
    /// martyr shares it.
    /// </summary>
    public void KillCreatures(BattleUnit unit, string spell, int creatures) => SpellDamage(unit, spell, unit.HealthOf(creatures), shieldable: false);

    /// <summary>
    /// The stack's top creature regains up to <paramref name="amount"/> health, never above its maximum; nothing happens (and nothing is reported)
    /// when it gains none.
    /// </summary>
    public void HealTop(BattleUnit unit, int amount)
    {
        var healed = Math.Min(amount, unit.Creature.Health - unit.TopHealth);
        if (healed > 0)
        {
            unit.TopHealth += healed;
            events.Publish(new BattleUnitHealed(unit.Id, healed, unit.TopHealth));
        }
    }

    /// <summary>
    /// Health back to the stack (ADR-123): its top creature first, then its dead up to its count when the battle started. Creatures revived
    /// without <paramref name="lasting"/> are lost again when the battle ends. A stack destroyed earlier stands again and acts from the next round.
    /// </summary>
    public void Revive(BattleUnit unit, string spell, int health, bool lasting)
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

    /// <summary>Notes a stack hit while blind; it wakes when the action is over (<see cref="WakeHit"/>).</summary>
    public void Hit(BattleUnit unit)
    {
        if (unit.Has(Magic.SpellStatus.Blind))
        {
            _hitWhileBlind.Add(unit);
        }
    }

    /// <summary>Blind ends on the stacks hit (ADR-123).</summary>
    public void WakeHit()
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

    /// <summary>Reports the stack's new side when a control spell (ADR-150) took hold or let go since it was <paramref name="before"/>.</summary>
    public void SideChanged(BattleUnit unit, BattleSide before)
    {
        if (unit.Side != before)
        {
            events.Publish(new BattleUnitSideChanged(unit.Id, unit.Side));
        }
    }

    /// <summary>
    /// A blow's damage to the stack (an attack, a damage spell, a zone, the towers, the moat): under Martyrdom (ADR-152) its martyr takes its share,
    /// once the stack is buried; the stack takes the rest. Losses that are not blows (Final Reckoning, decay, Blood Price) are not
    /// <paramref name="shieldable"/>.
    /// </summary>
    public int Take(BattleUnit target, int damage, bool shieldable = true)
    {
        if (shieldable && damage > 0 && target.Spells.Count > 0
            && target.Spells.FirstOrDefault(s => s.Martyr is not null && s.Modifiers.MartyrPercent > 0) is { } guard
            && battle.FindUnit(guard.Martyr!.Value) is { IsAlive: true } martyr && martyr != target && martyr.Side == target.Side
            && !martyr.Has(Magic.SpellStatus.Stasis))
        {
            var share = (int)((long)damage * Math.Min(100, guard.Modifiers.MartyrPercent) / 100);
            damage -= share;
            if (share > 0)
            {
                _shields.Add((martyr, target, guard.Spell, share));
            }
        }

        return Wound(target, damage);
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

    /// <summary>
    /// After a blow: a copy uses up one of its hits and vanishes when none is left (ADR-152); the stack is buried if gone; then the martyrs take
    /// their shares of the blow.
    /// </summary>
    public void Bury(BattleUnit target)
    {
        if (target.IsCopy && target.IsAlive && --target.CopyHits <= 0)
        {
            Vanish(target);
        }

        BuryOnly(target);
        if (_shields.Count == 0)
        {
            return;
        }

        var shares = _shields.ToList();
        _shields.Clear();
        foreach (var (martyr, guarded, spell, damage) in shares)
        {
            if (!martyr.IsAlive)
            {
                continue;
            }

            var killed = Wound(martyr, damage);
            events.Publish(new BattleUnitShielded(martyr.Id, guarded.Id, spell, damage, killed, martyr.Count, martyr.TopHealth));
            Hit(martyr);
            Bury(martyr);
        }
    }

    /// <summary>A copy is gone (ADR-152); <see cref="BuryOnly"/> reports it dead.</summary>
    public void Vanish(BattleUnit copy)
    {
        copy.Count = 0;
        copy.TopHealth = 0;
        events.Publish(new BattleCopyVanished(copy.Id, copy.CopySpell ?? string.Empty));
    }

    /// <summary>A stack that is gone leaves the round's order; a Phoenix with its rebirth left rises instead (ADR-120).</summary>
    public void BuryOnly(BattleUnit target)
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
            var before = target.Side;
            target.MutableSpells.Clear(); // a stack revived later starts without them (ADR-123); a controlled one is its owner's again (ADR-150)
            SideChanged(target, before);
            target.Revived = 0;
            battle.MutableQueue.Remove(target.Id);
            battle.MutableWaiting.Remove(target.Id);
            events.Publish(new BattleUnitDied(target.Id));
        }
    }
}
