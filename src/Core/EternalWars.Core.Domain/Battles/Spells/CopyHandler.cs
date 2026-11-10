using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Magic;

namespace EternalWars.Core.Domain.Battles.Spells;

/// <summary>
/// Mirror Image (ADR-152): a copy of the stack (its count, top creature's health and shots, none of its spells or casts) stands on the free
/// placement nearest it for the caster's side; not of a copy, and only with room. It is summoned (gone after the battle, worth nothing), does not
/// keep its side in the battle, acts from the next round and vanishes when its hits are used up or its rounds end. Worth the control share of the
/// stack's value for its rounds (proposal).
/// </summary>
internal sealed class CopyHandler : SpellEffectHandler
{
    public override SpellEffectKind Kind => SpellEffectKind.Copy;

    public override string? AffectedRefusal(CastContext cast, BattleUnit unit) =>
        unit.IsCopy ? $"{cast.Known.Spell.Name} does not work on a copy."
        : BattleMagic.PlacementNear(cast.Battle, unit, unit.Creature) is null ? $"There is no room for a copy of {unit.Creature.Name}."
        : null;

    public override void Apply(CastContext cast, BattleSpellResolver resolver)
    {
        var (battle, side, known) = (cast.Battle, cast.Side, cast.Known);
        var unit = battle.GetUnit(cast.Target!.Value);
        var until = BattleMagic.UntilRound(battle, side, known, unit, cast.CreaturePower);
        var copy = new BattleUnit(battle.NextUnitId(), side, unit.Creature, unit.Count, -1, BattleMagic.PlacementNear(battle, unit, unit.Creature)!.Value)
        {
            IsSummoned = true,
            IsCopy = true,
            CopyHits = known.Effect!.CopyHits,
            CopyUntilRound = until,
            CopySpell = known.Spell.Id,
            MutableCastsLeft = new Dictionary<string, int>(StringComparer.Ordinal),
            TopHealth = unit.TopHealth,
            ShotsLeft = unit.ShotsLeft,
        };
        battle.MutableUnits.Add(copy);
        resolver.Events.Publish(new BattleUnitCopied(copy.Info(), unit.Id, known.Spell.Id, until));
    }

    // A copy fights like the stack for its rounds, or until hit (ADR-152, proposal): the control share of its value a round.
    public override long Value(CastContext cast) =>
        cast.Target is { } copied && cast.Battle.FindUnit(copied) is { } original
            ? BattleAi.StackValue(original) * BattleAi.ControlPercentPerRound * Math.Min(BattleAi.MaxValuedRounds, cast.Effect.Rounds ?? BattleAi.MaxValuedRounds) / 100
            : 0;
}
