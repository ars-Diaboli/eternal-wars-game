using System.Text.Json;
using System.Text.Json.Nodes;
using EternalWars.Core.Domain.Heroes;

namespace EternalWars.Core.Data.Maps;

/// <summary>One step that upgrades an older map file's raw JSON (ADR-174); it runs on every file older than <see cref="Version"/>.</summary>
internal interface IMapMigration
{
    /// <summary>The format version that made the change this step upgrades to.</summary>
    int Version { get; }

    void Apply(JsonObject map);
}

/// <summary>
/// Upgrades an older map file's JSON to the current format before it is read (ADR-174), so <see cref="MapDocument"/> and <see cref="MapConverter"/>
/// know only the current format. A version that only added fields needs no step: its files read as they are. A file of the current version is read
/// as it is, without the upgrade's parse.
/// </summary>
internal static class MapMigrations
{
    /// <summary>The version of a map file that does not say which it is: the first, so every step applies to it.</summary>
    public const int UnversionedFormatVersion = 1;

    /// <summary>The steps, oldest first.</summary>
    public static IReadOnlyList<IMapMigration> Steps { get; } = [new SkillsReplaceTactics(), new GuardModePerMonster(), new RuinsAreTheCrypt()];

    private static readonly JsonNodeOptions NodeOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// The JSON upgraded to the current format, with the map at the root or at the root's <paramref name="mapProperty"/> (a saved game's map);
    /// null when it needs no upgrade, or is no map at all (the reader then says why).
    /// </summary>
    public static JsonNode? Upgrade(string json, string? mapProperty = null)
    {
        int version;
        try
        {
            using var file = JsonDocument.Parse(json);
            var map = mapProperty is null ? file.RootElement : Property(file.RootElement, mapProperty);
            version = map is { } element ? VersionOf(element) : MapDocument.CurrentFormatVersion;
        }
        catch (JsonException)
        {
            return null;
        }

        if (version >= MapDocument.CurrentFormatVersion)
        {
            return null;
        }

        var root = JsonNode.Parse(json, NodeOptions)!.AsObject();
        Upgrade(mapProperty is null ? root : root[mapProperty]!.AsObject(), version);
        return root;
    }

    /// <summary>Runs every step newer than <paramref name="version"/> on the map, which is then of the current version.</summary>
    public static void Upgrade(JsonObject map, int version)
    {
        foreach (var step in Steps.Where(s => s.Version > version))
        {
            step.Apply(map);
        }

        map["formatVersion"] = MapDocument.CurrentFormatVersion;
    }

    /// <summary>The version a map's JSON says it is in; one that names none is the first. Anything unreadable counts as current, for the reader to refuse.</summary>
    private static int VersionOf(JsonElement map)
    {
        if (map.ValueKind != JsonValueKind.Object)
        {
            return MapDocument.CurrentFormatVersion;
        }

        return Property(map, "formatVersion") is not { } version ? UnversionedFormatVersion
            : version.ValueKind == JsonValueKind.Number && version.TryGetInt32(out var number) ? number
            : MapDocument.CurrentFormatVersion;
    }

    /// <summary>A property of an object, matched without case like the reader does.</summary>
    private static JsonElement? Property(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }

        return null;
    }

    private static IEnumerable<JsonObject> Items(JsonObject map, string name) => (map[name] as JsonArray ?? []).OfType<JsonObject>();

    private static bool IsKind(JsonObject mapObject, string kind) => mapObject["kind"] is JsonValue value && value.TryGetValue<string>(out var name) && name == kind;

    /// <summary>v5 (ADR-048): a hero's skill levels (<c>skills</c>) replace v4's <c>tactics</c>, which becomes its Tactics level.</summary>
    private sealed class SkillsReplaceTactics : IMapMigration
    {
        public int Version => 5;

        public void Apply(JsonObject map)
        {
            foreach (var hero in Items(map, "heroes"))
            {
                var tactics = hero["tactics"];
                hero.Remove("tactics");
                if (hero["skills"] is null && tactics?.Deserialize<SkillLevel>(JsonDefaults.Options) is not (null or SkillLevel.None))
                {
                    hero["skills"] = new JsonObject { [BuiltInSkills.Tactics] = tactics };
                }
            }
        }
    }

    /// <summary>v13 (ADR-065): each monster has its own guard <c>mode</c>; v4–v12's map-wide <c>guardMode</c> becomes that of every monster without one.</summary>
    private sealed class GuardModePerMonster : IMapMigration
    {
        public int Version => 13;

        public void Apply(JsonObject map)
        {
            var mode = map["guardMode"];
            map.Remove("guardMode");
            if (mode is null)
            {
                return;
            }

            foreach (var monster in Items(map, "objects").Where(o => IsKind(o, "monster") && o["mode"] is null))
            {
                monster["mode"] = mode.DeepClone();
            }
        }
    }

    /// <summary>v23 (ADR-070): the undead creature bank <c>bank.ruins</c> is <c>bank.crypt</c>.</summary>
    private sealed class RuinsAreTheCrypt : IMapMigration
    {
        public int Version => 23;

        public void Apply(JsonObject map)
        {
            foreach (var bank in Items(map, "objects").Where(o => o["definition"] is JsonValue value && value.TryGetValue<string>(out var id) && id == "bank.ruins"))
            {
                bank["definition"] = "bank.crypt";
            }
        }
    }
}
