using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Game;

namespace EternalWars.Core.Tests;

/// <summary>Saves scenarios in the map format and loads them back, the way the editor and the game do.</summary>
internal static class TestFormat
{
    /// <summary>The map-format JSON of <paramref name="state"/>.</summary>
    public static string Save(GameState state) => MapSerializer.Serialize(MapConverter.ToDocument(state));

    /// <summary>Loads map-format JSON into a game state (arguments as for <see cref="MapConverter.ToGameState"/>).</summary>
    public static GameState Load(string json, ContentCatalogs? content = null, GameOptions? options = null, bool checkEvents = true) =>
        MapConverter.ToGameState(MapSerializer.Deserialize(json), content, options, checkEvents);

    /// <summary>Saves <paramref name="state"/> to JSON and loads it back.</summary>
    public static GameState RoundTrip(GameState state, ContentCatalogs? content = null, GameOptions? options = null, bool checkEvents = true) =>
        Load(Save(state), content, options, checkEvents);
}
