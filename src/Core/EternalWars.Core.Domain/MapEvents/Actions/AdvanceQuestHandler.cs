using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;

namespace EternalWars.Core.Domain.MapEvents.Actions;

/// <summary>
/// The last step of a quest stage's completion (EVT-5): the quest object moves on, the other players' open entries for it go, and the completer's
/// log shows the next stage (its proposal is shown at once) or the quest done; a Quest Guard whose last stage is done leaves the map.
/// </summary>
internal sealed class AdvanceQuestHandler : MapEventActionHandler<MapEventService.AdvanceQuestAction>
{
    protected override bool Apply(MapEventActionContext context, MapEventService.AdvanceQuestAction action)
    {
        if (context.Hero is { } hero && context.Run.Player is { } completer)
        {
            Advance(context, action, hero, completer);
        }

        return true;
    }

    private static void Advance(MapEventActionContext context, MapEventService.AdvanceQuestAction advance, Hero hero, PlayerId completer)
    {
        var (service, run, events) = (context.Service, context.Run, context.Events);
        if (context.State.FindObject(advance.Site) is not QuestSite site || site.Stage != advance.Stage)
        {
            return;
        }

        site.Stage++;
        events.Publish(new QuestStageCompleted(site.Id, run.Event.Name, advance.Stage, hero.Id, completer));
        foreach (var player in context.Data.DropOpenQuestEntries(site.Id, completer))
        {
            events.Publish(new QuestLogChanged(player));
        }

        if (site.Stage < run.Event.Stages.Count)
        {
            service.WriteLog(completer, new QuestLogEntry(site.Id, run.Event.Name, site.Stage, Done: false));
            run.Frames.Push(new MapEventFrame([new MessageAction(run.Event.Stages[site.Stage].Proposal)]));
            return;
        }

        service.WriteLog(completer, new QuestLogEntry(site.Id, run.Event.Name, advance.Stage, Done: true));
        if (site.Kind == MapObjectKind.QuestGuard)
        {
            service.Steps.RemoveObject(site);
        }
    }
}
