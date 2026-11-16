using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;

namespace EternalWars.Core.Network;

/// <summary>What a LAN host and its clients send each other (ADR-192 to ADR-196): one JSON object per message, its kind in <c>"type"</c>.</summary>
public abstract record NetMessage;

/// <summary>A client asks for a seat: the colour <paramref name="Seat"/>, or the first free one when null.</summary>
/// <param name="Protocol">The client's <see cref="NetSerializer.ProtocolVersion"/>; the host refuses another.</param>
/// <param name="Name">The player's name, for the lobby, the chat and the host's messages.</param>
/// <param name="Seat">The colour asked for, or null for any free one.</param>
public sealed record JoinRequest(int Protocol, string Name, PlayerId? Seat = null) : NetMessage;

/// <summary>
/// The host gives the client <paramref name="Seat"/> and what of the game never changes and hides nothing (ADR-193): the ground of the map
/// (terrain, roads, rivers, heights; no object, no hero), the players, the options (the seed left out) and the scenario's rules. Everything
/// else comes in <see cref="ViewUpdate"/>s, the first of which follows at once.
/// </summary>
/// <param name="Seat">The client's colour.</param>
/// <param name="Ground">The map without its objects and heroes.</param>
/// <param name="Players">Every player of the game, in its order.</param>
/// <param name="Options">The game's <c>GameOptions</c> (<see cref="StateCodec"/>), its seed zeroed.</param>
/// <param name="Rules">The scenario's <c>ScenarioRules</c> (<see cref="StateCodec"/>).</param>
public sealed record JoinAccepted(PlayerId Seat, MapDocument Ground, IReadOnlyList<PlayerSeat> Players, JsonNode Options, JsonNode Rules) : NetMessage;

/// <summary>A player of the game as everyone may know it: its colour, name, whether a person plays it and its team.</summary>
public sealed record PlayerSeat(PlayerId Id, string Name, bool IsHuman, TeamId Team);

/// <summary>
/// What changed in the client's view of the game (ADR-193) since the last update, the <paramref name="Sequence"/>-th (from 1): the pieces of
/// the game it may see, the tiles it explored, the tiles in its sight, and the events it may see, in the order they happened.
/// </summary>
/// <param name="Sequence">The update's number; the client follows them one by one.</param>
/// <param name="Changes">Pieces of the view put in place or taken away, in order.</param>
/// <param name="Explored">Tiles newly explored.</param>
/// <param name="Sight">Every tile in sight now under refog, when that changed; null when it did not (or the fog never comes back).</param>
/// <param name="Events">The events (<see cref="StateCodec"/>, each with its type), to publish once the changes are in place.</param>
public sealed record ViewUpdate(
    int Sequence, IReadOnlyList<ViewChange> Changes, IReadOnlyList<MapPosition> Explored, IReadOnlyList<MapPosition>? Sight,
    IReadOnlyList<JsonNode> Events) : NetMessage;

/// <summary>
/// One piece of a client's view: <paramref name="Kind"/> (<c>game</c>, <c>player</c>, <c>hero</c>, <c>object</c>, <c>memory</c>, <c>retired</c>,
/// <c>caravans</c>) and the id of that hero, object or player (0 for the others), as <see cref="StateCodec"/> writes it; null takes it away.
/// </summary>
public sealed record ViewChange(string Kind, int Id, JsonNode? Value);

/// <summary>The host turns the client away; the connection closes after it.</summary>
public sealed record JoinRefused(string Reason) : NetMessage;

/// <summary>How a colour of a LAN game is played, as the host sets it in the lobby (ADR-194).</summary>
public enum SeatKind
{
    /// <summary>On the host's computer.</summary>
    Host,

    /// <summary>By a player on another computer: one who has joined it (<see cref="LobbySeat.Player"/>) or, while nobody has, an open colour.</summary>
    Remote,

    /// <summary>By the computer, on the host.</summary>
    Computer,
}

/// <summary>
/// A colour of the game in the lobby (ADR-194): how it is played, the name of whoever plays it (the host's player, the client that took it; null
/// for an open colour or the computer) and what the host chose for it (town, hero, team), in words.
/// </summary>
/// <param name="Id">The colour.</param>
/// <param name="Colour">Its name ("Red").</param>
/// <param name="Kind">How it is played.</param>
/// <param name="Player">Who plays it; null while open, and for the computer.</param>
/// <param name="Details">The town, hero and team chosen for it, in words.</param>
public sealed record LobbySeat(PlayerId Id, string Colour, SeatKind Kind, string? Player, string Details);

/// <summary>
/// The game a LAN host is setting up (ADR-194), as every computer in its lobby sees it: the map, its settings in words (the clients need no copy
/// of the map or the template), its colours and the players who have joined without a colour yet.
/// </summary>
/// <param name="Host">The host's player.</param>
/// <param name="Game">The map, or "Random map: " and the template.</param>
/// <param name="Settings">One line per setting (size, fog, complexity, timer, ...).</param>
/// <param name="Seats">The colours of the game, in order.</param>
/// <param name="Waiting">The joined players who have no colour.</param>
public sealed record LobbyState(string Host, string Game, IReadOnlyList<string> Settings, IReadOnlyList<LobbySeat> Seats, IReadOnlyList<string> Waiting)
{
    /// <summary>The open colours: played on another computer, nobody has taken them yet.</summary>
    public IEnumerable<LobbySeat> Open => Seats.Where(s => s.Kind == SeatKind.Remote && s.Player is null);
}

/// <summary>The lobby as it stands now, and the client's colour in it (null while it has none).</summary>
public sealed record LobbyUpdate(LobbyState Lobby, PlayerId? Seat) : NetMessage;

/// <summary>A client in the lobby asks for an open colour (in place of the one it has).</summary>
public sealed record TakeSeat(PlayerId Seat) : NetMessage;

/// <summary>A player writes in the chat; the host passes it to everyone (<see cref="ChatLine"/>).</summary>
public sealed record ChatSay(string Text) : NetMessage;

/// <summary>A line of the chat, from a player (by name), or from the host's game itself when <paramref name="From"/> is empty (who joined, who left).</summary>
public sealed record ChatLine(string From, string Text) : NetMessage;

/// <summary>A client's command for its own colour; <paramref name="Sequence"/> numbers it, so a refusal can name it.</summary>
public sealed record SendCommand(int Sequence, GameCommand Command) : NetMessage;

/// <summary>The host refused the client's command number <paramref name="Sequence"/>; nothing changed.</summary>
public sealed record CommandRefused(int Sequence, string Error) : NetMessage;

/// <summary>
/// The game is paused for everyone while a colour played on another computer has nobody to play it (ADR-195): why, in words; null when the game
/// goes on again.
/// </summary>
public sealed record GamePaused(string? Reason) : NetMessage;

/// <summary>
/// A client asks for the game's whole record (ADR-196), to save it on its computer: labelled <paramref name="SaveName"/> for a save, without a
/// label (null) for a replay. <paramref name="Request"/> numbers it, so the answer (<see cref="RecordSent"/>) can name it.
/// </summary>
public sealed record RecordRequest(int Request, string? SaveName) : NetMessage;

/// <summary>The host's answer to <see cref="RecordRequest"/> number <paramref name="Request"/>: the record, or why there is none.</summary>
public sealed record RecordSent(int Request, GameRecordDocument? Record, string? Error) : NetMessage;

/// <summary>Turns messages into JSON and back, with the save files' settings for the commands and records they carry.</summary>
public static class NetSerializer
{
    /// <summary>Raised whenever a message changes shape; a host and a client of different versions do not play together.</summary>
    public const int ProtocolVersion = 5;

    private static readonly IReadOnlyList<(Type Type, string Name)> MessageTypes =
    [
        (typeof(JoinRequest), "join"),
        (typeof(JoinAccepted), "joined"),
        (typeof(JoinRefused), "refusedJoin"),
        (typeof(SendCommand), "command"),
        (typeof(ViewUpdate), "view"),
        (typeof(CommandRefused), "refused"),
        (typeof(LobbyUpdate), "lobby"),
        (typeof(TakeSeat), "takeSeat"),
        (typeof(ChatSay), "say"),
        (typeof(ChatLine), "chat"),
        (typeof(GamePaused), "paused"),
        (typeof(RecordRequest), "askRecord"),
        (typeof(RecordSent), "record"),
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
