using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Editing.Checks;

/// <summary>
/// Start zones (MP-4, ADR-197): a zone of a player the map does not have is an error. Warnings: a player of a map with start zones that has
/// none (the simultaneous first week needs one for each, MP-2; the tournament's own check makes it an error there, ADR-366); a zone without
/// any of its player's towns, or holding another player's town or hero (the zones overlap the starts); two enemies' zones that touch (a hero
/// steps from one into the other at once; allies' zones may touch, ADR-199, and a tournament's zones touch across the wall between them, ADR-366).
/// </summary>
internal sealed class StartZoneCheck : IMapCheck
{
    public IssueKind Kind => IssueKind.Players;

    public IEnumerable<MapIssue> Check(GameState scenario)
    {
        var map = scenario.Map;
        if (!map.HasStartZones)
        {
            yield break;
        }

        var owners = map.StartZoneOwners;
        foreach (var owner in owners.Where(o => !scenario.HasPlayer(o)))
        {
            yield return new(IssueSeverity.Error, $"A start zone belongs to player {owner.Value + 1}, which the map does not have.", map.StartZoneTiles(owner).First());
        }

        foreach (var player in scenario.Players)
        {
            var towns = scenario.ObjectsOf<Town>().Where(t => t.Owner == player.Id).ToList();
            if (!owners.Contains(player.Id))
            {
                if (!scenario.Rules.Tournament) // a tournament's check calls it an error
                {
                    yield return new(IssueSeverity.Warning, $"{player.Name} has no start zone: a simultaneous first week needs one for every player.");
                }

                continue;
            }

            var first = map.StartZoneTiles(player.Id).First();
            if (towns.Count > 0 && towns.All(t => map.GetStartZone(t.Position) != player.Id))
            {
                yield return new(IssueSeverity.Warning, $"{player.Name}'s start zone holds none of {player.Name}'s towns.", first);
            }
        }

        foreach (var town in scenario.ObjectsOf<Town>().Where(t => t.Owner is not null && map.GetStartZone(t.Position) is { } zone && zone != t.Owner))
        {
            yield return new(IssueSeverity.Warning, $"{town.Name} stands in {NameOf(scenario, map.GetStartZone(town.Position)!.Value)}'s start zone.", town.Position);
        }

        foreach (var hero in scenario.Heroes.Where(h => map.Contains(h.Position) && map.GetStartZone(h.Position) is { } zone && zone != h.Owner))
        {
            yield return new(IssueSeverity.Warning, $"Hero {hero.Name} stands in {NameOf(scenario, map.GetStartZone(hero.Position)!.Value)}'s start zone.", hero.Position);
        }

        foreach (var ((a, b), at) in TouchingZones(map).Where(_ => !scenario.Rules.Tournament).Where(t => !(scenario.HasPlayer(t.Pair.A) && scenario.HasPlayer(t.Pair.B) && scenario.AreAllies(t.Pair.A, t.Pair.B))))
        {
            yield return new(IssueSeverity.Warning, $"{NameOf(scenario, a)}'s and {NameOf(scenario, b)}'s start zones touch: a hero steps from one into the other at once.", at);
        }
    }

    private static string NameOf(GameState scenario, PlayerId player) => scenario.HasPlayer(player) ? scenario.GetPlayer(player).Name : $"Player {player.Value + 1}";

    /// <summary>Each pair of players whose start zones touch (side or corner), with the first tile where they do.</summary>
    private static IEnumerable<((PlayerId A, PlayerId B) Pair, MapPosition At)> TouchingZones(AdventureMap map)
    {
        var found = new Dictionary<(PlayerId, PlayerId), MapPosition>();
        for (var level = 0; level < map.Levels; level++)
        {
            foreach (var tile in map.Positions(level))
            {
                if (map.GetStartZone(tile) is not { } zone)
                {
                    continue;
                }

                foreach (var (dx, dy) in new[] { (1, 0), (0, 1), (1, 1), (1, -1) })
                {
                    var next = tile.Offset(dx, dy);
                    if (map.Contains(next) && map.GetStartZone(next) is { } other && other != zone)
                    {
                        var pair = zone.Value < other.Value ? (zone, other) : (other, zone);
                        found.TryAdd(pair, tile);
                    }
                }
            }
        }

        return found.OrderBy(f => f.Key.Item1.Value).ThenBy(f => f.Key.Item2.Value).Select(f => (f.Key, f.Value));
    }
}
