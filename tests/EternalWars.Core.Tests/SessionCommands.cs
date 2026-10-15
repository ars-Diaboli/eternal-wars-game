using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;

namespace EternalWars.Core.Tests;

/// <summary>
/// Short forms of the commands tests issue, always through <see cref="GameSession.Execute"/> like every
/// front-end. Refused commands throw <see cref="InvalidOperationException"/>.
/// </summary>
internal static class SessionCommands
{
    public static MoveResult Step(this GameSession session, HeroId hero, Direction direction) =>
        (MoveResult)session.Execute(new StepHeroCommand(OwnerOf(session, hero), hero, direction)).ThrowIfRejected().Outcome!;

    public static MovementOutcome MoveTo(this GameSession session, HeroId hero, MapPosition target) =>
        (MovementOutcome)session.Execute(new MoveHeroCommand(OwnerOf(session, hero), hero, target)).ThrowIfRejected().Outcome!;

    public static void EndTurn(this GameSession session) =>
        session.Execute(new EndTurnCommand(session.State.CurrentPlayer.Id)).ThrowIfRejected();

    public static void ResolveChest(this GameSession session, TreasureChestReward reward) =>
        session.Execute(new ResolveTreasureChestCommand(session.State.CurrentPlayer.Id, reward)).ThrowIfRejected();

    public static void CompleteExchange(this GameSession session) =>
        session.Execute(new CompleteExchangeCommand(session.State.CurrentPlayer.Id)).ThrowIfRejected();

    public static void RespondToGuard(this GameSession session, bool accept) =>
        session.Execute(new RespondToGuardCommand(session.State.CurrentPlayer.Id, accept)).ThrowIfRejected();

    /// <summary>Quick combat on behalf of whoever commands the active side (or the attacker while choosing width).</summary>
    public static void AutoResolve(this GameSession session)
    {
        var battle = session.State.Battle ?? throw new InvalidOperationException("No battle.");
        var player = battle.Attacker.Owner ?? battle.Defender.Owner!.Value;
        session.Execute(new AutoResolveBattleCommand(player)).ThrowIfRejected();
    }

    /// <summary>The player commanding the battle's active unit.</summary>
    public static PlayerId ActivePlayer(this GameSession session)
    {
        var battle = session.State.Battle ?? throw new InvalidOperationException("No battle.");
        return session.Battles.Commander(battle.ActiveUnit!.Side) ?? throw new InvalidOperationException("The AI commands the active unit.");
    }

    public static BattleUnit Active(this GameSession session) => session.State.Battle!.ActiveUnit!;

    private static PlayerId OwnerOf(GameSession session, HeroId hero) => session.State.GetHero(hero).Owner;
}
