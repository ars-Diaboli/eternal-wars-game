using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Heroes;

namespace EternalWars.Core.Domain.Campaigns;

/// <summary>One stack of a carried hero's army, by army slot (0–6).</summary>
public sealed record CarriedStack(int Slot, string Creature, int Count);

/// <summary>
/// A hero as a campaign carries it from one map to the next (ADR-241): the roster hero <see cref="Hero"/> with what it gained. <see cref="Army"/>
/// and <see cref="Artifacts"/> / <see cref="Backpack"/> are null when the campaign does not carry them; the hero then keeps what the next map gives
/// it. Part of <see cref="Game.GameOptions.CarriedHeroes"/>, so a save or replay of the next map starts the same.
/// </summary>
public sealed record CarriedHero(string Hero, string Name)
{
    public int Level { get; init; } = 1;

    public int Experience { get; init; }

    public int Attack { get; init; }

    public int Defense { get; init; }

    public int SpellPower { get; init; }

    public int Knowledge { get; init; }

    public IReadOnlyDictionary<string, SkillLevel> Skills { get; init; } = new Dictionary<string, SkillLevel>();

    public bool SpellBook { get; init; }

    /// <summary>The spells it knows, each with its rank (ADR-122).</summary>
    public IReadOnlyDictionary<string, int> Spells { get; init; } = new Dictionary<string, int>();

    public IReadOnlyList<WarMachineSlot> WarMachines { get; init; } = [];

    /// <summary>The artifacts it wears (each goes to the slot its definition names); null: not carried.</summary>
    public IReadOnlyList<string>? Artifacts { get; init; }

    /// <summary>The artifacts in its backpack; null: not carried.</summary>
    public IReadOnlyList<string>? Backpack { get; init; }

    /// <summary>Its army; null: not carried.</summary>
    public IReadOnlyList<CarriedStack>? Army { get; init; }

    public bool Equals(CarriedHero? other) =>
        other is not null && Hero == other.Hero && Name == other.Name && Level == other.Level && Experience == other.Experience
        && Attack == other.Attack && Defense == other.Defense && SpellPower == other.SpellPower && Knowledge == other.Knowledge
        && SpellBook == other.SpellBook && Same(Skills, other.Skills) && Same(Spells, other.Spells) && WarMachines.SequenceEqual(other.WarMachines)
        && Same(Artifacts, other.Artifacts) && Same(Backpack, other.Backpack) && Same(Army, other.Army);

    public override int GetHashCode() => HashCode.Combine(Hero, Level, Experience);

    private static bool Same<T>(IReadOnlyList<T>? a, IReadOnlyList<T>? b) => a is null ? b is null : b is not null && a.SequenceEqual(b);

    private static bool Same<TValue>(IReadOnlyDictionary<string, TValue> a, IReadOnlyDictionary<string, TValue> b) =>
        a.Count == b.Count && a.All(p => b.TryGetValue(p.Key, out var v) && EqualityComparer<TValue>.Default.Equals(p.Value, v));
}
