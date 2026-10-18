using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Events;

public sealed record ResourcesChanged(PlayerId Player, ResourceSet Previous, ResourceSet Current) : IEvent;

/// <summary>
/// A player traded at a marketplace (ADR-067): <paramref name="Amount"/> units of <paramref name="Resource"/> were bought or sold for
/// <paramref name="Gold"/>. <paramref name="Marketplace"/> is the map marketplace used, null for the player's towns.
/// </summary>
public sealed record ResourcesTraded(
    PlayerId Player, ResourceType Resource, TradeDirection Direction, int Amount, int Gold, MapObjectId? Marketplace) : IEvent;
