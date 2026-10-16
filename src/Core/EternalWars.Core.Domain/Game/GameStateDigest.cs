using System.Security.Cryptography;
using System.Text;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Game;

/// <summary>
/// Fingerprint of everything that matters in a running game (calendar, RNG, players, heroes and armies, objects,
/// pending decision, battle). Two games with equal digests are in the same state; replays and the simulator
/// use it to prove determinism.
/// </summary>
public static class GameStateDigest
{
    public static string Compute(GameState state) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Describe(state))))[..16];

    /// <summary>The canonical text the digest is computed from (handy when two digests differ).</summary>
    public static string Describe(GameState state)
    {
        var text = new StringBuilder();
        Line($"date {state.Date} player {state.CurrentPlayerIndex} rng {state.RandomState} started {state.IsStarted}");
        foreach (var player in state.Players)
        {
            Line($"player {player.Id.Value} team {player.Team.Value} {player.Resources} defeated {player.IsDefeated} townless {player.TownlessSince?.AbsoluteDay}");
        }

        foreach (var hero in state.Heroes.OrderBy(h => h.Id.Value))
        {
            var army = Slots(hero.Army);
            Line($"hero {hero.Id.Value} owner {hero.Owner.Value} at {hero.Position} mp {hero.MovementPoints}/{hero.MaxMovementPoints} xp {hero.Experience} stats {hero.Attack}/{hero.Defense}/{hero.SpellPower}/{hero.Knowledge} level {hero.Level} skills {hero.Skills} machines {string.Join(',', hero.WarMachines.Order())} artifacts {hero.Artifacts} army {army}");
        }

        foreach (var mapObject in state.Objects.OrderBy(o => o.Id.Value))
        {
            var detail = mapObject switch
            {
                ResourcePile pile => $"amount {pile.Amount}",
                Mine mine => $"owner {mine.Owner?.Value} guard {mine.Guard}",
                TreasureChest chest => $"fixed {chest.FixedValues}",
                ArtifactPickup pickup => $"artifact {pickup.Artifact}",
                MonsterGuard monster => $"{monster.Creature}:{monster.Count} {monster.Disposition}",
                Town town => $"owner {town.Owner?.Value} built {Pairs(town.Buildings)} dwellings {Pairs(town.Dwellings)} available {Pairs(town.Available)} "
                             + $"garrison {Slots(town.Garrison)} last {town.LastBuilt?.AbsoluteDay}",
                _ => mapObject.TeleportTarget?.ToString() ?? "",
            };
            Line($"object {mapObject.Id.Value} {mapObject.Definition.Id} at {mapObject.Position} {detail}");
        }

        Line($"outcome {state.Outcome?.Winners.Count} {state.Outcome?.Date}");
        Line($"decision {state.PendingDecision}");
        if (state.Battle is { } battle)
        {
            Line($"battle {battle.Phase} width {battle.Width} round {battle.Round} active {battle.ActiveUnit?.Id}");
            foreach (var unit in battle.Units)
            {
                Line($"unit {unit.Id} {unit.Side} {unit.Creature.Id} {unit.Count}/{unit.TopHealth} at {unit.Position} shots {unit.ShotsLeft}");
            }
        }

        return text.ToString();

        void Line(FormattableString line) => text.Append(FormattableString.Invariant(line)).Append('\n');
    }

    private static string Slots(Armies.Army army) => string.Join(",", army.Slots.Select(s => s is null ? "-" : $"{s.Creature}:{s.Count}"));

    private static string Pairs(IReadOnlyDictionary<string, int> values) =>
        string.Join(",", values.OrderBy(v => v.Key, StringComparer.Ordinal).Select(v => FormattableString.Invariant($"{v.Key}:{v.Value}")));
}
