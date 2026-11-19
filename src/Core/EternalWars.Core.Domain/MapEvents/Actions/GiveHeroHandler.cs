using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;

namespace EternalWars.Core.Domain.MapEvents.Actions;

/// <summary>A roster hero joins the player (EVT-4 c): not when it is in play or the player has as many heroes as it may.</summary>
internal sealed class GiveHeroHandler : MapEventActionHandler<GiveHeroAction>
{
    protected override bool Apply(MapEventActionContext context, GiveHeroAction action)
    {
        if (context.Run.Player is { } owner)
        {
            Give(context, owner, action);
        }

        return true;
    }

    protected override MapPosition? Place(GiveHeroAction action) =>
        action is { X: { } x, Y: { } y } ? new MapPosition(x, y, action.Level) : null;

    private static void Give(MapEventActionContext context, PlayerId owner, GiveHeroAction give)
    {
        var (state, run) = (context.State, context.Run);
        if (!state.HeroRoster.Contains(give.Hero) || state.HeroLimitReached(owner) || InPlay(state, give.Hero))
        {
            return;
        }

        var target = give.X is { } x && give.Y is { } y ? new MapPosition(x, y, give.Level) : context.Hero?.Position ?? context.Town?.VisitPosition;
        if (target is not { } around || FreeCellNear(state, around) is not { } position)
        {
            return;
        }

        // Given in the middle of a turn, it moves next turn, like a prisoner.
        var joined = context.Service.Recruits.Join(owner, position, state.HeroRoster.Get(give.Hero), Math.Max(0, give.Experience), movesToday: run.StartOfTurn);
        run.Place = joined.Position;
        context.Events.Publish(new MapEventHeroGiven(joined.Id, owner, give.Hero, joined.Level));
    }

    private static bool InPlay(GameState state, string definition) =>
        state.Heroes.Concat(state.GarrisonHeroes).Concat(state.RetiredHeroes).Any(h => h.Definition == definition)
        || state.ObjectsOf<Objects.Prison>().Any(p => p.Hero == definition);

    /// <summary>The place itself when a hero may stand there, else the first such neighbour.</summary>
    private static MapPosition? FreeCellNear(GameState state, MapPosition around) =>
        new[] { around }.Concat(DirectionExtensions.All.Select(around.Step))
            .Cast<MapPosition?>()
            .FirstOrDefault(p => MovementRules.CheckTile(state, p!.Value) == MoveResult.Moved && state.HeroAt(p.Value) is null && state.ObjectCellAt(p.Value) is null);

    protected override string Line(GiveHeroAction action, GameState? state) =>
        $"Give the hero {(state is not null && state.HeroRoster.Contains(action.Hero) ? state.HeroRoster.Get(action.Hero).Name : action.Hero)}";

    protected override EventWorth Worth(IMapEventValuer valuer, GiveHeroAction action) =>
        valuer.Scope.Player is { } owner && valuer.State.HeroRoster.Contains(action.Hero) && !valuer.State.HeroLimitReached(owner)
            ? EventWorth.Gain(2500)
            : EventWorth.Nothing;
}
