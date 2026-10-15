using System.Text;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Players;

namespace EternalWars.Core.Data.Maps;

/// <summary>Maps between <see cref="MapDocument"/> and domain objects.</summary>
public static class MapConverter
{
    public static MapDocument ToDocument(GameState state) =>
        ToDocument(state.Map) with
        {
            Players = state.Players.Select(p => new PlayerSlotDocument(p.Name, p.IsHuman)).ToList(),
            Heroes = state.Heroes
                .Select(h => new HeroPlacementDocument(
                    h.Id.Value, h.Name, h.Owner.Value, h.Position.X, h.Position.Y, h.Position.Level))
                .ToList(),
        };

    public static MapDocument ToDocument(AdventureMap map)
    {
        var levels = new List<IReadOnlyList<string>>(map.Levels);
        for (var level = 0; level < map.Levels; level++)
        {
            var rows = new List<string>(map.Height);
            var row = new StringBuilder(map.Width);
            for (var y = 0; y < map.Height; y++)
            {
                row.Clear();
                for (var x = 0; x < map.Width; x++)
                {
                    row.Append(TerrainCodes.ToCode(map.GetTerrain(new MapPosition(x, y, level))));
                }

                rows.Add(row.ToString());
            }

            levels.Add(rows);
        }

        return new MapDocument { Name = map.Name, Width = map.Width, Height = map.Height, Terrain = levels };
    }

    public static AdventureMap ToMap(MapDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.FormatVersion > MapDocument.CurrentFormatVersion)
        {
            throw new NotSupportedException(
                $"Map format v{document.FormatVersion} is newer than supported v{MapDocument.CurrentFormatVersion}.");
        }

        var map = new AdventureMap(document.Width, document.Height, document.Terrain.Count) { Name = document.Name };
        for (var level = 0; level < document.Terrain.Count; level++)
        {
            var rows = document.Terrain[level];
            if (rows.Count != document.Height)
            {
                throw new FormatException($"Level {level} has {rows.Count} rows, expected {document.Height}.");
            }

            for (var y = 0; y < rows.Count; y++)
            {
                if (rows[y].Length != document.Width)
                {
                    throw new FormatException($"Level {level}, row {y} has {rows[y].Length} tiles, expected {document.Width}.");
                }

                for (var x = 0; x < rows[y].Length; x++)
                {
                    map.SetTerrain(new MapPosition(x, y, level), TerrainCodes.FromCode(rows[y][x]));
                }
            }
        }

        return map;
    }

    public static GameState ToGameState(MapDocument document)
    {
        var map = ToMap(document);
        var players = document.Players.Select((p, i) => new Player(new PlayerId(i), p.Name, p.IsHuman));
        var heroes = document.Heroes.Select(h =>
            new Hero(new HeroId(h.Id), h.Name, new PlayerId(h.Owner), new MapPosition(h.X, h.Y, h.Level)));
        return new GameState(map, players, heroes);
    }
}
