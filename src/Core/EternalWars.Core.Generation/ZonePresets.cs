using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Generation;

/// <summary>
/// A zone kept to be reused (ADR-273): its settings under a name. <paramref name="Zone"/> holds no id that matters, no player slot, team,
/// mirror, level or graph place: those belong to the zone the preset is applied to. <paramref name="BuiltIn"/>: one the game ships.
/// </summary>
public sealed record ZonePreset(string Name, TemplateZone Zone, bool BuiltIn = false);

/// <summary>The template editor's zone presets (ADR-273): the built-in ones, and how a preset is made from a zone and applied to one.</summary>
public static class ZonePresets
{
    /// <summary>The id a preset's zone carries (a preset is not a zone of any template).</summary>
    public const string PresetId = "preset";

    /// <summary>Rich treasure, mine junction, guarded start.</summary>
    public static IReadOnlyList<ZonePreset> BuiltIn { get; } =
    [
        new("Rich treasure", new TemplateZone
        {
            Id = PresetId, Type = ZoneType.Treasure, Size = 3, NeutralTowns = new CountRange(0, 1), Monsters = MonsterStrength.Strong,
            Mines = new Dictionary<ResourceType, int> { [ResourceType.Gold] = 1, [ResourceType.Crystal] = 1, [ResourceType.Mercury] = 1, [ResourceType.Sulfur] = 1 },
            ExtraPiles = 4, Treasure = [new TreasureBand(3000, 6000, 4), new TreasureBand(6000, 10000, 2)],
        }, BuiltIn: true),
        new("Mine junction", new TemplateZone
        {
            Id = PresetId, Type = ZoneType.Junction, Size = 2, Monsters = MonsterStrength.Strong, Obstacles = ObstacleDensity.Normal,
            Mines = new Dictionary<ResourceType, int> { [ResourceType.Gold] = 1, [ResourceType.Lumber] = 1, [ResourceType.Ore] = 1 },
            MineGuards = MineGuarding.Always, ExtraPiles = 2,
        }, BuiltIn: true),
        new("Guarded start", new TemplateZone
        {
            Id = PresetId, Type = ZoneType.PlayerStart, Size = 3, PlayerTowns = CountRange.Exactly(1), Monsters = MonsterStrength.Weak,
            Mines = new Dictionary<ResourceType, int> { [ResourceType.Gold] = 1, [ResourceType.Lumber] = 1, [ResourceType.Ore] = 1 },
            MineGuards = MineGuarding.Always, Obstacles = ObstacleDensity.Normal, ExtraPiles = 3, Treasure = [new TreasureBand(500, 1500, 4)],
            StartBuildings = new Dictionary<string, int> { [BuiltInBuildings.Fort] = 1 },
        }, BuiltIn: true),
    ];

    /// <summary>A preset named <paramref name="name"/> of the zone's settings, without what belongs to the zone itself (id, slot, team, mirror, level, place).</summary>
    public static ZonePreset Of(string name, TemplateZone zone)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(zone);
        return new ZonePreset(name.Trim(), Settings(zone));
    }

    /// <summary>
    /// The zone's settings as a preset holds them: its own lists and tables (copies, so zones made from one preset share nothing), with
    /// what a file may leave null (a list, a range, a treasure band) made empty, so a hand-edited preset cannot put a null into a template.
    /// </summary>
    public static TemplateZone Settings(TemplateZone zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        return zone with
        {
            Id = PresetId, Owner = null, Team = null, Mirror = null, Level = 0, GraphX = null, GraphY = null,
            PlayerTowns = zone.PlayerTowns ?? CountRange.None,
            NeutralTowns = zone.NeutralTowns ?? CountRange.None,
            Mines = new Dictionary<ResourceType, int>(zone.Mines ?? new Dictionary<ResourceType, int>()),
            Treasure = [.. (zone.Treasure ?? []).Where(b => b is not null)],
            TownTypes = zone.TownTypes is null ? null : [.. zone.TownTypes.Where(t => t is not null)],
            Terrains = zone.Terrains is null ? null : [.. zone.Terrains],
            StartBuildings = zone.StartBuildings is null ? null : new Dictionary<string, int>(zone.StartBuildings),
            StartDwellings = zone.StartDwellings is null ? null : [.. zone.StartDwellings],
        };
    }

    /// <summary>
    /// <paramref name="target"/> with the preset's settings: everything but its id, player slot, team, mirror, level and graph place. A zone
    /// made a start zone takes <paramref name="slot"/> (when it has none); made something else, it drops slot and team.
    /// </summary>
    public static TemplateZone Apply(TemplateZone target, ZonePreset preset, Func<int> slot)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(preset);
        ArgumentNullException.ThrowIfNull(slot);
        var applied = preset.Zone with
        {
            Id = target.Id, Owner = target.Owner, Team = target.Team, Mirror = target.Mirror, Level = target.Level, GraphX = target.GraphX, GraphY = target.GraphY,
        };
        return applied.Type is ZoneType.PlayerStart or ZoneType.AiStart
            ? applied with { Owner = applied.Owner ?? slot() }
            : applied with { Owner = null, Team = null };
    }
}
