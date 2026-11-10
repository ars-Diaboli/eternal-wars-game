using EternalWars.Core.Domain.Battles.Spells;

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
    /// A confused stack's turn (ADR-123), for any side: one of the enemy stacks it can shoot or reach, or one move to a placement it can reach,
    /// drawn at random from the battle's generator; it defends when it can do none of them. Its hero does not cast during this turn.
    /// </summary>
    public static void ActConfused(BattleState battle, BattleEngine engine)
    {
        var unit = battle.ActiveUnit ?? throw new InvalidOperationException("No unit is active.");
        var enemies = battle.LivingUnitsOf(unit.Side.Opponent()).OrderBy(u => u.Id).ToList();
        var options = new List<Func<string?>>();
        if (BattleGrid.CanShootNow(battle, unit))
        {
            options.AddRange(enemies.Where(e => BattleGrid.CanShoot(battle, unit, e, out _)).Select(e => (Func<string?>)(() => engine.Shoot(e.Id))));
        }

        foreach (var enemy in enemies)
        {
            if (BattleGrid.AttackPositions(battle, unit, enemy) is { Count: > 0 } spots)
            {
                options.Add(() => engine.Attack(enemy.Id, spots[battle.Random.Next(spots.Count)]));
            }
        }

        var moves = BattleGrid.Reachable(battle, unit).Keys.Where(p => p != unit.Position).OrderBy(p => p.Y).ThenBy(p => p.X).ToList();
        if (moves.Count > 0)
        {
            options.Add(() => engine.Move(moves[battle.Random.Next(moves.Count)]));
        }

        if (options.Count > 0 && options[battle.Random.Next(options.Count)]() is null)
        {
            return;
        }

        engine.Defend();
    }

    /// <summary>
    /// A frenzied stack's turn (ADR-150), for any side: it goes for the nearest other stack, friend or foe (by the gap between the footprints, then
    /// by id), that is not in Stasis or unseen by it. It shoots that stack when it can, else attacks it from the nearest placement, else walks to
    /// the reachable placement closest to it; it defends when it can do none of them. Its hero does not cast during this turn.
    /// </summary>
    public static void ActFrenzied(BattleState battle, BattleEngine engine)
    {
        var unit = battle.ActiveUnit ?? throw new InvalidOperationException("No unit is active.");
        var nearest = battle.LivingUnits
            .Where(u => u != unit && BattleGrid.CanBeAttacked(unit, u, melee: false))
            .OrderBy(u => unit.Rect.GapTo(u.Rect)).ThenBy(u => u.Id)
            .FirstOrDefault();
        if (nearest is not null)
        {
            if (BattleGrid.CanShoot(battle, unit, nearest, out _) && engine.Shoot(nearest.Id) is null)
            {
                return;
            }

            if (BattleGrid.AttackPositions(battle, unit, nearest) is { Count: > 0 } spots && engine.Attack(nearest.Id, spots[0]) is null)
            {
                return;
            }

            var gap = unit.Rect.GapTo(nearest.Rect);
            var closer = BattleGrid.Reachable(battle, unit)
                .Select(r => (Position: r.Key, Length: r.Value, Gap: unit.RectAt(r.Key).GapTo(nearest.Rect)))
                .Where(r => r.Gap < gap)
                .OrderBy(r => r.Gap).ThenBy(r => r.Length).ThenBy(r => r.Position.Y).ThenBy(r => r.Position.X)
                .Select(r => (CellPosition?)r.Position)
                .FirstOrDefault();
            if (closer is { } to && engine.Move(to) is null)
            {
                return;
            }
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

    /// <summary>The creature value of the hit points the unit's blow or shot is expected to take off the target.</summary>
    internal static long Score(BattleState battle, BattleUnit unit, BattleUnit target, bool isRanged, int penalty = 0)
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

        (long Value, string? Spell, int? Target, CellPosition? Cell, int? Second) best = (0, null, null, null, null);
        foreach (var known in caster.Spells.Where(s => s.Effect is not null).OrderBy(s => s.Spell.Id, StringComparer.Ordinal))
        {
            if (battle.ManaOf(side) < BattleMagic.Cost(battle, side, known))
            {
                continue;
            }

            var effect = known.Effect!;
            var handler = SpellEffectHandlers.For(effect.Kind);
            foreach (var (target, cell) in handler.Aims(battle, side, effect))
            {
                var drinker = effect.DrainPercent > 0 ? Drinker(battle, side, known, target)
                    : effect.Modifiers.MartyrPercent > 0 ? Martyr(battle, side, target)
                    : null;
                if (BattleMagic.CastRefusal(battle, side, known.Spell.Id, target, cell, drinker?.Id) is not null)
                {
                    continue;
                }

                var value = handler.Value(new CastContext(battle, side, known, target, cell, drinker?.Id)) + (drinker?.Worth ?? 0);
                if (value > best.Value)
                {
                    best = (value, known.Spell.Id, target, cell, drinker?.Id);
                }
            }
        }

        if (best.Spell is { } spell)
        {
            engine.Cast(side, spell, best.Target, best.Cell, best.Second);
        }
    }

    /// <summary>The own stack a drain (ADR-151) does most for, with what that is worth (the creature value of the health it gives back); null when none gains.</summary>
    private static (int Id, long Worth)? Drinker(BattleState battle, BattleSide side, KnownSpell known, int? target)
    {
        if (target is null)
        {
            return null;
        }

        var effect = known.Effect!;
        var dealt = BattleMagic.DamagePlan(battle, side, known, target, null).Where(h => h.Unit.Side != side).Sum(h => (long)Math.Min(h.Damage, h.Unit.TotalHealth));
        var health = BattleMagic.Drained(effect, dealt);
        return battle.LivingArmyOf(side).Where(u => !u.Has(Magic.SpellStatus.Stasis))
            .Select(u => (u.Id, Worth: BattleMagic.DrainHeals(effect, u, health) * u.Creature.Value / u.Creature.Health))
            .Where(d => d.Worth > 0)
            .OrderByDescending(d => d.Worth).ThenBy(d => d.Id)
            .Select(d => ((int Id, long Worth)?)d)
            .FirstOrDefault();
    }

    /// <summary>
    /// The own stack that takes the blows for the aimed one under Martyrdom (ADR-152, proposal): the other own creature stack with the most health,
    /// not a copy; it adds nothing to the spell's worth.
    /// </summary>
    private static (int Id, long Worth)? Martyr(BattleState battle, BattleSide side, int? target) =>
        battle.LivingArmyOf(side).Where(u => u.Id != target && !u.IsCopy && !u.Has(Magic.SpellStatus.Stasis))
            .OrderByDescending(u => u.TotalHealth).ThenBy(u => u.Id)
            .Select(u => ((int Id, long Worth)?)(u.Id, 0L))
            .FirstOrDefault();

    /// <summary>What a buff or curse is worth a round, in percent of the stack's creature value (ADR-123, proposal).</summary>
    public const int EnchantPercentPerRound = 4;

    /// <summary>The same for a spell that takes a stack's turns away or makes it act at random (Blind, Stasis, Confusion, Frenzy; proposal).</summary>
    public const int ControlPercentPerRound = 10;

    /// <summary>The same for a spell that makes the stack fight for the caster (Hypnotize, Puppet Master; ADR-150, proposal): its turns lost and won.</summary>
    public const int TakenPercentPerRound = 20;

    /// <summary>The most rounds a lasting spell is counted for (a spell for the battle counts this many).</summary>
    public const int MaxValuedRounds = 5;

    /// <summary>A stack's creature value.</summary>
    internal static long StackValue(BattleUnit unit) => (long)unit.Count * unit.Creature.Value;

    /// <summary>The creature value of the hit points (up to the stack's health) a blow or spell takes off or gives back.</summary>
    internal static long Worth(BattleUnit unit, int damage) => (long)Math.Min(damage, unit.TotalHealth) * unit.Creature.Value / unit.Creature.Health;

    /// <summary>
    /// A creature caster's turn (ADR-120): it casts the aim worth most when that beats its own best blow or shot now; a buff goes on the most
    /// valuable own stack it works on when the caster has nothing to hit this turn.
    /// </summary>
    private static bool CreatureCast(BattleState battle, BattleEngine engine, BattleUnit unit, List<BattleUnit> enemies)
    {
        if (BattleMagic.CreatureSpell(battle, unit) is not { Effect: { } effect } known)
        {
            return false;
        }

        var handler = SpellEffectHandlers.For(effect.Kind);
        var power = BattleMagic.CreatureSpellPower(unit);
        var own = OwnBest(battle, unit, enemies);
        (long Value, int? Target, CellPosition? Cell, bool Found) best = (0, null, null, false);
        foreach (var (target, cell) in Aims(battle, unit.Side, effect))
        {
            if (BattleMagic.CreatureCastRefusal(battle, unit, target, cell) is not null)
            {
                continue;
            }

            // A buff is cast only when the caster has nothing to hit itself (ADR-120).
            var value = handler.CreatureCastsWhenIdle && own > 0 ? 0 : handler.Value(new CastContext(battle, unit.Side, known, target, cell, CreaturePower: power));
            if (value > best.Value)
            {
                best = (value, target, cell, true);
            }
        }

        return best.Found && (handler.CreatureCastsWhenIdle || best.Value > own) && engine.CreatureCast(best.Target, best.Cell) is null;
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

    /// <summary>
    /// Where a spell may be aimed: each enemy stack for a hostile stack spell, each own stack for a friendly one (and each own stack destroyed in the
    /// battle for a revive that brings them back), every stack for one aimed at either side; the centre of each enemy stack for an area; nowhere for
    /// a side or the field.
    /// </summary>
    internal static IEnumerable<(int? Target, CellPosition? Cell)> Aims(BattleState battle, BattleSide side, Magic.SpellEffect effect) => effect.Reach switch
    {
        Magic.SpellReach.Stack => battle.Units
            .Where(u => effect.EitherSide || (u.Side != side) == effect.IsHostile)
            .Where(u => u.IsAlive || BattleMagic.IsFallen(battle, side, effect, u))
            .OrderBy(u => u.Id).Select(u => ((int?)u.Id, (CellPosition?)null)),
        Magic.SpellReach.Area => battle.LivingUnitsOf(side.Opponent()).OrderBy(u => u.Id)
            .Select(u => ((int?)null, (CellPosition?)new CellPosition(u.Position.X + (u.Width / 2), u.Position.Y + (u.Height / 2)))),
        _ => [(null, null)],
    };

    /// <summary>The reachable placement closest to the nearest enemy, if it is closer than standing still.</summary>
    private static CellPosition? Advance(BattleState battle, BattleUnit unit, List<BattleUnit> enemies)
    {
        if (battle.Siege is not null && !unit.Creature.MovesFreely)
        {
            return SiegeAdvance(battle, unit, enemies);
        }

        // A walker on a field with a dividing cliff (ADR-146) goes the way round, through a gap, rather than up to the cliff's face.
        if (!unit.MovesFreely && battle.Obstacles.Any(o => o.Class == ObstacleClass.Cliff)
            && BattleGrid.ApproachSteps(battle, unit, enemies) is var steps && steps.TryGetValue(unit.Position, out var now))
        {
            var way = BattleGrid.Reachable(battle, unit)
                .Where(r => steps.ContainsKey(r.Key))
                .Select(r => (Position: r.Key, Length: r.Value, Steps: steps[r.Key]))
                .OrderBy(r => r.Steps).ThenBy(r => r.Length).ThenBy(r => r.Position.Y).ThenBy(r => r.Position.X)
                .First();
            return way.Steps < now ? way.Position : null;
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
