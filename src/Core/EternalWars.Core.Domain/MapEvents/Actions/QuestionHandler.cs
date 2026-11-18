using EternalWars.Core.Domain.Decisions;

namespace EternalWars.Core.Domain.MapEvents.Actions;

/// <summary>A question: the player picks an answer, whose actions run next; skipped without a player or answers.</summary>
internal sealed class QuestionHandler : MapEventActionHandler<QuestionAction>
{
    protected override bool Apply(MapEventActionContext context, QuestionAction action)
    {
        var (service, run) = (context.Service, context.Run);
        if (run.Player is not null && action.Answers.Count > 0)
        {
            run.Answers = action.Answers;
            service.Ask(run, new MapEventDecision(
                context.Hero?.Id ?? MapEventDecision.NoHero, MapEventService.EventOf(run).Name, action.Text, [], action.Answers.Select(a => a.Text).ToList())
            {
                Place = service.PlaceOf(run, []),
            });
        }

        return true;
    }
}
