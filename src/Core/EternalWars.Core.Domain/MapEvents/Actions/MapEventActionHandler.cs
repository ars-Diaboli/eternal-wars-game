using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;

namespace EternalWars.Core.Domain.MapEvents.Actions;

/// <summary>The defaults of a <see cref="IMapEventActionHandler"/> for <typeparamref name="TAction"/>: kept as it is in a message, no line, no place.</summary>
internal abstract class MapEventActionHandler<TAction> : IMapEventActionHandler
    where TAction : EventAction
{
    public Type Action => typeof(TAction);

    public bool Apply(MapEventActionContext context, EventAction action) => Apply(context, (TAction)action);

    public EventAction Fix(MapEventActionContext context, EventAction action) => Fix(context, (TAction)action);

    public string? Describe(GameState state, EventAction action) => Describe(state, (TAction)action);

    public MapPosition? Place(EventAction action) => Place((TAction)action);

    /// <summary>Runs the action; false stops the event.</summary>
    protected abstract bool Apply(MapEventActionContext context, TAction action);

    protected virtual EventAction Fix(MapEventActionContext context, TAction action) => action;

    protected virtual string? Describe(GameState state, TAction action) => null;

    protected virtual MapPosition? Place(TAction action) => null;

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
