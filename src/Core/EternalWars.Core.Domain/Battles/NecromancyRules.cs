using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Battles;

/// <summary>Creatures Necromancy raised after a victory, as they joined the army.</summary>
public sealed record RaisedUndead(string Creature, int Count);

/// <summary>
/// Necromancy (ADR-069, ADR-072, MAG-7, draft numbers): after a victory, a hero with Death attunement raises 5 / 10 / 15 / 20 % of the hit
/// points of the enemy creatures slain as Necropolis undead, +5 % when its owner has a Necromancy Amplifier. The level caps the undead tier
/// (<see cref="UndeadTier"/>); a tier's hit points are split between its two lines in a random proportion; constructs, elementals and war
/// machines do not count, nor do the hero's own losses.
/// </summary>
public static class NecromancyRules
{
    public const int PercentPerLevel = 5;

    /// <summary>What the Necromancy Amplifier adds for every hero of its owner with the skill; more amplifiers add nothing (ADR-070).</summary>
    public const int AmplifierPercent = 5;

    /// <summary>The Necropolis building that amplifies Necromancy (ADR-070, TOWN-6), the first town-specific building.</summary>
    public const string Amplifier = "necromancy-amplifier";

    /// <summary>Necromancy comes with Death attunement (the Necropolis school).</summary>
    public static string Skill { get; } = ManaRules.AttunementSkill(TownIds.Necropolis);

    /// <summary>The share of the slain hit points raised, in percent, with the artifacts' Necromancy points (ADR-111); none without the skill.</summary>
    public static int Percent(SkillLevel level, bool amplified, int artifactPercent = 0) =>
        level == SkillLevel.None ? 0 : (PercentPerLevel * (int)level) + (amplified ? AmplifierPercent : 0) + artifactPercent;

    /// <summary>Whether the player owns a town with the Necromancy Amplifier (a captured one helps its new owner).</summary>
    public static bool IsAmplified(GameState state, PlayerId player) =>
        state.ObjectsOf<Town>().Any(t => t.Owner == player && t.Level(Amplifier) > 0);

    /// <summary>
    /// The undead tier a slain enemy of <paramref name="enemyTier"/> counts towards: Basic all → 1; Advanced 1–3 → 1, 4–5 → 2; Expert 1–2 → 1,
    /// 3–4 → 2, 5 → 3; Master 1–2 → 1, 3 → 2, 4 → 3, 5 → 4.
    /// </summary>
    public static int UndeadTier(SkillLevel level, int enemyTier) => level switch
    {
        SkillLevel.Basic => 1,
        SkillLevel.Advanced => enemyTier <= 3 ? 1 : 2,
        SkillLevel.Expert => enemyTier <= 2 ? 1 : enemyTier <= 4 ? 2 : 3,
        _ => enemyTier <= 2 ? 1 : enemyTier - 1,
    };

    /// <summary>Constructs and elementals leave nothing to raise; enemy undead do (ADR-072).</summary>
    public static bool Counts(CreatureDefinition creature) =>
        !creature.HasTag(CreatureTags.Construct) && !creature.HasTag(CreatureTags.Elemental);

    /// <summary>
    /// The base undead raised from the slain enemies: per undead tier the counted hit points × the share, split between the tier's lines in
    /// a random proportion (game RNG), each part divided by its creature's hit points, rounded down. Lines in tier and id order.
    /// </summary>
    public static IReadOnlyList<RaisedUndead> Raise(
        IEnumerable<(CreatureDefinition Creature, int Slain)> slain, SkillLevel level, bool amplified, CreatureCatalog creatures, GameRandom random,
        int artifactPercent = 0)
    {
        var percent = Percent(level, amplified, artifactPercent);
        if (percent == 0)
        {
            return [];
        }

        var health = new SortedDictionary<int, long>();
        foreach (var (creature, count) in slain.Where(s => s.Slain > 0 && Counts(s.Creature)))
        {
            var tier = UndeadTier(level, creature.Tier);
            health[tier] = health.GetValueOrDefault(tier) + ((long)count * creature.Health);
        }

        var raised = new List<RaisedUndead>();
        foreach (var (tier, total) in health)
        {
            var pool = total * percent / 100;
            var lines = creatures.BaseCreaturesOf(TownIds.Necropolis).Where(c => c.Tier == tier).ToList();
            if (pool <= 0 || lines.Count == 0)
            {
                continue;
            }

            var shares = new long[lines.Count];
            shares[0] = pool;
            if (lines.Count > 1)
            {
                // Two lines (there are never more): the first gets a random 0–100 % of the pool, the second the rest.
                shares[0] = pool * random.Next(101) / 100;
                shares[1] = pool - shares[0];
            }

            for (var i = 0; i < lines.Count; i++)
            {
                var count = (int)Math.Min(int.MaxValue, shares[i] / lines[i].Health);
                if (count > 0)
                {
                    raised.Add(new RaisedUndead(lines[i].Id, count));
                }
            }
        }

        return raised;
    }
}
