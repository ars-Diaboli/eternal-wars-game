using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;

namespace EternalWars.Core.Domain.Objects;

/// <summary>Which creature bank a definition is (ADR-068); the definition's <see cref="MapObjectDefinition.Bank"/>.</summary>
public enum BankRule
{
    Churchyard,
    Mansion,
    Ruins,
    DwarvenTreasury,
    ImpCache,
    MedusaStores,
    NagaBank,
}

/// <summary>
/// One row of a bank's table: how likely it is, the guard, and the reward for beating it. With <paramref name="Upgraded"/>, half the time a fifth
/// of each guard stack of its base creature is the upgraded creature instead.
/// </summary>
public sealed record BankTier(int Weight, IReadOnlyList<CreatureStack> Guard, ResourceSet Reward, ArtifactTier? Artifact = null, string? Upgraded = null);

/// <summary>
/// A creature bank on the adventure map (ADR-068): a guard that always fights, and a reward for whoever beats it. The tier is rolled when the game
/// starts (it is not shown and the map cannot fix it); a retreat or a lost battle leaves the guard intact. A plundered bank fills again, with a new
/// roll, <see cref="CreatureBankRules.RefillDays"/> days later. Its state is game state, not map data.
/// </summary>
public sealed class CreatureBank(MapObjectId id, MapObjectDefinition definition, MapPosition position)
    : MapObject(id, definition, position, MapObjectKind.CreatureBank)
{
    public BankRule Rule => Definition.Bank!.Value;

    /// <summary>The rolled guard; empty until the game starts and while the bank is plundered.</summary>
    public IReadOnlyList<CreatureStack> Guard { get; internal set; } = [];

    /// <summary>What beating the guard pays.</summary>
    public ResourceSet Reward { get; internal set; }

    /// <summary>The tier of the artifact beating the guard gives, if any.</summary>
    public ArtifactTier? RewardArtifact { get; internal set; }

    /// <summary>The day the bank was plundered; null while it is full.</summary>
    public GameDate? PlunderedOn { get; internal set; }

    /// <summary>Whether a visit means a fight.</summary>
    public bool IsGuarded => PlunderedOn is null && Guard.Count > 0;

    public override string ToString() => $"{base.ToString()} guard {string.Join(',', Guard)} plundered {PlunderedOn?.AbsoluteDay}";
}

/// <summary>The banks' tables (ADR-068); creature names follow the roster (Zombie → Ghoul, Power Lich → Arch Lich, …); the Dwarf and Battle Dwarf are neutrals.</summary>
public static class CreatureBankRules
{
    /// <summary>A plundered bank fills again this many days after (MAP-14).</summary>
    public const int RefillDays = 28;

    /// <summary>A later visit to a plundered Churchyard: morale for the next battle (MAP-7).</summary>
    public const int ChurchyardMorale = -1;

    /// <summary>The Ruins reveal the terrain within this many tiles on every visit.</summary>
    public const int RuinsRevealRadius = 12;

    private static IReadOnlyList<int> Usual { get; } = [30, 30, 30, 10];

    private static ResourceSet Gold(int gold) => new(Gold: gold);

    private static CreatureStack[] One(string creature, int count) => [new(creature, count)];

    public static IReadOnlyDictionary<BankRule, IReadOnlyList<BankTier>> Tables { get; } = new Dictionary<BankRule, IReadOnlyList<BankTier>>
    {
        [BankRule.Churchyard] = [new(100, One("necropolis.ghoul", 90), Gold(2500), ArtifactTier.Minor)],
        [BankRule.Mansion] = [.. new[] { (40, 2500, 2), (60, 5000, 3), (80, 7500, 4), (100, 10000, 5) }.Select((t, i) =>
            new BankTier(Usual[i], One("necropolis.vampire-lord", t.Item1), new ResourceSet(Gold: t.Item2, Crystal: t.Item3, Mercury: t.Item3, Sulfur: t.Item3), ArtifactTier.Major))],
        [BankRule.Ruins] = [.. new[] { (20, 3, 3, 10, 1, 1000), (30, 5, 5, 10, 1, 2000), (40, 7, 7, 20, 2, 3000), (50, 9, 9, 20, 3, 4000) }.Select((t, i) =>
            new BankTier(Usual[i], [new("necropolis.skeleton", t.Item1), new("necropolis.wight", t.Item2), new("necropolis.wraith", t.Item3),
                new("necropolis.skeleton-warrior", t.Item4), new("necropolis.arch-lich", t.Item5)], Gold(t.Item6)))],
        [BankRule.DwarvenTreasury] = Simple("neutral.dwarf", "neutral.battle-dwarf", [(50, 2500, 2), (75, 4000, 4), (100, 5000, 6), (150, 7500, 10)], ResourceType.Crystal),
        [BankRule.ImpCache] = Simple("inferno.imp", "inferno.familiar", [(100, 1000, 2), (150, 1500, 4), (200, 2000, 6), (300, 3000, 8)], ResourceType.Mercury),
        [BankRule.MedusaStores] = Simple("dungeon.medusa", "dungeon.putrid-medusa", [(15, 2000, 4), (30, 3000, 6), (40, 4000, 8), (50, 5000, 10)], ResourceType.Sulfur),
        [BankRule.NagaBank] = Simple("academy.naga", "academy.naga-queen", [(5, 3000, 0), (10, 6000, 0), (15, 9000, 0), (20, 12000, 0)], ResourceType.Gold),
    };

    private static IReadOnlyList<BankTier> Simple(string creature, string upgraded, (int Count, int Gold, int Resource)[] rows, ResourceType resource) =>
        [.. rows.Select((r, i) => new BankTier(Usual[i], One(creature, r.Count), Gold(r.Gold) + (resource == ResourceType.Gold ? ResourceSet.Empty : ResourceSet.Of(resource, r.Resource)), null, upgraded))];

    /// <summary>Rolls a bank's tier (and the upgraded fifth) into its guard and reward.</summary>
    internal static void Fill(CreatureBank bank, GameRandom random)
    {
        var tier = WeightedRoll.Pick([.. Tables[bank.Rule].Select(t => (t.Weight, t))], random);
        var guard = tier.Guard.ToList();
        if (tier.Upgraded is { } upgraded && random.Next(2) == 0)
        {
            guard = [.. guard.SelectMany(s => s.Count / 5 is > 0 and var fifth
                ? new[] { new CreatureStack(s.Creature, s.Count - fifth), new CreatureStack(upgraded, fifth) }
                : [s])];
        }

        bank.Guard = guard;
        bank.Reward = tier.Reward;
        bank.RewardArtifact = tier.Artifact;
        bank.PlunderedOn = null;
    }

    /// <summary>Whether a plundered bank fills again on <paramref name="date"/>.</summary>
    public static bool RefillsOn(CreatureBank bank, GameDate date) =>
        bank.PlunderedOn is { } plundered && date.AbsoluteDay >= plundered.AbsoluteDay + RefillDays;
}
