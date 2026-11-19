using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.MapEvents.Actions;

/// <summary>
/// What an action handler prices an action with for the AI (EVT-6, ADR-237): the event's hero, town and player, expressions at their averages (the
/// AI draws no random number), and the AI's own values. The AI's target valuer is the one valuer.
/// </summary>
internal interface IMapEventValuer
{
    GameState State { get; }

    /// <summary>Whom the event is for.</summary>
    EventScope Scope { get; }

    /// <summary>The event's hero, or null.</summary>
    Hero? Hero { get; }

    /// <summary>The event's town, or null.</summary>
    Town? Town { get; }

    /// <summary>The expression with every random number at its average.</summary>
    double Average(EventExpr expr);

    /// <summary>Whether the condition holds, its comparisons made with averages.</summary>
    bool Holds(EventCondition condition);

    /// <summary>What the actions are worth, and whether they end the event.</summary>
    EventWorth Worth(IReadOnlyList<EventAction> actions);

    /// <summary>What another event's actions are worth, a level deeper; 0 once followed deep enough. A stop in them ends only them.</summary>
    double RunWorth(IReadOnlyList<EventAction> actions);

    /// <summary>The hero fighting the army: what winning is worth, or the hero lost and the event ended.</summary>
    EventWorth Fight(IReadOnlyList<CreatureStack> army);

    /// <summary>What the artifact is worth to the hero (0 without a hero or for an unknown one).</summary>
    double ArtifactWorth(string artifact);
}

/// <summary>What actions are worth to the AI in gold, and whether they end the event.</summary>
internal readonly record struct EventWorth(double Value, bool Stops)
{
    public static EventWorth Nothing => default;

    /// <summary>The event stops here, nothing gained.</summary>
    public static EventWorth Stop => new(0, true);

    public static EventWorth Gain(double value) => new(value, false);
}
