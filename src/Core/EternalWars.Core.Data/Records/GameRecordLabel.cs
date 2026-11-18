using System.Buffers;
using System.Text.Json;

namespace EternalWars.Core.Data.Records;

/// <summary>
/// What the save menu and the replay list need of a record file (ADR-224): its save label, and the name of its map for a file without one (a
/// replay, a save from before labels).
/// </summary>
/// <param name="Save">The record's <see cref="GameRecordDocument.Save"/>; null when it has none.</param>
/// <param name="MapName">The map's name when it came before the label in the file (it does in every file the game writes); null otherwise.</param>
public sealed record GameRecordLabel(SaveInfo? Save, string? MapName)
{
    private const int FirstBufferSize = 16 * 1024;

    /// <summary>
    /// Reads the label of a record file without reading the record: a forward-only <see cref="Utf8JsonReader"/> pass over the bytes, a buffer at
    /// a time, that takes the map's name on the way, steps over the map and the commands without building them, and stops once it has the
    /// <c>save</c> object. A file that is not JSON up to there throws <see cref="JsonException"/>, one that is not a record object
    /// <see cref="FormatException"/>; what comes after the label is not read at all.
    /// </summary>
    public static GameRecordLabel Read(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1, FileOptions.SequentialScan);
        return Read(stream);
    }

    /// <summary>Reads the label from <paramref name="stream"/> like <see cref="Read(string)"/>, from its current position.</summary>
    public static GameRecordLabel Read(Stream stream) => Read(stream, FirstBufferSize);

    /// <summary><see cref="Read(Stream)"/> starting with a buffer of <paramref name="bufferSize"/> bytes (tests cut tokens with small ones).</summary>
    internal static GameRecordLabel Read(Stream stream, int bufferSize)
    {
        var options = GameRecordSerializer.Options;
        var state = new JsonReaderState(new JsonReaderOptions
        {
            CommentHandling = options.ReadCommentHandling,
            AllowTrailingCommas = options.AllowTrailingCommas,
            MaxDepth = options.MaxDepth,
        });
        var scan = new Scan();
        var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        try
        {
            var (start, length, final, first) = (0, 0, false, true);
            while (true)
            {
                while (!final && length < buffer.Length)
                {
                    var read = stream.Read(buffer, length, buffer.Length - length);
                    final = read == 0;
                    length += read;
                }

                if (first && length >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
                {
                    start = 3; // a UTF-8 byte order mark, as File.ReadAllText skips it
                }

                first = false;
                var reader = new Utf8JsonReader(buffer.AsSpan(start, length - start), final, state);
                if (scan.Run(ref reader))
                {
                    return new GameRecordLabel(scan.Save, scan.MapName);
                }

                if (final)
                {
                    throw new JsonException("The file ends before the record does.");
                }

                // Keep the bytes not yet consumed (a token or the label cut by the buffer's end) and read on after them.
                state = reader.CurrentState;
                var kept = length - start - (int)reader.BytesConsumed;
                if (kept == buffer.Length)
                {
                    var larger = ArrayPool<byte>.Shared.Rent(buffer.Length * 2);
                    Buffer.BlockCopy(buffer, start, larger, 0, kept);
                    ArrayPool<byte>.Shared.Return(buffer);
                    buffer = larger;
                }
                else
                {
                    Buffer.BlockCopy(buffer, length - kept, buffer, 0, kept);
                }

                (start, length) = (0, kept);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private enum Phase
    {
        Start,
        Root,
        SaveValue,
        MapValue,
        MapProperties,
        MapName,
        SkipValue,
        Skipping,
    }

    /// <summary>Where the pass is in the record, kept across buffers: the reader itself only lives for one buffer.</summary>
    private sealed class Scan
    {
        private Phase _phase = Phase.Start;
        private Phase _afterSkip;
        private int _skipDepth;

        public SaveInfo? Save { get; private set; }

        public string? MapName { get; private set; }

        /// <summary>True when the label is read (or the record ended); false when the buffer ran out, with the reader back at the last whole step.</summary>
        public bool Run(ref Utf8JsonReader reader)
        {
            while (true)
            {
                var checkpoint = reader;
                if (!reader.Read())
                {
                    reader = checkpoint;
                    return false;
                }

                switch (_phase)
                {
                    case Phase.Start:
                        _phase = reader.TokenType switch
                        {
                            JsonTokenType.StartObject => Phase.Root,
                            JsonTokenType.Null => throw new FormatException("Replay file is empty."),
                            _ => throw new FormatException("The file is not a game record."),
                        };
                        break;
                    case Phase.Root when reader.TokenType == JsonTokenType.EndObject:
                        return true;
                    case Phase.Root:
                        _phase = reader.GetString() switch
                        {
                            var name when Is(name, "save") => Phase.SaveValue,
                            var name when Is(name, "map") => Phase.MapValue,
                            _ => SkipThen(Phase.Root),
                        };
                        break;
                    case Phase.SaveValue when reader.TokenType == JsonTokenType.Null:
                        _phase = Phase.Root;
                        break;
                    case Phase.SaveValue:
                        var probe = reader;
                        if (!probe.TrySkip())
                        {
                            reader = checkpoint;
                            return false;
                        }

                        Save = JsonSerializer.Deserialize<SaveInfo>(ref reader, GameRecordSerializer.Options);
                        return true;
                    case Phase.MapValue:
                        _phase = reader.TokenType == JsonTokenType.StartObject ? Phase.MapProperties : AfterValue(ref reader, Phase.Root);
                        break;
                    case Phase.MapProperties when reader.TokenType == JsonTokenType.EndObject:
                        _phase = Phase.Root;
                        break;
                    case Phase.MapProperties:
                        _phase = Is(reader.GetString(), "name") ? Phase.MapName : SkipThen(Phase.MapProperties);
                        break;
                    case Phase.MapName:
                        MapName = reader.TokenType == JsonTokenType.String ? reader.GetString() : MapName;
                        _phase = AfterValue(ref reader, Phase.MapProperties);
                        break;
                    case Phase.SkipValue:
                        _phase = AfterValue(ref reader, _afterSkip);
                        break;
                    case Phase.Skipping:
                        if (reader.TokenType is JsonTokenType.EndObject or JsonTokenType.EndArray && reader.CurrentDepth == _skipDepth)
                        {
                            _phase = _afterSkip;
                        }

                        break;
                }
            }
        }

        /// <summary>Property names match without case, like the record's reader (web defaults).</summary>
        private static bool Is(string? name, string expected) => string.Equals(name, expected, StringComparison.OrdinalIgnoreCase);

        private Phase SkipThen(Phase then)
        {
            _afterSkip = then;
            return Phase.SkipValue;
        }

        /// <summary>After a value's first token: an object or array is stepped over to its end before going on with <paramref name="then"/>.</summary>
        private Phase AfterValue(ref Utf8JsonReader reader, Phase then)
        {
            if (reader.TokenType is not (JsonTokenType.StartObject or JsonTokenType.StartArray))
            {
                return then;
            }

            (_skipDepth, _afterSkip) = (reader.CurrentDepth, then);
            return Phase.Skipping;
        }
    }
}
