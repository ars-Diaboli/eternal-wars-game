using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;

namespace EternalWars.Core.Domain.Battles;

/// <summary>A side's losses on the result window (ADR-314): its name and each creature it lost with how many, in the order the battle lists them.</summary>
public sealed record SideLosses(BattleSide Side, string Name, IReadOnlyList<BattleCasualty> Lost);

/// <summary>
/// The battle result's summary (ADR-314): the title for the screen's viewer, "Victory" or "Defeat" when exactly one side plays at this screen,
/// "&lt;Name&gt; wins" when both or neither do (a hot-seat battle between two people, a battle between computers, the replay viewer); the reason
/// under it; each side's losses, the attacker's first; the experience the winning hero gained (0 for none).
/// </summary>
public sealed record BattleSummary(string Title, string Reason, IReadOnlyList<SideLosses> Losses, int Experience)
{
    public static BattleSummary Of(BattleEnded ended, IReadOnlySet<BattleSide> playedHere, Func<BattleSide, string> sideName)
    {
        var winner = sideName(ended.Winner);
        var loser = sideName(ended.Winner.Opponent());
        var title = playedHere.Count == 1 ? playedHere.Contains(ended.Winner) ? "Victory" : "Defeat" : $"{winner} wins";
        var reason = ended.Reason switch
        {
            BattleEndReason.Retreated => $"{loser} retreated.",
            BattleEndReason.Surrendered => $"{loser} surrendered.",
            _ => $"{winner} wins the battle.", // a rout, or the round limit (the defender holds the field): not always a defeat by arms
        };
        var losses = new[] { BattleSide.Attacker, BattleSide.Defender }
            .Select(side => new SideLosses(side, sideName(side), Lost(ended, side)))
            .ToList();
        return new BattleSummary(title, reason, losses, ended.Experience);
    }

    /// <summary>
    /// The sides played at this screen: those whose owner is a person (quick combat or not) for whom <paramref name="playsHere"/> holds (every
    /// person on one computer when null). A neutral side and a computer player's never are.
    /// </summary>
    public static IReadOnlySet<BattleSide> SidesPlayedHere(GameState state, BattleState battle, Func<PlayerId, bool>? playsHere) =>
        new[] { BattleSide.Attacker, BattleSide.Defender }
            .Where(side => battle.Participant(side).Owner is { } owner
                           && state.Players.FirstOrDefault(p => p.Id == owner) is { IsHuman: true }
                           && (playsHere?.Invoke(owner) ?? true))
            .ToHashSet();

    /// <summary>The side's losses, one entry a creature (a creature lost from two stacks counted once).</summary>
    private static List<BattleCasualty> Lost(BattleEnded ended, BattleSide side) =>
        ended.Casualties.Where(c => c.Side == side && c.Lost > 0)
            .GroupBy(c => c.Creature)
            .Select(g => new BattleCasualty(side, g.Key, g.Sum(c => c.Lost)))
            .ToList();
}
