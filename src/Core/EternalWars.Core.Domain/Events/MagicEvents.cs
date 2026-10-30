using EternalWars.Core.Domain.Common;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Events;

/// <summary>A hero learned spells from a town's mage guild (ADR-110), in guild order.</summary>
public sealed record SpellsLearned(HeroId Hero, MapObjectId Town, IReadOnlyList<string> Spells) : IEvent;

/// <summary>A hero learned the warcries up to <paramref name="Level"/> at a town's war lodge (ADR-071, placeholder).</summary>
public sealed record WarcriesLearned(HeroId Hero, MapObjectId Town, int Level) : IEvent;

/// <summary>A hero bought a spell book in a town's mage guild (ADR-071).</summary>
public sealed record SpellBookBought(MapObjectId Town, HeroId Hero) : IEvent;

/// <summary>A hero's mana went down by <paramref name="Spent"/>, to <paramref name="Mana"/> (spells cast in a battle, ADR-110).</summary>
public sealed record HeroManaSpent(HeroId Hero, int Spent, int Mana, int MaxMana) : IEvent;
