using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;

namespace EternalWars.Core.Data.Records;

/// <summary>
/// A replayable game (<c>*.ewreplay.json</c>): the map as it was at the start, the game options (incl. the
/// seed) and every accepted command. <see cref="FinalDigest"/> lets a replay prove it reached the same state.
/// </summary>
public sealed record GameRecordDocument
{
    public const int CurrentFormatVersion = 1;

    public int FormatVersion { get; init; } = CurrentFormatVersion;

    public required MapDocument Map { get; init; }

    public required GameOptions Options { get; init; }

    public required IReadOnlyList<GameCommand> Commands { get; init; }

    /// <summary><see cref="GameStateDigest"/> after the last command, if recorded.</summary>
    public string? FinalDigest { get; init; }
}

/// <summary>Records and replays games from their command logs (ADR-036).</summary>
public static class GameReplay
{
    /// <summary>
    /// Captures a finished or ongoing session. <paramref name="initialMap"/> must be the map document the
    /// session was created from (save it before starting: the live state has changed since).
    /// </summary>
    public static GameRecordDocument Record(MapDocument initialMap, GameSession session) => new()
    {
        Map = initialMap,
        Options = session.State.Options,
        Commands = session.CommandLog.ToList(),
        FinalDigest = GameStateDigest.Compute(session.State),
    };

    /// <summary>
    /// Rebuilds the game from the record: same map, options and commands. Throws <see cref="InvalidOperationException"/>
    /// if a command is refused or the final digest differs (the game is not deterministic, or content changed).
    /// </summary>
    public static GameSession Replay(
        GameRecordDocument record,
        MapObjectCatalog? catalog = null,
        CreatureCatalog? creatures = null)
    {
        var session = new GameSession(MapConverter.ToGameState(record.Map, catalog, record.Options, creatures: creatures));
        session.Start();
        for (var i = 0; i < record.Commands.Count; i++)
        {
            var result = session.Execute(record.Commands[i]);
            if (!result.IsAccepted)
            {
                throw new InvalidOperationException($"Replay diverged at command {i} ({record.Commands[i]}): {result.Error}");
            }
        }

        if (record.FinalDigest is { } expected && GameStateDigest.Compute(session.State) is var actual && actual != expected)
        {
            throw new InvalidOperationException($"Replay ended in state {actual}, the record says {expected}.");
        }

        return session;
    }
}

public static class GameRecordSerializer
{
    public const string FileExtension = ".ewreplay.json";

    private static readonly IReadOnlyList<(Type Type, string Name)> CommandTypes =
    [
        (typeof(StepHeroCommand), "step"),
        (typeof(MoveHeroCommand), "move"),
        (typeof(EndTurnCommand), "endTurn"),
        (typeof(ResolveTreasureChestCommand), "chest"),
        (typeof(CompleteExchangeCommand), "closeExchange"),
        (typeof(MoveStackCommand), "moveStack"),
        (typeof(RespondToGuardCommand), "guard"),
        (typeof(ChooseBattleWidthCommand), "battleWidth"),
        (typeof(BattleMoveCommand), "battleMove"),
        (typeof(BattleAttackCommand), "battleAttack"),
        (typeof(BattleShootCommand), "battleShoot"),
        (typeof(BattleWaitCommand), "battleWait"),
        (typeof(BattleDefendCommand), "battleDefend"),
        (typeof(BattleRetreatCommand), "battleRetreat"),
        (typeof(AutoResolveBattleCommand), "battleAuto"),
    ];

    /// <summary>Map JSON settings plus the command hierarchy (kept out of Domain) and compact ids.</summary>
    private static readonly JsonSerializerOptions Options = new(JsonDefaults.Options)
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { AddCommandTypes } },
        Converters = { new IdConverter<PlayerId>(v => new(v), id => id.Value), new IdConverter<HeroId>(v => new(v), id => id.Value) },
    };

    public static string Serialize(GameRecordDocument record) => JsonSerializer.Serialize(record, Options);

    public static GameRecordDocument Deserialize(string json) =>
        JsonSerializer.Deserialize<GameRecordDocument>(json, Options) ?? throw new FormatException("Replay file is empty.");

    public static void Save(GameRecordDocument record, string path) => File.WriteAllText(path, Serialize(record));

    public static GameRecordDocument Load(string path) => Deserialize(File.ReadAllText(path));

    private static void AddCommandTypes(JsonTypeInfo info)
    {
        if (info.Type != typeof(GameCommand))
        {
            return;
        }

        info.PolymorphismOptions = new JsonPolymorphismOptions
        {
            TypeDiscriminatorPropertyName = "type",
            UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
        };
        foreach (var (type, name) in CommandTypes)
        {
            info.PolymorphismOptions.DerivedTypes.Add(new JsonDerivedType(type, name));
        }
    }

    /// <summary>Writes strongly typed ids as plain numbers.</summary>
    private sealed class IdConverter<T>(Func<int, T> create, Func<T, int> value) : JsonConverter<T>
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => create(reader.GetInt32());

        public override void Write(Utf8JsonWriter writer, T id, JsonSerializerOptions options) => writer.WriteNumberValue(value(id));
    }
}
