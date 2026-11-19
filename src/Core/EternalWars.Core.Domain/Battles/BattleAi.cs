using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Battles.Spells;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Battles;

/// <summary>
/// The battle AI's steps (ADR-188): each level adds one, so <c>sim ai</c> can measure a step against the one before it. The game always
/// plays <see cref="Latest"/>.
/// </summary>
public enum BattleAiLevel
{
    /// <summary>Before ADR-188: each unit hits the target whose lost hit points are worth most (Fight Value of the health it takes off).</summary>
    Greedy,

    /// <summary>ADR-188 (a): focus fire on the biggest threat (Fight Value × count) and finish off stacks it can destroy this turn.</summary>
    FocusFire,

    /// <summary>
    /// ADR-188 (d): a blow or shot counts the retaliation it draws against what it takes off, and a stack waits for an own stack still to act
    /// that hits its target without being hit back.
    /// </summary>
    AvoidRetaliation,

    /// <summary>ADR-188 (b): melee stacks go for the enemy shooters (a blow at one that can shoot counts its shots stopped).</summary>
    ProtectShooters,

    /// <summary>
    /// ADR-188 (c): a stack with nothing to hit waits for the enemy to come closer, and while its side shoots better it holds its ground
    /// beside its shooters instead of walking out.
    /// </summary>
    HoldBack,

    /// <summary>
    /// ADR-188 (e): on its side's first turn of a round the hero tries its best spells, and casting nothing yet, on copies of the battle played
    /// to the end of the next round, and casts what leaves its side best off.
    /// </summary>
    CastAhead,

    /// <summary>
    /// ADR-188 (f): in a siege the attackers go through the gate or a breach together, a siege engine the hero aims breaks the gate first, and the
    /// defenders' walking stacks block an open way in while their shooters fire from behind the walls.
    /// </summary>
    Sieges,

    /// <summary>The level the game plays.</summary>
    Latest = Sieges,
}

/// <summary>
/// Simple, deterministic battle AI for neutrals, computer players and quick combat. Per turn: shoot the best target, else melee the best
/// reachable target, else close in on the nearest enemy, else defend. The best target is the one whose lost hit points are worth most
/// (<see cref="BattleAiLevel.Greedy"/>), weighted by its threat, with a bonus for destroying it (<see cref="BattleAiLevel.FocusFire"/>, ADR-188).
/// </summary>
internal static class BattleAi
{
    /// <summary>A shooter that can shoot now, or a caster with casts left, counts this much more as a threat, in percent (ADR-187, proposal).</summary>
    public const int ActiveThreatPercent = 125;

    public static void Act(BattleState battle, BattleEngine engine, BattleAiLevel level = BattleAiLevel.Latest) =>
        Act(battle, engine, level, mayCast: true, mayLookAhead: true);

    /// <summary>
    /// The active unit's turn. Its hero casts first only with <paramref name="mayCast"/>, and weighs its spells a round ahead only with
    /// <paramref name="mayLookAhead"/> (not inside a look-ahead's own copy).
    /// </summary>
    private static void Act(BattleState battle, BattleEngine engine, BattleAiLevel level, bool mayCast, bool mayLookAhead)
    {
        var unit = battle.ActiveUnit ?? throw new InvalidOperationException("No unit is active.");
        var enemies = battle.LivingUnitsOf(unit.Side.Opponent()).ToList();

        // The siege engine breaks the fortifications first (ADR-106); with nothing left standing it shoots troops if Ballistics lets it.
        if (SiegeRules.IsSiegeEngine(battle, unit))
        {
            var aimed = level >= BattleAiLevel.Sieges && !SiegeRules.FiresByItself(battle, unit) ? AimedStructure(battle.Siege!) : battle.Siege!.AutoTarget();
            if (aimed is { } structure && engine.ShootStructure(structure.Id) is null)
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
        if (mayCast)
        {
            Cast(battle, engine, unit, level, mayLookAhead);
            if (battle.ActiveUnit != unit)
            {
                return;
            }
        }

        // A creature caster casts when its spell is worth more than its own blow or shot this turn (ADR-120).
        if (CreatureCast(battle, engine, unit, enemies))
        {
            return;
        }
        var biggestThreat = level >= BattleAiLevel.FocusFire ? enemies.Select(e => Threat(battle, e)).DefaultIfEmpty(0).Max() : 0;
        if (BattleGrid.CanShootNow(battle, unit))
        {
            var shot = enemies
                .Select(e => (Target: e, CanShoot: BattleGrid.CanShoot(battle, unit, e, out var penalty), Penalty: penalty))
                .Where(s => s.CanShoot)
                .OrderByDescending(s => TargetScore(battle, unit, s.Target, isRanged: true, s.Penalty, level, biggestThreat))
                .ThenBy(s => s.Target.Id)
                .FirstOrDefault();
            if (shot.Target is not null && engine.Shoot(shot.Target.Id) is null)
            {
                return;
            }
        }

        var reach = BattleGrid.Reachable(battle, unit);
        var melee = enemies
            .Select(e => (Target: e, Positions: BattleGrid.AttackPositions(reach, unit, e)))
            .Where(m => m.Positions.Count > 0)
            .OrderByDescending(m => TargetScore(battle, unit, m.Target, isRanged: false, 0, level, biggestThreat))
            .ThenBy(m => m.Target.Id)
            .FirstOrDefault();
        if (melee.Target is not null)
        {
            // ADR-188 (d): an own stack still to act that hits the target without being hit back goes first.
            if (level >= BattleAiLevel.AvoidRetaliation && WaitsForAFreeHit(battle, unit, melee.Target) && engine.Wait() is null)
            {
                return;
            }

            if (engine.Attack(melee.Target.Id, BestStand(battle, unit, melee.Positions, level)) is null)
            {
                return;
            }
        }

        // ADR-188 (f): a besieger does not go to the way in ahead of its side's other walking stacks.
        if (level >= BattleAiLevel.Sieges && Gathers(battle, engine, unit))
        {
            return;
        }

        if (BreakGate(battle, engine, unit, enemies))
        {
            return;
        }

        // ADR-188 (c): nothing to hit yet: let the enemy come first, once a round.
        if (level >= BattleAiLevel.HoldBack && WaitsForTheEnemy(battle, unit) && engine.Wait() is null)
        {
            return;
        }

        // In a siege the defenders hold the walls: they fight what reaches them but do not go out (ADR-106). ADR-188 (c): a side that shoots
        // better keeps its melee stacks with its shooters.
        // ADR-188 (f): a defender's walking stack blocks an open way in instead.
        if (level >= BattleAiLevel.Sieges && BlocksWayIn(battle, engine, unit))
        {
            return;
        }

        var holds = (battle.Siege is not null && unit.Side == BattleSide.Defender) || (level >= BattleAiLevel.HoldBack && HoldsGround(battle, unit, enemies));
        if (enemies.Count > 0 && !holds && Advance(battle, unit, enemies, level) is { } destination && engine.Move(destination) is null)
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

        var reach = BattleGrid.Reachable(battle, unit);
        foreach (var enemy in enemies)
        {
            if (BattleGrid.AttackPositions(reach, unit, enemy) is { Count: > 0 } spots)
            {
                options.Add(() => engine.Attack(enemy.Id, spots[battle.Random.Next(spots.Count)]));
            }
        }

        var moves = reach.Keys.Where(p => p != unit.Position).OrderBy(p => p.Y).ThenBy(p => p.X).ToList();
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
        return (long)damage * target.Creature.FightValue / target.Creature.Health;
    }

    /// <summary>
    /// How much a stack matters (ADR-188): its Fight Value × its count, raised by <see cref="ActiveThreatPercent"/> while it can shoot unhindered
    /// or still has casts (ADR-187).
    /// </summary>
    internal static long Threat(BattleState battle, BattleUnit unit)
    {
        var threat = StackValue(unit);
        return BattleGrid.CanShootNow(battle, unit) || BattleMagic.CreatureSpell(battle, unit) is not null ? threat * ActiveThreatPercent / 100 : threat;
    }

    /// <summary>
    /// What a shot or blow at <paramref name="target"/> is worth to the AI. <see cref="BattleAiLevel.Greedy"/>: <see cref="Score"/>.
    /// <see cref="BattleAiLevel.FocusFire"/> (ADR-188 a): that worth counts up to twice, by the target's threat against the biggest one, so the
    /// stacks focus on the biggest threat; a blow expected to destroy the stack adds its whole threat, so stacks it can kill are finished off.
    /// </summary>
    internal static long TargetScore(BattleState battle, BattleUnit unit, BattleUnit target, bool isRanged, int penalty, BattleAiLevel level, long biggestThreat)
    {
        if (level < BattleAiLevel.FocusFire)
        {
            return Score(battle, unit, target, isRanged, penalty);
        }

        var expected = DamageCalculator.Expected(battle, unit, target, isRanged, penalty);
        var worth = Worth(target, expected);
        var threat = Threat(battle, target);
        var focus = worth + (worth * threat / Math.Max(1, biggestThreat));
        var score = expected > 0 && expected >= target.TotalHealth ? focus + threat : focus;
        if (level >= BattleAiLevel.AvoidRetaliation)
        {
            score -= RetaliationLoss(battle, unit, target, isRanged, expected);
        }

        if (level >= BattleAiLevel.ProtectShooters && !isRanged && BattleGrid.CanShootNow(battle, target) && expected < target.TotalHealth)
        {
            score += threat * BlockPercent / 100; // a shooter it stands next to cannot shoot
        }

        return score;
    }

    /// <summary>A blow at an enemy shooter that can shoot counts this much of its threat more, in percent: its shots stopped (ADR-188 b, proposal).</summary>
    public const int BlockPercent = 25;

    /// <summary>
    /// The creature value the unit is expected to lose to the target's retaliation (ADR-188 d): none when it cannot hit back (it has hit back this
    /// round, cannot, or is destroyed), else the blow of the creatures left after <paramref name="expected"/> damage (all of them before a first strike).
    /// </summary>
    internal static long RetaliationLoss(BattleState battle, BattleUnit unit, BattleUnit target, bool isRanged, int expected)
    {
        var penalty = 0;
        var hitsBack = !target.IsMachine && !unit.Creature.Has(CapabilityKind.NoEnemyRetaliation)
            && !target.Has(Magic.SpellStatus.NoRetaliation | Magic.SpellStatus.Stasis | Magic.SpellStatus.Blind | Magic.SpellStatus.NoAttack)
            && (isRanged
                ? target.Creature.Has(CapabilityKind.RangedRetaliation) && (!target.HasRetaliated || target.Creature.Has(CapabilityKind.UnlimitedRetaliation))
                  && BattleGrid.CanShoot(battle, target, unit, out penalty)
                : !target.HasRetaliated || target.Creature.Has(CapabilityKind.UnlimitedRetaliation) || target.Has(Magic.SpellStatus.Vigil));
        if (!hitsBack)
        {
            return 0;
        }

        var first = !isRanged && target.Creature.Has(CapabilityKind.FirstStrike) && !unit.Creature.Has(CapabilityKind.FirstStrike);
        var left = first ? target.TotalHealth : target.TotalHealth - expected;
        if (left <= 0)
        {
            return 0;
        }

        var survivors = (left + target.Creature.Health - 1) / target.Creature.Health;
        var damage = (long)DamageCalculator.Expected(battle, target, unit, isRanged, penalty) * survivors / Math.Max(1, target.Count);
        return Worth(unit, (int)Math.Min(int.MaxValue, damage));
    }

    /// <summary>
    /// Whether the unit waits for an own stack still to act this round that hits <paramref name="target"/> without being hit back (ADR-188 d):
    /// the target would hit back, the unit has not waited this round, and such a stack can shoot it or reach it now.
    /// </summary>
    private static bool WaitsForAFreeHit(BattleState battle, BattleUnit unit, BattleUnit target)
    {
        if (battle.WaitedThisRound.Contains(unit.Id) || RetaliationLoss(battle, unit, target, isRanged: false, 0) == 0)
        {
            return false;
        }

        return battle.Queue.Select(battle.GetUnit).Any(u => u.IsAlive && u.Side == unit.Side && !u.IsMachine
            && ((BattleGrid.CanShootNow(battle, u) && BattleGrid.CanShoot(battle, u, target, out _) && RetaliationLoss(battle, u, target, isRanged: true, 0) == 0)
                || (u.Creature.Has(CapabilityKind.NoEnemyRetaliation) && BattleGrid.AttackPositions(battle, u, target).Count > 0)));
    }

    /// <summary>
    /// Where the unit stands to strike (ADR-188 b): from <see cref="BattleAiLevel.ProtectShooters"/> on, the placement next to the enemy shooters
    /// that can shoot with the most threat (they cannot while it stands there), else the first.
    /// </summary>
    private static CellPosition BestStand(BattleState battle, BattleUnit unit, IReadOnlyList<CellPosition> positions, BattleAiLevel level)
    {
        if (level < BattleAiLevel.ProtectShooters || positions.Count == 1)
        {
            return positions[0];
        }

        var shooters = battle.LivingUnitsOf(unit.Side.Opponent()).Where(e => BattleGrid.CanShootNow(battle, e)).ToList();
        return shooters.Count == 0
            ? positions[0]
            : positions.Select((p, i) => (Position: p, Index: i, Blocked: shooters.Where(e => unit.RectAt(p).IsAdjacentTo(e.Rect)).Sum(e => Threat(battle, e))))
                .OrderByDescending(p => p.Blocked).ThenBy(p => p.Index).First().Position;
    }

    /// <summary>
    /// Whether a unit with nothing to hit waits for the enemy to come (ADR-188 c): it has not waited this round, it is not a shooter, it is not
    /// in a siege, and an enemy stack that can walk is still to act this round.
    /// </summary>
    private static bool WaitsForTheEnemy(BattleState battle, BattleUnit unit) =>
        battle.Siege is null && !battle.WaitedThisRound.Contains(unit.Id) && unit.Creature.Shots == 0
        && battle.Queue.Select(battle.GetUnit).Any(u => u.IsAlive && u.Side != unit.Side && !u.IsMachine && !u.IsFixed);

    /// <summary>
    /// Whether the unit holds its ground (ADR-188 c): outside a siege, a stack that is not a shooter stays while its side's shooters that can
    /// shoot are worth more than the enemy's (the enemy comes to them under fire) and an enemy melee stack can walk to it.
    /// </summary>
    private static bool HoldsGround(BattleState battle, BattleUnit unit, List<BattleUnit> enemies)
    {
        if (battle.Siege is not null || unit.Creature.Shots > 0)
        {
            return false;
        }

        long Shooting(IEnumerable<BattleUnit> side) => side.Where(u => BattleGrid.CanShootNow(battle, u)).Sum(StackValue);
        return Shooting(battle.LivingUnitsOf(unit.Side)) > Shooting(enemies) && enemies.Any(e => !e.IsMachine && !e.IsFixed && e.Creature.Shots == 0);
    }

    /// <summary>
    /// The hero's cast for the turn: the spell and aim worth most (<see cref="CastOptions"/>); nothing is cast when no aim is worth anything. From
    /// <see cref="BattleAiLevel.CastAhead"/> on (ADR-188 e), on its side's first turn of the round, the best aims of the
    /// <see cref="LookAheadSpells"/> best spells and casting nothing are each played out on a copy of the battle (<see cref="Outlook"/>), and
    /// the one that leaves the side best off is chosen (on a tie, the one that looked best); the hero then casts on no other turn of that round.
    /// </summary>
    private static void Cast(BattleState battle, BattleEngine engine, BattleUnit unit, BattleAiLevel level, bool mayLookAhead)
    {
        var side = unit.Side;
        if (battle.Participant(side).Caster is null || battle.HasCastThisRound(side))
        {
            return;
        }

        var options = CastOptions(battle, side);
        if (options.Count == 0)
        {
            return;
        }

        var pick = options[0];
        if (level >= BattleAiLevel.CastAhead && mayLookAhead)
        {
            if (!IsFirstTurnOfRound(battle, unit))
            {
                return; // the round's cast was weighed on the side's first turn
            }

            // The best-looking cast wins a tie with the others and with casting nothing: the look-ahead overrules it only when it sees better.
            var best = long.MinValue;
            foreach (var option in options.DistinctBy(o => o.Spell).Take(LookAheadSpells))
            {
                var outlook = Outlook(battle, unit, option, level);
                if (outlook > best)
                {
                    (best, pick) = (outlook, option);
                }
            }

            if (Outlook(battle, unit, null, level) > best)
            {
                return;
            }
        }

        engine.Cast(side, pick.Spell, pick.Target, pick.Cell, pick.Second);
    }

    /// <summary>A hero's cast the AI weighs: the spell, its aim and second aim, and what it is worth by the spell's own measure.</summary>
    internal readonly record struct CastOption(long Value, Common.SpellId Spell, int? Target, CellPosition? Cell, int? Second);

    /// <summary>
    /// Every cast of the hero's spells it has the mana for, with what it is worth (the creature value of the hit points it takes off enemy stacks,
    /// less that of own stacks it hits; a lasting effect's share), best first; only those worth something.
    /// </summary>
    internal static List<CastOption> CastOptions(BattleState battle, BattleSide side)
    {
        var caster = battle.Participant(side).Caster!;
        var options = new List<CastOption>();
        foreach (var known in caster.Spells.Where(s => s.Effect is not null).OrderBy(s => s.Spell.Id))
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
                if (value > 0)
                {
                    options.Add(new CastOption(value, known.Spell.Id, target, cell, drinker?.Id));
                }
            }
        }

        // A stable sort: of aims worth the same, the first found stays first, as the AI has always picked.
        return [.. options.OrderByDescending(o => o.Value)];
    }

    /// <summary>How many of the hero's spells (each at its best aim) the look-ahead plays out, besides casting nothing (ADR-188 e, proposal).</summary>
    public const int LookAheadSpells = 3;

    /// <summary>A bound on the turns one look-ahead plays (a round has at most a few dozen).</summary>
    private const int LookAheadTurns = 200;

    /// <summary>Mixed into the battle's generator state for the look-ahead's own, so that it neither foresees nor draws this battle's rolls.</summary>
    private const ulong LookAheadSeed = 0x5EED_A11E_AD00_0188;

    /// <summary>
    /// Whether this is the side's first turn of the round (ADR-188 e): the unit has not waited, and every other own stack but a war machine is still
    /// to act. The look-ahead is tried then only, so that it costs a few plays a round.
    /// </summary>
    private static bool IsFirstTurnOfRound(BattleState battle, BattleUnit unit) =>
        !battle.WaitedThisRound.Contains(unit.Id)
        && battle.LivingUnitsOf(unit.Side).All(u => u == unit || u.IsMachine || battle.Queue.Contains(u.Id));

    /// <summary>
    /// What the side has at the end of the next round if the hero casts <paramref name="option"/> now, or nothing (null): a copy of the battle with
    /// a generator of its own, the cast made, then every turn played by this AI for both sides, the side's hero casting nothing more (so each
    /// option is one cast against none) and the enemy's casting as usual, without looking ahead; <see cref="Standing"/> of the copy then.
    /// </summary>
    private static long Outlook(BattleState battle, BattleUnit unit, CastOption? option, BattleAiLevel level)
    {
        var copy = battle.Copy(new GameRandom(battle.Random.State ^ LookAheadSeed));
        var unheard = new EventBus();
        if (option is { } cast && new BattleEngine(copy, unheard).Cast(unit.Side, cast.Spell, cast.Target, cast.Cell, cast.Second) is not null)
        {
            return long.MinValue;
        }

        var until = battle.Round + 1;
        for (var turn = 0; turn < LookAheadTurns && copy.Phase == BattlePhase.Fighting && copy.Round <= until && copy.ActiveUnit is { } active; turn++)
        {
            var engine = new BattleEngine(copy, unheard);
            if (active.Has(Magic.SpellStatus.Frenzied))
            {
                ActFrenzied(copy, engine);
            }
            else if (active.Has(Magic.SpellStatus.Confused))
            {
                ActConfused(copy, engine);
            }
            else
            {
                Act(copy, engine, level, mayCast: active.Side != unit.Side, mayLookAhead: false);
            }
        }

        return Standing(copy, unit.Side);
    }

    /// <summary>
    /// How well <paramref name="side"/> stands (ADR-188 e): the creature value of the health of its stacks less the enemy's (copies left out; a
    /// beaten side's count for nothing, its army is lost), plus what the lasting spells on them are still worth, at the usual share a round for
    /// the rounds or turns they have left, for the side that cast them and against the other.
    /// </summary>
    internal static long Standing(BattleState battle, BattleSide side)
    {
        long standing = 0;
        foreach (var unit in battle.LivingUnits.Where(u => !u.IsCopy && (battle.Winner is not { } winner || u.Side == winner)))
        {
            standing += (unit.Side == side ? 1 : -1) * Worth(unit, unit.TotalHealth);
            foreach (var spell in unit.Spells.Where(s => !s.IsMartyr && !s.Modifiers.IsEmpty))
            {
                var left = Math.Min(MaxValuedRounds, spell.TurnsLeft ?? (spell.UntilRound - battle.Round + 1));
                if (left <= 0)
                {
                    continue;
                }

                var m = spell.Modifiers;
                var percent = m.Has(Magic.SpellStatus.Controlled) ? TakenPercentPerRound
                    : m.Has(Magic.SpellStatus.Blind | Magic.SpellStatus.Stasis | Magic.SpellStatus.Confused | Magic.SpellStatus.Frenzied) ? ControlPercentPerRound
                    : EnchantPercentPerRound;
                standing += (spell.Caster == side ? 1 : -1) * StackValue(unit) * percent * left / 100;
            }
        }

        return standing;
    }

    /// <summary>
    /// Where a siege engine the hero aims fires (ADR-188 f, proposal): the gate while it stands (one hit breaks it and opens the bridged way in,
    /// with no moat), then the keep and the towers (they shoot every round), then the wall sections, those beside the gate first.
    /// </summary>
    internal static SiegeStructure? AimedStructure(SiegeState siege)
    {
        var middle = (2 * SiegeRules.GateY) + SiegeRules.GateHeight;
        return siege.Structures.Where(s => s.IsStanding)
            .OrderBy(s => s.Kind switch { SiegeStructureKind.Gate => 0, SiegeStructureKind.Keep => 1, SiegeStructureKind.Tower => 2, _ => 3 })
            .ThenBy(s => Math.Abs((2 * s.Rect.Y) + s.Rect.Height - middle)).ThenBy(s => s.Id)
            .FirstOrDefault();
    }

    /// <summary>A besieger walks no nearer the way in than this many cells while its side's other walking stacks cannot follow (ADR-188 f, proposal).</summary>
    public const int GatherGap = 3;

    /// <summary>A stack that walks to fight and is not held in place: no war machine, shooter, flyer or stack in the keep.</summary>
    private static bool Walks(BattleUnit unit) => !unit.IsMachine && unit.Creature.Shots == 0 && !unit.MovesFreely && !unit.IsFixed;

    /// <summary>The way in a besieger makes for (ADR-188 f): the nearest open one (a breach, the broken gate), else the standing gate (to break it).</summary>
    internal static CellRect WayIn(SiegeState siege, BattleUnit unit) =>
        siege.Structures.Where(s => s.OnField && (!s.IsStanding || s.Kind == SiegeStructureKind.Gate))
            .OrderBy(s => s.IsStanding ? 1 : 0).ThenBy(s => unit.Rect.GapTo(s.Rect)).ThenBy(s => s.Id)
            .First().Rect;

    /// <summary>
    /// A besieger's walking stack with nothing to hit, still outside the walls, gathers with its side (ADR-188 f): while less than half of the
    /// side's other walking stacks outside the walls (by Fight Value × count) can get within <see cref="GatherGap"/> of the way in on their next
    /// move, it walks only that near (or defends), instead of reaching the way in, or the gate to break it, alone. True when it acted.
    /// </summary>
    private static bool Gathers(BattleState battle, BattleEngine engine, BattleUnit unit)
    {
        if (battle.Siege is not { } siege || unit.Side != BattleSide.Attacker || !Walks(unit) || unit.Rect.Right > SiegeRules.WallX)
        {
            return false;
        }

        var wayIn = WayIn(siege, unit);
        var reach = BattleGrid.Reachable(battle, unit);
        if (reach.Keys.Select(p => unit.RectAt(p).GapTo(wayIn)).DefaultIfEmpty(int.MaxValue).Min() > GatherGap || !AheadOfItsSide(battle, unit, wayIn))
        {
            return false; // it cannot get near this turn anyway, or the others can follow
        }

        var fearsMoat = SiegeRules.FearsMoat(battle, unit.Side);
        var now = unit.Rect.GapTo(wayIn);
        var spot = reach
            .Where(r => unit.RectAt(r.Key).GapTo(wayIn) > GatherGap && (!fearsMoat || !siege.InMoat(unit.RectAt(r.Key))))
            .Select(r => (Position: r.Key, Length: r.Value, Gap: unit.RectAt(r.Key).GapTo(wayIn)))
            .OrderBy(r => r.Gap).ThenBy(r => r.Length).ThenBy(r => r.Position.Y).ThenBy(r => r.Position.X)
            .Select(r => ((CellPosition Position, int Gap)?)(r.Position, r.Gap))
            .FirstOrDefault();
        if (spot is { } to && to.Gap < now && engine.Move(to.Position) is null)
        {
            return true;
        }

        return engine.Defend() is null;
    }

    /// <summary>
    /// Whether less than half of the side's other walking stacks outside the walls that can move (by Fight Value × count) can get within
    /// <see cref="GatherGap"/> of <paramref name="wayIn"/> on their next move (ADR-188 f).
    /// </summary>
    private static bool AheadOfItsSide(BattleState battle, BattleUnit unit, CellRect wayIn)
    {
        long total = 0, near = 0;
        foreach (var other in battle.LivingUnitsOf(unit.Side).Where(u => u != unit && Walks(u) && u.Rect.Right <= SiegeRules.WallX))
        {
            var reach = BattleGrid.Reachable(battle, other);
            if (reach.Count <= 1)
            {
                continue; // it cannot move: nobody waits for it
            }

            total += StackValue(other);
            near += reach.Keys.Min(p => other.RectAt(p).GapTo(wayIn)) <= GatherGap ? StackValue(other) : 0;
        }

        return near * 2 < total;
    }

    /// <summary>
    /// A defender's walking stack with nothing to hit blocks an open way in (ADR-188 f): it walks to the reachable placement inside the walls
    /// (it may stand in the gap itself) nearest the breach or broken gate closest to the attackers' walking stacks, so they come in through it
    /// under the defenders' fire. True when it moved; it stays where it is once it is as near as it gets, and with no way in open.
    /// </summary>
    private static bool BlocksWayIn(BattleState battle, BattleEngine engine, BattleUnit unit)
    {
        if (battle.Siege is not { } siege || unit.Side != BattleSide.Defender || !Walks(unit))
        {
            return false;
        }

        var attackers = battle.LivingUnitsOf(BattleSide.Attacker).Where(Walks).ToList();
        var open = siege.Structures.Where(s => s.OnField && !s.IsStanding).ToList();
        if (attackers.Count == 0 || open.Count == 0)
        {
            return false;
        }

        var gap = open.OrderBy(s => attackers.Min(a => a.Rect.GapTo(s.Rect))).ThenBy(s => s.Id).First().Rect;
        var now = unit.Rect.GapTo(gap);
        var best = BattleGrid.Reachable(battle, unit)
            .Where(r => r.Key.X >= SiegeRules.WallX)
            .Select(r => (Position: r.Key, Length: r.Value, Gap: unit.RectAt(r.Key).GapTo(gap)))
            .OrderBy(r => r.Gap).ThenBy(r => r.Length).ThenBy(r => r.Position.Y).ThenBy(r => r.Position.X)
            .Select(r => ((CellPosition Position, int Gap)?)(r.Position, r.Gap))
            .FirstOrDefault();
        return best is { } to && to.Gap < now && engine.Move(to.Position) is null;
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
            .Select(u => (u.Id, Worth: BattleMagic.DrainHeals(effect, u, health) * u.Creature.FightValue / u.Creature.Health))
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
    internal static long StackValue(BattleUnit unit) => (long)unit.Count * unit.Creature.FightValue;

    /// <summary>The creature value of the hit points (up to the stack's health) a blow or spell takes off or gives back.</summary>
    internal static long Worth(BattleUnit unit, int damage) => (long)Math.Min(damage, unit.TotalHealth) * unit.Creature.FightValue / unit.Creature.Health;

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
        var reach = BattleGrid.Reachable(battle, unit);
        var blows = enemies.Where(e => BattleGrid.AttackPositions(reach, unit, e).Count > 0).Select(e => Score(battle, unit, e, isRanged: false));
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

    /// <summary>
    /// The reachable placement closest to the nearest enemy, if it is closer than standing still; from <see cref="BattleAiLevel.ProtectShooters"/>
    /// on, a melee stack heads for the enemy shooter that can shoot with the biggest threat instead, while there is one.
    /// </summary>
    private static CellPosition? Advance(BattleState battle, BattleUnit unit, List<BattleUnit> enemies, BattleAiLevel level)
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

        var shooters = level >= BattleAiLevel.ProtectShooters && unit.Creature.Shots == 0
            ? enemies.Where(e => !e.IsMachine && BattleGrid.CanShootNow(battle, e)).ToList()
            : [];
        var nearest = shooters.Count > 0
            ? shooters.OrderByDescending(e => Threat(battle, e)).ThenBy(e => e.Id).First()
            : enemies.OrderBy(e => unit.Rect.GapTo(e.Rect)).ThenBy(e => e.Id).First();
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
