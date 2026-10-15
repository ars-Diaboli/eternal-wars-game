using System.Text.Json;
using System.Text.Json.Serialization;

namespace EternalWars.Core.Data;

/// <summary>Shared JSON settings for every file format: camelCase, indented, enums as strings.</summary>
internal static class JsonDefaults
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,

        // Hand-edited files need not put the "kind" discriminator first.
        AllowOutOfOrderMetadataProperties = true,
        Converters = { new JsonStringEnumConverter() },
    };
}
