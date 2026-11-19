using System.Text.Json;
using System.Text.Json.Serialization;
using EternalWars.Core.Domain.Common;

namespace EternalWars.Core.Data;

/// <summary>
/// Writes every content id (<see cref="IContentId{TSelf}"/>, ADR-243) as its plain string, as a value and as a dictionary key, so typed ids leave
/// the file formats as they were.
/// </summary>
internal sealed class ContentIdJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsValueType && typeToConvert.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IContentId<>));

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(typeof(Converter<>).MakeGenericType(typeToConvert))!;

    private sealed class Converter<T> : JsonConverter<T>
        where T : struct, IContentId<T>
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            T.From(reader.GetString() ?? throw new JsonException($"A {typeof(T).Name} cannot be null."));

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) => writer.WriteStringValue(value.Value);

        public override T ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            T.From(reader.GetString()!);

        public override void WriteAsPropertyName(Utf8JsonWriter writer, T value, JsonSerializerOptions options) => writer.WritePropertyName(value.Value);
    }
}
