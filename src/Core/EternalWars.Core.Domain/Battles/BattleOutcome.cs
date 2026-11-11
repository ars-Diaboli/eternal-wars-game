using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;

namespace EternalWars.Core.Domain.Battles;

/// <summary>
/// What a finished battle comes to, read before anything changes: <see cref="BattleAftermath.Finish"/> applies it and auto combat's preview
/// (<see cref="BattleCommandHandler.Preview"/>) shows it, so the two cannot disagree.
/// </summary>
/// <param name="Winner">The side that won.</param>
/// <param name="Reason">How the battle ended.</param>
/// <param name="Casualties">What each side lost (summoned creatures do not count).</param>
/// <param name="Experience">The winning hero's experience: 0 without a winning hero and in an arena map's battle (ADR-097).</param>
/// <param name="Loot">The defeated hero's artifacts the winning hero takes, best first (empty unless a hero beat a hero that did not flee).</param>
internal sealed record BattleOutcome(
    BattleSide Winner, BattleEndReason Reason, IReadOnlyList<BattleCasualty> Casualties, int Experience, IReadOnlyList<string> Loot)
{
    /// <summary>The outcome of <paramref name="battle"/>, which has a winner.</summary>
    public static BattleOutcome Of(GameState state, BattleState battle)
    {
        var winner = battle.Winner!.Value;
        var reason = battle.EndReason!.Value;
        var winning = battle.Participant(winner);
        var losing = battle.Participant(winner.Opponent());
        var arena = state.Arena is { Result: ArenaResult.Fighting };
        var experience = winning.Hero is null || arena ? 0 : ExperienceOf(battle);
        IReadOnlyList<string> loot = reason == BattleEndReason.Defeated && winning.Hero is not null && losing.Hero is { } loserId
            ? ArtifactValue.BestFirst(state.Artifacts, state.GetHero(loserId).Artifacts.AllWorn.Select(w => w.Id).Concat(state.GetHero(loserId).Artifacts.Backpack)).ToList()
            : [];
        return new BattleOutcome(winner, reason, CasualtiesOf(battle), experience, loot);
    }

    /// <summary>What a finished battle is worth to the winner: the health of the creatures it killed, and a defeated hero.</summary>
    public static int ExperienceOf(BattleState battle)
    {
        var loser = battle.Winner!.Value.Opponent();
        var killedHealth = battle.Units.Where(u => u.Owner == loser && !u.IsSummoned).Sum(u => (long)(u.InitialCount - u.KeptCount) * u.Creature.Health);
        var heroDefeated = battle.Participant(loser).Hero is not null && battle.EndReason == BattleEndReason.Defeated;
        return (int)Math.Min(int.MaxValue, killedHealth + (heroDefeated ? BattleAftermath.HeroDefeatExperience : 0));
    }

    public static List<BattleCasualty> CasualtiesOf(BattleState battle) => battle.Units
        .Where(u => !u.IsSummoned)
        .GroupBy(u => (Side: u.Owner, u.Creature.Id))
        .Select(g => new BattleCasualty(g.Key.Side, g.Key.Id, g.Sum(u => u.InitialCount - u.KeptCount)))
        .Where(c => c.Lost > 0)
        .ToList();

    /// <summary>The army that fought for <paramref name="side"/> (a hero's, a town's or a map garrison's) keeps what lived, slot by slot.</summary>
    /// <remarks>
    /// On the winning side, summoned creatures that stay (Summon Demons III, ADR-177) then join it: a stack of their creature or a free slot, by
    /// id; those with neither are lost.
    /// </remarks>
    public static void KeepSurvivors(BattleState battle, BattleSide side, Army army)
    {
        foreach (var (slot, stack) in army.Stacks.ToList())
        {
            var survivors = battle.Units.Where(u => u.Owner == side && u.SourceSlot == slot).Sum(u => u.KeptCount);
            army.Set(slot, survivors > 0 ? new CreatureStack(stack.Creature, survivors) : null);
        }

        if (battle.Winner != side)
        {
            return;
        }

        foreach (var summoned in battle.Units.Where(u => u.Owner == side && u.IsAlive && u.IsSummoned && u.Stays && !u.IsCopy).OrderBy(u => u.Id))
        {
            army.TryAdd(new CreatureStack(summoned.Creature.Id, summoned.Count));
        }
    }
}
