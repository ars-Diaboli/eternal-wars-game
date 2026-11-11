using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Magic;

namespace EternalWars.Core.Domain.Battles.Spells;

/// <summary>
/// Damage (ADR-110, ADR-123, ADR-151): every hit of the damage plan (the stacks in reach, then splash and chain hits) is dealt in order; a stack that
/// lives takes the spell's lasting effect, then may be stunned; a drain then heals the picked own stack. Its value is the creature value of the hit
/// points it takes off enemy stacks (and of its lasting effect on them), less that of own stacks.
/// </summary>
internal sealed class DamageHandler : SpellEffectHandler
{
    public override SpellEffectKind Kind => SpellEffectKind.Damage;

    public override bool HitsMachines => true;

    public override void Apply(CastContext cast, BattleSpellResolver resolver) =>
        resolver.OnReach(cast, _ =>
        {
            var (battle, effect, spellId) = (cast.Battle, cast.Effect, cast.Known.Spell.Id);

            // Splash and chain hits come after the stacks in reach (ADR-123); every damage is worked out before it is dealt.
            long dealt = 0;
            foreach (var (unit, damage) in BattleMagic.DamagePlan(battle, cast.Side, cast.Known, cast.Target, cast.Cell, cast.CreaturePower))
            {
                dealt += Math.Min(damage, unit.TotalHealth);
                resolver.Wounds.SpellDamage(unit, spellId, damage);
                if (!unit.IsAlive)
                {
                    continue;
                }

                if (!effect.Modifiers.IsEmpty)
                {
                    EnchantHandler.Enchant(cast, resolver, unit, martyr: null);
                }

                if (effect.StunPercent > 0 && battle.Random.Next(100) < effect.StunPercent)
                {
                    unit.LosesNextAction = true;
                    resolver.Events.Publish(new BattleUnitStunned(unit.Id, spellId));
                }
            }

            if (effect.DrainPercent > 0 && cast.Second is { } healed && battle.GetUnit(healed) is { IsAlive: true } drinker)
            {
                Drain(cast, resolver, drinker, BattleMagic.Drained(effect, dealt));
            }

            if (effect.Rubble && cast.Cell is { } centre)
            {
                Rubble(cast, resolver, centre);
            }
        });

    /// <summary>Meteor Shower III (ADR-177): once the damage is dealt, the area's free cells become one-cell rubble for the rest of the battle.</summary>
    private static void Rubble(CastContext cast, BattleSpellResolver resolver, CellPosition centre)
    {
        var battle = cast.Battle;
        var rubble = BattleMagic.RubbleCells(battle, cast.Effect, centre)
            .Select((cell, i) => new BattleObstacle(battle.Obstacles.Count + 1 + i, cell, ObstacleClass.Rubble))
            .ToList();
        if (rubble.Count == 0)
        {
            return;
        }

        battle.MutableObstacles.AddRange(rubble);
        resolver.Events.Publish(new BattleRubbleFell(cast.Known.Spell.Id, rubble));
    }

    public override long Value(CastContext cast) =>
        BattleMagic.DamagePlan(cast.Battle, cast.Side, cast.Known, cast.Target, cast.Cell, cast.CreaturePower)
            .Sum(h => (h.Unit.Side == cast.Side ? -1 : 1) * (BattleAi.Worth(h.Unit, h.Damage) + EnchantHandler.LastingValue(cast.Battle, cast.Side, cast.Known, h.Unit)));

    /// <summary>A drain (ADR-151) gives the own stack its share of the damage: its top creature's wounds, and with <c>drain-revive</c> its dead, for good.</summary>
    private static void Drain(CastContext cast, BattleSpellResolver resolver, BattleUnit unit, int health)
    {
        var effect = cast.Effect;
        if (effect.DrainRevives)
        {
            resolver.Wounds.Revive(unit, cast.Known.Spell.Id, health, lasting: true);
            return;
        }

        resolver.Wounds.HealTop(unit, (int)BattleMagic.DrainHeals(effect, unit, health));
    }
}
