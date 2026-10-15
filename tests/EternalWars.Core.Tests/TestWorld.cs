using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Players;

namespace EternalWars.Core.Tests;

/// <summary>
/// Builds small worlds from ASCII art so tests read like the situation they describe.
/// Legend: <c>.</c> grass, <c>d</c> dirt, <c>s</c> swamp, <c>r</c> rough, <c>~</c> water, <c>#</c> rock,
/// <c>A</c> hero <see cref="A"/> (player 0) on grass, <c>B</c> hero <see cref="B"/> (player 1) on grass.
/// </summary>
internal static class TestWorld
{
    public static readonly HeroId A = new(1);
    public static readonly HeroId B = new(2);
    public static readonly PlayerId Blue = new(0);
    public static readonly PlayerId Red = new(1);

    public static GameState Create(params string[] rows) => CreateLevels(rows);

    public static GameState Create(GameOptions options, params string[] rows) => CreateLevels(options, rows);

    public static GameState CreateLevels(params string[][] levels) => CreateLevels(GameOptions.Default, levels);

    public static GameState CreateLevels(GameOptions options, params string[][] levels)
    {
        var map = new AdventureMap(levels[0][0].Length, levels[0].Length, levels.Length);
        var heroes = new List<Hero>();
        for (var level = 0; level < levels.Length; level++)
        {
            for (var y = 0; y < levels[level].Length; y++)
            {
                for (var x = 0; x < levels[level][y].Length; x++)
                {
                    var position = new MapPosition(x, y, level);
                    var symbol = levels[level][y][x];
                    map.SetTerrain(position, symbol switch
                    {
                        'd' => TerrainType.Dirt,
                        's' => TerrainType.Swamp,
                        'r' => TerrainType.Rough,
                        '~' => TerrainType.Water,
                        '#' => TerrainType.Rock,
                        _ => TerrainType.Grass,
                    });

                    if (symbol == 'A')
                    {
                        heroes.Add(new Hero(A, "Alpha", Blue, position));
                    }
                    else if (symbol == 'B')
                    {
                        heroes.Add(new Hero(B, "Bravo", Red, position));
                    }
                }
            }
        }

        var players = new[] { new Player(Blue, "Blue", true), new Player(Red, "Red", true) };
        return new GameState(map, players, heroes, options: options);
    }

    /// <summary>Starts a session; by default every tile is pre-explored so fog does not interfere.</summary>
    public static GameSession Start(GameState state, bool revealAll = true)
    {
        if (revealAll)
        {
            foreach (var player in state.Players)
            {
                state.Fog.RevealAll(player.Id);
            }
        }

        var session = new GameSession(state);
        session.Turns.StartGame();
        return session;
    }

    public static T Add<T>(GameState state, string definitionId, int x, int y, int level = 0)
        where T : MapObject
    {
        var mapObject = MapObject.Create(state.NextObjectId(), MapObjectCatalog.Default.Get(definitionId), new MapPosition(x, y, level));
        state.AddObject(mapObject);
        return (T)mapObject;
    }

    public static void Link(SubterraneanGate first, SubterraneanGate second)
    {
        first.LinkedGate = second.Id;
        second.LinkedGate = first.Id;
    }
}
