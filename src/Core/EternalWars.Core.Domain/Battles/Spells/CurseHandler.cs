using EternalWars.Core.Domain.Magic;

namespace EternalWars.Core.Domain.Battles.Spells;

/// <summary>A curse on enemy stacks (<see cref="EnchantHandler"/>), including the spells that take control of a stack (ADR-150, ADR-152).</summary>
internal sealed class CurseHandler : EnchantHandler
{
    public override SpellEffectKind Kind => SpellEffectKind.Curse;
}
