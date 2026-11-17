using EternalWars.Core.Domain.Common;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Events;

[SeenBy(Audience.Everyone)]
public sealed record GameStarted(GameDate Date) : IEvent;

[SeenBy(Audience.Everyone)]
public sealed record DayStarted(GameDate Date) : IEvent;

[SeenBy(Audience.Everyone)]
public sealed record WeekStarted(GameDate Date) : IEvent;

/// <summary>The week's event (ADR-073) is set, before the week's growth; published only for a week with an event.</summary>
[SeenBy(Audience.Everyone)]
public sealed record WeekEventAnnounced(GameDate Date, Turns.WeekEvent Event) : IEvent;

/// <summary>A month of a creature put new stacks of it on the map, next to map objects (ADR-073); each also has its <c>MapObjectPlaced</c>.</summary>
[SeenBy(Audience.Witnesses)]
public sealed record MonstersSpawned(string Creature, IReadOnlyList<MapObjectId> Monsters) : IEvent;

[SeenBy(Audience.Everyone)]
public sealed record TurnStarted(PlayerId Player, GameDate Date) : IEvent;

[SeenBy(Audience.Everyone)]
public sealed record TurnEnded(PlayerId Player, GameDate Date) : IEvent;
