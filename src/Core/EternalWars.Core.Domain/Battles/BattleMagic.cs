using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Magic;

namespace EternalWars.Core.Domain.Battles;

/// <summary>
/// A hero's spells in battle (ADR-071, ADR-072, ADR-110), at their ranks (ADR-122): at most one cast a round per side, during the turn of one of its own units,
/// which keeps its action. Damage is the spell's base × (10 + Spell Power) / 10, durations too (at least 1 round); a spell specialty adds 3 % a
/// hero level to the amount; holy spells do double damage to undead and demons; the enemy hero's Spell resistance cuts hostile spells' damage and
/// duration; Interference takes Spell Power off and adds mana to the cost. Creatures' immunities and spell resistance stop spells for good.
/// </summary>
public static class BattleMagic
{
    /// <summary>A spell specialty's effect per hero level, in percent (ADR-072, MAG-6).</summary>
    public const int SpecialtyPercentPerLevel = 3;

    /// <summary>The spells' tag for holy damage, doubled against undead and demons and lost on holy creatures.</summary>
    public const string Holy = "holy";

    /// <summary>Mana the spell costs the side's hero now: its own cost plus the enemy hero's Interference.</summary>
    public static int Cost(BattleState battle, BattleSide side, KnownSpell spell) =>
        spell.Cost + battle.Participant(side.Opponent()).Bonus.EnemySpellCostPenalty;

    /// <summary>The side's Spell Power after the enemy hero's Interference, never below 0.</summary>
    public static int SpellPower(BattleState battle, BattleSide side) =>
        Math.Max(0, (battle.Participant(side).Caster?.SpellPower ?? 0) - battle.Participant(side.Opponent()).Bonus.EnemySpellPowerPenalty);

    /// <summary>
    /// Why the side's hero cannot cast the spell now at the stack or cell, or null. A stack spell needs a living target of the right side (enemies
    /// for damage and curses, own stacks for healing and buffs) the spell works on; an area spell needs a cell on the field. A move (ADR-151) also
    /// needs the cell its stack goes to (<paramref name="cell"/>, the footprint's top-left); a drain may name the own stack it heals
    /// (<paramref name="second"/>).
    /// </summary>
    public static string? CastRefusal(BattleState battle, BattleSide side, string spellId, int? target, CellPosition? cell, int? second = null)
    {
        if (battle.Participant(side).Caster is not { } caster)
        {
            return "The hero has no spell book.";
        }

        if (caster.Find(spellId) is not { } known)
        {
            return $"The hero does not know '{spellId}'.";
        }

        if (known.Effect is not { } effect)
        {
            return $"{known.Spell.Name} cannot be cast in battle.";
        }

        if (SpellLevelCap(battle) is { } cap && known.Spell.Level > cap)
        {
            return $"No spell above level {cap} can be cast in this battle.";
        }

        if (battle.HasCastThisRound(side))
        {
            return "The hero has already cast a spell this round.";
        }

        if (battle.ManaOf(side) < Cost(battle, side, known))
        {
            return $"Not enough mana for {known.Spell.Name}.";
        }

        return TargetRefusal(battle, side, known, target, cell, second: second);
    }

    /// <summary>
    /// Why the active creature caster cannot cast its spell now (ADR-120), or null: it needs a cast left of a spell that works in battle, within
    /// the battle's spell level cap, and a fitting target. Casting is its action; it costs no mana and is not the hero's spell of the round.
    /// </summary>
    public static string? CreatureCastRefusal(BattleState battle, BattleUnit unit, int? target, CellPosition? cell)
    {
        if (CreatureSpell(unit) is not { } known)
        {
            return $"{unit.Creature.Name} has no spell to cast now.";
        }

        if (known.Effect is null)
        {
            return $"{known.Spell.Name} cannot be cast in battle yet.";
        }

        if (SpellLevelCap(battle) is { } cap && known.Spell.Level > cap)
        {
            return $"No spell above level {cap} can be cast in this battle.";
        }

        return TargetRefusal(battle, unit.Side, known, target, cell, CreatureSpellPower(unit));
    }

    /// <summary>
    /// The creature table's caster spells (ADR-042) as spell table ids ([spells.md](../content/spells.md#creature-casters), ADR-099); <c>rebirth</c>
    /// is the Phoenix's own ability, not a spell; <c>summon-imps</c> calls imps like Hellgate.
    /// </summary>
    public static IReadOnlyDictionary<string, string> CasterSpells { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["fireball"] = "chaos.fireball",
        ["frost-bolt"] = "arcana.frost-bolt",
        ["blind"] = "shadow.blind",
        ["bloodlust"] = "chaos.bloodlust",
        ["cure"] = "light.heal",
        ["lightning"] = "nature.call-lightning",
        ["confusion"] = "shadow.confusion",
        ["death-cloud"] = "death.death-cloud",
        ["resurrection"] = "light.resurrection",
        ["summon-imps"] = "chaos.hellgate",
    };

    /// <summary>The Phoenix's rebirth (ADR-120): not a spell, it rises once with 30 % of its stack.</summary>
    public const string Rebirth = "rebirth";

    /// <summary>What the Phoenix's stack rises with, in percent of its count at the start of the battle.</summary>
    public const int RebirthPercent = 30;

    /// <summary>The spell the creature casts now (the first of its caster spells with casts left that is a spell), at rank I; null when none.</summary>
    public static KnownSpell? CreatureSpell(BattleUnit unit) =>
        unit.CastsLeft.Where(c => c.Value > 0 && CasterSpells.ContainsKey(c.Key)).OrderBy(c => c.Key, StringComparer.Ordinal)
            .Select(c => SpellCatalog.Default.TryGet(CasterSpells[c.Key], out var spell) ? new KnownSpell(spell, 1, 0) : null)
            .FirstOrDefault(k => k is not null);

    /// <summary>A creature caster's Spell Power (ADR-120, proposal): half its count, rounded down.</summary>
    public static int CreatureSpellPower(BattleUnit unit) => unit.Count / 2;

    /// <summary>Why the spell cannot be aimed at the stack or cell (or, for a summon, why there is no room), or null.</summary>
    private static string? TargetRefusal(
        BattleState battle, BattleSide side, KnownSpell known, int? target, CellPosition? cell, int? creaturePower = null, int? second = null)
    {
        var effect = known.Effect!;
        if (effect.Kind == SpellEffectKind.Summon)
        {
            return SummonPlacement(battle, side, known, creaturePower) is null ? $"There is no room to call {known.Spell.Name}'s creatures." : null;
        }

        switch (effect.Reach)
        {
            case SpellReach.Stack:
                if (target is not { } id || battle.FindUnit(id) is not { } unit || !(unit.IsAlive || IsFallen(battle, side, effect, unit)))
                {
                    return $"{known.Spell.Name} needs a living stack to aim at.";
                }

                if (!effect.EitherSide && (unit.Side != side) != effect.IsHostile)
                {
                    return effect.IsHostile ? $"{known.Spell.Name} is cast on enemy stacks." : $"{known.Spell.Name} is cast on own stacks.";
                }

                if (unit.Side != side && unit.Has(SpellStatus.Unseen))
                {
                    return $"{unit.Creature.Name} cannot be seen.";
                }

                if (effect.Kind == SpellEffectKind.Revive && Missing(unit) == 0)
                {
                    return $"{unit.Creature.Name} has nothing to revive.";
                }

                if (effect.HealthLimit > 0 && unit.TotalHealth > HealthLimit(battle, side, known, creaturePower))
                {
                    return $"{unit.Creature.Name} is too strong for {known.Spell.Name} ({HealthLimit(battle, side, known, creaturePower)} health at most).";
                }

                if (effect.Kind == SpellEffectKind.Dispel && Dispelled(side, effect, unit).Count == 0)
                {
                    return $"{unit.Creature.Name} has no spells {known.Spell.Name} takes off.";
                }

                if (!Affects(battle, side, known, unit))
                {
                    return $"{known.Spell.Name} has no effect on {unit.Creature.Name}.";
                }

                if (effect.Kind == SpellEffectKind.Move)
                {
                    return MoveRefusal(battle, side, known, unit, cell);
                }

                return effect.DrainPercent > 0 && second is { } healed ? DrainRefusal(battle, side, known, healed) : null;
            case SpellReach.Area:
                return cell is { } at && at.X >= 0 && at.Y >= 0 && at.X < battle.Width && at.Y < BattleState.Height
                    ? null
                    : $"{known.Spell.Name} needs a cell on the battlefield.";
            default:
                return null;
        }
    }

    /// <summary>The highest spell level either side may cast: the lower cap of the two heroes' artifacts (the Recanter's Cloak, ADR-111), or null.</summary>
    public static int? SpellLevelCap(BattleState battle) =>
        (battle.Attacker.Bonus.SpellLevelCap, battle.Defender.Bonus.SpellLevelCap) switch
        {
            ({ } a, { } d) => Math.Min(a, d),
            (var a, var d) => a ?? d,
        };

    /// <summary>
    /// The square an area spell covers around its centre cell (the same size whatever the targets' sizes, ADR-072, MAG-11); for a column spell
    /// (ADR-123) the columns around the cell's, the field's whole height; for a zone's line (ADR-149) its width and length around the cell, running
    /// down the field.
    /// </summary>
    public static CellRect Area(SpellEffect effect, CellPosition centre) => effect.Column
        ? new(centre.X - (effect.AreaSize / 2), 0, effect.AreaSize, BattleState.Height)
        : effect.AreaHeight > 0
            ? new(centre.X - (effect.AreaSize / 2), centre.Y - (effect.AreaHeight / 2), effect.AreaSize, effect.AreaHeight)
            : new(centre.X - (effect.AreaSize / 2), centre.Y - (effect.AreaSize / 2), effect.AreaSize, effect.AreaSize);

    /// <summary>
    /// An own stack destroyed in this battle that a <c>fallen</c> revive may bring back (ADR-123): not a war machine or a summoned stack, and its
    /// place is free.
    /// </summary>
    public static bool IsFallen(BattleState battle, BattleSide side, SpellEffect effect, BattleUnit unit) =>
        effect is { Kind: SpellEffectKind.Revive, Fallen: true } && !unit.IsAlive && unit.Side == side && !unit.IsMachine && !unit.IsSummoned
        && BattleGrid.IsFree(battle, unit, unit.Position);

    /// <summary>The health a revive can give the stack: its top creature's wounds and its dead since the battle started.</summary>
    public static long Missing(BattleUnit unit) => unit.IsMachine
        ? 0
        : ((long)unit.InitialCount * unit.Creature.Health) - unit.TotalHealth;

    /// <summary>
    /// The spells a dispel takes off the stack (ADR-123): from an own stack the enemy's (Purge), from an enemy stack those its own side cast
    /// (Unravel), from any stack all of them (Dispel).
    /// </summary>
    public static IReadOnlyList<ActiveSpell> Dispelled(BattleSide side, SpellEffect effect, BattleUnit unit) =>
        effect.Dispels == DispelScope.Any ? [.. unit.Spells] : [.. unit.Spells.Where(s => s.Caster != side)];

    /// <summary>
    /// The damage a damage spell deals, stack by stack, in order (ADR-123): each stack in its reach it affects; then for <c>splash</c> the enemy stacks
    /// next to each target at that percent; then for <c>chain</c> the nearest stack not yet hit (enemies only with <c>spare-own</c>), each jump at
    /// half the damage of the one before. A stack is hit once.
    /// </summary>
    public static IReadOnlyList<(BattleUnit Unit, int Damage)> DamagePlan(
        BattleState battle, BattleSide side, KnownSpell known, int? target, CellPosition? cell, int? creaturePower = null)
    {
        var effect = known.Effect!;
        var plan = new List<(BattleUnit Unit, int Damage)>();
        var hit = new HashSet<BattleUnit>();
        foreach (var unit in Reach(battle, side, known, target, cell).Where(u => Affects(battle, side, known, u)))
        {
            plan.Add((unit, Damage(battle, side, known, unit, creaturePower)));
            hit.Add(unit);
        }

        if (effect.SplashPercent > 0)
        {
            foreach (var (unit, _) in plan.ToList())
            {
                foreach (var next in battle.LivingUnitsOf(side.Opponent()).Where(u => !hit.Contains(u) && u.Rect.IsAdjacentTo(unit.Rect)).OrderBy(u => u.Id))
                {
                    if (Affects(battle, side, known, next))
                    {
                        plan.Add((next, Math.Max(1, Damage(battle, side, known, next, creaturePower) * effect.SplashPercent / 100)));
                        hit.Add(next);
                    }
                }
            }
        }

        var last = plan.Count > 0 ? plan[0].Unit : null;
        var percent = 100;
        for (var jump = 0; jump < effect.Chain && last is not null; jump++)
        {
            percent /= 2;
            var from = last;
            last = battle.LivingUnits
                .Where(u => !hit.Contains(u) && !(effect.SpareOwn && u.Side == side) && Affects(battle, side, known, u))
                .OrderBy(u => u.Rect.DistanceTo(from.Rect)).ThenBy(u => u.Id)
                .FirstOrDefault();
            if (last is not null)
            {
                plan.Add((last, Math.Max(1, Damage(battle, side, known, last, creaturePower) * percent / 100)));
                hit.Add(last);
            }
        }

        return plan;
    }

    /// <summary>The creatures a slay takes from the stack (ADR-123): its percent of the count, at least 1.</summary>
    public static int Slain(SpellEffect effect, BattleUnit unit) => Math.Max(1, unit.Count * effect.Amount / 100);

    /// <summary>
    /// The stacks in the spell's reach at its rank, by id: the target, and for <c>targets N</c> (ADR-122) the N − 1 other stacks of its side nearest
    /// to it that the spell works on (nearer first, then by id); every stack with a cell in the area (both sides); every stack of the enemy side for
    /// a hostile spell or of the own side otherwise; every stack on the field. <c>spare-own</c> leaves the caster's stacks out of an area or the field.
    /// </summary>
    public static IReadOnlyList<BattleUnit> Reach(BattleState battle, BattleSide side, KnownSpell known, int? target, CellPosition? cell)
    {
        var effect = known.Effect!;
        IEnumerable<BattleUnit> units = effect.Reach switch
        {
            SpellReach.Stack => battle.FindUnit(target!.Value) is { } unit && (unit.IsAlive || IsFallen(battle, side, effect, unit)) ? [unit, .. Others(unit)] : [],
            SpellReach.Area => battle.LivingUnits.Where(u => u.Rect.Intersects(Area(effect, cell!.Value)) && !(effect.Ring && u.Rect.Contains(cell.Value))),
            SpellReach.Side => battle.LivingUnitsOf(effect.IsHostile ? side.Opponent() : side),
            _ => battle.LivingUnits,
        };
        return units.Where(u => !effect.SpareOwn || u.Side != side).OrderBy(u => u.Id).ToList();

        IEnumerable<BattleUnit> Others(BattleUnit aimed) => effect.Targets <= 1
            ? []
            : battle.LivingUnitsOf(aimed.Side).Where(u => u != aimed && Affects(battle, side, known, u) && (effect.Kind != SpellEffectKind.Revive || Missing(u) > 0))
                .OrderBy(u => u.Rect.DistanceTo(aimed.Rect)).ThenBy(u => u.Id).Take(effect.Targets - 1);
    }

    /// <summary>
    /// Whether the spell, at its rank, works on the stack: war machines only take damage (with no filter, or the ground filter); the spell's filter
    /// (living, undead, undead and demons, shooters, melee stacks, stacks on the ground); creature immunities to the spell's tags; creature spell
    /// resistance and the stack's hero's artifact immunities (ADR-111) against the enemy's hostile spells; holy creatures ignore holy spells.
    /// </summary>
    public static bool Affects(BattleState battle, BattleSide side, KnownSpell known, BattleUnit unit)
    {
        var spell = known.Spell;
        var effect = known.Effect!;
        var creature = unit.Creature;
        if (unit.IsMachine)
        {
            return effect.Kind == SpellEffectKind.Damage && effect.Filter is SpellFilter.None or SpellFilter.Ground;
        }

        // Wards on the stack (ADR-123): Stasis keeps every spell but a dispel off; Anti-Magic every spell up to its level, Shroud the enemy's;
        // Oakheart the mind spells; Steadfast makes a stack fearless, so Terror does not take hold.
        var wards = unit.SpellModifiers;
        var enemySpell = unit.Side != side;
        if ((wards.Has(SpellStatus.Stasis) && effect.Kind != SpellEffectKind.Dispel)
            || spell.Level <= wards.ImmuneLevel
            || (enemySpell && spell.Level <= wards.ShroudLevel)
            || (wards.Has(SpellStatus.WardMind) && spell.HasTag(SpellTags.Mind))
            || (wards.Has(SpellStatus.Fearless) && effect.Modifiers.Has(SpellStatus.NoAttack)))
        {
            return false;
        }

        var fits = effect.Filter switch
        {
            SpellFilter.Living => !creature.HasTag(CreatureTags.Undead) && !creature.HasTag(CreatureTags.Construct),
            SpellFilter.Undead => creature.HasTag(CreatureTags.Undead),
            SpellFilter.UndeadOrDemon => creature.HasTag(CreatureTags.Undead) || creature.HasTag(CreatureTags.Demon),
            SpellFilter.Shooter => creature.Shots > 0,
            SpellFilter.Melee => creature.Shots == 0,
            SpellFilter.Ground => !creature.IsFlying,
            _ => true,
        };
        if (!fits || creature.IsImmuneTo(spell.Tags) || (spell.HasTag(Holy) && creature.HasTag(CreatureTags.Holy)))
        {
            return false;
        }

        var enemy = (effect.IsHostile || effect.EitherSide) && enemySpell;
        return !(enemy && ((creature.ResistsSpell(spell.Level, known.Rank) && !effect.Pierce) || battle.Participant(unit.Side).Bonus.IsImmuneTo(spell.Tags)));
    }

    /// <summary>
    /// The spell's damage to the stack: base × (10 + Spell Power) / 10, the specialty's +3 % a level, ×2 for holy damage to undead and demons,
    /// less the enemy hero's Spell resistance, changed by the stack's spell-taken percent (Arcane Ward); at least 1.
    /// </summary>
    public static int Damage(BattleState battle, BattleSide side, KnownSpell known, BattleUnit unit, int? creaturePower = null)
    {
        var spell = known.Spell;
        var effect = known.Effect!;
        if (effect.ExecutePercent > 0 && unit.TotalHealth * 100L < (long)unit.InitialCount * unit.Creature.Health * effect.ExecutePercent)
        {
            return unit.TotalHealth; // Harvest (ADR-123): a stack worn below the mark is destroyed
        }

        long damage = Amount(battle, side, known, creaturePower) + (unit.DamageDealt * effect.DebtPercent / 100);
        if (spell.HasTag(Holy) && (unit.Creature.HasTag(CreatureTags.Undead) || unit.Creature.HasTag(CreatureTags.Demon)))
        {
            damage *= 2;
        }

        if (unit.Side != side && !effect.Pierce)
        {
            damage = damage * (100 - battle.Participant(unit.Side).Bonus.SpellResistancePercent) / 100;
        }

        damage = damage * Math.Max(0, 100 + unit.SpellModifiers.SpellTakenPercent) / 100;
        return (int)Math.Clamp(damage, 1, int.MaxValue);
    }

    /// <summary>
    /// A damage, healing or summoned amount from the side's hero: base × (10 + Spell Power) / 10, with its specialty and its elemental orbs' percent
    /// (ADR-111); from a creature caster (<paramref name="creaturePower"/>) its own Spell Power and nothing else.
    /// </summary>
    public static int Amount(BattleState battle, BattleSide side, KnownSpell known, int? creaturePower = null, string? bound = null) =>
        Scaled(battle, side, known, known.Effect!.Amount, creaturePower, bound);

    /// <summary>
    /// A base number scaled like a spell's amount: × (10 + Spell Power) / 10, the hero's specialty and elemental orbs (the bound kind's element for
    /// Bind Elemental, <paramref name="bound"/>); a creature caster only its own Spell Power.
    /// </summary>
    public static int Scaled(BattleState battle, BattleSide side, KnownSpell known, int baseAmount, int? creaturePower = null, string? bound = null)
    {
        var amount = (long)baseAmount * (10 + (creaturePower ?? SpellPower(battle, side))) / 10;
        if (creaturePower is null && battle.Participant(side).Caster is { } caster)
        {
            if (caster.Specialty == known.Spell.Id)
            {
                amount = amount * (100 + (SpecialtyPercentPerLevel * caster.Level)) / 100;
            }

            amount = amount * (100 + caster.ElementBonus(known.Spell, bound)) / 100;
        }

        return (int)Math.Min(int.MaxValue, amount);
    }

    /// <summary>
    /// The last round a buff or curse lasts on the stack: the rest of this round plus its rounds × (10 + Spell Power) / 10, cut by the enemy hero's
    /// Spell resistance for a hostile spell (rounded up), at least 1; a spell for the battle, or one that lasts turns (ADR-150), lasts to its end.
    /// </summary>
    public static int UntilRound(BattleState battle, BattleSide side, KnownSpell known, BattleUnit unit, int? creaturePower = null)
    {
        if (known.Effect!.Rounds is not { } rounds)
        {
            return BattleEngine.MaxRounds;
        }

        if (rounds == 0)
        {
            return battle.Round; // the rest of this round (Vigil, Fleetfoot, ADR-123), not lengthened
        }

        return battle.Round + Duration(battle, side, known, unit, rounds, creaturePower);
    }

    /// <summary>
    /// How many of the stack's turns a spell that lasts turns takes (ADR-150): its turns lengthened and cut like rounds (<see cref="UntilRound"/>);
    /// null for a spell that lasts rounds.
    /// </summary>
    public static int? Turns(BattleState battle, BattleSide side, KnownSpell known, BattleUnit unit, int? creaturePower = null) =>
        known.Effect!.Turns is { } turns ? Duration(battle, side, known, unit, turns, creaturePower) : null;

    private static int Duration(BattleState battle, BattleSide side, KnownSpell known, BattleUnit unit, int length, int? creaturePower)
    {
        var duration = Math.Max(1, length * (10 + (creaturePower ?? SpellPower(battle, side))) / 10);
        if (known.Effect!.IsHostile && unit.Side != side)
        {
            var resistance = battle.Participant(unit.Side).Bonus.SpellResistancePercent;
            duration = Math.Max(1, ((duration * (100 - resistance)) + 99) / 100);
        }

        return duration;
    }

    /// <summary>The most total health a stack may have for the spell to take control of it (Hypnotize, ADR-150): its base scaled like an amount.</summary>
    public static int HealthLimit(BattleState battle, BattleSide side, KnownSpell known, int? creaturePower = null) =>
        Scaled(battle, side, known, known.Effect!.HealthLimit, creaturePower);

    /// <summary>
    /// Why the move (ADR-151) cannot take the stack to <paramref name="to"/> (its footprint's top-left), or null: a rooted stack and the keep's stack
    /// stay put; the footprint must fit there (inside the field, clear of obstacles and other stacks) somewhere else than it stands, within the
    /// spell's range (a straight line from where it stands, like a flyer's reach), and for <c>shadow</c> not next to an enemy stack.
    /// </summary>
    public static string? MoveRefusal(BattleState battle, BattleSide side, KnownSpell known, BattleUnit unit, CellPosition? to)
    {
        if (unit.IsRooted || unit.InKeep)
        {
            return $"{unit.Creature.Name} cannot be moved.";
        }

        if (to is not { } at)
        {
            return $"{known.Spell.Name} needs the cell {unit.Creature.Name} goes to.";
        }

        return CanLand(battle, side, known.Effect!, unit, at) ? null : $"{unit.Creature.Name} cannot be moved to {at}.";
    }

    /// <summary>Whether the move may take the stack to the placement (<see cref="MoveRefusal"/>).</summary>
    public static bool CanLand(BattleState battle, BattleSide side, SpellEffect effect, BattleUnit unit, CellPosition at) =>
        at != unit.Position
        && (effect.MoveRange == 0
            || BattleGrid.Length(at.X - unit.Position.X, at.Y - unit.Position.Y) <= (effect.MoveRange * BattleGrid.Unit) + BattleGrid.ReachSlack)
        && BattleGrid.IsFree(battle, unit, at)
        && !(effect.Shadow && battle.LivingUnits.Any(u => u.Side != side && u.Rect.IsAdjacentTo(unit.RectAt(at))));

    /// <summary>Why a drain (ADR-151) cannot heal the stack, or null: it must be a living own creature stack not in Stasis.</summary>
    public static string? DrainRefusal(BattleState battle, BattleSide side, KnownSpell known, int healed) =>
        battle.FindUnit(healed) is { IsAlive: true, IsMachine: false } unit && unit.Side == side && !unit.Has(SpellStatus.Stasis)
            ? null
            : $"{known.Spell.Name} heals a living own stack.";

    /// <summary>The health a drain gives back: its percent of the damage dealt.</summary>
    public static int Drained(SpellEffect effect, long dealt) => (int)Math.Min(int.MaxValue, dealt * effect.DrainPercent / 100);

    /// <summary>
    /// What the drained health does for the stack: the wounds of its top creature, and with <c>drain-revive</c> its dead up to its count when the
    /// battle started.
    /// </summary>
    public static long DrainHeals(SpellEffect effect, BattleUnit unit, int health) =>
        Math.Min(health, effect.DrainRevives ? Missing(unit) : unit.Creature.Health - unit.TopHealth);

    /// <summary>
    /// What a summon calls (ADR-106, ADR-120): the first of its creatures the side has not summoned yet this battle (else the first), as many as
    /// its health amount buys (at least 1), and where: an own summoned stack of that creature takes them, else the free placement nearest the
    /// side's own edge and the field's middle row. Null when nothing fits.
    /// </summary>
    public static (CreatureDefinition Creature, int Count, BattleUnit? Joins, CellPosition At)? SummonPlacement(
        BattleState battle, BattleSide side, KnownSpell known, int? creaturePower = null)
    {
        var effect = known.Effect!;
        var summoned = battle.Units.Where(u => u.Side == side && u.IsSummoned).Select(u => u.Creature.Id).ToHashSet(StringComparer.Ordinal);
        var id = effect.Summons.FirstOrDefault(c => !summoned.Contains(c)) ?? effect.Summons.FirstOrDefault();
        if (id is null || !battle.Creatures.TryGet(id, out var creature))
        {
            return null;
        }

        var count = Math.Max(1, Amount(battle, side, known, creaturePower, id) / Math.Max(1, creature.Health));
        if (battle.LivingUnitsOf(side).FirstOrDefault(u => u.IsSummoned && u.Creature.Id == id) is { } stack)
        {
            return (creature, count, stack, stack.Position);
        }

        var probe = new BattleUnit(0, side, creature, 1, -1, new CellPosition(0, 0));
        var size = creature.Size.Cells();
        var spot = Enumerable.Range(0, battle.Width - size + 1)
            .SelectMany(x => Enumerable.Range(0, BattleState.Height - size + 1).Select(y => new CellPosition(x, y)))
            .Where(p => BattleGrid.IsFree(battle, probe, p))
            .OrderBy(p => side == BattleSide.Attacker ? p.X : battle.Width - size - p.X)
            .ThenBy(p => Math.Abs(p.Y + (size / 2) - (BattleState.Height / 2))).ThenBy(p => p.Y)
            .Select(p => (CellPosition?)p)
            .FirstOrDefault();
        return spot is { } at ? (creature, count, null, at) : null;
    }
}
