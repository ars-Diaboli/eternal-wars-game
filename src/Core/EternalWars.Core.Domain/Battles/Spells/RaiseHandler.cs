using EternalWars.Core.Domain.Magic;

namespace EternalWars.Core.Domain.Battles.Spells;

/// <summary>
/// Raise Dead (ADR-152): once a battle for the side, it raises skeletons after the battle, and for ranks II and III destroyed stacks during it
/// (<see cref="BattleSpellResolver.Rise"/>, at once and as rounds start). Worth the skeletons the enemy creatures slain so far would give after the
/// battle, and for ranks II and III the destroyed stack that would rise now (proposal).
/// </summary>
internal sealed class RaiseHandler : SpellEffectHandler
{
    public override SpellEffectKind Kind => SpellEffectKind.Raise;

    public override string? CastRefusal(CastContext cast) =>
        cast.Battle.Raising.ContainsKey(cast.Side) ? $"{cast.Known.Spell.Name} already works in this battle." : null;

    public override void Apply(CastContext cast, BattleSpellResolver resolver)
    {
        var effect = cast.Effect;
        cast.Battle.Raising[cast.Side] = new RaiseDead(cast.Known.Spell.Id, effect.Amount, effect.Rises);
        resolver.Rise(cast.Side);
    }

    public override long Value(CastContext cast)
    {
        var (battle, side, effect) = (cast.Battle, cast.Side, cast.Effect);
        if (battle.Raising.ContainsKey(side) || !battle.Creatures.TryGet(BattleMagic.RaisedCreature, out var skeleton))
        {
            return 0;
        }

        var slain = battle.Units.Where(u => u.Owner != side && !u.IsMachine && !u.IsSummoned && NecromancyRules.Counts(u.Creature))
            .Sum(u => (long)(u.InitialCount - u.KeptCount) * u.Creature.Health);
        var value = slain * effect.Amount / 100 / skeleton.Health * skeleton.Value;
        if (effect.Rises != SpellRises.None && BattleMagic.NextRising(battle, effect.Amount) is { } rising)
        {
            value += (long)rising.Count * skeleton.Value;
        }

        return value;
    }
}
