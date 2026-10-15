using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Events;

public sealed record ResourcesChanged(PlayerId Player, ResourceSet Previous, ResourceSet Current) : IEvent;
