using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Magic;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Turns;

namespace EternalWars.Core.Domain.Commands;

/// <summary>The adventure map's commands: moving, ending the turn, a guard's offer, a level-up, a hero's army, adventure spells and digging.</summary>
internal static class AdventureCommandHandlers
{
    internal static void Register(
        CommandHandlers handlers, HeroMovementService movement, TurnService turns, GuardService guards, ProgressionService progression, ArmyService armies,
        AdventureMagicService magic, GrailService grail)
    {
        handlers.AddWithOutcome<StepHeroCommand>(c => movement.TryStep(c.Hero, c.Direction));
        handlers.AddWithOutcome<MoveHeroCommand>(c => movement.MoveTo(c.Hero, c.Target));
        handlers.Add<EndTurnCommand>(_ => turns.EndTurn());
        handlers.Add<RespondToGuardCommand>(c => guards.Respond(c.Accept));
        handlers.Add<ChooseSkillCommand>(c => progression.Choose(c.Skill));
        handlers.Add<MoveStackCommand>(c => armies.MoveStack(c.Player, c.FromHero, c.FromSlot, c.ToHero, c.ToSlot, c.Count));
        handlers.Add<CastAdventureSpellCommand>(c => magic.Cast(c.Player, c.Hero, c.Spell, c.Target));
        handlers.Add<DigCommand>(c => grail.Dig(c.Hero));
    }
}
