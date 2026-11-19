using EternalWars.Core.Domain.Game;

namespace EternalWars.Core.Domain.MapEvents.Actions;

/// <summary>
/// The end of the game for the event's player (ADR-242): noted for the game flow, which ends the game or defeats the player once the command is
/// settled; the event stops here. Without a player (a neutral town's event) nothing happens.
/// </summary>
internal sealed class EndGameHandler : MapEventActionHandler<EndGameAction>
{
    public override bool IsReward => false;

    protected override bool Apply(MapEventActionContext context, EndGameAction action)
    {
        if (context.Run.Player is { } player)
        {
            context.State.ScriptedEnd = new ScriptedEnd(player, action.Won, action.Text);
        }

        return false;
    }

    protected override string Line(EndGameAction action, GameState? state) =>
        (action.Won ? "The player wins the game" : "The player loses the game")
        + (action.Text.Length > 0 ? $": \"{MapEventText.Short(action.Text)}\"" : "");

    protected override EventWorth Worth(IMapEventValuer valuer, EndGameAction action) => EventWorth.Stop;
}
