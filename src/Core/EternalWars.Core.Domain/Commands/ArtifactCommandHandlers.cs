using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Artifacts;

namespace EternalWars.Core.Domain.Commands;

/// <summary>A hero's equipment: wearing, assembling and giving artifacts, and handing over war machines.</summary>
internal static class ArtifactCommandHandlers
{
    internal static void Register(CommandHandlers handlers, ArtifactService artifacts, ArmyService armies)
    {
        handlers.Add<EquipArtifactCommand>(c => artifacts.Equip(c.Player, c.Hero, c.Artifact));
        handlers.Add<UnequipArtifactCommand>(c => artifacts.Unequip(c.Player, c.Hero, c.Artifact));
        handlers.Add<AssembleRelicCommand>(c => artifacts.Assemble(c.Player, c.Hero, c.Relic));
        handlers.Add<TakeRelicApartCommand>(c => artifacts.TakeApart(c.Player, c.Hero, c.Relic));
        handlers.Add<GiveArtifactCommand>(c => artifacts.Give(c.Player, c.FromHero, c.Artifact, c.ToHero, c.Worn, c.Wear));
        handlers.Add<GiveWarMachineCommand>(c => armies.GiveWarMachine(c.Player, c.FromHero, c.Slot, c.ToHero));
    }
}
