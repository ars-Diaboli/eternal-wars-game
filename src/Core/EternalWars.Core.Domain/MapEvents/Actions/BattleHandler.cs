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
}
