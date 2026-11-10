using EternalWars.Core.Domain.Magic;

namespace EternalWars.Core.Domain.Battles.Spells;

/// <summary>
/// One cast as the rules and the AI see it: the battle, the casting side, the spell at its rank, its aim (the stack, the cell, the second aim of a
/// two-click spell: a drain's drinker or a martyr) and a creature caster's Spell Power (null for the hero's own, ADR-120).
/// </summary>
internal sealed record CastContext(
    BattleState Battle, BattleSide Side, KnownSpell Known, int? Target, CellPosition? Cell, int? Second = null, int? CreaturePower = null)
{
    /// <summary>The spell's effect at its rank (a cast always has one).</summary>
    public SpellEffect Effect => Known.Effect!;

    /// <summary>The stacks in the spell's reach it works on, by id (<see cref="BattleMagic.Reach"/>, <see cref="BattleMagic.Affects"/>).</summary>
    public List<BattleUnit> Affected() => [.. BattleMagic.Reach(Battle, Side, Known, Target, Cell).Where(u => BattleMagic.Affects(Battle, Side, Known, u))];
}
