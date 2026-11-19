using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;

namespace EternalWars.Core.Domain.MapEvents.Actions;

/// <summary>The defaults of a <see cref="IMapEventActionHandler"/> for <typeparamref name="TAction"/>: kept as it is in a message, no line, no place.</summary>
internal abstract class MapEventActionHandler<TAction> : IMapEventActionHandler
    where TAction : EventAction
{
    public Type Action => typeof(TAction);

    public virtual bool IsReward => true;

    public bool Apply(MapEventActionContext context, EventAction action) => Apply(context, (TAction)action);

    public EventAction Fix(MapEventActionContext context, EventAction action) => Fix(context, (TAction)action);

    public string? Describe(GameState state, EventAction action) => Describe(state, (TAction)action);

    public MapPosition? Place(EventAction action) => Place((TAction)action);

    public IReadOnlyList<IReadOnlyList<EventAction>> Lists(EventAction action) => Lists((TAction)action);

    public IReadOnlyList<string> ListNames(EventAction action) => ListNames((TAction)action);

    public EventAction WithLists(EventAction action, IReadOnlyList<IReadOnlyList<EventAction>> lists) => WithLists((TAction)action, lists);

    public IEnumerable<EventCondition> Conditions(EventAction action) => Conditions((TAction)action);

    public IEnumerable<EventExpr> Expressions(EventAction action) => Expressions((TAction)action);

    public string Line(EventAction action, GameState? state) => Line((TAction)action, state);

    public EventWorth Worth(IMapEventValuer valuer, EventAction action) => Worth(valuer, (TAction)action);

    /// <summary>Runs the action; false stops the event.</summary>
    protected abstract bool Apply(MapEventActionContext context, TAction action);

    protected virtual EventAction Fix(MapEventActionContext context, TAction action) => action;

    protected virtual string? Describe(GameState state, TAction action) => null;

    protected virtual MapPosition? Place(TAction action) => null;

    protected virtual IReadOnlyList<IReadOnlyList<EventAction>> Lists(TAction action) => [];

    protected virtual IReadOnlyList<string> ListNames(TAction action) => [];

    protected virtual EventAction WithLists(TAction action, IReadOnlyList<IReadOnlyList<EventAction>> lists) => action;

    protected virtual IEnumerable<EventCondition> Conditions(TAction action) => [];

    protected virtual IEnumerable<EventExpr> Expressions(TAction action) => [];

    protected abstract string Line(TAction action, GameState? state);

    /// <summary>Nothing the AI values, by default.</summary>
    protected virtual EventWorth Worth(IMapEventValuer valuer, TAction action) => EventWorth.Nothing;

    /// <summary>An expression as the editor shows it.</summary>
    protected static string Expr(EventExpr expr) => MapEventText.Describe(expr);

    /// <summary>"Give 5 what", or "Take 5 what" for a negative number.</summary>
    protected static string Gain(EventExpr amount, string what) =>
        amount is NumberExpr { Value: < 0 } taken ? $"Take {-taken.Value} {what}" : $"Give {Expr(amount)} {what}";

    /// <summary>A creature's name, its id without a state or when unknown.</summary>
    protected static string CreatureName(GameState? state, Common.CreatureId id) => state is not null && state.Creatures.TryGet(id, out var creature) ? creature.Name : id.Value;

    /// <summary>A creature's fight value, 0 when unknown.</summary>
    protected static double CreatureValue(GameState state, Common.CreatureId id) => state.Creatures.TryGet(id, out var creature) ? creature.FightValue : 0;

    /// <summary>A number with its sign, as a message's line shows it.</summary>
    protected static string Signed(int value) => value >= 0 ? $"+{value}" : $"{value}";

    /// <summary>A drawn amount; 0 for one not drawn.</summary>
    protected static int Amount(EventExpr expr) => expr is NumberExpr number ? number.Value : 0;
}

/// <summary>An action that changes the hero itself (EVT-4 b): skipped when the event has no hero.</summary>
internal abstract class HeroActionHandler<TAction> : MapEventActionHandler<TAction>
    where TAction : EventAction
{
    protected sealed override bool Apply(MapEventActionContext context, TAction action)
    {
        if (context.Hero is { } hero)
        {
            ApplyTo(context, hero, action);
        }

        return true;
    }

    /// <summary>Changes the hero.</summary>
    protected abstract void ApplyTo(MapEventActionContext context, Hero hero, TAction action);
}
