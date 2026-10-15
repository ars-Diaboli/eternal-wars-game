using System.Text;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
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
            Objects = state.Objects.Select(ToDocument).ToList(),
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

        return new MapDocument
        {
            Name = map.Name,
            Description = map.Description,
            Width = map.Width,
            Height = map.Height,
            Terrain = levels,
        };
    }

    public static AdventureMap ToMap(MapDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.FormatVersion > MapDocument.CurrentFormatVersion)
        {
            throw new NotSupportedException(
                $"Map format v{document.FormatVersion} is newer than supported v{MapDocument.CurrentFormatVersion}.");
        }

        var map = new AdventureMap(document.Width, document.Height, document.Terrain.Count)
        {
            Name = document.Name,
            Description = document.Description,
        };
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

    /// <summary>
    /// Builds the initial state of a scenario. <paramref name="catalog"/> resolves object definitions
    /// (defaults to the built-in catalog); <paramref name="options"/> are the rules chosen at game creation.
    /// Throws <see cref="FormatException"/> for inconsistent maps.
    /// </summary>
    public static GameState ToGameState(MapDocument document, MapObjectCatalog? catalog = null, GameOptions? options = null)
    {
        catalog ??= MapObjectCatalog.Default;
        var map = ToMap(document);
        var players = document.Players.Select((p, i) => new Player(new PlayerId(i), p.Name, p.IsHuman));
        var heroes = document.Heroes.Select(h =>
            new Hero(new HeroId(h.Id), h.Name, new PlayerId(h.Owner), new MapPosition(h.X, h.Y, h.Level)));
        var objects = document.Objects.Select(o => ToObject(o, catalog));
        try
        {
            return new GameState(map, players, heroes, objects, options);
        }
        catch (ArgumentException e)
        {
            throw new FormatException($"Invalid map '{document.Name}': {e.Message}", e);
        }
    }

    private static MapObjectDocument ToDocument(MapObject mapObject)
    {
        var (id, definition, p) = (mapObject.Id.Value, mapObject.Definition.Id, mapObject.Position);
        return mapObject switch
        {
            ResourcePile pile => new ResourcePileDocument
            {
                Id = id, Definition = definition, X = p.X, Y = p.Y, Level = p.Level, Amount = pile.Amount,
            },
            Mine mine => new MineDocument
            {
                Id = id, Definition = definition, X = p.X, Y = p.Y, Level = p.Level, Owner = mine.Owner?.Value,
            },
            TreasureChest chest => new TreasureChestDocument
            {
                Id = id, Definition = definition, X = p.X, Y = p.Y, Level = p.Level, Gold = chest.Gold, Experience = chest.Experience,
            },
            SubterraneanGate gate => new GateDocument
            {
                Id = id, Definition = definition, X = p.X, Y = p.Y, Level = p.Level, LinkedGate = gate.LinkedGate?.Value,
            },
            _ => throw new NotSupportedException($"Cannot save {mapObject.GetType().Name}."),
        };
    }

    private static MapObject ToObject(MapObjectDocument document, MapObjectCatalog catalog)
    {
        if (!catalog.TryGet(document.Definition, out var definition))
        {
            throw new FormatException($"Object {document.Id} uses unknown definition '{document.Definition}'.");
        }

        var id = new MapObjectId(document.Id);
        var position = new MapPosition(document.X, document.Y, document.Level);
        try
        {
            return document switch
            {
                ResourcePileDocument pile => new ResourcePile(id, definition, position, pile.Amount),
                MineDocument mine => new Mine(id, definition, position, mine.Owner is { } owner ? new PlayerId(owner) : null),
                TreasureChestDocument chest => new TreasureChest(id, definition, position, chest.Gold, chest.Experience),
                GateDocument gate => new SubterraneanGate(id, definition, position, gate.LinkedGate is { } link ? new MapObjectId(link) : null),
                _ => throw new FormatException($"Unsupported object kind {document.GetType().Name}."),
            };
        }
        catch (ArgumentException e)
        {
            throw new FormatException($"Object {document.Id}: {e.Message}", e);
        }
    }
}
