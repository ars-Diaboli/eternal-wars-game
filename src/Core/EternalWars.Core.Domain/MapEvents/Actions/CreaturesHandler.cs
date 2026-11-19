using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.MapEvents.Actions;

/// <summary>
/// Creatures for the hero, or for a town event's garrison; negative takes them. A hero's join a stack or a free slot, and a full army asks a person
/// what to leave and keeps a computer's strongest; a garrison's without room are lost.
/// </summary>
internal sealed class CreaturesHandler : MapEventActionHandler<CreaturesAction>
{
    protected override bool Apply(MapEventActionContext context, CreaturesAction action)
    {
        if (context.Hero is { } hero)
        {
            GiveHero(context, hero, action.Creature, context.Evaluate(action.Count));
        }
        else if (context.Town is { } town)
        {
            GiveGarrison(context, town, action.Creature, context.Evaluate(action.Count));
        }

        return true;
    }

    protected override EventAction Fix(MapEventActionContext context, CreaturesAction action) =>
        action with { Count = new NumberExpr(context.Evaluate(action.Count)) };

    protected override string Describe(GameState state, CreaturesAction action) =>
        $"{Signed(Amount(action.Count))} {(state.Creatures.TryGet(action.Creature, out var c) ? c.Name : action.Creature)}";

    private static void GiveHero(MapEventActionContext context, Hero hero, string creature, int count)
    {
        var (state, events) = (context.State, context.Events);
        if (!state.Creatures.Contains(creature) || count == 0)
        {
            return;
        }

        if (count < 0)
        {
            if (Take(hero.Army, creature, -count))
            {
                events.Publish(new ArmyChanged(hero.Id, hero.Army.Slots.ToList()));
            }

            return;
        }

        var stack = new CreatureStack(creature, count);
        if (hero.Army.TryAdd(stack))
        {
            events.Publish(new ArmyChanged(hero.Id, hero.Army.Slots.ToList()));
            return;
        }

        var all = hero.Army.Stacks.Select(s => s.Stack).Append(stack).ToList();
        if (state.GetPlayer(hero.Owner).IsHuman)
        {
            context.Service.Ask(context.Run, new MapEventArmyDecision(hero.Id, all));
            return;
        }

        MapEventService.Refill(hero.Army, all.OrderByDescending(s => (long)s.Count * state.Creatures.Get(s.Creature).Health).Take(Army.SlotCount));
        events.Publish(new ArmyChanged(hero.Id, hero.Army.Slots.ToList()));
    }

    private static void GiveGarrison(MapEventActionContext context, Town town, string creature, int count)
    {
        var changed = count switch
        {
            > 0 when context.State.Creatures.Contains(creature) => town.Garrison.TryAdd(new CreatureStack(creature, count)),
            < 0 => Take(town.Garrison, creature, -count),
            _ => false,
        };
        if (changed)
        {
            context.Events.Publish(new GarrisonChanged(town.Id, town.Garrison.Slots.ToList()));
        }
    }

    /// <summary>Takes up to <paramref name="count"/> of the creature from the army, last stack first; false when it has none.</summary>
    private static bool Take(Army army, string creature, int count)
    {
        var taken = false;
        foreach (var (slot, stack) in army.Stacks.Where(s => s.Stack.Creature == creature).Reverse().ToList())
        {
            if (count <= 0)
            {
                break;
            }

            var removed = Math.Min(count, stack.Count);
            army.Set(slot, stack.Count == removed ? null : new CreatureStack(creature, stack.Count - removed));
            count -= removed;
            taken = true;
        }

        return taken;
    }

    protected override IEnumerable<EventExpr> Expressions(CreaturesAction action) => [action.Count];

    protected override string Line(CreaturesAction action, GameState? state) => Gain(action.Count, CreatureName(state, action.Creature));

    /// <summary>A hero's, if its army takes them (taken: down to what it has); a garrison's, at a smaller share.</summary>
    protected override EventWorth Worth(IMapEventValuer valuer, CreaturesAction action)
    {
        var count = valuer.Average(action.Count);
        var value = CreatureValue(valuer.State, action.Creature);
        if (valuer.Hero is { } hero)
        {
            return EventWorth.Gain(count >= 0
                ? hero.Army.CanAccept(action.Creature) ? 0.85 * count * value : 0
                : -Math.Min(-count, hero.Army.Stacks.Where(s => s.Stack.Creature == action.Creature).Sum(s => s.Stack.Count)) * value);
        }

        return valuer.Town is null ? EventWorth.Nothing : EventWorth.Gain(0.6 * count * value);
    }
}
