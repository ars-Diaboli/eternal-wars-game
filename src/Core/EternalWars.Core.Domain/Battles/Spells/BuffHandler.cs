using EternalWars.Core.Domain.Magic;

namespace EternalWars.Core.Domain.Battles.Spells;

/// <summary>A buff on own stacks (<see cref="EnchantHandler"/>); a creature caster casts one only when it has nothing to hit itself (ADR-120).</summary>
internal sealed class BuffHandler : EnchantHandler
{
    public override SpellEffectKind Kind => SpellEffectKind.Buff;

    public override bool CreatureCastsWhenIdle => true;
}
