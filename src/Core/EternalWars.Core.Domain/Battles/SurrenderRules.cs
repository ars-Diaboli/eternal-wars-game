using EternalWars.Core.Domain.Game;

namespace EternalWars.Core.Domain.Battles;

/// <summary>
/// Leaving a battle early (ADR-070, CMB-1; built ADR-081). A hero may <b>retreat</b> from any battle: its army is lost, the hero waits in
/// its player's taverns. It may <b>surrender</b> to a player (never to neutrals) for half the gold cost of its surviving creatures, paid to
/// that player: it keeps its army. Computer players retreat when their army is almost beaten; they do not surrender.
/// </summary>
public static class SurrenderRules
{
    /// <summary>The AI retreats once its army is worth this share of the enemy's or less (tuning, combat.md).</summary>
    public const double RetreatBelowShare = 0.2;

    /// <summary>Half the gold cost of the side's surviving creatures (war machines are free), rounded up.</summary>
    public static int Price(BattleState battle, BattleSide side) =>
        (int)Math.Min(int.MaxValue, (battle.LivingArmyOf(side).Sum(u => (long)u.Count * u.Creature.Cost.Gold) + 1) / 2);

    /// <summary>Why the side cannot surrender now, or null when it can.</summary>
    public static string? Check(GameState state, BattleState battle, BattleSide side)
    {
        var participant = battle.Participant(side);
        if (participant.Hero is null || participant.Owner is not { } owner)
        {
            return "Only a hero can surrender.";
        }

        if (battle.Participant(side.Opponent()).Owner is null)
        {
            return "Neutral creatures do not take surrenders.";
        }

        var price = Price(battle, side);
        return state.GetPlayer(owner).Resources.Gold >= price ? null : $"Surrendering costs {price} gold.";
    }

    /// <summary>
    /// Whether a computer player's hero gives the battle up (ADR-070): its army is worth at most <see cref="RetreatBelowShare"/> of the
    /// enemy's. War machines do not count.
    /// </summary>
    public static bool ShouldRetreat(BattleState battle, BattleSide side) =>
        battle.Participant(side).Hero is not null && Worth(battle, side) <= Worth(battle, side.Opponent()) * RetreatBelowShare;

    private static long Worth(BattleState battle, BattleSide side) => battle.LivingArmyOf(side).Sum(u => (long)u.Count * u.Creature.Value);
}
