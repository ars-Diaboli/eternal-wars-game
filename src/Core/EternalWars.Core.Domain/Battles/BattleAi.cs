namespace EternalWars.Core.Domain.Battles;

/// <summary>
/// Simple, deterministic battle AI for neutrals, computer players and quick combat. Greedy per turn:
/// shoot the most valuable target, else melee the most valuable reachable target, else close in on the
/// nearest enemy, else defend. Value = creature value of the hit points it expects to remove.
/// </summary>
internal static class BattleAi
{
    public static void Act(BattleState battle, BattleEngine engine)
    {
        var unit = battle.ActiveUnit ?? throw new InvalidOperationException("No unit is active.");
        var enemies = battle.LivingUnitsOf(unit.Side.Opponent()).ToList();

        // The siege engine breaks the fortifications first (ADR-106); with nothing left standing it shoots troops if Ballistics lets it.
        if (SiegeRules.IsSiegeEngine(battle, unit))
        {
            if (battle.Siege!.AutoTarget() is { } structure && engine.ShootStructure(structure.Id) is null)
            {
                return;
            }

            if (SiegeRules.FiresByItself(battle, unit))
            {
                engine.Defend();
                return;
            }
        }

        // A Stronghold hero's warcries are free: use the strongest one it still has (once a round, ADR-067).
        foreach (var warcry in new[] { Warcry.Terror, Warcry.Intimidate, Warcry.Rally })
        {
            if (engine.UseWarcry(unit.Side, warcry) is null)
            {
                break;
            }
        }

        // A hero with a spell book casts the damage spell worth most, once a round (ADR-110); the unit still acts, unless the spell ended it.
        Cast(battle, engine, unit.Side);
        if (battle.ActiveUnit != unit)
        {
            return;
        }

        // A creature caster casts when its spell is worth more than its own blow or shot this turn (ADR-120).
        if (CreatureCast(battle, engine, unit, enemies))
        {
            return;
        }
        if (BattleGrid.CanShootNow(battle, unit))
        {
            var shot = enemies
                .Select(e => (Target: e, CanShoot: BattleGrid.CanShoot(battle, unit, e, out var penalty), Penalty: penalty))
                .Where(s => s.CanShoot)
                .OrderByDescending(s => Score(battle, unit, s.Target, isRanged: true, s.Penalty))
                .ThenBy(s => s.Target.Id)
                .FirstOrDefault();
            if (shot.Target is not null && engine.Shoot(shot.Target.Id) is null)
            {
                return;
            }
        }

        var melee = enemies
            .Select(e => (Target: e, Positions: BattleGrid.AttackPositions(battle, unit, e)))
            .Where(m => m.Positions.Count > 0)
            .OrderByDescending(m => Score(battle, unit, m.Target, isRanged: false))
            .ThenBy(m => m.Target.Id)
            .FirstOrDefault();
        if (melee.Target is not null && engine.Attack(melee.Target.Id, melee.Positions[0]) is null)
        {
            return;
        }

        if (BreakGate(battle, engine, unit, enemies))
        {
            return;
        }

        // In a siege the defenders hold the walls: they fight what reaches them but do not go out (ADR-106).
        var holds = battle.Siege is not null && unit.Side == BattleSide.Defender;
        if (enemies.Count > 0 && !holds && Advance(battle, unit, enemies) is { } destination && engine.Move(destination) is null)
        {
            return;
        }

        engine.Defend();
    }

    /// <summary>
    /// A walking attacker in a siege with no way in to a stack it could fight (ADR-120): it hits the standing gate when it can reach it, else it
    /// walks to the gate's bridge (the moat leaves the gate's rows open).
    /// </summary>
    private static bool BreakGate(BattleState battle, BattleEngine engine, BattleUnit unit, List<BattleUnit> enemies)
    {
        if (battle.Siege is not { } siege || unit.Creature.MovesFreely || SiegeRules.StructureAttackRefusal(battle, unit, siege.Gate.Id) is not null)
        {
            return false;
        }

        var reachable = enemies.Where(e => !e.InKeep && !e.IsMachine).ToList();
        if (reachable.Count == 0 || BattleGrid.ApproachSteps(battle, unit, reachable).ContainsKey(unit.Position))
        {
            return false; // there is a way in already: walk it
        }

        var gate = siege.Gate;
        if (BattleGrid.StructureAttackPositions(battle, unit, gate) is { Count: > 0 } spots)
        {
            return engine.AttackStructure(gate.Id, spots[0]) is null;
        }

        var fearsMoat = SiegeRules.FearsMoat(battle, unit.Side);
        var closer = BattleGrid.Reachable(battle, unit)
            .Where(r => !fearsMoat || !siege.InMoat(unit.RectAt(r.Key)))
            .Select(r => (Position: r.Key, Length: r.Value, Gap: unit.RectAt(r.Key).GapTo(gate.Rect)))
            .OrderBy(r => r.Gap).ThenBy(r => r.Length).ThenBy(r => r.Position.Y).ThenBy(r => r.Position.X)
            .First();
        return closer.Gap < unit.Rect.GapTo(gate.Rect) && engine.Move(closer.Position) is null;
    }

    private static long Score(BattleState battle, BattleUnit unit, BattleUnit target, bool isRanged, int penalty = 0)
    {
        var damage = Math.Min(DamageCalculator.Expected(battle, unit, target, isRanged, penalty), target.TotalHealth);
        return (long)damage * target.Creature.Value / target.Creature.Health;
    }

    /// <summary>
    /// The damage spell and aim worth most: the creature value of the hit points it takes off enemy stacks, less that of own stacks it hits.
    /// Nothing is cast when no aim is worth anything.
    /// </summary>
    private static void Cast(BattleState battle, BattleEngine engine, BattleSide side)
    {
        if (battle.Participant(side).Caster is not { } caster || battle.HasCastThisRound(side))
        {
            return;
        }

        (long Value, string? Spell, int? Target, CellPosition? Cell) best = (0, null, null, null);
        foreach (var known in caster.Spells.Where(s => s.Effect is { Kind: Magic.SpellEffectKind.Damage or Magic.SpellEffectKind.Summon }).OrderBy(s => s.Spell.Id, StringComparer.Ordinal))
        {
            if (battle.ManaOf(side) < BattleMagic.Cost(battle, side, known))
            {
                continue;
            }

            foreach (var (target, cell) in Aims(battle, side, known.Effect!))
            {
                if (BattleMagic.CastRefusal(battle, side, known.Spell.Id, target, cell) is not null)
                {
                    continue;
                }

                var value = Value(battle, side, known, target, cell, null);
                if (value > best.Value)
                {
                    best = (value, known.Spell.Id, target, cell);
                }
            }
        }

        if (best.Spell is { } spell)
        {
            engine.Cast(side, spell, best.Target, best.Cell);
        }
    }

    /// <summary>
    /// What a cast is worth (ADR-120): for damage, the creature value of the hit points it takes off enemy stacks less that of own stacks; for a
    /// summon, the value of the creatures it calls; for healing, the value of the health it gives back; buffs and curses are not weighed (0).
    /// </summary>
    private static long Value(BattleState battle, BattleSide side, KnownSpell known, int? target, CellPosition? cell, int? creaturePower)
    {
        var effect = known.Effect!;
        switch (effect.Kind)
        {
            case Magic.SpellEffectKind.Summon:
                return BattleMagic.SummonPlacement(battle, side, known, creaturePower) is { } summon ? (long)summon.Count * summon.Creature.Value : 0;
            case Magic.SpellEffectKind.Heal:
                return target is { } id && battle.FindUnit(id) is { } wounded
                    ? Worth(wounded, Math.Min(BattleMagic.Amount(battle, side, known, creaturePower), wounded.Creature.Health - wounded.TopHealth))
                    : 0;
            case Magic.SpellEffectKind.Damage:
                return BattleMagic.Reach(battle, side, known, target, cell)
                    .Where(u => BattleMagic.Affects(battle, side, known, u))
                    .Sum(u => (u.Side == side ? -1 : 1) * Worth(u, BattleMagic.Damage(battle, side, known, u, creaturePower)));
            default:
                return 0;
        }
    }

    /// <summary>
    /// A creature caster's turn (ADR-120): it casts the aim worth most when that beats its own best blow or shot now; a buff goes on the most
    /// valuable own stack it works on when the caster has nothing to hit this turn.
    /// </summary>
    private static bool CreatureCast(BattleState battle, BattleEngine engine, BattleUnit unit, List<BattleUnit> enemies)
    {
        if (BattleMagic.CreatureSpell(unit) is not { Effect: { } effect } known)
        {
            return false;
        }

        var power = BattleMagic.CreatureSpellPower(unit);
        var own = OwnBest(battle, unit, enemies);
        IEnumerable<(int? Target, CellPosition? Cell)> aims = effect.Kind switch
        {
            Magic.SpellEffectKind.Heal or Magic.SpellEffectKind.Buff => battle.LivingArmyOf(unit.Side).OrderBy(u => u.Id).Select(u => ((int?)u.Id, (CellPosition?)null)),
            _ => Aims(battle, unit.Side, effect),
        };
        (long Value, int? Target, CellPosition? Cell, bool Found) best = (0, null, null, false);
        foreach (var (target, cell) in aims)
        {
            if (BattleMagic.CreatureCastRefusal(battle, unit, target, cell) is not null)
            {
                continue;
            }

            var value = effect.Kind == Magic.SpellEffectKind.Buff
                ? (own > 0 || battle.FindUnit(target!.Value)!.Spells.Any(s => s.Spell == known.Spell.Id) ? 0 : (long)battle.FindUnit(target!.Value)!.Count * battle.FindUnit(target.Value)!.Creature.Value)
                : Value(battle, unit.Side, known, target, cell, power);
            if (value > best.Value)
            {
                best = (value, target, cell, true);
            }
        }

        return best.Found && (effect.Kind == Magic.SpellEffectKind.Buff || best.Value > own) && engine.CreatureCast(best.Target, best.Cell) is null;
    }

    /// <summary>The unit's own best shot or blow this turn, as the value of the hit points it would take off; 0 when it cannot hit anything.</summary>
    private static long OwnBest(BattleState battle, BattleUnit unit, List<BattleUnit> enemies)
    {
        var shots = BattleGrid.CanShootNow(battle, unit)
            ? enemies.Where(e => BattleGrid.CanShoot(battle, unit, e, out _)).Select(e => Score(battle, unit, e, isRanged: true, BattleGrid.RangedPenaltyEighths(battle, unit, e)))
            : [];
        var blows = enemies.Where(e => BattleGrid.AttackPositions(battle, unit, e).Count > 0).Select(e => Score(battle, unit, e, isRanged: false));
        return shots.Concat(blows).DefaultIfEmpty(0).Max();
    }

    /// <summary>Where a spell may be aimed: each enemy stack, the centre of each enemy stack for an area, or nowhere for a side or the field.</summary>
    private static IEnumerable<(int? Target, CellPosition? Cell)> Aims(BattleState battle, BattleSide side, Magic.SpellEffect effect) => effect.Reach switch
    {
        Magic.SpellReach.Stack => battle.LivingUnitsOf(side.Opponent()).OrderBy(u => u.Id).Select(u => ((int?)u.Id, (CellPosition?)null)),
        Magic.SpellReach.Area => battle.LivingUnitsOf(side.Opponent()).OrderBy(u => u.Id)
            .Select(u => ((int?)null, (CellPosition?)new CellPosition(u.Position.X + (u.Width / 2), u.Position.Y + (u.Height / 2)))),
        _ => [(null, null)],
    };

    private static long Worth(BattleUnit unit, int damage) => (long)Math.Min(damage, unit.TotalHealth) * unit.Creature.Value / unit.Creature.Health;
    /// <summary>The reachable placement closest to the nearest enemy, if it is closer than standing still.</summary>
    private static CellPosition? Advance(BattleState battle, BattleUnit unit, List<BattleUnit> enemies)
    {
        if (battle.Siege is not null && !unit.Creature.MovesFreely)
        {
            return SiegeAdvance(battle, unit, enemies);
        }

        var nearest = enemies.OrderBy(e => unit.Rect.GapTo(e.Rect)).ThenBy(e => e.Id).First();
        var best = BattleGrid.Reachable(battle, unit)
            .Select(r => (Position: r.Key, Steps: r.Value, Gap: unit.RectAt(r.Key).GapTo(nearest.Rect)))
            .OrderBy(r => r.Gap).ThenBy(r => r.Steps).ThenBy(r => r.Position.Y).ThenBy(r => r.Position.X)
            .First();
        return best.Gap < unit.Rect.GapTo(nearest.Rect) ? best.Position : null;
    }

    /// <summary>
    /// A walking attacker in a siege (ADR-106): along the way round the walls to the nearest enemy (through a breach or the broken gate); while
    /// the walls hold, it waits where it is rather than walk into the moat.
    /// </summary>
    private static CellPosition? SiegeAdvance(BattleState battle, BattleUnit unit, List<BattleUnit> enemies)
    {
        // The keep's stack cannot be reached in melee (ADR-120): walk for the others while there are any.
        var reachable = enemies.Where(e => !e.InKeep || enemies.All(x => x.InKeep)).ToList();
        var steps = BattleGrid.ApproachSteps(battle, unit, reachable.Where(e => !e.IsMachine || reachable.All(x => x.IsMachine)));
        if (!steps.TryGetValue(unit.Position, out var now))
        {
            // No way in yet: hit the gate when it can be reached (ADR-120), else close up to it over the bridge.
            if (SiegeRules.StructureAttackRefusal(battle, unit, battle.Siege!.Gate.Id) is null)
            {
                return null; // Act hits the gate before it would advance
            }

            // No way in yet: close up to the moat, not into it.
            var nearest = enemies.OrderBy(e => unit.Rect.GapTo(e.Rect)).ThenBy(e => e.Id).First();
            var fearsMoat = SiegeRules.FearsMoat(battle, unit.Side);
            var closer = BattleGrid.Reachable(battle, unit)
                .Where(r => !fearsMoat || !battle.Siege!.InMoat(unit.RectAt(r.Key)))
                .Select(r => (Position: r.Key, Length: r.Value, Gap: unit.RectAt(r.Key).GapTo(nearest.Rect)))
                .OrderBy(r => r.Gap).ThenBy(r => r.Length).ThenBy(r => r.Position.Y).ThenBy(r => r.Position.X)
                .Select(r => ((CellPosition Position, int Gap)?)(r.Position, r.Gap))
                .FirstOrDefault();
            return closer is { } c && c.Gap < unit.Rect.GapTo(nearest.Rect) ? c.Position : null;
        }

        var best = BattleGrid.Reachable(battle, unit)
            .Where(r => steps.ContainsKey(r.Key))
            .Select(r => (Position: r.Key, Length: r.Value, Steps: steps[r.Key]))
            .OrderBy(r => r.Steps).ThenBy(r => r.Length).ThenBy(r => r.Position.Y).ThenBy(r => r.Position.X)
            .First();
        return best.Steps < now ? best.Position : null;
    }
}
