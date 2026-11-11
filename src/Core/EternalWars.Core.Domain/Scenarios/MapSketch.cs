using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Players;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Scenarios;

/// <summary>Which way a <see cref="MapSketch"/> mirrors what it places: none, left to right, or top to bottom.</summary>
internal enum MirrorAxis
{
    None,
    X,
    Y,
}

/// <summary>
/// Builds a hand-made map in code (the shipped arena maps, ADR-180): the terrain from rows of letters, objects by their top-left cell. While
/// <see cref="Mirror"/> is set, every cell given is mirrored across the map, so one side's layout written once makes the other side's.
/// </summary>
internal sealed class MapSketch
{
    private readonly ContentCatalogs _content;
    private readonly List<MapObject> _objects = [];

    public MapSketch(ContentCatalogs content, string name, string description, IReadOnlyList<string> rows)
    {
        _content = content;
        Map = new AdventureMap(rows[0].Length, rows.Count, 1) { Name = name, Description = description };
        for (var y = 0; y < rows.Count; y++)
        {
            if (rows[y].Length != Map.Width)
            {
                throw new ArgumentException($"Row {y} is {rows[y].Length} cells wide, not {Map.Width}.", nameof(rows));
            }

            for (var x = 0; x < Map.Width; x++)
            {
                Map.SetTerrain(new MapPosition(x, y), Terrain(rows[y][x]));
            }
        }
    }

    public AdventureMap Map { get; }

    /// <summary>The mirror applied to the cells given from now on.</summary>
    public MirrorAxis Mirror { get; set; }

    /// <summary>The terrain letters: g grass, d dirt, r rough, h highlands, f forest floor, w wasteland, s snow, l lava, a sand, m swamp, ~ water, # rock.</summary>
    public static TerrainType Terrain(char letter) => letter switch
    {
        'g' => TerrainType.Grass,
        'd' => TerrainType.Dirt,
        'r' => TerrainType.Rough,
        'h' => TerrainType.Highlands,
        'f' => TerrainType.ForestFloor,
        'w' => TerrainType.Wasteland,
        's' => TerrainType.Snow,
        'l' => TerrainType.Lava,
        'a' => TerrainType.Sand,
        'm' => TerrainType.Swamp,
        '~' => TerrainType.Water,
        '#' => TerrainType.Rock,
        _ => throw new ArgumentOutOfRangeException(nameof(letter), letter, "Unknown terrain letter."),
    };

    /// <summary>Left half, middle and the left half mirrored: rows of a map whose two sides are each other's mirror image.</summary>
    public static IReadOnlyList<string> MirroredX(IReadOnlyList<string> half, IReadOnlyList<string> middle) =>
        half.Select((row, y) => row + middle[y] + new string(row.Reverse().ToArray())).ToList();

    /// <summary>Top half, middle rows and the top half upside down (its letters recoloured by <paramref name="recolor"/>).</summary>
    public static IReadOnlyList<string> MirroredY(IReadOnlyList<string> half, IReadOnlyList<string> middle, Func<char, char> recolor) =>
        [.. half, .. middle, .. half.Reverse().Select(row => new string(row.Select(recolor).ToArray()))];

    /// <summary>The cell given, mirrored by <see cref="Mirror"/>, for something <paramref name="width"/> × <paramref name="height"/> placed there.</summary>
    public MapPosition At(int x, int y, int width = 1, int height = 1) => Mirror switch
    {
        MirrorAxis.X => new MapPosition(Map.Width - x - width, y),
        MirrorAxis.Y => new MapPosition(x, Map.Height - y - height),
        _ => new MapPosition(x, y),
    };

    public MapObject Place(string definitionId, int x, int y)
    {
        var definition = _content.Objects.Get(definitionId);
        var mapObject = MapObject.Create(new MapObjectId(_objects.Count + 1), definition, At(x, y, definition.Footprint.Width, definition.Footprint.Height));
        _objects.Add(mapObject);
        return mapObject;
    }

    /// <summary>A town of <paramref name="type"/>, or a random one (ADR-126) that takes its owner's faction chosen on the new-game screen.</summary>
    public Town AddTown(string? type, int x, int y, PlayerId owner, string name)
    {
        var town = (Town)Place(BuiltInMapObjects.TownId(type ?? TownIds.Haven), x, y);
        (town.Owner, town.Name, town.IsRandom) = (owner, name, type is null);
        return town;
    }

    public void Pile(ResourceType resource, int x, int y, int amount) => ((ResourcePile)Place(BuiltInMapObjects.PileId(resource), x, y)).Amount = amount;

    public void Monster(string creature, int count, int x, int y, GuardDisposition disposition = GuardDisposition.Hostile)
    {
        var monster = (MonsterGuard)Place(BuiltInMapObjects.Monster, x, y);
        (monster.Creature, monster.Count, monster.Disposition) = (creature, count, disposition);
    }

    public void Box(int x, int y, BoxContents contents, params CreatureStack[] guard)
    {
        var box = (PandorasBox)Place(BuiltInMapObjects.PandorasBox, x, y);
        (box.Contents, box.Guard) = (contents, guard);
    }

    public void WitchHut(int x, int y, string first, string second) => ((WitchHut)Place(BuiltInMapObjects.WitchHut, x, y)).Skills = [first, second];

    public void Dwelling(string creature, int x, int y) => ((CreatureDwelling)Place(BuiltInMapObjects.Dwelling, x, y)).Creature = creature;

    /// <summary>
    /// A hero placeholder (ADR-126): a random hero of its owner's faction. It keeps <paramref name="army"/> rather than its own starter
    /// stack, which ranges from a few tier-4 creatures to a tier-1 stack, so every player starts the arena with the same troops.
    /// </summary>
    public Hero AddRandomHero(int id, PlayerId owner, int x, int y, Army army) =>
        Hero.Create(new HeroId(id), owner, At(x, y), army, definition: null, placeholder: true);

    public Hero AddHero(int id, PlayerId owner, int x, int y, string roster, Army army) =>
        Hero.Create(new HeroId(id), owner, At(x, y), army, _content.Heroes.Get(roster));

    /// <summary>The map, with the arena switch on (ADR-158).</summary>
    public GameState BuildArena(IEnumerable<Player> players, IEnumerable<Hero> heroes)
    {
        var state = new GameState(Map, players, heroes, _objects, content: _content);
        state.SetRules(ScenarioRules.Default with { Arena = true });
        return state;
    }
}
