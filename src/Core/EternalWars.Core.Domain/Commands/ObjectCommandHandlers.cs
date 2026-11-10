using EternalWars.Core.Domain.MapEvents;
using EternalWars.Core.Domain.Objects;

namespace EternalWars.Core.Domain.Commands;

/// <summary>The map objects' commands: answers to their decisions, their shops and services, and answers to map events.</summary>
internal static class ObjectCommandHandlers
{
    internal static void Register(CommandHandlers handlers, ObjectDecisionService decisions, ObjectShopService shops, MapEventService mapEvents)
    {
        handlers.Add<ResolveTreasureChestCommand>(c => decisions.ResolveTreasureChest(c.Reward));
        handlers.Add<AnswerCartographerCommand>(c => decisions.ResolveCartographer(c.Buy));
        handlers.Add<AnswerDepositCommand>(c => decisions.ResolveDeposit(c.Buy));
        handlers.Add<AnswerSphinxCommand>(c => decisions.ResolveSphinx(c.Option));
        handlers.Add<AnswerPandorasBoxCommand>(c => decisions.ResolvePandorasBox(c.Open));
        handlers.Add<ChooseWitchHutSkillCommand>(c => decisions.ResolveWitchHut(c.Skill));
        handlers.Add<ChooseTrainingCommand>(c => decisions.ResolveTraining(c.Option));
        handlers.Add<ChooseTownGateCommand>(c => decisions.ResolveTownGate(c.Town));
        handlers.Add<CompleteExchangeCommand>(_ => decisions.CompleteExchange());

        handlers.Add<HireAtMapTavernCommand>(c => shops.HireAtMapTavern(c.Player, c.Tavern, c.Hero));
        handlers.Add<MoveGarrisonStackCommand>(c => shops.MoveGarrisonStack(c.Player, c.Garrison, c.Slot, c.ToGarrison));
        handlers.Add<TakeLessonCommand>(c => shops.TakeLesson(c.Player, c.School, c.Skill));
        handlers.Add<BuyArtifactCommand>(c => shops.BuyArtifact(c.Player, c.Merchant, c.Slot));
        handlers.Add<RecruitAtRefugeeCampCommand>(c => shops.RecruitAtRefugeeCamp(c.Player, c.Camp, c.Count));
        handlers.Add<SacrificeCreaturesCommand>(c => shops.SacrificeCreatures(c.Player, c.Altar, c.Slot, c.Count));
        handlers.Add<SacrificeArtifactCommand>(c => shops.SacrificeArtifact(c.Player, c.Altar, c.Artifact));
        handlers.Add<UpgradeAtHillFortCommand>(c => shops.UpgradeAtHillFort(c.Player, c.Fort, c.Slot));
        handlers.Add<BuyWarMachineAtFactoryCommand>(c => shops.BuyAtFactory(c.Player, c.Factory, c.Slot));
        handlers.Add<RecruitAtDwellingCommand>(c => shops.RecruitAtDwelling(c.Player, c.Dwelling, c.Count));

        handlers.Add<AnswerMapEventCommand>(c => mapEvents.Answer(c.Option));
        handlers.Add<ChooseEventArmyCommand>(c => mapEvents.ChooseArmy(c.Keep));
    }
}
