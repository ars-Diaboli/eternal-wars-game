using EternalWars.Core.Domain.Common;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Events;

public sealed record GameStarted(GameDate Date) : IEvent;

public sealed record DayStarted(GameDate Date) : IEvent;

public sealed record WeekStarted(GameDate Date) : IEvent;

/// <summary>The week's event (ADR-073) is set, before the week's growth; published only for a week with an event.</summary>
public sealed record WeekEventAnnounced(GameDate Date, Turns.WeekEvent Event) : IEvent;

/// <summary>A month of a creature put new stacks of it on the map, next to map objects (ADR-073); each also has its <c>MapObjectPlaced</c>.</summary>
public sealed record MonstersSpawned(string Creature, IReadOnlyList<MapObjectId> Monsters) : IEvent;

public sealed record TurnStarted(PlayerId Player, GameDate Date) : IEvent;

public sealed record TurnEnded(PlayerId Player, GameDate Date) : IEvent;
