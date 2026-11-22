using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Events;

[SeenBy(Audience.Owner)]
public sealed record ResourcesChanged(PlayerId Player, ResourceSet Previous, ResourceSet Current) : IEvent;

/// <summary>
/// A player traded at a marketplace (ADR-067): <paramref name="Amount"/> units of <paramref name="Resource"/> were bought or sold for
/// <paramref name="Gold"/>. <paramref name="Marketplace"/> is the map marketplace used, null for the player's towns.
/// </summary>
[SeenBy(Audience.Owner)]
public sealed record ResourcesTraded(
    PlayerId Player, ResourceType Resource, TradeDirection Direction, int Amount, int Gold, MapObjectId? Marketplace) : IEvent;

/// <summary>
/// A player traded one resource for another at a marketplace (ADR-296): <paramref name="Gave"/> of <paramref name="Give"/> for <paramref name="Got"/> of
/// <paramref name="Get"/>. <paramref name="Marketplace"/> is the map marketplace used, null for the player's towns.
/// </summary>
[SeenBy(Audience.Owner)]
public sealed record ResourcesExchanged(PlayerId Player, ResourceType Give, int Gave, ResourceType Get, int Got, MapObjectId? Marketplace) : IEvent;

/// <summary>A player sent resources to a teammate (ADR-296); both see their own <see cref="ResourcesChanged"/>.</summary>
[SeenBy(Audience.Owner)]
public sealed record ResourcesSent(PlayerId Player, PlayerId To, ResourceSet Amount) : IEvent;
