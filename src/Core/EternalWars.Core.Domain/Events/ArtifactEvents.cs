using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Events;

/// <summary>Where a gained artifact came from.</summary>
public enum ArtifactSource
{
    /// <summary>Taken from the map.</summary>
    Pickup,

    /// <summary>Looted from a defeated hero.</summary>
    Loot,

    /// <summary>Found in a treasure chest or a loot site (ADR-068), or a sphinx's reward.</summary>
    Found,

    /// <summary>Bought from an artifact merchant (ADR-079).</summary>
    Bought,
}

/// <summary>A hero put an artifact on (from its backpack) into <paramref name="Slot"/> (ADR-061).</summary>
public sealed record ArtifactEquipped(HeroId Hero, string Artifact, ArtifactSlot Slot) : IEvent;

/// <summary>A hero took an artifact off; it is in the backpack.</summary>
public sealed record ArtifactUnequipped(HeroId Hero, string Artifact) : IEvent;

/// <summary>An artifact went from one own hero's backpack or body to another's backpack (exchange).</summary>
public sealed record ArtifactGiven(HeroId From, HeroId To, string Artifact) : IEvent;

/// <summary>An artifact joined a hero's backpack.</summary>
public sealed record ArtifactGained(HeroId Hero, string Artifact, ArtifactSource Source) : IEvent;

/// <summary>Loot that did not fit in the winner's backpack is gone.</summary>
public sealed record ArtifactLost(HeroId Hero, string Artifact) : IEvent;

/// <summary>A hero stepped on an artifact it has no room for; the artifact stays where it is.</summary>
public sealed record ArtifactBackpackFull(HeroId Hero, string Artifact) : IEvent;

/// <summary>What a hero wears and carries changed: the editor, or an artifact sacrificed at an altar (ADR-079).</summary>
public sealed record HeroArtifactsChanged(HeroId Hero) : IEvent;

/// <summary>A hero now wears every piece of an assembled relic and may join them (ADR-121): the hero screen asks.</summary>
public sealed record RelicReady(HeroId Hero, string Relic) : IEvent;

/// <summary>A hero joined the pieces it wore into an assembled relic, worn in the first piece's slot (ADR-121).</summary>
public sealed record RelicAssembled(HeroId Hero, string Relic, IReadOnlyList<string> Pieces) : IEvent;

/// <summary>A hero took an assembled relic apart: the pieces are worn again (<paramref name="Worn"/>) or in the backpack (ADR-121).</summary>
public sealed record RelicTakenApart(HeroId Hero, string Relic, IReadOnlyList<string> Pieces, bool Worn) : IEvent;
