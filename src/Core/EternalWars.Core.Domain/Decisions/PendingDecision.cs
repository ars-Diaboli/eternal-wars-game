using EternalWars.Core.Domain.Common;

namespace EternalWars.Core.Domain.Decisions;

/// <summary>
/// A choice the game is waiting for before it can continue (treasure chest, exchange, guard offer, battle,
/// level-up; seer huts later). While one is pending, movement and End Turn are refused.
/// </summary>
public abstract record PendingDecision(HeroId Hero);

public enum TreasureChestReward
{
    Gold,
    Experience,
}

public sealed record TreasureChestDecision(HeroId Hero, MapObjectId Chest, int Gold, int Experience) : PendingDecision(Hero);

/// <summary>
/// A training site asks what the visitor takes (ADR-068): a stat (Arena, Colosseum) or a way to pay (Gazebo, Tree of Knowledge). <c>ChooseTrainingCommand</c>
/// answers; with <paramref name="CanDecline"/> it may also answer nothing and leave.
/// </summary>
public sealed record TrainingDecision(HeroId Hero, MapObjectId Site, IReadOnlyList<Objects.TrainingOption> Options, bool CanDecline) : PendingDecision(Hero)
{
    public bool Equals(TrainingDecision? other) =>
        other is not null && Hero == other.Hero && Site == other.Site && CanDecline == other.CanDecline && Options.SequenceEqual(other.Options);

    public override int GetHashCode() => HashCode.Combine(Hero, Site, Options.Count, CanDecline);

    public override string ToString() => $"TrainingDecision {{ Hero = {Hero}, Site = {Site}, Options = {string.Join(" | ", Options)}, CanDecline = {CanDecline} }}";
}

/// <summary>A warehouse offers the visitor its resource or gold (ADR-068): <c>ChooseResourceRewardCommand</c> picks one of <paramref name="Options"/>.</summary>
public sealed record ResourceChoiceDecision(HeroId Hero, MapObjectId Provider, IReadOnlyList<Economy.ResourceSet> Options) : PendingDecision(Hero)
{
    // Records compare lists by reference; two decisions with the same options are equal.
    public bool Equals(ResourceChoiceDecision? other) =>
        other is not null && Hero == other.Hero && Provider == other.Provider && Options.SequenceEqual(other.Options);

    public override int GetHashCode() => HashCode.Combine(Hero, Provider, Options.Count);

    public override string ToString() => $"ResourceChoiceDecision {{ Hero = {Hero}, Provider = {Provider}, Options = {string.Join(" | ", Options)} }}";
}

/// <summary>
/// Two own heroes met: the exchange screen is open. Stacks move with <c>MoveStackCommand</c> while it is
/// open (artifacts in Phase 3); closing the screen resolves the decision.
/// </summary>
public sealed record HeroExchangeDecision(HeroId Hero, HeroId Other) : PendingDecision(Hero);

public enum GuardOffer
{
    /// <summary>The neutrals offer to join the hero's army for free.</summary>
    Join,

    /// <summary>The neutrals try to flee; the hero lets them go or gives chase.</summary>
    Flee,
}

/// <summary>A neutral guard reacted to the hero (ADR-040). Accept = take the offer, decline = fight.</summary>
public sealed record GuardEncounterDecision(HeroId Hero, MapObjectId Guard, GuardOffer Offer, string Creature, int Count)
    : PendingDecision(Hero);

/// <summary>
/// A battle is being fought (<c>GameState.Battle</c>). It resolves itself when the battle ends; the players
/// command their sides through battle commands meanwhile.
/// </summary>
public sealed record BattleDecision(HeroId Hero) : PendingDecision(Hero);

/// <summary>
/// A hero reached <paramref name="Level"/>: the owner picks one of <paramref name="Options"/> (skill ids) to raise by one
/// level (ADR-048). <paramref name="Stat"/> is the primary stat the level raises, rolled with the offers (ADR-055).
/// </summary>
public sealed record LevelUpDecision(HeroId Hero, int Level, IReadOnlyList<string> Options, Heroes.PrimaryStat Stat = Heroes.PrimaryStat.Attack)
    : PendingDecision(Hero);
