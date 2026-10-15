using EternalWars.Core.Domain.Common;

namespace EternalWars.Core.Domain.Decisions;

/// <summary>
/// A choice the current player must make before the game can continue (treasure chest now;
/// level-ups, seer huts, battle results later). While one is pending, movement and End Turn are refused.
/// </summary>
public abstract record PendingDecision(HeroId Hero);

public enum TreasureChestReward
{
    Gold,
    Experience,
}

public sealed record TreasureChestDecision(HeroId Hero, MapObjectId Chest, int Gold, int Experience) : PendingDecision(Hero);
