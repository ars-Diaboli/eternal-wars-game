using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Objects;

namespace EternalWars.Core.Domain.Towns;

/// <summary>
/// A town on the adventure map (ADR-046): 7×7 with the corners free, entered through the bottom-middle cell. Its
/// definition names the town type (<see cref="MapObjectDefinition.Town"/>), whose creature lines are its nine dwellings
/// (ADR-030). Per instance: owner, name, building levels, dwellings (base creature id → 1 built, 2 upgraded), creatures
/// waiting in each dwelling, the garrison, and the day it last built something (one build per day).
/// </summary>
public sealed class Town : MapObject, IFlaggable
{
    /// <summary>Owned towns reveal and watch this far around their entrance.</summary>
    public const int TownScoutingRadius = 5;

    private readonly Dictionary<string, int> _buildings;
    private readonly Dictionary<string, int> _dwellings;
    private readonly Dictionary<string, int> _available;

    public Town(
        MapObjectId id,
        MapObjectDefinition definition,
        MapPosition position,
        PlayerId? owner = null,
        string? name = null,
        IReadOnlyDictionary<string, int>? buildings = null,
        IReadOnlyDictionary<string, int>? dwellings = null,
        IReadOnlyDictionary<string, int>? available = null,
        Army? garrison = null,
        GameDate? lastBuilt = null)
        : base(id, definition, position, MapObjectKind.Town)
    {
        Owner = owner;
        Name = name ?? definition.Name;
        _buildings = Copy(buildings ?? BuiltInBuildings.DefaultStart);
        _dwellings = Copy(dwellings);
        _available = Copy(available);
        Garrison = garrison ?? new Army();
        LastBuilt = lastBuilt;
    }

    /// <summary>Town type (<see cref="TownIds"/>).</summary>
    public string TownType => Definition.Town!;

    public PlayerId? Owner { get; internal set; }

    public int ScoutingRadius => TownScoutingRadius;

    public string Name { get; internal set; }

    /// <summary>Built level per building id (dwellings are in <see cref="Dwellings"/>); absent = not built.</summary>
    public IReadOnlyDictionary<string, int> Buildings => _buildings;

    /// <summary>Dwelling level per creature line (base creature id): 1 built, 2 upgraded.</summary>
    public IReadOnlyDictionary<string, int> Dwellings => _dwellings;

    /// <summary>Creatures waiting to be recruited per creature line; they accumulate week by week.</summary>
    public IReadOnlyDictionary<string, int> Available => _available;

    public Army Garrison { get; }

    /// <summary>The hero appointed governor (ADR-063); valid only while that hero is alive, owned by the town's owner and has Stewardship (<see cref="StewardshipRules.GovernorOf"/>).</summary>
    public HeroId? Governor { get; internal set; }

    /// <summary>The day the town last built something; one building per town per day.</summary>
    public GameDate? LastBuilt { get; internal set; }

    public int Level(string building) => _buildings.GetValueOrDefault(building);

    public int DwellingLevel(string line) => _dwellings.GetValueOrDefault(line);

    public int AvailableOf(string line) => _available.GetValueOrDefault(line);

    internal void SetLevel(string building, int level) => Set(_buildings, building, level);

    internal void SetDwelling(string line, int level) => Set(_dwellings, line, level);

    internal void SetAvailable(string line, int count) => Set(_available, line, count);

    private static void Set(Dictionary<string, int> values, string key, int value)
    {
        if (value <= 0)
        {
            values.Remove(key);
        }
        else
        {
            values[key] = value;
        }
    }

    private static Dictionary<string, int> Copy(IReadOnlyDictionary<string, int>? values) =>
        values?.Where(v => v.Value > 0).ToDictionary(v => v.Key, v => v.Value, StringComparer.Ordinal)
        ?? new Dictionary<string, int>(StringComparer.Ordinal);
}
