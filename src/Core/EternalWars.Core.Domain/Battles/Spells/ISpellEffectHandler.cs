using EternalWars.Core.Domain.Magic;

namespace EternalWars.Core.Domain.Battles.Spells;

/// <summary>
/// What one <see cref="SpellEffectKind"/> does in battle, side by side with what the AI thinks it is worth: its own checks on the aim, how a cast
/// works, and its value. The aim checks every kind shares (reach, sides, immunities, Martyrdom, drains) are <see cref="BattleMagic"/>'s; the
/// handler for a kind is <see cref="SpellEffectHandlers.For"/>.
/// </summary>
internal interface ISpellEffectHandler
{
    /// <summary>The kind this handler works.</summary>
    SpellEffectKind Kind { get; }

    /// <summary>
    /// Whether a war machine takes the spell with no filter or the ground filter (the <c>machine</c> filter aims at machines whatever the kind).
    /// </summary>
    bool HitsMachines { get; }

    /// <summary>Whether the spell works on a stack in Stasis (ADR-123).</summary>
    bool PassesStasis { get; }

    /// <summary>A creature caster casts it only when it has nothing to hit itself this turn, and then whatever its worth (ADR-120).</summary>
    bool CreatureCastsWhenIdle { get; }

    /// <summary>Why the kind cannot be cast now whatever its aim (no room to summon, Raise Dead already working), or null.</summary>
    string? CastRefusal(CastContext cast);

    /// <summary>Why the kind cannot be aimed at the stack, checked before whether the spell works on it (nothing to revive or dispel), or null.</summary>
    string? AimRefusal(CastContext cast, BattleUnit unit);

    /// <summary>Why the kind cannot be aimed at a stack it works on (the move's cell, copies, a copy's room), or null.</summary>
    string? AffectedRefusal(CastContext cast, BattleUnit unit);

    /// <summary>Whether a <c>targets N</c> spell also takes this other stack of the aimed side it works on.</summary>
    bool SpreadsTo(BattleUnit unit);

    /// <summary>Works a cast that passed every check, publishing what happens, in order.</summary>
    void Apply(CastContext cast, BattleSpellResolver resolver);

    /// <summary>What the cast is worth to the AI, in creature value (ADR-120, ADR-123); 0 or less is not cast.</summary>
    long Value(CastContext cast);

    /// <summary>Where the AI tries a hero's spell of this kind (each is then checked and valued).</summary>
    IEnumerable<(int? Target, CellPosition? Cell)> Aims(BattleState battle, BattleSide side, SpellEffect effect);
}
