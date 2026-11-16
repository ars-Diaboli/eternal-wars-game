using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;

namespace EternalWars.Core.Network;

/// <summary>What a LAN host and its clients send each other (ADR-192): one JSON object per message, its kind in <c>"type"</c>.</summary>
public abstract record NetMessage;

/// <summary>A client asks for a seat: the colour <paramref name="Seat"/>, or the first free one when null.</summary>
/// <param name="Protocol">The client's <see cref="NetSerializer.ProtocolVersion"/>; the host refuses another.</param>
/// <param name="Name">The player's name, for the host's messages.</param>
/// <param name="Seat">The colour asked for, or null for any free one.</param>
public sealed record JoinRequest(int Protocol, string Name, PlayerId? Seat = null) : NetMessage;

/// <summary>
/// The host gives the client <paramref name="Seat"/> and the game as it stands: the start map, the options and every command so far, which the
/// client replays to its copy (digest-checked).
/// </summary>
public sealed record JoinAccepted(PlayerId Seat, GameRecordDocument Game) : NetMessage;

/// <summary>The host turns the client away; the connection closes after it.</summary>
public sealed record JoinRefused(string Reason) : NetMessage;

/// <summary>A client's command for its own colour; <paramref name="Sequence"/> numbers it, so a refusal can name it.</summary>
public sealed record SendCommand(int Sequence, GameCommand Command) : NetMessage;

/// <summary>A command the host has run, any player's: the <paramref name="Index"/>-th of the game's command log.</summary>
public sealed record CommandApplied(int Index, GameCommand Command) : NetMessage;

/// <summary>The host refused the client's command number <paramref name="Sequence"/>; nothing changed.</summary>
public sealed record CommandRefused(int Sequence, string Error) : NetMessage;

/// <summary>Turns messages into JSON and back, with the save files' settings for the commands and records they carry.</summary>
public static class NetSerializer
{
    /// <summary>Raised whenever a message changes shape; a host and a client of different versions do not play together.</summary>
    public const int ProtocolVersion = 1;

    private static readonly IReadOnlyList<(Type Type, string Name)> MessageTypes =
    [
        (typeof(JoinRequest), "join"),
        (typeof(JoinAccepted), "joined"),
        (typeof(JoinRefused), "refusedJoin"),
        (typeof(SendCommand), "command"),
        (typeof(CommandApplied), "applied"),
        (typeof(CommandRefused), "refused"),
    ];

    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static byte[] Serialize(NetMessage message) => JsonSerializer.SerializeToUtf8Bytes(message, Options);

    /// <summary>Reads one message; throws <see cref="JsonException"/> for anything else.</summary>
    public static NetMessage Deserialize(ReadOnlySpan<byte> json) =>
        JsonSerializer.Deserialize<NetMessage>(json, Options) ?? throw new JsonException("An empty message.");

    private static JsonSerializerOptions CreateOptions()
    {
        var options = GameRecordSerializer.CreateOptions(AddMessageTypes);
        options.WriteIndented = false;
        return options;
    }

    private static void AddMessageTypes(JsonTypeInfo info)
    {
        if (info.Type != typeof(NetMessage))
        {
            return;
        }

        info.PolymorphismOptions = new JsonPolymorphismOptions
        {
            TypeDiscriminatorPropertyName = "type",
            UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
        };
        foreach (var (type, name) in MessageTypes)
        {
            info.PolymorphismOptions.DerivedTypes.Add(new JsonDerivedType(type, name));
        }
    }
}
