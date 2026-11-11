using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;

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

    /// <summary>
    /// Walks the hero onto the object's entrance, the way a player visits it; throws unless the hero gets there. A hero already standing on the
    /// entrance visits again with <see cref="Revisit"/>.
    /// </summary>
    public static MovementOutcome Visit(this GameSession session, HeroId hero, MapObject mapObject)
    {
        if (session.State.GetHero(hero).Position == mapObject.VisitPosition)
        {
            throw new InvalidOperationException($"The hero already stands on the entrance of {mapObject.Definition.Id}: revisit it.");
        }

        var outcome = session.MoveTo(hero, mapObject.VisitPosition);
        return outcome.Arrived ? outcome : throw new InvalidOperationException($"The hero stopped short of {mapObject.Definition.Id}: {outcome.StopReason}.");
    }

    /// <summary>Steps off the entrance the hero stands on, towards <paramref name="off"/>, and back on: another visit.</summary>
    public static void Revisit(this GameSession session, HeroId hero, Direction off = Direction.South)
    {
        foreach (var direction in new[] { off, (Direction)(((int)off + 4) % 8) })
        {
            if (session.Step(hero, direction) is var result && result != MoveResult.Moved)
            {
                throw new InvalidOperationException($"Could not step {direction}: {result}.");
            }
        }
    }

    public static void EndTurn(this GameSession session) =>
        session.Execute(new EndTurnCommand(session.State.CurrentPlayer.Id)).ThrowIfRejected();

    /// <summary>Every player ends turns until the calendar reaches <paramref name="day"/> (absolute; day 1 is the first).</summary>
    public static void PlayUntilDay(this GameSession session, int day)
    {
        while (session.State.Date.AbsoluteDay < day)
        {
            session.EndTurn();
        }
    }

    /// <summary>Every player ends turns until the next day begins.</summary>
    public static void NextDay(this GameSession session) => session.PlayUntilDay(session.State.Date.AbsoluteDay + 1);

    /// <summary>Answers every level-up offered right now with its first skill.</summary>
    public static void TakeLevelUps(this GameSession session)
    {
        while (session.State.PendingDecision is LevelUpDecision decision)
        {
            session.Execute(new ChooseSkillCommand(session.State.CurrentPlayer.Id, decision.Options[0])).ThrowIfRejected();
        }
    }

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
