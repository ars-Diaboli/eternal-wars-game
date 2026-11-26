using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Magic;

namespace EternalWars.Core.Domain.Battles;

/// <summary>What a mark on the stack panel stands for (ADR-314).</summary>
public enum StackMarkKind
{
    /// <summary>The stack defends this round.</summary>
    Defending,

    /// <summary>The stack fights on its native terrain.</summary>
    NativeTerrain,

    /// <summary>A spell on the stack, the side's own or the enemy's.</summary>
    Spell,
}

/// <summary>
/// A small mark on the stack panel (ADR-314) and what the status bar names it. <paramref name="Spell"/> is the spell of a
/// <see cref="StackMarkKind.Spell"/> mark, <paramref name="Friendly"/> whether its caster is the stack's own side.
/// </summary>
public sealed record StackMark(StackMarkKind Kind, string Name, SpellId? Spell = null, bool Friendly = true);

/// <summary>
/// The stack panel's numbers and marks (ADR-314): the stack's creature and count, its Attack and Defense as it strikes and is struck now
/// (<see cref="DamageCalculator.AttackOf"/>, <see cref="DamageCalculator.DefenseOf"/>), its damage, the top creature's health, its speed and
/// initiative now, its shots, morale and luck; and its marks: defending, native terrain, the spells on it. A war machine has only the numbers
/// it has: its health, and a shooting one its Attack, damage, initiative and shots; the others are null. Shots are null for a stack that does
/// not shoot (unlimited ones are marked: an ammo cart's, ADR-056); morale is null for a creature without it (undead, constructs, elementals).
/// <paramref name="Casts"/> is a creature caster's casts left this battle, all its spells together (ADR-120); null for a stack that casts
/// none.
/// </summary>
public sealed record StackPanel(
    int Unit,
    BattleSide Side,
    CreatureId Creature,
    string Name,
    int Count,
    bool IsMachine,
    int? Attack,
    int? Defense,
    int? MinDamage,
    int? MaxDamage,
    int Health,
    int MaxHealth,
    int? Speed,
    int? Initiative,
    int? Shots,
    bool UnlimitedShots,
    int? Morale,
    int? Luck,
    IReadOnlyList<StackMark> Marks,
    int? Casts = null)
{
    /// <summary>The panel of <paramref name="unit"/>; spells are named from <paramref name="spells"/> (the built-in catalog by default).</summary>
    public static StackPanel Of(BattleState battle, BattleUnit unit, SpellCatalog? spells = null)
    {
        var c = unit.Creature;
        int? shots = !c.IsRanged ? null : unit.ShotsLeft;
        var unlimited = c.IsRanged && battle.HasUnlimitedShots(unit);
        if (unit.IsMachine)
        {
            return c.IsRanged
                ? new StackPanel(unit.Id, unit.Side, c.Id, c.Name, unit.Count, IsMachine: true, DamageCalculator.AttackOf(battle, unit), null,
                    c.MinDamage, c.MaxDamage, unit.TopHealth, c.Health, null, battle.InitiativeOf(unit), shots, unlimited, null, null, [])
                : new StackPanel(unit.Id, unit.Side, c.Id, c.Name, unit.Count, IsMachine: true, null, null,
                    null, null, unit.TopHealth, c.Health, null, null, null, false, null, null, []);
        }

        return new StackPanel(
            unit.Id, unit.Side, c.Id, c.Name, unit.Count, IsMachine: false, DamageCalculator.AttackOf(battle, unit), DamageCalculator.DefenseOf(battle, unit),
            c.MinDamage, c.MaxDamage, unit.TopHealth, c.Health, battle.SpeedOf(unit), battle.InitiativeOf(unit), shots, unlimited,
            MoraleAndLuck.HasMorale(c) ? battle.MoraleOf(unit) : null, battle.LuckOf(unit), MarksOf(battle, unit, spells ?? SpellCatalog.Default),
            unit.CastsLeft.Count == 0 ? null : unit.CastsLeft.Values.Sum());
    }

    private static List<StackMark> MarksOf(BattleState battle, BattleUnit unit, SpellCatalog spells)
    {
        var marks = new List<StackMark>();
        if (unit.IsDefending)
        {
            marks.Add(new StackMark(StackMarkKind.Defending, "Defending: more Defense until its next turn"));
        }

        if (battle.IsNative(unit))
        {
            marks.Add(new StackMark(
                StackMarkKind.NativeTerrain,
                $"Native terrain: +{BattleState.NativeAttack} Attack, +{BattleState.NativeDefense} Defense, +{BattleState.NativeSpeed} Speed"));
        }

        foreach (var spell in unit.Spells)
        {
            var name = spells.TryGet(spell.Spell, out var definition) ? definition.Name : spell.Spell.Value;
            var lasts = spell.TurnsLeft is { } turns
                ? $"{turns} more turn{(turns == 1 ? string.Empty : "s")}"
                : $"until the end of round {spell.UntilRound}";
            marks.Add(new StackMark(StackMarkKind.Spell, $"{name}: {lasts}", spell.Spell, Friendly: spell.Caster == unit.Owner));
        }

        return marks;
    }
}
