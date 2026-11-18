using EternalWars.Core.Domain.Events;

namespace EternalWars.Core.Domain.MapEvents.Actions;

/// <summary>Remove cell: the cell that ran this hero event goes.</summary>
internal sealed class RemoveCellHandler : MapEventActionHandler<RemoveCellAction>
{
    protected override bool Apply(MapEventActionContext context, RemoveCellAction action)
    {
        if (context.Run.Cell is { } cell && context.Data.RemoveCell(cell))
        {
            context.Events.Publish(new MapEventCellRemoved(cell));
        }

        return true;
    }
}
