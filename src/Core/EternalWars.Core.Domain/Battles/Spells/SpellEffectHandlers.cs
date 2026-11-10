using EternalWars.Core.Domain.Magic;

namespace EternalWars.Core.Domain.Battles.Spells;

/// <summary>
/// The registry of battle spell effects: the one <see cref="ISpellEffectHandler"/> of each <see cref="SpellEffectKind"/>. The aim checks
/// (<see cref="BattleMagic.CastRefusal"/>), the resolver and the battle AI ask it instead of switching on the kind, so a new effect kind is its
/// parse in <see cref="SpellEffect.Parse"/> plus one handler class registered here (a test checks every kind has exactly one).
/// </summary>
internal static class SpellEffectHandlers
{
    /// <summary>Every handler, in <see cref="SpellEffectKind"/> order.</summary>
    public static IReadOnlyList<ISpellEffectHandler> All { get; } =
    [
        new DamageHandler(),
        new HealHandler(),
        new BuffHandler(),
        new CurseHandler(),
        new SummonHandler(),
        new ReviveHandler(),
        new DispelHandler(),
        new SlayHandler(),
        new ZoneHandler(),
        new MoveHandler(),
        new CopyHandler(),
        new RaiseHandler(),
        new SacrificeHandler(),
    ];

    private static readonly Dictionary<SpellEffectKind, ISpellEffectHandler> ByKind = All.ToDictionary(h => h.Kind);

    /// <summary>The handler of the kind.</summary>
    public static ISpellEffectHandler For(SpellEffectKind kind) =>
        ByKind.TryGetValue(kind, out var handler) ? handler : throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unregistered spell effect kind.");
}
