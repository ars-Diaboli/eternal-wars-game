using System.Globalization;

namespace EternalWars.Core.Domain.Heroes;

/// <summary>What a temporary hero status changes (ADR-068).</summary>
public enum HeroStatusKind
{
    /// <summary>Added to the hero's morale (<see cref="Hero.TotalMorale"/>); battles still keep −5…+5.</summary>
    Morale,

    /// <summary>Added to the hero's luck (<see cref="Hero.TotalLuck"/>).</summary>
    Luck,

    /// <summary>Flat movement points added to the daily allowance (<see cref="MovementAllowance"/>), e.g. the Stables for the rest of the week.</summary>
    Movement,
}

/// <summary>When a temporary hero status ends (ADR-068).</summary>
public enum StatusExpiry
{
    /// <summary>When the hero's next battle ends, won, lost or fled.</summary>
    NextBattle,

    /// <summary>At the start of the owner's next turn.</summary>
    EndOfDay,

    /// <summary>When a new week starts.</summary>
    EndOfWeek,
}

/// <summary>
/// A temporary effect on a hero from a map object (Faerie Ring, Temple, Stables, Warrior's Tomb, ADR-068). <see cref="Source"/> is the
/// definition id of what gave it: a second status of the same source and kind replaces the first instead of adding to it (how
/// different sources stack is MAP-10).
/// </summary>
public sealed record HeroStatus(string Source, HeroStatusKind Kind, int Value, StatusExpiry Expires)
{
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Source}:{Kind}{Value:+0;-0;0}/{Expires}");
}
