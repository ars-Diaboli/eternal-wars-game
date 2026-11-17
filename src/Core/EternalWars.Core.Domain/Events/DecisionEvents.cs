using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Events;

/// <summary>The UI (or AI) must answer this before the game continues.</summary>
[SeenBy(Audience.Special)]
public sealed record DecisionRequested(PendingDecision Decision) : IEvent;

[SeenBy(Audience.Special)]
public sealed record DecisionResolved(PendingDecision Decision) : IEvent;
