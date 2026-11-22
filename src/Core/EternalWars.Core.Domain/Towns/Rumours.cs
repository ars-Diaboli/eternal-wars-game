using System.Text.RegularExpressions;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;

namespace EternalWars.Core.Domain.Towns;

/// <summary>
/// The rumour table (<c>src/Core/data/rumours.csv</c>, TOWN-17 h, ADR-296): <c>id, text, notes</c>. A text may name things the game fills in:
/// <c>{strongest}</c> (the player with the strongest armies), <c>{richest}</c> (the most gold), <c>{mosttowns}</c> and <c>{towns}</c> (the player with
/// the most towns and how many), <c>{treasure}</c> and <c>{where}</c> (an artifact lying on the map and the part of the land it lies in).
/// </summary>
public static partial class Rumours
{
    public const string ResourceName = "EternalWars.Rumours.csv";

    public static IReadOnlyList<string> Columns { get; } = ["id", "text", "notes"];

    /// <summary>The shipped table's texts.</summary>
    public static IReadOnlyList<string> Default { get; } = Parse(EmbeddedTable());

    public static IReadOnlyList<string> Parse(string text) =>
        CsvTable.Parse(text, "Rumour", ["id", "text"])
            .Read(row => row.Text("id") is { Length: > 0 } ? row.Text("text") : null)
            .OfType<string>()
            .ToList();

    public static string EmbeddedTable()
    {
        using var stream = typeof(Rumours).Assembly.GetManifestResourceStream(ResourceName)
                           ?? throw new InvalidOperationException($"The rumour table '{ResourceName}' is not embedded.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// The rumour <paramref name="player"/>'s taverns tell this week when the map has none of its own: one of <paramref name="table"/>'s texts whose
    /// names the game can fill, drawn from a source seeded by the game's seed, the player and the week (like the tavern's offer, ADR-070), so it
    /// is the same in every tavern of the player and all week while what it names stays (an artifact picked up, or a new leader, may change it),
    /// replays and saves give it back without storing it, and it draws nothing from the game's own random source. Players out of the game are
    /// never named. Null when no text can be filled.
    /// </summary>
    public static string? OfWeek(GameState state, PlayerId player, IReadOnlyList<string>? table = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        var week = (ulong)state.Date.AbsoluteWeek;
        var random = new GameRandom((state.Options.Seed * 0xD1B54A32D192ED03UL) ^ ((ulong)(player.Value + 1) * 0x9E3779B97F4A7C15UL) ^ (week * 0xBF58476D1CE4E5B9UL));
        var treasures = state.ObjectsOf<ArtifactPickup>().Where(a => a.Artifact is not null).OrderBy(a => a.Id.Value).ToList();
        var treasure = treasures.Count == 0 ? null : treasures[random.Next(treasures.Count)];
        var values = Values(state, treasure);
        var fit = (table ?? Default).Select(t => Fill(t, values)).OfType<string>().ToList();
        return fit.Count == 0 ? null : fit[random.Next(fit.Count)];
    }

    private static Dictionary<string, string> Values(GameState state, ArtifactPickup? treasure)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        string Best(ThievesGuildRow row, out long value)
        {
            var best = ThievesGuild.Rank(state, row).FirstOrDefault();
            value = best?.Value ?? 0;
            return best is null || best.Value <= 0 ? "" : state.GetPlayer(best.Player).Name;
        }

        if (Best(ThievesGuildRow.ArmyStrength, out _) is { Length: > 0 } strongest)
        {
            values["strongest"] = strongest;
        }

        if (Best(ThievesGuildRow.Gold, out _) is { Length: > 0 } richest)
        {
            values["richest"] = richest;
        }

        if (Best(ThievesGuildRow.Towns, out var towns) is { Length: > 0 } most)
        {
            values["mosttowns"] = most;
            values["towns"] = towns.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (treasure is not null && state.Artifacts.TryGet(treasure.Artifact!.Value, out var artifact))
        {
            values["treasure"] = artifact.Name;
            values["where"] = Region(state, treasure.Position);
        }

        return values;
    }

    /// <summary>"north-west", "middle", "south", or "depths" for the underground, by the map's thirds.</summary>
    public static string Region(GameState state, MapPosition position)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (position.Level > 0)
        {
            return "depths";
        }

        var column = Math.Clamp(position.X * 3 / Math.Max(1, state.Map.Width), 0, 2);
        var row = Math.Clamp(position.Y * 3 / Math.Max(1, state.Map.Height), 0, 2);
        var northSouth = row == 0 ? "north" : row == 2 ? "south" : "";
        var westEast = column == 0 ? "west" : column == 2 ? "east" : "";
        return (northSouth, westEast) switch
        {
            ("", "") => "middle",
            (_, "") => northSouth,
            ("", _) => westEast,
            _ => $"{northSouth}-{westEast}",
        };
    }

    /// <summary>The text with its names filled, or null when it names something the game has not got.</summary>
    private static string? Fill(string text, IReadOnlyDictionary<string, string> values)
    {
        var missing = false;
        var filled = Token().Replace(text, m =>
        {
            if (values.TryGetValue(m.Groups[1].Value, out var value))
            {
                return value;
            }

            missing = true;
            return m.Value;
        });
        return missing ? null : filled;
    }

    [GeneratedRegex(@"\{([a-z]+)\}")]
    private static partial Regex Token();
}
