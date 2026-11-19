using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.MapEvents;
using EternalWars.Core.Domain.MapEvents.Actions;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.AI;

/// <summary>
/// The worth of map events (EVT-6): what a quest stage or an answer gives against what it takes, by the AI's usual values. The AI does not look
/// ahead at events: event cells, player and town events and a Pandora's Box's event are worth nothing to it until they run. Random numbers count
/// as their average, so the AI still draws none.
/// </summary>
internal sealed partial class TargetValuer
{
    /// <summary>How deep the worth of a "run another event" is followed.</summary>
    private const int EventWorthDepth = 4;

    /// <summary>
    /// What doing the quest object's current stage is worth to the hero, or 0 when it does not meet it (or it is a gate, which it simply walks
    /// through once it does): the stage's actions (what it takes, then its rewards), and for a guard the way it opens.
    /// </summary>
    public double QuestWorth(Hero hero, QuestSite site)
    {
        if (site.Kind == MapObjectKind.QuestGate || QuestRules.Current(_state, site) is not { } current || !QuestRules.Meets(_state, hero, site))
        {
            return 0;
        }

        var worth = Worth(current.Stage.Actions, new EventScope(hero.Owner, hero, null), 0).Value;
        return site.Kind == MapObjectKind.QuestGuard ? worth + _weights.GuardOpened + OpensTheWay(hero.Owner, site.VisitPosition) : worth;
    }

    /// <summary>
    /// The actions' worth in gold, and whether they end the event (a stop, or a battle the hero would lose): each action's handler prices it
    /// (ADR-237) by the AI's usual values (those of <see cref="BoxValue"/>), through <see cref="EventValuer"/>.
    /// </summary>
    public EventWorth Worth(IReadOnlyList<EventAction> actions, EventScope scope, int depth) => new EventValuer(this, scope, depth).Worth(actions);

    /// <summary>An expression's value with every random number at its average (the AI draws none).</summary>
    private double Average(EventExpr expr, EventScope scope) => expr switch
    {
        RandomExpr random => (random.Min + random.Max) / 2.0,
        ArithmeticExpr arithmetic when Average(arithmetic.Left, scope) is var left && Average(arithmetic.Right, scope) is var right => arithmetic.Op switch
        {
            ArithmeticOp.Add => left + right,
            ArithmeticOp.Subtract => left - right,
            ArithmeticOp.Multiply => left * right,
            _ => right == 0 ? 0 : left / right,
        },
        _ => MapEventEvaluator.Evaluate(_state, scope, expr), // no random number in it
    };

    /// <summary>A condition, its comparisons made with averages.</summary>
    private bool EventHolds(EventCondition condition, EventScope scope) => condition switch
    {
        AllCondition all => all.Conditions.All(c => EventHolds(c, scope)),
        AnyCondition any => any.Conditions.Any(c => EventHolds(c, scope)),
        NotCondition not => !EventHolds(not.Condition, scope),
        CompareCondition compare => Average(compare.Left, scope) is var left && Average(compare.Right, scope) is var right && compare.Op switch
        {
            CompareOp.Equal => left == right,
            CompareOp.NotEqual => left != right,
            CompareOp.Less => left < right,
            CompareOp.LessOrEqual => left <= right,
            CompareOp.Greater => left > right,
            _ => left >= right,
        },
        _ => MapEventEvaluator.Test(_state, scope, condition),
    };

    /// <summary>What the action handlers price an event's actions with: this valuer's averages and values, for one scope, at one depth.</summary>
    private sealed class EventValuer(TargetValuer owner, EventScope scope, int depth) : IMapEventValuer
    {
        public GameState State => owner._state;

        public EventScope Scope => scope;

        public Hero? Hero => scope.Hero;

        public Town? Town { get; } = scope.Town is { } town ? owner._state.FindObject(town) as Town : null;

        public double Average(EventExpr expr) => owner.Average(expr, scope);

        public bool Holds(EventCondition condition) => owner.EventHolds(condition, scope);

        public EventWorth Worth(IReadOnlyList<EventAction> actions)
        {
            var total = 0.0;
            foreach (var action in actions)
            {
                var worth = MapEventActionHandlers.For(action).Worth(this, action);
                total += worth.Value;
                if (worth.Stops)
                {
                    return new EventWorth(total, true);
                }
            }

            return EventWorth.Gain(total);
        }

        public double RunWorth(IReadOnlyList<EventAction> actions) =>
            depth < EventWorthDepth ? new EventValuer(owner, scope, depth + 1).Worth(actions).Value : 0;

        public EventWorth Fight(IReadOnlyList<CreatureStack> army)
        {
            var hero = scope.Hero!;
            var enemy = Army.Of([.. army.Take(Army.SlotCount)]);
            return owner._ai.Estimator.Fight(hero, enemy).IsSafe(owner._weights.MonsterLoss)
                ? EventWorth.Gain(owner._weights.GuardArmyShare * owner._ai.ArmyPower(enemy))
                : EventWorth.Stop with { Value = -(owner._ai.ArmyPower(hero.Army) + 5000.0) }; // the hero would be lost
        }

        public double ArtifactWorth(Common.ArtifactId artifact) =>
            scope.Hero is { } hero && owner._state.Artifacts.TryGet(artifact, out var definition) ? owner.ArtifactWorth(hero, definition) : 0;
    }
}
