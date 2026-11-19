using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;

namespace EternalWars.Core.Domain.MapEvents.Actions;

/// <summary>Remove cell: the cell that ran this hero event goes.</summary>
internal sealed class RemoveCellHandler : MapEventActionHandler<RemoveCellAction>
{
    public override bool IsReward => false;

    protected override bool Apply(MapEventActionContext context, RemoveCellAction action)
    {
        if (context.Run.Cell is { } cell && context.Data.RemoveCell(cell))
        {
            context.Events.Publish(new MapEventCellRemoved(cell));
        }

        return true;
    }

    protected override string Line(RemoveCellAction action, GameState? state) => "Remove this cell";
}
