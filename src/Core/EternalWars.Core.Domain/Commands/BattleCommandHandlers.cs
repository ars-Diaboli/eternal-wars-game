using EternalWars.Core.Domain.Battles;

namespace EternalWars.Core.Domain.Commands;

/// <summary>The battle's commands (<see cref="IBattleCommand"/>) and the player's auto combat setting.</summary>
internal static class BattleCommandHandlers
{
    internal static void Register(CommandHandlers handlers, BattleService battles)
    {
        handlers.Add<ChooseBattleWidthCommand>(c => battles.ChooseWidth(c.Player, c.Width));
        handlers.Add<BattleDeployCommand>(c => battles.Redeploy(c.Player, c.Unit, c.To));
        handlers.Add<BattleStartCommand>(c => battles.EndDeployment(c.Player));
        handlers.Add<BattleMoveCommand>(c => battles.Act(c.Player, new MoveAction(c.To)));
        handlers.Add<BattleAttackCommand>(c => battles.Act(c.Player, new AttackAction(c.Target, c.From)));
        handlers.Add<BattleShootCommand>(c => battles.Act(c.Player, new ShootAction(c.Target)));
        handlers.Add<BattleShootStructureCommand>(c => battles.Act(c.Player, new ShootStructureAction(c.Structure)));
        handlers.Add<BattleAttackStructureCommand>(c => battles.Act(c.Player, new AttackStructureAction(c.Structure, c.From)));
        handlers.Add<BattleCreatureCastCommand>(c => battles.Act(c.Player, new CreatureCastAction(c.Target, c.Cell)));
        handlers.Add<BattleWarcryCommand>(c => battles.UseWarcry(c.Player, c.Warcry));
        handlers.Add<BattleCastCommand>(c => battles.Cast(c.Player, c.Spell, c.Target, c.Cell, c.Second));
        handlers.Add<BattleWaitCommand>(c => battles.Act(c.Player, new WaitAction()));
        handlers.Add<BattleDefendCommand>(c => battles.Act(c.Player, new DefendAction()));
        handlers.Add<BattleRetreatCommand>(c => battles.Retreat(c.Player));
        handlers.Add<BattleSurrenderCommand>(c => battles.Surrender(c.Player));
        handlers.Add<AutoResolveBattleCommand>(c => battles.AutoResolve(c.Player));
        handlers.Add<SetAutoCombatCommand>(c => battles.SetAutoCombat(c.Player, c.Mode));
    }
}
