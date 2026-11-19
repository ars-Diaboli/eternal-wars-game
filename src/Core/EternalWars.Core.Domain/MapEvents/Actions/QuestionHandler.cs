using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Game;

namespace EternalWars.Core.Domain.MapEvents.Actions;

/// <summary>A question: the player picks an answer, whose actions run next; skipped without a player or answers.</summary>
internal sealed class QuestionHandler : MapEventActionHandler<QuestionAction>
{
    public override bool IsReward => false;

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

    protected override IReadOnlyList<IReadOnlyList<EventAction>> Lists(QuestionAction action) => [.. action.Answers.Select(a => a.Actions)];

    protected override IReadOnlyList<string> ListNames(QuestionAction action) => [.. action.Answers.Select(a => $"Answer \"{a.Text}\"")];

    protected override EventAction WithLists(QuestionAction action, IReadOnlyList<IReadOnlyList<EventAction>> lists) =>
        action with { Answers = [.. action.Answers.Select((a, i) => a with { Actions = lists[i] })] };

    protected override string Line(QuestionAction action, GameState? state) => $"Ask: \"{MapEventText.Short(action.Text)}\"";

    /// <summary>The best answer.</summary>
    protected override EventWorth Worth(IMapEventValuer valuer, QuestionAction action) =>
        action.Answers.Count == 0 ? EventWorth.Nothing : action.Answers.Select(a => valuer.Worth(a.Actions)).MaxBy(w => w.Value);
}
