using EternalWars.Core.Domain.Battles.Spells;
using EternalWars.Core.Domain.Magic;

namespace EternalWars.Core.Tests.Battles;

/// <summary>
/// Every spell effect kind has its one handler (<see cref="SpellEffectHandlers"/>): the aim checks, the resolver and the battle AI dispatch
/// through it, so a kind left out fails here, not in a battle.
/// </summary>
public sealed class SpellEffectHandlersTests
{
    public static TheoryData<SpellEffectKind> Kinds() => [.. Enum.GetValues<SpellEffectKind>()];

    [Theory]
    [MemberData(nameof(Kinds))]
    public void EveryKind_HasExactlyOneHandler(SpellEffectKind kind)
    {
        Assert.Single(SpellEffectHandlers.All, h => h.Kind == kind);
        Assert.Equal(kind, SpellEffectHandlers.For(kind).Kind);
    }

    [Fact]
    public void TheRegistry_HoldsNothingElse() =>
        Assert.Equal(Enum.GetValues<SpellEffectKind>(), SpellEffectHandlers.All.Select(h => h.Kind));

    [Fact]
    public void OnlyDamageHitsMachines_OnlyDispelPassesStasis_OnlyBuffsWaitForAnIdleCreatureCaster()
    {
        Assert.Equal([SpellEffectKind.Damage], SpellEffectHandlers.All.Where(h => h.HitsMachines).Select(h => h.Kind));
        Assert.Equal([SpellEffectKind.Dispel], SpellEffectHandlers.All.Where(h => h.PassesStasis).Select(h => h.Kind));
        Assert.Equal([SpellEffectKind.Buff], SpellEffectHandlers.All.Where(h => h.CreatureCastsWhenIdle).Select(h => h.Kind));
    }
}
