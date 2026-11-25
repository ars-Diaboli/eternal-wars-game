using EternalWars.Core.Domain.Heroes;

namespace EternalWars.Core.Domain.Battles;

/// <summary>
/// One spell in the battle's spell book (ADR-314): the spell at its rank, its school (the town id, <paramref name="SchoolName"/> its name), what it
/// costs the hero now (<see cref="BattleMagic.Cost"/>) and why it is greyed (<see cref="BattleMagic.BookRefusal"/>), null when it can be cast.
/// </summary>
public sealed record SpellBookSpell(KnownSpell Known, string School, string SchoolName, int Cost, string? Refusal);

/// <summary>One warcry in the battle's spell book (ADR-067, ADR-314) and why it is greyed (<see cref="BattleState.WarcryRefusal"/>), null when it can be cried.</summary>
public sealed record SpellBookWarcry(Warcry Warcry, string? Refusal);

/// <summary>
/// A side's spell book in battle (ADR-314): its hero's mana, the spells it can cast in battle by school (in the schools' order, then by spell
/// level and name), and the warcries its hero knows as a category of their own, in the order the Warcry skill teaches them. Adventure spells are
/// left out; a creature caster's spell is not in it (it has its own button).
/// </summary>
public sealed record BattleSpellBook(int Mana, IReadOnlyList<SpellBookSpell> Spells, IReadOnlyList<SpellBookWarcry> Warcries)
{
    /// <summary>The side's spell book; null when its hero has neither a spell book nor a warcry (or the side has no hero).</summary>
    public static BattleSpellBook? Of(BattleState battle, BattleSide side)
    {
        var participant = battle.Participant(side);
        var warcries = Enum.GetValues<Warcry>().Where(w => PrimalFury.Knows(participant.Bonus.Warcry, w))
            .OrderBy(PrimalFury.LevelOf).Select(w => new SpellBookWarcry(w, battle.WarcryRefusal(side, w))).ToList();
        if (participant.Caster is null && warcries.Count == 0)
        {
            return null;
        }

        var schools = BuiltInSkills.Schools.Keys.ToList();
        var spells = (participant.Caster?.Spells ?? [])
            .Where(k => k.Effect is not null)
            .OrderBy(k => schools.IndexOf(k.Spell.School) is var i and >= 0 ? i : schools.Count)
            .ThenBy(k => k.Spell.School, StringComparer.Ordinal)
            .ThenBy(k => k.Spell.Level)
            .ThenBy(k => k.Spell.Name, StringComparer.Ordinal)
            .Select(k => new SpellBookSpell(
                k, k.Spell.School, BuiltInSkills.Schools.GetValueOrDefault(k.Spell.School, k.Spell.School), BattleMagic.Cost(battle, side, k),
                BattleMagic.BookRefusal(battle, side, k)))
            .ToList();
        return new BattleSpellBook(battle.ManaOf(side), spells, warcries);
    }
}
