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

    /// <summary>The undead bank, called Ruins until ADR-070.</summary>
    Crypt,
    DwarvenTreasury,
    ImpCache,
    MedusaStores,
    NagaBank,

    /// <summary>The second catalogue's banks (ADR-069, ADR-070); Ruins is the orc bank now.</summary>
    Ruins,
    Lair,
    Keep,
    Hideout,
    ForestGlen,
    DragonCity,
}

/// <summary>
/// One row of a bank's table: how likely it is, the guard, and the reward for beating it. With <paramref name="Upgraded"/>, half the time a fifth
/// of the first guard stack (its base creature) is the upgraded creature instead.
/// </summary>
public sealed record BankTier(int Weight, IReadOnlyList<CreatureStack> Guard, ResourceSet Reward, ArtifactTier? Artifact = null, string? Upgraded = null)
{
    /// <summary>Artifacts the reward brings besides <see cref="Artifact"/> (Dragon City's Major artifacts, ADR-069).</summary>
    public IReadOnlyList<ArtifactTier> MoreArtifacts { get; init; } = [];
}

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

    /// <summary>The tiers of further artifacts beating the guard gives (Dragon City).</summary>
    public IReadOnlyList<ArtifactTier> MoreArtifacts { get; internal set; } = [];

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
    public const int CryptRevealRadius = 12;

    private static IReadOnlyList<int> Usual { get; } = [30, 30, 30, 10];

    private static ResourceSet Gold(int gold) => new(Gold: gold);

    private static ResourceSet Rare(int gold, int each) => new(Gold: gold, Crystal: each, Mercury: each, Sulfur: each);

    /// <summary>
    /// The faction re-draft (ADR-070, MAP-22; drafts, ADR-112): every bank's guard is 2–3 creature types of one faction (neutral banks: neutral creatures),
    /// in four rows at 30 / 30 / 30 / 10 %, the reward about half the guard's gold value. The upgraded fifth counts as one of the types.
    /// </summary>
    public static IReadOnlyDictionary<BankRule, IReadOnlyList<BankTier>> Tables { get; } = new Dictionary<BankRule, IReadOnlyList<BankTier>>
    {
        [BankRule.Churchyard] = Bank(["necropolis.skeleton", "necropolis.ghoul"],
            [([40, 30], Gold(3000), ArtifactTier.Minor), ([60, 40], Gold(4500), ArtifactTier.Minor), ([80, 50], Gold(6000), ArtifactTier.Minor), ([100, 70], Gold(8000), ArtifactTier.Major)]),
        [BankRule.Mansion] = Bank(["necropolis.vampire", "necropolis.vampire-lord"],
            [([25, 15], Rare(8000, 2), ArtifactTier.Major), ([35, 25], Rare(12000, 3), ArtifactTier.Major), ([45, 35], Rare(16000, 4), ArtifactTier.Major), ([60, 50], Rare(22000, 5), ArtifactTier.Major)]),
        [BankRule.Crypt] = Bank(["necropolis.skeleton", "necropolis.wight", "necropolis.lich"],
            [([30, 5, 1], Gold(2000), null), ([40, 8, 2], Gold(3500), null), ([50, 10, 3], Gold(5000), null), ([60, 15, 4], Gold(7000), null)]),
        [BankRule.DwarvenTreasury] = Bank(["neutral.dwarf", "neutral.thane"],
            [([40, 5], Gold(3000) + new ResourceSet(Crystal: 2), null), ([60, 8], Gold(4500) + new ResourceSet(Crystal: 4), null),
             ([80, 12], Gold(6000) + new ResourceSet(Crystal: 6), null), ([110, 18], Gold(8500) + new ResourceSet(Crystal: 10), null)], "neutral.battle-dwarf"),
        [BankRule.ImpCache] = Bank(["inferno.imp", "inferno.horned-demon"],
            [([80, 20], Gold(1500) + new ResourceSet(Mercury: 2), null), ([120, 30], Gold(2000) + new ResourceSet(Mercury: 4), null),
             ([160, 40], Gold(3000) + new ResourceSet(Mercury: 6), null), ([240, 60], Gold(4000) + new ResourceSet(Mercury: 8), null)], "inferno.familiar"),
        [BankRule.MedusaStores] = Bank(["dungeon.medusa", "dungeon.minotaur"],
            [([12, 5], Gold(2000) + new ResourceSet(Sulfur: 4), null), ([20, 8], Gold(3000) + new ResourceSet(Sulfur: 6), null),
             ([28, 12], Gold(4000) + new ResourceSet(Sulfur: 8), null), ([38, 16], Gold(5000) + new ResourceSet(Sulfur: 10), null)], "dungeon.putrid-medusa"),
        [BankRule.NagaBank] = Bank(["academy.naga", "academy.gargoyle"],
            [([4, 15], Gold(3500), null), ([8, 20], Gold(6500), null), ([12, 30], Gold(10000), null), ([16, 40], Gold(13000), null)], "academy.naga-queen"),
        [BankRule.Ruins] = Bank(["stronghold.orc", "stronghold.goblin", "stronghold.ogre"],
            [([15, 20, 0], Gold(2000), null), ([20, 30, 2], Gold(3000), null), ([30, 40, 3], Gold(4000), null), ([40, 50, 5], Gold(6000), null)]),
        [BankRule.Lair] = Bank(["dungeon.spectator", "dungeon.troglodyte"],
            [([3, 20], Gold(2000), ArtifactTier.Minor), ([5, 25], Gold(3000), ArtifactTier.Minor), ([7, 30], Gold(4000), ArtifactTier.Minor), ([10, 40], Gold(6000), ArtifactTier.Major)], "dungeon.beholder"),
        [BankRule.Keep] = Bank(["haven.pikeman", "haven.archer", "haven.griffin"],
            [([20, 10, 0], Gold(1500), ArtifactTier.Minor), ([30, 15, 0], Gold(2500), ArtifactTier.Minor), ([40, 20, 3], Gold(3500), ArtifactTier.Minor), ([60, 30, 5], Gold(5000), ArtifactTier.Major)]),
        [BankRule.Hideout] = Bank(["neutral.bandit", "neutral.troll"],
            [([12, 2], Gold(1500), null), ([20, 3], Gold(2500), null), ([28, 4], Gold(3500), null), ([40, 6], Gold(5000), null)]),
        [BankRule.ForestGlen] = Bank(["neutral.wolf", "neutral.mammoth"],
            [([25, 1], Gold(1500) + new ResourceSet(Lumber: 5), null), ([40, 1], Gold(2000) + new ResourceSet(Lumber: 10), null),
             ([55, 2], Gold(3000) + new ResourceSet(Lumber: 15), null), ([80, 3], Gold(4500) + new ResourceSet(Lumber: 20), null)], "neutral.dire-wolf"),
        [BankRule.DragonCity] = [.. Bank(["dragon-utopia.black-dragon", "dragon-utopia.red-dragon", "dragon-utopia.green-dragon"],
            [([2, 0, 2], Gold(10000), ArtifactTier.Relic), ([3, 2, 0], Gold(15000), ArtifactTier.Relic), ([4, 2, 2], Gold(20000), ArtifactTier.Relic), ([5, 3, 3], Gold(30000), ArtifactTier.Relic)])
            .Select((t, i) => t with { MoreArtifacts = [.. Enumerable.Repeat(ArtifactTier.Major, new[] { 1, 1, 2, 3 }[i])] })],
    };

    /// <summary>A bank's four rows: the counts go with <paramref name="creatures"/> (0 leaves a type out of that row).</summary>
    private static IReadOnlyList<BankTier> Bank(string[] creatures, (int[] Counts, ResourceSet Reward, ArtifactTier? Artifact)[] rows, string? upgraded = null) =>
        [.. rows.Select((r, i) => new BankTier(Usual[i], [.. creatures.Zip(r.Counts).Where(c => c.Second > 0).Select(c => new CreatureStack(c.First, c.Second))], r.Reward, r.Artifact, upgraded))];

    /// <summary>Rolls a bank's tier (and the upgraded fifth of its first stack) into its guard and reward.</summary>
    internal static void Fill(CreatureBank bank, GameRandom random)
    {
        var tier = WeightedRoll.Pick([.. Tables[bank.Rule].Select(t => (t.Weight, t))], random);
        var guard = tier.Guard.ToList();
        if (tier.Upgraded is { } upgraded && random.Next(2) == 0 && guard[0].Count / 5 is > 0 and var fifth)
        {
            guard = [new CreatureStack(guard[0].Creature, guard[0].Count - fifth), new CreatureStack(upgraded, fifth), .. guard.Skip(1)];
        }

        bank.Guard = guard;
        bank.Reward = tier.Reward;
        bank.RewardArtifact = tier.Artifact;
        bank.MoreArtifacts = tier.MoreArtifacts;
        bank.PlunderedOn = null;
    }

    /// <summary>Whether a plundered bank fills again on <paramref name="date"/>.</summary>
    public static bool RefillsOn(CreatureBank bank, GameDate date) =>
        bank.PlunderedOn is { } plundered && date.AbsoluteDay >= plundered.AbsoluteDay + RefillDays;
}
