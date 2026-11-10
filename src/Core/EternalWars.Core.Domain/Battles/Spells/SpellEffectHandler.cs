using EternalWars.Core.Domain.Magic;

namespace EternalWars.Core.Domain.Battles.Spells;

/// <summary>The defaults of a <see cref="ISpellEffectHandler"/>: no checks of its own, machines and Stasis keep it off, the AI's usual aims.</summary>
internal abstract class SpellEffectHandler : ISpellEffectHandler
{
    public abstract SpellEffectKind Kind { get; }

    public virtual bool HitsMachines => false;

    public virtual bool PassesStasis => false;

    public virtual bool CreatureCastsWhenIdle => false;

    public virtual string? CastRefusal(CastContext cast) => null;

    public virtual string? AimRefusal(CastContext cast, BattleUnit unit) => null;

    public virtual string? AffectedRefusal(CastContext cast, BattleUnit unit) => null;

    public virtual bool SpreadsTo(BattleUnit unit) => true;

    public abstract void Apply(CastContext cast, BattleSpellResolver resolver);

    public abstract long Value(CastContext cast);

    public virtual IEnumerable<(int? Target, CellPosition? Cell)> Aims(BattleState battle, BattleSide side, SpellEffect effect) =>
        BattleAi.Aims(battle, side, effect);
}
