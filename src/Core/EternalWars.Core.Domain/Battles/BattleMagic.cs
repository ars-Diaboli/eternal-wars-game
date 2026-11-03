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
    /// for damage and curses, own stacks for healing and buffs) the spell works on; an area spell needs a cell on the field.
    /// </summary>
    public static string? CastRefusal(BattleState battle, BattleSide side, string spellId, int? target, CellPosition? cell)
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

        return TargetRefusal(battle, side, known, target, cell);
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
    private static string? TargetRefusal(BattleState battle, BattleSide side, KnownSpell known, int? target, CellPosition? cell, int? creaturePower = null)
    {
        var effect = known.Effect!;
        if (effect.Kind == SpellEffectKind.Summon)
        {
            return SummonPlacement(battle, side, known, creaturePower) is null ? $"There is no room to call {known.Spell.Name}'s creatures." : null;
        }

        switch (effect.Reach)
        {
            case SpellReach.Stack:
                if (target is not { } id || battle.FindUnit(id) is not { IsAlive: true } unit)
                {
                    return $"{known.Spell.Name} needs a living stack to aim at.";
                }

                if ((unit.Side != side) != effect.IsHostile)
                {
                    return effect.IsHostile ? $"{known.Spell.Name} is cast on enemy stacks." : $"{known.Spell.Name} is cast on own stacks.";
                }

                return Affects(battle, side, known, unit) ? null : $"{known.Spell.Name} has no effect on {unit.Creature.Name}.";
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

    /// <summary>The square an area spell covers around its centre cell (the same size whatever the targets' sizes, ADR-072, MAG-11).</summary>
    public static CellRect Area(SpellEffect effect, CellPosition centre) =>
        new(centre.X - (effect.AreaSize / 2), centre.Y - (effect.AreaSize / 2), effect.AreaSize, effect.AreaSize);

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
            SpellReach.Stack => battle.FindUnit(target!.Value) is { IsAlive: true } unit ? [unit, .. Others(unit)] : [],
            SpellReach.Area => battle.LivingUnits.Where(u => u.Rect.Intersects(Area(effect, cell!.Value))),
            SpellReach.Side => battle.LivingUnitsOf(effect.IsHostile ? side.Opponent() : side),
            _ => battle.LivingUnits,
        };
        return units.Where(u => !effect.SpareOwn || u.Side != side).OrderBy(u => u.Id).ToList();

        IEnumerable<BattleUnit> Others(BattleUnit aimed) => effect.Targets <= 1
            ? []
            : battle.LivingUnitsOf(aimed.Side).Where(u => u != aimed && Affects(battle, side, known, u))
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

        var enemy = effect.IsHostile && unit.Side != side;
        return !(enemy && (creature.ResistsSpell(spell.Level, known.Rank) || battle.Participant(unit.Side).Bonus.IsImmuneTo(spell.Tags)));
    }

    /// <summary>
    /// The spell's damage to the stack: base × (10 + Spell Power) / 10, the specialty's +3 % a level, ×2 for holy damage to undead and demons,
    /// less the enemy hero's Spell resistance, changed by the stack's spell-taken percent (Arcane Ward); at least 1.
    /// </summary>
    public static int Damage(BattleState battle, BattleSide side, KnownSpell known, BattleUnit unit, int? creaturePower = null)
    {
        var spell = known.Spell;
        long damage = Amount(battle, side, known, creaturePower);
        if (spell.HasTag(Holy) && (unit.Creature.HasTag(CreatureTags.Undead) || unit.Creature.HasTag(CreatureTags.Demon)))
        {
            damage *= 2;
        }

        if (unit.Side != side)
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
    public static int Amount(BattleState battle, BattleSide side, KnownSpell known, int? creaturePower = null)
    {
        var amount = (long)known.Effect!.Amount * (10 + (creaturePower ?? SpellPower(battle, side))) / 10;
        if (creaturePower is null && battle.Participant(side).Caster is { } caster)
        {
            if (caster.Specialty == known.Spell.Id)
            {
                amount = amount * (100 + (SpecialtyPercentPerLevel * caster.Level)) / 100;
            }

            amount = amount * (100 + caster.ElementBonus(known.Spell)) / 100;
        }

        return (int)Math.Min(int.MaxValue, amount);
    }

    /// <summary>
    /// The last round a buff or curse lasts on the stack: the rest of this round plus its rounds × (10 + Spell Power) / 10, cut by the enemy hero's
    /// Spell resistance for a hostile spell (rounded up), at least 1; a spell for the battle lasts to its end.
    /// </summary>
    public static int UntilRound(BattleState battle, BattleSide side, KnownSpell known, BattleUnit unit, int? creaturePower = null)
    {
        if (known.Effect!.Rounds is not { } rounds)
        {
            return BattleEngine.MaxRounds;
        }

        var duration = Math.Max(1, rounds * (10 + (creaturePower ?? SpellPower(battle, side))) / 10);
        if (known.Effect.IsHostile && unit.Side != side)
        {
            var resistance = battle.Participant(unit.Side).Bonus.SpellResistancePercent;
            duration = Math.Max(1, ((duration * (100 - resistance)) + 99) / 100);
        }

        return battle.Round + duration;
    }

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

        var count = Math.Max(1, Amount(battle, side, known, creaturePower) / Math.Max(1, creature.Health));
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
