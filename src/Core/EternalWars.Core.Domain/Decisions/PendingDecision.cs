using EternalWars.Core.Domain.Common;

namespace EternalWars.Core.Domain.Decisions;

/// <summary>
/// A choice the game is waiting for before it can continue (treasure chest, exchange, guard offer, battle;
/// level-ups and seer huts later). While one is pending, movement and End Turn are refused.
/// </summary>
public abstract record PendingDecision(HeroId Hero);

public enum TreasureChestReward
{
    Gold,
    Experience,
}

public sealed record TreasureChestDecision(HeroId Hero, MapObjectId Chest, int Gold, int Experience) : PendingDecision(Hero);

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
