using EternalWars.Core.Domain.Common;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Events;

public sealed record GameStarted(GameDate Date) : IEvent;

public sealed record DayStarted(GameDate Date) : IEvent;

public sealed record WeekStarted(GameDate Date) : IEvent;

public sealed record TurnStarted(PlayerId Player, GameDate Date) : IEvent;

public sealed record TurnEnded(PlayerId Player, GameDate Date) : IEvent;
