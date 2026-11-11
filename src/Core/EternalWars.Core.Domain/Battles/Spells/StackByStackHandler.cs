namespace EternalWars.Core.Domain.Battles.Spells;

/// <summary>
/// A kind that works on each stack in its reach on its own, by id (healing, reviving, dispelling, slaying, sacrifice, buffs and curses), inside
/// the resolver's frame of a cast on its reach (<see cref="BattleSpellResolver.OnReach"/>). A kind may add what the cast does once, around it.
/// </summary>
internal abstract class StackByStackHandler : SpellEffectHandler
{
    public override void Apply(CastContext cast, BattleSpellResolver resolver) =>
        resolver.OnReach(cast, reach =>
        {
            foreach (var unit in reach.Where(u => BattleMagic.Affects(cast.Battle, cast.Side, cast.Known, u)))
            {
                ApplyTo(cast, resolver, unit);
            }
        });

    /// <summary>What the cast does to one stack in its reach it works on.</summary>
    protected abstract void ApplyTo(CastContext cast, BattleSpellResolver resolver, BattleUnit unit);
}
