using System.Text;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
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
            Players = state.Players
                .Select(p => new PlayerSlotDocument(p.Name, p.IsHuman, p.Team.Value == p.Id.Value ? null : p.Team.Value))
                .ToList(),
            TeamsLocked = state.TeamsLocked,
            GuardMode = state.GuardMode == GuardMode.Classic ? null : state.GuardMode,
            Heroes = state.Heroes
                .Select(h => new HeroPlacementDocument(
                    h.Id.Value, h.Name, h.Owner.Value, h.Position.X, h.Position.Y, h.Position.Level,
                    h.Attack, h.Defense, h.Tactics == SkillLevel.None ? null : h.Tactics,
                    h.Army.IsEmpty ? null : h.Army.Stacks.Select(s => new ArmySlotDocument(s.Slot, s.Stack.Creature, s.Stack.Count)).ToList()))
                .ToList(),
            Objects = state.Objects.Select(ToDocument).ToList(),
        };

    public static MapDocument ToDocument(AdventureMap map) => new()
    {
        Name = map.Name,
        Description = map.Description,
        Width = map.Width,
        Height = map.Height,
        Terrain = WriteLayer(map, p => TerrainCodes.ToCode(map.GetTerrain(p))),
        Roads = map.HasRoads ? WriteLayer(map, p => RoadCodes.ToCode(map.GetRoad(p))) : null,
    };

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
        ReadLayer(document, document.Terrain, "Terrain", (p, c) => map.SetTerrain(p, TerrainCodes.FromCode(c)));
        if (document.Roads is { } roads)
        {
            ReadLayer(document, roads, "Roads", (p, c) => map.SetRoad(p, RoadCodes.FromCode(c)));
        }

        return map;
    }

    /// <summary>
    /// Builds the initial state of a scenario. <paramref name="catalog"/> resolves object definitions and
    /// <paramref name="terrains"/> supplies terrain rules (both default to the built-in content);
    /// <paramref name="options"/> are the rules chosen at game creation.
    /// Throws <see cref="FormatException"/> for inconsistent maps.
    /// </summary>
    public static GameState ToGameState(
        MapDocument document,
        MapObjectCatalog? catalog = null,
        GameOptions? options = null,
        TerrainCatalog? terrains = null,
        CreatureCatalog? creatures = null)
    {
        catalog ??= MapObjectCatalog.Default;
        creatures ??= CreatureCatalog.Default;
        var map = ToMap(document);
        var players = document.Players.Select((p, i) =>
            new Player(new PlayerId(i), p.Name, p.IsHuman, p.Team is { } team ? new TeamId(team) : null));
        var heroes = document.Heroes.Select(h => new Hero(
            new HeroId(h.Id), h.Name, new PlayerId(h.Owner), new MapPosition(h.X, h.Y, h.Level),
            ToArmy(h, creatures), h.Attack, h.Defense, h.Tactics ?? SkillLevel.None));
        var objects = document.Objects.Select(o => ToObject(o, catalog));
        try
        {
            var state = new GameState(map, players, heroes, objects, options, terrains, creatures);
            state.SetTeamsLocked(document.TeamsLocked);
            state.SetGuardMode(document.GuardMode ?? GuardMode.Classic);
            return state;
        }
        catch (ArgumentException e)
        {
            throw new FormatException($"Invalid map '{document.Name}': {e.Message}", e);
        }
    }

    private static Army ToArmy(HeroPlacementDocument hero, CreatureCatalog creatures)
    {
        var army = new CreatureStack?[Army.SlotCount];
        foreach (var slot in hero.Army ?? [])
        {
            if (!Army.IsValidSlot(slot.Slot) || army[slot.Slot] is not null || slot.Count < 1)
            {
                throw new FormatException($"Hero {hero.Id} has an invalid or duplicate army slot {slot.Slot}.");
            }

            if (!creatures.Contains(slot.Creature))
            {
                throw new FormatException($"Hero {hero.Id} has unknown creature '{slot.Creature}'.");
            }

            army[slot.Slot] = new CreatureStack(slot.Creature, slot.Count);
        }

        return new Army(army);
    }

    private static List<IReadOnlyList<string>> WriteLayer(AdventureMap map, Func<MapPosition, char> code)
    {
        var levels = new List<IReadOnlyList<string>>(map.Levels);
        var row = new StringBuilder(map.Width);
        for (var level = 0; level < map.Levels; level++)
        {
            var rows = new List<string>(map.Height);
            for (var y = 0; y < map.Height; y++)
            {
                row.Clear();
                for (var x = 0; x < map.Width; x++)
                {
                    row.Append(code(new MapPosition(x, y, level)));
                }

                rows.Add(row.ToString());
            }

            levels.Add(rows);
        }

        return levels;
    }

    private static void ReadLayer(
        MapDocument document, IReadOnlyList<IReadOnlyList<string>> layer, string name, Action<MapPosition, char> apply)
    {
        if (layer.Count != document.Terrain.Count)
        {
            throw new FormatException($"{name} has {layer.Count} levels, expected {document.Terrain.Count}.");
        }

        for (var level = 0; level < layer.Count; level++)
        {
            var rows = layer[level];
            if (rows.Count != document.Height)
            {
                throw new FormatException($"{name} level {level} has {rows.Count} rows, expected {document.Height}.");
            }

            for (var y = 0; y < rows.Count; y++)
            {
                if (rows[y].Length != document.Width)
                {
                    throw new FormatException($"{name} level {level}, row {y} has {rows[y].Length} tiles, expected {document.Width}.");
                }

                for (var x = 0; x < rows[y].Length; x++)
                {
                    apply(new MapPosition(x, y, level), rows[y][x]);
                }
            }
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
                Guard = mine.Guard is { } guard ? new GuardDocument(guard.Creature, guard.Count) : null,
            },
            TreasureChest chest => new TreasureChestDocument
            {
                Id = id, Definition = definition, X = p.X, Y = p.Y, Level = p.Level, Gold = chest.FixedValues?.Gold, Experience = chest.FixedValues?.Experience,
            },
            TwoWayGate gate => new GateDocument
            {
                Id = id, Definition = definition, X = p.X, Y = p.Y, Level = p.Level, LinkedGate = gate.LinkedGate?.Value,
            },
            OneWayGateEntrance entrance => new GateEntranceDocument
            {
                Id = id, Definition = definition, X = p.X, Y = p.Y, Level = p.Level, Exit = entrance.Exit?.Value,
            },
            OneWayGateExit => new GateExitDocument
            {
                Id = id, Definition = definition, X = p.X, Y = p.Y, Level = p.Level,
            },
            MonsterGuard monster => new MonsterDocument
            {
                Id = id, Definition = definition, X = p.X, Y = p.Y, Level = p.Level,
                Creature = monster.Creature, Count = monster.Count, Disposition = monster.Disposition, NeverFlees = monster.NeverFlees,
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
                MineDocument mine => new Mine(
                    id, definition, position, mine.Owner is { } owner ? new PlayerId(owner) : null,
                    mine.Guard is { } guard ? new GuardStack(guard.Creature, guard.Count) : null),
                TreasureChestDocument { Gold: { } gold, Experience: { } experience } => new TreasureChest(id, definition, position, new ChestValues(gold, experience)),
                TreasureChestDocument { Gold: null, Experience: null } => new TreasureChest(id, definition, position),
                TreasureChestDocument => throw new FormatException("A chest needs both gold and experience, or neither."),
                MonsterDocument monster => new MonsterGuard(id, definition, position, monster.Creature, monster.Count, monster.Disposition, monster.NeverFlees),
                GateDocument gate => new TwoWayGate(id, definition, position, ToId(gate.LinkedGate)),
                GateEntranceDocument entrance => new OneWayGateEntrance(id, definition, position, ToId(entrance.Exit)),
                GateExitDocument => new OneWayGateExit(id, definition, position),
                _ => throw new FormatException($"Unsupported object kind {document.GetType().Name}."),
            };
        }
        catch (ArgumentException e)
        {
            throw new FormatException($"Object {document.Id}: {e.Message}", e);
        }

        static MapObjectId? ToId(int? value) => value is { } v ? new MapObjectId(v) : null;
    }
}
