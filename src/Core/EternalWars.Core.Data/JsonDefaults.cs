using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace EternalWars.Core.Data;

/// <summary>Shared JSON settings for every file format: camelCase, indented, enums as strings.</summary>
internal static class JsonDefaults
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,

        // Optional data (roads, mine owners, gate links, teams) is simply absent when unset.
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,

        // Hand-edited files need not put the "kind" discriminator first.
        AllowOutOfOrderMetadataProperties = true,
        Converters = { new JsonStringEnumConverter() },

        // The map objects' kinds (ADR-166) and the map events' node hierarchies (v31, ADR-115).
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { Maps.MapConverter.AddObjectTypes, Maps.MapEventJson.AddTypes } },
    };
}
