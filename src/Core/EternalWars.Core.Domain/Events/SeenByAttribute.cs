namespace EternalWars.Core.Domain.Events;

/// <summary>
/// Which players may learn of an event (ADR-202): a LAN host passes an event on to a client only when its player is in the event's audience.
/// Every event declares one; an event without it reaches no client.
/// </summary>
public enum Audience
{
    /// <summary>Every player: the calendar, whose turn it is, who is out, the game's end.</summary>
    Everyone,

    /// <summary>Only the player its <c>Player</c> or <c>Owner</c> property names: its resources, its intel, its fog.</summary>
    Owner,

    /// <summary>
    /// The players who see what it is about: a hero it names (its owner, or a player who sees the hero); naming no hero, a map object it names
    /// (its owner, or a player with one of its cells in sight).
    /// </summary>
    Witnesses,

    /// <summary>The players fighting the current battle.</summary>
    BattleSides,

    /// <summary>Decided event by event by the host (commands, questions, hero moves).</summary>
    Special,

    /// <summary>No client: map-script internals the players never see, and the editors' events.</summary>
    Nobody,
}

/// <summary>Declares an event's <see cref="Audience"/> (ADR-202).</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class SeenByAttribute(Audience audience) : Attribute
{
    public Audience Audience { get; } = audience;
}
