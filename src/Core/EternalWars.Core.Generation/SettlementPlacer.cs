using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Generation;

/// <summary>
/// Stage 4 of the generator (ADR-064): towns, mines and resource piles, zone by zone. HoMM3-style defaults:
/// <list type="bullet">
/// <item><b>Start zones</b> get their player towns (<see cref="TemplateZone.PlayerTowns"/>) in the middle, as towns of a placeholder type
/// (the zone's first allowed town type, else Haven) that <see cref="GeneratedMap.SetTownType"/> changes when players choose theirs. They start
/// unowned on the map; <see cref="GeneratedTown.Slot"/> says whose they are. Their mines stand close to the town and are not guarded.</item>
/// <item><b>Neutral towns</b> (<see cref="TemplateZone.NeutralTowns"/>) are of a type from the zone's list, else any playable town, and
/// garrisoned with two stacks of the zone's monster strength (<see cref="ZoneGuards.TownGarrisonValue"/>).</item>
/// <item><b>Mines</b>: the zone's minimum of each resource, anywhere in the zone, guarded by the monster strength of the zone
/// (<see cref="ZoneGuards.MineGuardValue"/>) unless <see cref="TemplateZone.MineGuards"/> says otherwise or it is a start zone.</item>
/// <item><b>Piles</b>: <see cref="TemplateZone.ExtraPiles"/> resource piles (mostly gold, then lumber and ore, rarely the rare resources), unguarded; their amounts are rolled when the game starts.</item>
/// </list>
/// Everything stands on free ground with a free tile all around it, so it cannot block a way; a zone's objects must all be reachable from each other
/// (a problem is listed otherwise). What cannot be placed is listed in <see cref="GeneratedMap.Problems"/>. Streams: <c>towns</c>, <c>mines</c>, <c>piles</c> per zone.
/// </summary>
public static class SettlementPlacer
{
    /// <summary>A mine in a start zone stands within this many tiles of the zone's town, at most (less in a small zone).</summary>
    public const int StartMineRadius = 14;

    private static readonly (int Dx, int Dy)[] Four = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    /// <summary>Places the towns, mines and piles; <paramref name="startTowns"/> (player slot → town type, RMG-7) gives start towns their type at once.</summary>
    public static void Place(MapTemplate template, GeneratedMap map, ulong seed, CreatureCatalog? creatures = null, IReadOnlyDictionary<int, string>? startTowns = null)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(map);
        creatures ??= CreatureCatalog.Default;
        if (map.Layout.ZoneIds.Count != template.Zones.Count)
        {
            throw new ArgumentException("The map was not made from this template.", nameof(map));
        }

        var streams = new SeedStreams(seed);
        for (var zone = 0; zone < template.Zones.Count; zone++)
        {
            var spec = template.Zones[zone];
            var tiles = map.Layout.Tiles(zone).Select(t => new MapPosition(t.X, t.Y, t.Level)).ToList();
            var isStart = spec.Type is ZoneType.PlayerStart or ZoneType.AiStart;
            var townVisits = new List<MapPosition>();

            if (isStart)
            {
                PlayerTowns(map, spec, zone, tiles, townVisits, streams.For("towns", zone), spec.Owner is { } slot && startTowns?.GetValueOrDefault(slot) is { } picked ? picked : null);
            }

            NeutralTowns(map, spec, zone, tiles, townVisits, streams.For("towns", zone + 1000), creatures);
            Mines(map, spec, zone, tiles, isStart && townVisits.Count > 0 ? townVisits[0] : null, streams.For("mines", zone), creatures);
            Piles(map, spec, zone, tiles, streams.For("piles", zone));
            CheckReachable(map, spec, tiles);
        }
    }

    // Towns -----------------------------------------------------------------------------------

    private static void PlayerTowns(GeneratedMap map, TemplateZone spec, int zone, List<MapPosition> tiles, List<MapPosition> visits, GameRandom random, string? picked)
    {
        var count = Math.Max(1, spec.PlayerTowns.Min + random.Next(spec.PlayerTowns.Max - spec.PlayerTowns.Min + 1));
        var type = picked ?? (spec.TownTypes is { Count: > 0 } list ? list[0] : TownIds.Haven);
        var definition = MapObjectCatalog.Default.Get(BuiltInMapObjects.TownId(type));
        var centre = Centroid(tiles);
        for (var i = 0; i < count; i++)
        {
            // The first in the middle of the zone, the next ones near it but a town apart.
            if ((Find(map, definition, tiles, random, centre, closest: true, avoid: visits, spacing: 12) ?? Find(map, definition, tiles, random, centre, closest: true, avoid: visits, spacing: 12, margin: false)) is not { } spot)
            {
                map.Problem($"Zone '{spec.Id}': no room for player town {i + 1} of {count}.");
                return;
            }

            var town = map.Add(new Town(map.NextId(), definition, spot));
            map.AddTown(new GeneratedTown(spec.Owner, zone, town));
            visits.Add(town.VisitPosition);
        }
    }

    private static void NeutralTowns(GeneratedMap map, TemplateZone spec, int zone, List<MapPosition> tiles, List<MapPosition> visits, GameRandom random, CreatureCatalog creatures)
    {
        var count = spec.NeutralTowns.Min + random.Next(spec.NeutralTowns.Max - spec.NeutralTowns.Min + 1);
        var types = spec.TownTypes is { Count: > 0 } list ? list : BuiltInMapObjects.PlayableTowns;
        for (var i = 0; i < count; i++)
        {
            var definition = MapObjectCatalog.Default.Get(BuiltInMapObjects.TownId(types[random.Next(types.Count)]));
            if ((Find(map, definition, tiles, random, null, closest: false, avoid: visits, spacing: 12) ?? Find(map, definition, tiles, random, null, closest: false, avoid: visits, spacing: 8, margin: false)) is not { } spot)
            {
                map.Problem($"Zone '{spec.Id}': no room for neutral town {i + 1} of {count}.");
                return;
            }

            var value = ZoneGuards.TownGarrisonValue(spec.Monsters);
            var (first, firstCount) = GuardPicker.Pick(creatures, value / 2, random);
            var (second, secondCount) = GuardPicker.Pick(creatures, value / 2, random);
            var town = map.Add(new Town(map.NextId(), definition, spot, garrison: Army.Of(new CreatureStack(first.Id, firstCount), new CreatureStack(second.Id, secondCount))));
            map.AddTown(new GeneratedTown(null, zone, town));
            visits.Add(town.VisitPosition);
        }
    }

    // Mines and piles -------------------------------------------------------------------------

    private static void Mines(GeneratedMap map, TemplateZone spec, int zone, List<MapPosition> tiles, MapPosition? townVisit, GameRandom random, CreatureCatalog creatures)
    {
        var isStart = spec.Type is ZoneType.PlayerStart or ZoneType.AiStart;
        var guarded = spec.MineGuards switch
        {
            MineGuarding.Always => true,
            MineGuarding.Never => false,
            _ => !isStart && spec.Monsters != MonsterStrength.None,
        };
        var radius = Math.Min(StartMineRadius, Math.Max(8, (int)Math.Sqrt(tiles.Count) / 3));
        var placed = new List<MapPosition>();
        foreach (var resource in Enum.GetValues<ResourceType>())
        {
            for (var i = 0; i < spec.Mines.GetValueOrDefault(resource); i++)
            {
                var definition = MapObjectCatalog.Default.Get(BuiltInMapObjects.MineId(resource));
                var near = townVisit is { } visit ? (MapPosition?)visit : null;
                var spot = Find(map, definition, tiles, random, near, closest: false, avoid: placed, spacing: 4, radius: radius)
                           ?? Find(map, definition, tiles, random, null, closest: false, avoid: placed, spacing: 4);
                if (spot is not { } position)
                {
                    map.Problem($"Zone '{spec.Id}': no room for a {resource} mine.");
                    continue;
                }

                GuardStack? guard = null;
                if (guarded)
                {
                    var strength = spec.Monsters == MonsterStrength.None ? MonsterStrength.Weak : spec.Monsters;
                    var (creature, count) = GuardPicker.Pick(creatures, ZoneGuards.MineGuardValue(strength, resource), random);
                    guard = new GuardStack(creature.Id, count);
                }

                var mine = map.Add(new Mine(map.NextId(), definition, position, guard: guard));
                placed.Add(mine.VisitPosition);
            }
        }
    }

    private static readonly (ResourceType Resource, int Weight)[] PileWeights =
    [
        (ResourceType.Gold, 3), (ResourceType.Lumber, 2), (ResourceType.Ore, 2), (ResourceType.Crystal, 1), (ResourceType.Mercury, 1), (ResourceType.Sulfur, 1),
    ];

    private static void Piles(GeneratedMap map, TemplateZone spec, int zone, List<MapPosition> tiles, GameRandom random)
    {
        var placed = new List<MapPosition>();
        for (var i = 0; i < spec.ExtraPiles; i++)
        {
            var roll = random.Next(PileWeights.Sum(w => w.Weight));
            var resource = PileWeights.First(w => (roll -= w.Weight) < 0).Resource;
            var definition = MapObjectCatalog.Default.Get(BuiltInMapObjects.PileId(resource));
            if (Find(map, definition, tiles, random, null, closest: false, avoid: placed, spacing: 3) is not { } spot)
            {
                map.Problem($"Zone '{spec.Id}': no room for resource pile {i + 1} of {spec.ExtraPiles}.");
                return;
            }

            placed.Add(map.Add(new ResourcePile(map.NextId(), definition, spot)).VisitPosition);
        }
    }

    // Placing ---------------------------------------------------------------------------------

    private static MapPosition Centroid(List<MapPosition> tiles) =>
        new((int)tiles.Average(t => t.X), (int)tiles.Average(t => t.Y), tiles[0].Level);

    /// <summary>
    /// A place for the object in the zone: free ground (with a free tile all around, unless <paramref name="margin"/> is off, the fallback for towns in a crowded zone), at least <paramref name="spacing"/> from the entrances in
    /// <paramref name="avoid"/>. With <paramref name="near"/> only places within <paramref name="radius"/> of it count; <paramref name="closest"/> takes the
    /// one nearest to it, otherwise one is drawn.
    /// </summary>
    internal static MapPosition? Find(GeneratedMap map, MapObjectDefinition definition, List<MapPosition> tiles, GameRandom random, MapPosition? near, bool closest,
        List<MapPosition> avoid, int spacing, int radius = int.MaxValue, bool margin = true)
    {
        var (vx, vy) = definition.Footprint.VisitOffset;
        var candidates = new List<MapPosition>();
        foreach (var tile in tiles)
        {
            var entrance = tile.Offset(vx, vy);
            if (!map.Fits(definition, tile) || (margin && !ConnectionPlacer.Margin(map, definition, tile)))
            {
                continue;
            }

            if (avoid.Any(a => a.Level == entrance.Level && Math.Max(Math.Abs(a.X - entrance.X), Math.Abs(a.Y - entrance.Y)) < spacing))
            {
                continue;
            }

            if (near is { } n && Math.Max(Math.Abs(n.X - entrance.X), Math.Abs(n.Y - entrance.Y)) > radius)
            {
                continue;
            }

            candidates.Add(tile);
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        if (closest && near is { } target)
        {
            var best = candidates.Min(c => Distance(c.Offset(vx, vy), target));
            candidates = candidates.Where(c => Distance(c.Offset(vx, vy), target) == best).ToList();
        }

        return candidates[random.Next(candidates.Count)];
    }

    private static int Distance(MapPosition a, MapPosition b) => ((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y));

    /// <summary>Every entrance placed in the zone can be walked to from the others (8 ways over free ground; guards are fought, not avoided).</summary>
    private static void CheckReachable(GeneratedMap map, TemplateZone spec, List<MapPosition> tiles)
    {
        var inZone = tiles.ToHashSet();
        var entrances = map.Objects.Where(o => inZone.Contains(o.Position) && o.Definition.Footprint.HasVisit).Select(o => o.VisitPosition).ToList();
        if (entrances.Count < 2)
        {
            return;
        }

        var blocked = map.Objects.SelectMany(o => o.OccupiedCells()).Where(c => c.Cell == FootprintCell.Blocked).Select(c => c.Position).ToHashSet();
        var seen = new HashSet<MapPosition> { entrances[0] };
        var queue = new Queue<MapPosition>([entrances[0]]);
        while (queue.TryDequeue(out var p))
        {
            foreach (var direction in DirectionExtensions.All)
            {
                var next = p.Step(direction);
                if (map.Map.Contains(next) && map.Map.GetTerrain(next) != TerrainPainter.WallTerrain && !blocked.Contains(next) && seen.Add(next))
                {
                    queue.Enqueue(next);
                }
            }
        }

        foreach (var entrance in entrances.Where(e => !seen.Contains(e)))
        {
            map.Problem($"Zone '{spec.Id}': the entrance at {entrance} cannot be reached from the zone's first object.");
        }
    }
}
