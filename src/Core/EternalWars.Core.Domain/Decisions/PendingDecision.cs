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

/// <summary>A deposit offers to become its mine for <paramref name="Price"/> (ADR-070, MAP-21): <c>AnswerDepositCommand</c>.</summary>
public sealed record DepositDecision(HeroId Hero, MapObjectId Deposit, Economy.ResourceSet Price) : PendingDecision(Hero);

/// <summary>A cartographer offers the map of a level for <paramref name="Price"/> gold (ADR-079): <c>AnswerCartographerCommand</c>.</summary>
public sealed record CartographerDecision(HeroId Hero, MapObjectId Cartographer, int Level, int Price) : PendingDecision(Hero);

/// <summary>A sphinx asks a riddle (ADR-079): <c>AnswerSphinxCommand</c> picks one of the three <paramref name="Options"/>.</summary>
public sealed record SphinxDecision(HeroId Hero, MapObjectId Sphinx, string Question, IReadOnlyList<string> Options, int Correct) : PendingDecision(Hero)
{
    public bool Equals(SphinxDecision? other) =>
        other is not null && Hero == other.Hero && Sphinx == other.Sphinx && Question == other.Question && Correct == other.Correct && Options.SequenceEqual(other.Options);

    public override int GetHashCode() => HashCode.Combine(Hero, Sphinx, Question, Correct);

    public override string ToString() => $"SphinxDecision {{ Hero = {Hero}, Sphinx = {Sphinx}, Question = {Question}, Options = {string.Join(" | ", Options)} }}";
}

/// <summary>A Pandora's Box asks whether to open it (ADR-100); the contents and any guard stay hidden. <c>AnswerPandorasBoxCommand</c> answers.</summary>
public sealed record PandorasBoxDecision(HeroId Hero, MapObjectId Box) : PendingDecision(Hero);

/// <summary>A witch hut offers up to two skills (ADR-079): <c>ChooseWitchHutSkillCommand</c> takes one, or none to leave.</summary>
public sealed record WitchHutDecision(HeroId Hero, MapObjectId Hut, IReadOnlyList<string> Options) : PendingDecision(Hero)
{
    public bool Equals(WitchHutDecision? other) =>
        other is not null && Hero == other.Hero && Hut == other.Hut && Options.SequenceEqual(other.Options);

    public override int GetHashCode() => HashCode.Combine(Hero, Hut, Options.Count);

    public override string ToString() => $"WitchHutDecision {{ Hero = {Hero}, Hut = {Hut}, Options = {string.Join(" | ", Options)} }}";
}

/// <summary>
/// A town gate lists its visitor's owner's towns (ADR-068); <c>ChooseTownGateCommand</c> picks an available one, or none to stay.
/// </summary>
public sealed record TownGateDecision(HeroId Hero, MapObjectId Gate, IReadOnlyList<Objects.TownGateOption> Towns) : PendingDecision(Hero)
{
    public bool Equals(TownGateDecision? other) =>
        other is not null && Hero == other.Hero && Gate == other.Gate && Towns.SequenceEqual(other.Towns);

    public override int GetHashCode() => HashCode.Combine(Hero, Gate, Towns.Count);

    public override string ToString() => $"TownGateDecision {{ Hero = {Hero}, Gate = {Gate}, Towns = {string.Join(" | ", Towns)} }}";
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
