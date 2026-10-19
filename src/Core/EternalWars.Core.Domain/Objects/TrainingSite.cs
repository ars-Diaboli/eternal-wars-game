using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Heroes;

namespace EternalWars.Core.Domain.Objects;

/// <summary>What a training site gives (ADR-068); the definition's <see cref="MapObjectDefinition.Training"/>.</summary>
public enum TrainingRule
{
    /// <summary>+1000 experience.</summary>
    LearningStone,

    /// <summary>2500 experience for 1500 gold, if the player wants.</summary>
    Gazebo,

    /// <summary>+2 to all four primary stats for a hero of level 10 or more.</summary>
    LibraryOfEnlightenment,

    /// <summary>Up to the next level, for 2500 gold or 5 crystal + 5 mercury + 5 sulfur, the player's choice, or not at all (MAP-9).</summary>
    TreeOfKnowledge,

    /// <summary>+2 Attack or +2 Defense, the player's choice.</summary>
    Arena,

    /// <summary>+2 Spell Power or +2 Knowledge, the player's choice.</summary>
    ColosseumOfTheMagi,
}

/// <summary>What the visitor of a training site may pick (<c>TrainingDecision</c>).</summary>
public enum TrainingOption
{
    /// <summary>Pay the site's gold price (Gazebo 1500, Tree of Knowledge 2500).</summary>
    PayGold,

    /// <summary>Pay 5 crystal, 5 mercury and 5 sulfur (Tree of Knowledge).</summary>
    PayMagicResources,

    Attack,
    Defense,
    SpellPower,
    Knowledge,
}

/// <summary>
/// A permanent power-up on the adventure map (ADR-068): Learning Stone, Gazebo, Library of Enlightenment, Tree of Knowledge, Arena, Colosseum of
/// the Magi. Each hero may train at each site once; a visit that pays nothing or is declined does not count.
/// </summary>
public sealed class TrainingSite(MapObjectId id, MapObjectDefinition definition, MapPosition position)
    : MapObject(id, definition, position, MapObjectKind.Training)
{
    public TrainingRule Rule => Definition.Training!.Value;
}

/// <summary>The numbers of <see cref="TrainingSite"/>s (ADR-068).</summary>
public static class TrainingRules
{
    public const int LearningStoneExperience = 1000;

    public const int GazeboExperience = 2500;

    public const int GazeboGold = 1500;

    public const int LibraryLevel = 10;

    public const int LibraryStats = 2;

    public const int TreeGold = 2500;

    public const int StatBonus = 2;

    /// <summary>The Tree of Knowledge's other price.</summary>
    public static ResourceSet TreeMagicResources { get; } = new(Crystal: 5, Mercury: 5, Sulfur: 5);

    /// <summary>What a payment option costs at a site.</summary>
    public static ResourceSet Price(TrainingRule rule, TrainingOption option) => (rule, option) switch
    {
        (TrainingRule.Gazebo, TrainingOption.PayGold) => new ResourceSet(Gold: GazeboGold),
        (TrainingRule.TreeOfKnowledge, TrainingOption.PayGold) => new ResourceSet(Gold: TreeGold),
        (TrainingRule.TreeOfKnowledge, TrainingOption.PayMagicResources) => TreeMagicResources,
        _ => ResourceSet.Empty,
    };

    /// <summary>The options a site offers (payments only when the player can afford them); empty when it gives without asking.</summary>
    public static IReadOnlyList<TrainingOption> Options(TrainingRule rule, ResourceSet resources) => rule switch
    {
        TrainingRule.Arena => [TrainingOption.Attack, TrainingOption.Defense],
        TrainingRule.ColosseumOfTheMagi => [TrainingOption.SpellPower, TrainingOption.Knowledge],
        TrainingRule.Gazebo or TrainingRule.TreeOfKnowledge =>
            [.. new[] { TrainingOption.PayGold, TrainingOption.PayMagicResources }
                .Where(o => Price(rule, o) != ResourceSet.Empty && resources.CanAfford(Price(rule, o)))],
        _ => [],
    };

    /// <summary>Whether the player may walk away from the offer (the paid ones); the stat choices must be made.</summary>
    public static bool CanDecline(TrainingRule rule) => rule is TrainingRule.Gazebo or TrainingRule.TreeOfKnowledge;

    /// <summary>Experience that takes the hero to the start of its next level (by experience, chosen or not).</summary>
    public static int ExperienceToNextLevel(Hero hero) =>
        ExperienceTable.For(ExperienceTable.LevelFor(hero.Experience) + 1) - hero.Experience;

    public static PrimaryStat? Stat(TrainingOption option) => option switch
    {
        TrainingOption.Attack => PrimaryStat.Attack,
        TrainingOption.Defense => PrimaryStat.Defense,
        TrainingOption.SpellPower => PrimaryStat.SpellPower,
        TrainingOption.Knowledge => PrimaryStat.Knowledge,
        _ => null,
    };
}
