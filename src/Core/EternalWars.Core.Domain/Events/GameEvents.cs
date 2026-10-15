using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Events;

// Turn flow -------------------------------------------------------------------

public sealed record GameStarted(GameDate Date) : IEvent;

public sealed record DayStarted(GameDate Date) : IEvent;

public sealed record WeekStarted(GameDate Date) : IEvent;

public sealed record TurnStarted(PlayerId Player, GameDate Date) : IEvent;

public sealed record TurnEnded(PlayerId Player, GameDate Date) : IEvent;

// Economy ---------------------------------------------------------------------

public sealed record ResourcesChanged(PlayerId Player, ResourceSet Previous, ResourceSet Current) : IEvent;

// Heroes ----------------------------------------------------------------------

public sealed record HeroMoved(HeroId Hero, MapPosition From, MapPosition To, int RemainingMovementPoints) : IEvent;

public sealed record HeroMovementRestored(HeroId Hero, int MovementPoints) : IEvent;

// Map editing -----------------------------------------------------------------

public sealed record TerrainPainted(IReadOnlyList<MapPosition> Positions, TerrainType Terrain) : IEvent;
