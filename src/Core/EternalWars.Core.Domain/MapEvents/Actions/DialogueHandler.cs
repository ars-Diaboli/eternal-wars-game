using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Game;

namespace EternalWars.Core.Domain.MapEvents.Actions;

/// <summary>A dialogue (ADR-242): a person reads its lines, each speaker named (a roster hero by its name); anyone else goes on at once.</summary>
internal sealed class DialogueHandler : MapEventActionHandler<DialogueAction>
{
    public override bool IsReward => false;

    protected override bool Apply(MapEventActionContext context, DialogueAction action)
    {
        var (service, run, state) = (context.Service, context.Run, context.State);
        if (action.Lines.Count > 0 && run.Player is { } player && state.GetPlayer(player).IsHuman)
        {
            var lines = action.Lines.Select(l => l with { Speaker = SpeakerName(state, l.Speaker) }).ToList();
            service.Ask(run, new MapEventDecision(service.HeroOf(run)?.Id ?? MapEventDecision.NoHero, MapEventService.EventOf(run).Name, lines[0].Text, [], [])
            {
                Dialogue = lines,
            });
        }

        return true;
    }

    /// <summary>A roster hero's name for its id; any other speaker as written.</summary>
    internal static string SpeakerName(GameState? state, string speaker) =>
        state is not null && state.HeroRoster.Contains(speaker) ? state.HeroRoster.Get(speaker).Name : speaker;

    protected override string Line(DialogueAction action, GameState? state) => action.Lines switch
    {
        [] => "Dialogue (no lines)",
        [var only] => $"Dialogue: {Said(state, only)}",
        [var first, ..] => $"Dialogue, {action.Lines.Count} lines: {Said(state, first)} …",
    };

    private static string Said(GameState? state, DialogueLine line) =>
        string.IsNullOrWhiteSpace(line.Speaker)
            ? $"\"{MapEventText.Short(line.Text)}\""
            : $"{SpeakerName(state, line.Speaker)}: \"{MapEventText.Short(line.Text)}\"";
}
