using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Visibility;

namespace EternalWars.Core.Tests.Domain;

/// <summary>
/// Sight under refog is worked out again only around the observers that changed; whatever they do, it is what working everything out afresh
/// gives (with its hysteresis: a tile stays in sight while an observer is within sight radius + margin).
/// </summary>
public class FogSightTests
{
    private static readonly PlayerId Red = new(0);

    private static (int, int, int) Key(MapPosition p) => (p.Level, p.Y, p.X);

    /// <summary>The rule worked out afresh from the tiles in sight before: what <see cref="FogOfWar.UpdateSight"/> must give.</summary>
    private static HashSet<MapPosition> Afresh(AdventureMap map, HashSet<MapPosition> previous, IEnumerable<(MapPosition Center, int Radius)> observers, int margin)
    {
        var current = new HashSet<MapPosition>();
        foreach (var (center, radius) in observers)
        {
            for (var dy = -radius - margin; dy <= radius + margin; dy++)
            {
                for (var dx = -radius - margin; dx <= radius + margin; dx++)
                {
                    var position = center.Offset(dx, dy);
                    if (map.Contains(position) && FogOfWar.IsWithinRadius(center, position, radius + margin)
                        && (previous.Contains(position) || FogOfWar.IsWithinRadius(center, position, radius)))
                    {
                        current.Add(position);
                    }
                }
            }
        }

        return current;
    }

    [Theory]
    [InlineData(1UL, 2)]
    [InlineData(2UL, 2)]
    [InlineData(3UL, 0)]
    [InlineData(4UL, 3)]
    public void Sight_IsWhatWorkingItOutAfreshGives_AfterEveryChange(ulong seed, int margin)
    {
        var random = new GameRandom(seed);
        var map = new AdventureMap(40, 30, levels: 2);
        var fog = new FogOfWar(map, FogMode.ShroudWithRefog);
        var expected = new HashSet<MapPosition>();
        var observers = new List<(MapPosition Center, int Radius)>();
        MapPosition Anywhere() => new(random.NextInclusive(-2, 41), random.NextInclusive(-2, 31), random.Next(2));

        for (var step = 0; step < 600; step++)
        {
            var roll = random.Next(100);
            if (observers.Count == 0 || roll < 10)
            {
                observers.Add((Anywhere(), random.NextInclusive(0, 7))); // a hero hired, a mine flagged (some partly off the map)
            }
            else if (roll < 15)
            {
                observers.RemoveAt(random.Next(observers.Count)); // a hero beaten, a town lost
            }
            else if (roll < 20)
            {
                var at = random.Next(observers.Count);
                observers[at] = observers[at] with { Radius = random.NextInclusive(0, 9) }; // Scouting, an Observatory
            }
            else if (roll < 23)
            {
                var at = random.Next(observers.Count);
                observers.Add(observers[at]); // two observers in one place
            }
            else if (roll < 26)
            {
                var at = random.Next(observers.Count);
                observers[at] = observers[at] with { Center = Anywhere() }; // a teleport, a level change
            }
            else
            {
                // A step, or a few at once (a move's events arrive after the hero walked on).
                var at = random.Next(observers.Count);
                var steps = random.NextInclusive(1, 3);
                var center = observers[at].Center;
                for (var i = 0; i < steps; i++)
                {
                    center = center.Offset(random.NextInclusive(-1, 1), random.NextInclusive(-1, 1));
                }

                observers[at] = observers[at] with { Center = center };
            }

            var before = expected;
            expected = Afresh(map, before, observers, margin);
            var (entered, left) = fog.UpdateSight(Red, [.. observers], margin);

            Assert.Equal(expected.OrderBy(Key), fog.SightOf(Red).OrderBy(Key));
            Assert.Equal(expected.Except(before).OrderBy(Key), entered.OrderBy(Key));
            Assert.Equal(before.Except(expected).OrderBy(Key), left.OrderBy(Key));
        }
    }

    [Fact]
    public void ANewMargin_OrSightSetFromOutside_IsWorkedOutAfresh()
    {
        var map = new AdventureMap(20, 20);
        var fog = new FogOfWar(map, FogMode.ShroudWithRefog);
        var observers = new List<(MapPosition Center, int Radius)> { (new MapPosition(5, 5), 3) };
        fog.UpdateSight(Red, observers, margin: 2);

        // Walked off, the old circle's edge stays in sight within the margin; a margin of 0 drops it.
        observers[0] = (new MapPosition(7, 5), 3);
        var kept = Afresh(map, [.. fog.SightOf(Red)], observers, 2);
        fog.UpdateSight(Red, observers, margin: 2);
        Assert.Equal(kept.OrderBy(Key), fog.SightOf(Red).OrderBy(Key));
        fog.UpdateSight(Red, observers, margin: 0);
        Assert.Equal(Afresh(map, [], observers, 0).OrderBy(Key), fog.SightOf(Red).OrderBy(Key));

        // A LAN client's sight comes from the host; the next update starts from it.
        fog.SetSight(Red, [new MapPosition(15, 15), new MapPosition(7, 6)]);
        var (entered, left) = fog.UpdateSight(Red, observers, margin: 0);
        Assert.Contains(new MapPosition(15, 15), left);
        Assert.DoesNotContain(new MapPosition(7, 6), entered);
        Assert.Equal(Afresh(map, [], observers, 0).OrderBy(Key), fog.SightOf(Red).OrderBy(Key));
    }
}
