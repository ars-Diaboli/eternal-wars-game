using EternalWars.Core.Domain.Analysis;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Players;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Editing.Checks;

/// <summary>
/// A tournament map (ADR-366). Errors for what keeps it from being one (<see cref="TournamentRules.MapProblems"/>, so such a game does not start:
/// an arena too, not exactly two players, the two allied, or a player allowed other than one hero); for a player without a start zone; and for
/// the two start zones joined (<see cref="Joined"/>), either way. A warning for each way one zone is much richer than the other
/// (<see cref="Richer"/>). The start zone and joined errors are the validator's only: a game on such a map still starts. A missing start zone
/// has a fix when its ground is clear (<see cref="MissingZone"/>).
/// </summary>
internal sealed class TournamentCheck : IMapCheck
{
    public IssueKind Kind => IssueKind.Rules;

    public IEnumerable<MapIssue> Check(GameState scenario)
    {
        foreach (var problem in TournamentRules.MapProblems(scenario))
        {
            yield return new(IssueSeverity.Error, problem);
        }

        if (!scenario.Rules.Tournament)
        {
            yield break;
        }

        var map = scenario.Map;
        var zoned = scenario.Players.Where(p => map.StartZoneOwners.Contains(p.Id)).ToList();
        foreach (var player in scenario.Players.Where(p => !map.StartZoneOwners.Contains(p.Id)))
        {
            yield return new(IssueSeverity.Error, $"{player.Name} has no start zone: each player of a tournament builds up in a zone of its own.")
            {
                Fix = MissingZone(scenario, player.Id) is not null ? new StartZoneFix($"Paint {player.Name}'s start zone over the ground it reaches", player.Id) : null,
            };
        }

        if (scenario.Players.Count != 2 || zoned.Count != 2)
        {
            yield break;
        }

        var (a, b) = (zoned[0], zoned[1]);
        if ((Joined(scenario, a.Id, b.Id) ?? Joined(scenario, b.Id, a.Id)) is { } at) // a one-way portal joins them one way only
        {
            yield return new(IssueSeverity.Error,
                $"{a.Name}'s and {b.Name}'s start zones are joined: a hero can walk, or go through a gate, portal or teleporter, from one into the other.", at);
        }

        foreach (var warning in Richer(scenario, a, b).Concat(Richer(scenario, b, a)))
        {
            yield return new(IssueSeverity.Warning, warning);
        }
    }

    /// <summary>
    /// The first tile of <paramref name="other"/>'s start zone a hero of <paramref name="player"/> can get to; null when none. From every tile
    /// of the player's zone a hero can stand on, and from where its heroes stand and its towns' entrances (a hero is hired there) wherever they
    /// are, over passable ground that no object blocks, in 8 directions, through any tile outside both zones, and from a gate's, portal's or
    /// teleporter's entrance to its exit. Nothing on the way stops a hero: guards can be beaten, garrisons taken, and a hero walks on from any
    /// entrance.
    /// </summary>
    internal static MapPosition? Joined(GameState scenario, PlayerId player, PlayerId other)
    {
        var map = scenario.Map;
        var reached = new HashSet<MapPosition>();
        var open = new Queue<MapPosition>();
        var starts = scenario.Heroes.Where(h => h.Owner == player).Select(h => h.Position)
            .Concat(scenario.ObjectsOf<Town>().Where(t => t.Owner == player).Select(t => t.VisitPosition));
        foreach (var tile in starts.Concat(map.StartZoneTiles(player)))
        {
            Visit(tile);
        }

        while (open.TryDequeue(out var position))
        {
            if (map.GetStartZone(position) == other)
            {
                return position;
            }

            if (scenario.ObjectCellAt(position) is { Cell: FootprintCell.Visit, Object: var entered } && scenario.TeleportDestination(entered) is { } exit)
            {
                Visit(exit);
            }

            foreach (var direction in DirectionExtensions.All)
            {
                Visit(position.Step(direction));
            }
        }

        return null;

        void Visit(MapPosition position)
        {
            if (map.Contains(position) && scenario.Terrains.IsPassable(map.GetTerrain(position))
                && scenario.ObjectCellAt(position) is not { Cell: FootprintCell.Blocked } && reached.Add(position))
            {
                open.Enqueue(position);
            }
        }
    }

    /// <summary>
    /// The start zone <paramref name="player"/> lacks, when the answer is clear (the fix of its error): every tile its heroes and its towns'
    /// entrances reach by walking (passable ground no object blocks, in 8 directions, over tiles in no start zone), and the whole footprint of each
    /// object whose entrance is among them. Null when the player has a zone already or nothing to start from, or when the ground is not clearly
    /// its own: a hero or town entrance of its stands in another player's zone, another player's hero or town entrance is on the ground, or a
    /// step leads from it into another player's zone.
    /// </summary>
    internal static IReadOnlyList<MapPosition>? MissingZone(GameState scenario, PlayerId player)
    {
        var map = scenario.Map;
        if (map.StartZoneOwners.Contains(player))
        {
            return null;
        }

        var starts = scenario.Heroes.Where(h => h.Owner == player).Select(h => h.Position)
            .Concat(scenario.ObjectsOf<Town>().Where(t => t.Owner == player).Select(t => t.VisitPosition))
            .ToList();
        if (starts.Any(s => map.Contains(s) && map.GetStartZone(s) is not null))
        {
            return null; // it starts inside another player's zone
        }

        var others = scenario.Heroes.Where(h => h.Owner != player).Select(h => h.Position)
            .Concat(scenario.ObjectsOf<Town>().Where(t => t.Owner is { } owner && owner != player).Select(t => t.VisitPosition))
            .ToHashSet();
        var reached = new HashSet<MapPosition>();
        var open = new Queue<MapPosition>();
        foreach (var start in starts.Where(Walkable))
        {
            if (reached.Add(start))
            {
                open.Enqueue(start);
            }
        }

        while (open.TryDequeue(out var position))
        {
            if (others.Contains(position))
            {
                return null;
            }

            foreach (var next in DirectionExtensions.All.Select(position.Step).Where(Walkable))
            {
                if (map.GetStartZone(next) is not null)
                {
                    return null; // the ground runs into another player's zone: whose it is, is not clear
                }

                if (reached.Add(next))
                {
                    open.Enqueue(next);
                }
            }
        }

        if (reached.Count == 0)
        {
            return null;
        }

        var footprints = scenario.Objects.Where(o => o.Definition.Footprint.HasVisit && reached.Contains(o.VisitPosition))
            .SelectMany(o => o.OccupiedCells().Select(c => c.Position))
            .Where(p => map.Contains(p) && map.GetStartZone(p) is null);
        return [.. reached.Concat(footprints).Distinct().OrderBy(p => p.Level).ThenBy(p => p.Y).ThenBy(p => p.X)];

        bool Walkable(MapPosition position) =>
            map.Contains(position) && scenario.Terrains.IsPassable(map.GetTerrain(position)) && scenario.ObjectCellAt(position) is not { Cell: FootprintCell.Blocked };
    }

    /// <summary>
    /// How <paramref name="rich"/>'s start zone is much richer than <paramref name="poor"/>'s, by what stands in each (an object counts in the
    /// zone of its entrance): more towns; more mines of a resource; treasure (<see cref="MapAnalysis.TreasureValue"/>) more than the generator
    /// lets alike starts differ (<see cref="MapAnalysis.AlikePercent"/> of the two zones' mean, at least <see cref="MapAnalysis.AlikeTreasureSlack"/>).
    /// </summary>
    private static IEnumerable<string> Richer(GameState scenario, Player rich, Player poor)
    {
        var (richer, poorer) = (Contents(scenario, rich.Id), Contents(scenario, poor.Id));
        var lead = $"{rich.Name}'s start zone is much richer than {poor.Name}'s";
        var (towns, fewer) = (richer.OfType<Town>().Count(), poorer.OfType<Town>().Count());
        if (towns > fewer)
        {
            yield return $"{lead}: {Many(towns, "town")} against {fewer}.";
        }

        foreach (var resource in Enum.GetValues<ResourceType>())
        {
            var (more, less) = (Mines(richer, resource), Mines(poorer, resource));
            if (more > less)
            {
                yield return $"{lead}: {Many(more, $"{resource.ToString().ToLowerInvariant()} mine")} against {less}.";
            }
        }

        var (gold, against) = (Treasure(scenario, richer), Treasure(scenario, poorer));
        var allowed = Math.Max((gold + against) / 2.0 * MapAnalysis.AlikePercent / 100, MapAnalysis.AlikeTreasureSlack);
        if (gold - against > 2 * allowed)
        {
            yield return $"{lead}: treasure worth {gold} gold against {against} (the generator lets alike starts be {MapAnalysis.AlikePercent} % off "
                + $"their mean, at least {MapAnalysis.AlikeTreasureSlack} gold).";
        }

        static int Mines(List<MapObject> contents, ResourceType resource) => contents.OfType<Mine>().Count(m => m.Resource == resource);
        static string Many(int count, string what) => count == 1 ? $"1 {what}" : $"{count} {what}s";
    }

    private static List<MapObject> Contents(GameState scenario, PlayerId player) =>
        scenario.Objects
            .Where(o => o.Definition.Footprint.HasVisit && scenario.Map.Contains(o.VisitPosition) && scenario.Map.GetStartZone(o.VisitPosition) == player)
            .ToList();

    private static long Treasure(GameState scenario, List<MapObject> contents) => contents.Sum(o => MapAnalysis.TreasureValue(o, scenario.Artifacts));
}
