using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Game;

namespace EternalWars.Core.Domain.MapEvents.Actions;

/// <summary>
/// A message: a person reads it with a line per reward, then gets the rewards; anyone else gets them at once. The rewards' amounts are drawn
/// now, so the lines tell what comes.
/// </summary>
internal sealed class MessageHandler : MapEventActionHandler<MessageAction>
{
    public override bool IsReward => false;

    protected override bool Apply(MapEventActionContext context, MessageAction action)
    {
        var (service, run) = (context.Service, context.Run);
        var rewards = action.Rewards.Select(r => MapEventActionHandlers.For(r).Fix(context, r)).ToList();
        run.Frames.Push(new MapEventFrame(rewards));
        if (run.Player is { } player && context.State.GetPlayer(player).IsHuman)
        {
            var lines = rewards.Select(r => MapEventActionHandlers.For(r).Describe(context.State, r)).OfType<string>().ToList();
            service.Ask(run, new MapEventDecision(service.HeroOf(run)?.Id ?? MapEventDecision.NoHero, MapEventService.EventOf(run).Name, action.Text, lines, [])
            {
                Place = service.PlaceOf(run, rewards),
            });
        }

        return true;
    }

    protected override IReadOnlyList<IReadOnlyList<EventAction>> Lists(MessageAction action) => [action.Rewards];

    protected override IReadOnlyList<string> ListNames(MessageAction action) => ["Rewards (given once it is read)"];

    protected override EventAction WithLists(MessageAction action, IReadOnlyList<IReadOnlyList<EventAction>> lists) => action with { Rewards = lists[0] };

    protected override string Line(MessageAction action, GameState? state) => $"Message: \"{MapEventText.Short(action.Text)}\"";

    protected override EventWorth Worth(IMapEventValuer valuer, MessageAction action) => valuer.Worth(action.Rewards);
}
