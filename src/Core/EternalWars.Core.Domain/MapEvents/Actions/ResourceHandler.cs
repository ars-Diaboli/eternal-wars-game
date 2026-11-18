using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Game;

namespace EternalWars.Core.Domain.MapEvents.Actions;

/// <summary>A resource for the player, or taken from it down to 0.</summary>
internal sealed class ResourceHandler : MapEventActionHandler<ResourceAction>
{
    protected override bool Apply(MapEventActionContext context, ResourceAction action)
    {
        if (context.Run.Player is { } player)
        {
            Give(context, player, action.Resource, context.Evaluate(action.Amount));
        }

        return true;
    }

    protected override EventAction Fix(MapEventActionContext context, ResourceAction action) =>
        action with { Amount = new NumberExpr(context.Evaluate(action.Amount)) };

    protected override string Describe(GameState state, ResourceAction action) => $"{Signed(Amount(action.Amount))} {action.Resource}";

    private static void Give(MapEventActionContext context, PlayerId player, ResourceType resource, int amount)
    {
        var economy = context.Service.Economy;
        if (amount > 0)
        {
            economy.Grant(player, ResourceSet.Of(resource, amount));
        }
        else if (Math.Min(-amount, context.State.GetPlayer(player).Resources[resource]) is > 0 and var taken)
        {
            economy.Set(player, context.State.GetPlayer(player).Resources - ResourceSet.Of(resource, taken));
        }
    }
}
