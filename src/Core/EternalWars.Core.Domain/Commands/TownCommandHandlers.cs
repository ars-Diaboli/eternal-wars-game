using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Commands;

/// <summary>The towns' commands: building, recruiting, the garrison, governors, caravans and the marketplace.</summary>
internal static class TownCommandHandlers
{
    internal static void Register(CommandHandlers handlers, TownService towns, CaravanService caravans, EconomyService economy)
    {
        handlers.Add<BuildCommand>(c => towns.Build(c.Player, c.Town, c.Building));
        handlers.Add<BuildDwellingCommand>(c => towns.BuildDwelling(c.Player, c.Town, c.Line));
        handlers.Add<RecruitCommand>(c => towns.Recruit(c.Player, c.Town, c.Creature, c.Count, c.Hero));
        handlers.Add<UpgradeStackCommand>(c => towns.UpgradeStack(c.Player, c.Town, c.Army, c.Slot));
        handlers.Add<TransformCreaturesCommand>(c => towns.Transform(c.Player, c.Town, c.Orders));
        handlers.Add<MoveTownStackCommand>(c => towns.MoveStack(c.Player, c.Town, c.From, c.FromSlot, c.To, c.ToSlot, c.Count));
        handlers.Add<HireHeroCommand>(c => towns.HireHero(c.Player, c.Town, c.Hero));
        handlers.Add<AppointGovernorCommand>(c => towns.AppointGovernor(c.Player, c.Town, c.Hero));
        handlers.Add<DismissGovernorCommand>(c => towns.DismissGovernor(c.Player, c.Town));
        handlers.Add<BuyWarMachineCommand>(c => towns.BuyWarMachine(c.Player, c.Town, c.Slot));
        handlers.Add<BuySpellBookCommand>(c => towns.BuySpellBook(c.Player, c.Town));
        handlers.Add<EnterGarrisonCommand>(c => towns.EnterGarrison(c.Player, c.Town));
        handlers.Add<LeaveGarrisonCommand>(c => towns.LeaveGarrison(c.Player, c.Town));

        handlers.Add<OrderCaravanCommand>(c => caravans.Order(c.Player, c.Town, c.Dwelling, c.Count));
        handlers.Add<FetchResourcesCommand>(c => caravans.Fetch(c.Player, c.Town, c.Provider));
        handlers.Add<SendTroopsCommand>(c => caravans.Send(c.Player, c.FromTown, c.Slot, c.ToTown, c.Count));

        handlers.Add<TradeResourceCommand>(c => economy.Trade(c.Player, c.Resource, c.Direction, c.Amount, c.MapMarketplace));
    }
}
