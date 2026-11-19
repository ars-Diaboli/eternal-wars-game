using EternalWars.Core.Domain.Game;

namespace EternalWars.Core.Domain.MapEvents.Actions;

/// <summary>The hero fights the author's army (EVT-4 d); with nobody to fight with the event stops and the hero stays.</summary>
internal sealed class BattleHandler : MapEventActionHandler<BattleAction>
{
    protected override bool Apply(MapEventActionContext context, BattleAction action)
    {
        if (context.Hero is not { } hero)
        {
            return true;
        }

        if (hero.Army.IsEmpty || action.Army.Count == 0)
        {
            return false;
        }

        context.Run.Waiting = MapEventWaiting.Battle;
        context.Service.Battles.BeginEventBattle(hero, action.Army);
        return true;
    }

    protected override string Line(BattleAction action, GameState? state) =>
        $"Fight {string.Join(", ", action.Army.Select(s => $"{s.Count} {CreatureName(state, s.Creature)}"))}";

    /// <summary>A hero without an army, or an empty battle, ends the event; otherwise the AI's estimate of the fight.</summary>
    protected override EventWorth Worth(IMapEventValuer valuer, BattleAction action) => valuer.Hero switch
    {
        null => EventWorth.Nothing,
        { Army.IsEmpty: true } => EventWorth.Stop,
        _ when action.Army.Count == 0 => EventWorth.Stop,
        _ => valuer.Fight(action.Army),
    };
}
