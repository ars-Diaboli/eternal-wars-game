using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Visibility;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

public class VisibilityTests
{
    private static readonly string[] OpenField = Enumerable.Repeat(new string('.', 20), 12).ToArray();

    private static string[] WithHeroes(params (char Symbol, int X, int Y)[] heroes)
    {
        var rows = OpenField.Select(r => r.ToCharArray()).ToArray();
        foreach (var (symbol, x, y) in heroes)
        {
            rows[y][x] = symbol;
        }

        return rows.Select(r => new string(r)).ToArray();
    }

    [Fact]
    public void GameStart_RevealsRoundedCircleAroundHeroes()
    {
        var session = Start(Create(WithHeroes(('A', 0, 0), ('B', 19, 11))), revealAll: false);
        var fog = session.State.Fog;

        Assert.True(fog.IsExplored(Blue, new MapPosition(5, 0)));
        Assert.True(fog.IsExplored(Blue, new MapPosition(5, 2)));
        Assert.False(fog.IsExplored(Blue, new MapPosition(5, 3)));
        Assert.False(fog.IsExplored(Blue, new MapPosition(6, 0)));
        Assert.False(fog.IsExplored(Red, new MapPosition(0, 0)));
    }

    [Fact]
    public void Moving_PublishesOnlyNewlyRevealedTiles()
    {
        var session = Start(Create(WithHeroes(('A', 5, 5), ('B', 19, 11))), revealAll: false);
        var revealed = new List<TilesRevealed>();
        session.Events.Subscribe<TilesRevealed>(revealed.Add);

        session.Movement.TryStep(A, Direction.East);

        var reveal = Assert.Single(revealed);
        Assert.Equal(Blue, reveal.Player);
        Assert.NotEmpty(reveal.Positions);
        Assert.All(reveal.Positions, p =>
        {
            Assert.True(DistanceSquared(p, new MapPosition(5, 5)) > 30, $"{p} was already visible");
            Assert.True(DistanceSquared(p, new MapPosition(6, 5)) <= 30, $"{p} is out of scouting range");
        });

        static int DistanceSquared(MapPosition a, MapPosition b) => ((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y));
    }

    private static readonly GameOptions Refog = new(FogMode.ShroudWithRefog, RefogMargin: 2);

    private static readonly string[] Corridor =
    [
        "............................",
        "............................",
        "A.........B.................",
        "............................",
        "............................",
    ];

    [Fact]
    public void Refog_EnemySeenWithinScoutingRadius_AndForgottenOnlyBeyondRefogDistance()
    {
        var session = Start(Create(Refog, Corridor), revealAll: false);
        var state = session.State;
        var enemy = state.GetHero(B);
        var sightChanges = new List<SightChanged>();
        session.Events.Subscribe<SightChanged>(e => { if (e.Player == Blue) sightChanges.Add(e); });

        Assert.False(state.CanSee(Blue, enemy)); // distance 10

        session.Movement.MoveTo(A, new MapPosition(5, 2));
        Assert.True(state.CanSee(Blue, enemy)); // distance 5 = scouting radius

        session.Movement.MoveTo(A, new MapPosition(3, 2));
        Assert.True(state.CanSee(Blue, enemy)); // distance 7 = radius + margin: still remembered

        session.Movement.TryStep(A, Direction.West);
        Assert.False(state.CanSee(Blue, enemy)); // distance 8: refogged
        Assert.True(state.Fog.IsExplored(Blue, enemy.Position)); // terrain stays revealed
        Assert.Contains(sightChanges, c => c.Left.Contains(enemy.Position));
        Assert.Equal(MinimapMarker.None, Minimap.Build(state, Blue, 0)[(2 * 28) + 10].Marker);
    }

    [Fact]
    public void PermanentReveal_EnemyStaysVisibleOnExploredTiles()
    {
        var session = Start(Create(Corridor), revealAll: false);
        var enemy = session.State.GetHero(B);
        var sightChanges = 0;
        session.Events.Subscribe<SightChanged>(_ => sightChanges++);

        session.Movement.MoveTo(A, new MapPosition(5, 2));
        session.Movement.MoveTo(A, new MapPosition(0, 2));

        Assert.True(session.State.CanSee(Blue, enemy));
        Assert.Equal(0, sightChanges);
    }

    [Fact]
    public void Refog_PlannerIgnoresUnseenEnemy_MovementBumpsIntoIt()
    {
        var session = Start(Create(Refog, "A......B....", "############"), revealAll: true);

        Assert.False(session.State.CanSee(Blue, session.State.GetHero(B)));
        Assert.NotNull(session.Movement.FindPath(A, new MapPosition(10, 0)));

        var outcome = session.Movement.MoveTo(A, new MapPosition(10, 0));

        Assert.Equal(new MovementOutcome(6, MoveResult.Occupied, Arrived: false), outcome);
        Assert.Null(session.Movement.FindPath(A, new MapPosition(10, 0))); // now seen, so it blocks
    }

    [Fact]
    public void Minimap_HidesUnexploredTilesAndMarksHeroes()
    {
        var session = Start(Create(WithHeroes(('A', 0, 0), ('B', 19, 11))), revealAll: false);

        var cells = Minimap.Build(session.State, Blue, level: 0);

        Assert.Equal(new MinimapCell(true, TerrainType.Grass, MinimapMarker.Hero, Blue), cells[0]);
        Assert.False(cells[(11 * 20) + 19].Explored);
        Assert.True(Minimap.Build(session.State, null, 0)[(11 * 20) + 19] is { Explored: true, Marker: MinimapMarker.Hero });
    }
}
