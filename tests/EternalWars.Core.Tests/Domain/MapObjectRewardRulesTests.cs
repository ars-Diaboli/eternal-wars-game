using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>
/// The rules every new adventure-map object shares (ADR-068): visit scopes, weighted rolls, temporary hero statuses and artifact
/// draws by tier with the relic pool.
/// </summary>
public class MapObjectRewardRulesTests
{
    private static readonly MapObjectDefinition Wheel = new("test.wheel", "Water Wheel", MapObjectKind.Marketplace, new Footprint(["BB", "BV"]))
    {
        VisitScope = VisitScope.Weekly,
    };

    private static MapObject Place(GameState state, VisitScope scope)
    {
        var mapObject = MapObject.Create(state.NextObjectId(), Wheel with { VisitScope = scope }, new MapPosition(4, 0));
        state.AddObject(mapObject);
        return mapObject;
    }

    private static GameState World() => Create("A........", ".........", "C.......B");

    [Fact]
    public void AbsoluteWeek_CountsOnAcrossMonths()
    {
        Assert.Equal(1, new GameDate(1).AbsoluteWeek);
        Assert.Equal(1, new GameDate(7).AbsoluteWeek);
        Assert.Equal(2, new GameDate(8).AbsoluteWeek);
        Assert.Equal(5, new GameDate(29).AbsoluteWeek); // week 1 of month 2
        Assert.Equal(1, new GameDate(29).Week);
    }

    [Fact]
    public void Weekly_CollectsOncePerObjectAWeek_WhoeverComes()
    {
        var log = new VisitLog();
        var monday = new GameDate(8);

        Assert.True(log.CanCollect(VisitScope.Weekly, A, monday));
        log.Record(A, monday);

        Assert.False(log.CanCollect(VisitScope.Weekly, A, new GameDate(14)));
        Assert.False(log.CanCollect(VisitScope.Weekly, C, new GameDate(14))); // per object, not per hero
        Assert.True(log.CanCollect(VisitScope.Weekly, C, new GameDate(15)));
        Assert.True(log.CanCollect(VisitScope.Weekly, A, new GameDate(36))); // a week of a later month that has the same Week number
    }

    [Fact]
    public void OncePerHero_FirstVisitor_AndUnlimited()
    {
        var log = new VisitLog();
        var day = GameDate.First;
        Assert.False(log.IsSpent);
        log.Record(A, day);

        Assert.True(log.IsSpent);
        Assert.False(log.CanCollect(VisitScope.OncePerHero, A, new GameDate(100)));
        Assert.True(log.CanCollect(VisitScope.OncePerHero, C, day));
        Assert.False(log.CanCollect(VisitScope.FirstVisitor, C, day));
        Assert.True(log.CanCollect(VisitScope.Unlimited, A, day));
        Assert.Equal(new[] { A }, log.Heroes);
    }

    [Fact]
    public void TryCollect_RecordsTheVisitAndPublishes_ThenRefusesUntilNextWeek()
    {
        var state = World();
        var wheel = Place(state, VisitScope.Weekly);
        var session = Start(state);
        var collected = new List<MapObjectCollected>();
        session.Events.Subscribe<MapObjectCollected>(collected.Add);

        Assert.True(session.Interactions.TryCollect(state.GetHero(A), wheel));
        Assert.False(session.Interactions.TryCollect(state.GetHero(C), wheel));

        Assert.Equal(new[] { new MapObjectCollected(A, wheel.Id) }, collected);
        Assert.Equal(1, wheel.Visits.CollectedWeek);
        Assert.Contains("visits week 1", GameStateDigest.Describe(state), StringComparison.Ordinal);

        state.Date = new GameDate(8);
        Assert.True(session.Interactions.TryCollect(state.GetHero(C), wheel));
    }

    [Fact]
    public void EachKindOfTheCatalogue_HasItsVisitScope_TheOlderKindsNone()
    {
        Assert.All(MapObjectCatalog.Default.All, d => Assert.Equal(
            d.Kind switch
            {
                MapObjectKind.Loot => VisitScope.FirstVisitor,
                MapObjectKind.Training or MapObjectKind.Observatory or MapObjectKind.Sphinx => VisitScope.OncePerHero,
                MapObjectKind.Buff => d.Buff is BuffRule.Stables or BuffRule.ManaVortex or BuffRule.FountainOfMagic ? VisitScope.WeeklyPerHero : VisitScope.DailyPerHero,
                _ => VisitScope.Unlimited,
            },
            d.VisitScope));
    }

    [Fact]
    public void DailyAndWeeklyPerHero_CountEachHeroOnItsOwn()
    {
        var log = new VisitLog();
        log.Record(A, new GameDate(3));

        Assert.False(log.CanCollect(VisitScope.DailyPerHero, A, new GameDate(3)));
        Assert.True(log.CanCollect(VisitScope.DailyPerHero, C, new GameDate(3)));
        Assert.True(log.CanCollect(VisitScope.DailyPerHero, A, new GameDate(4)));
        Assert.False(log.CanCollect(VisitScope.WeeklyPerHero, A, new GameDate(7)));
        Assert.True(log.CanCollect(VisitScope.WeeklyPerHero, A, new GameDate(8)));
        Assert.True(log.CanCollect(VisitScope.WeeklyPerHero, C, new GameDate(7)));
    }

    [Fact]
    public void WeightedRoll_FollowsTheWeights_AndNeverPicksAZeroWeight()
    {
        var random = new GameRandom(42);
        IReadOnlyList<(int, string)> table = [(30, "low"), (0, "never"), (60, "mid"), (10, "high")];
        var counts = Enumerable.Range(0, 10_000).Select(_ => WeightedRoll.Pick(table, random)).CountBy(v => v).ToDictionary();

        Assert.False(counts.ContainsKey("never"));
        Assert.InRange(counts["low"], 2700, 3300);
        Assert.InRange(counts["mid"], 5700, 6300);
        Assert.InRange(counts["high"], 800, 1200);
        Assert.Throws<ArgumentException>(() => WeightedRoll.Pick<string>([(0, "x")], random));
        Assert.Throws<ArgumentException>(() => WeightedRoll.Pick<string>([(-1, "x"), (5, "y")], random));
    }

    [Fact]
    public void Statuses_AddToMoraleLuckAndMovement_AndASourceReplacesItself()
    {
        var state = World();
        var session = Start(state);
        var hero = state.GetHero(A);
        var baseMovement = MovementAllowance.Daily(state, hero);
        var gained = new List<HeroStatusGained>();
        session.Events.Subscribe<HeroStatusGained>(gained.Add);

        session.Progression.GrantStatus(hero, new("temple", HeroStatusKind.Morale, 2, StatusExpiry.NextBattle));
        session.Progression.GrantStatus(hero, new("temple", HeroStatusKind.Morale, 3, StatusExpiry.NextBattle)); // replaces, does not add
        session.Progression.GrantStatus(hero, new("fountain", HeroStatusKind.Morale, 1, StatusExpiry.NextBattle));
        session.Progression.GrantStatus(hero, new("tomb", HeroStatusKind.Luck, -3, StatusExpiry.NextBattle));
        session.Progression.GrantStatus(hero, new("stables", HeroStatusKind.Movement, 400, StatusExpiry.EndOfWeek));

        Assert.Equal(4, hero.TotalMorale);
        Assert.Equal(-3, hero.TotalLuck);
        Assert.Equal(baseMovement + 400, MovementAllowance.Daily(state, hero));
        Assert.Equal(5, gained.Count);
        Assert.Contains("statuses temple:Morale+3/NextBattle", GameStateDigest.Describe(state), StringComparison.Ordinal);
    }

    [Fact]
    public void NextBattleStatuses_EndWithTheBattle_ForBothHeroes()
    {
        var state = Create("AB.......");
        var session = Start(state);
        session.Progression.GrantStatus(state.GetHero(A), new("ring", HeroStatusKind.Luck, 1, StatusExpiry.NextBattle));
        session.Progression.GrantStatus(state.GetHero(A), new("stables", HeroStatusKind.Movement, 400, StatusExpiry.EndOfWeek));
        session.Progression.GrantStatus(state.GetHero(B), new("temple", HeroStatusKind.Morale, 2, StatusExpiry.NextBattle));
        var expired = new List<HeroStatusesExpired>();
        session.Events.Subscribe<HeroStatusesExpired>(expired.Add);

        session.Step(A, Direction.East);
        Assert.Equal(1, state.Battle!.Participant(BattleSide.Attacker).Bonus.Luck); // the battle took the status in
        session.AutoResolve();

        Assert.Null(state.Battle);
        Assert.Contains(expired, e => e.Hero == A && e.Statuses.Single().Source == "ring");
        Assert.Contains(expired, e => e.Hero == B && e.Statuses.Single().Source == "temple");
        var survivor = state.Heroes.Single();
        Assert.DoesNotContain(survivor.Statuses, s => s.Expires == StatusExpiry.NextBattle);
    }

    [Fact]
    public void EndOfDayStatuses_EndAtTheOwnersNextTurn_AndEndOfWeekOnMonday()
    {
        var state = World();
        var session = Start(state);
        var hero = state.GetHero(A);
        session.Progression.GrantStatus(hero, new("spring", HeroStatusKind.Luck, 1, StatusExpiry.EndOfDay));
        session.Progression.GrantStatus(hero, new("stables", HeroStatusKind.Movement, 400, StatusExpiry.EndOfWeek));

        session.EndTurn(); // Red's turn, same day: Blue's statuses stay
        Assert.Equal(2, hero.Statuses.Count);
        session.EndTurn(); // day 2, Blue again
        Assert.Equal(new[] { "stables" }, hero.Statuses.Select(s => s.Source));
        Assert.Equal(MovementAllowance.Daily(state, hero), hero.MaxMovementPoints); // the Stables still count today

        while (state.Date.AbsoluteDay < 8)
        {
            session.EndTurn();
        }

        Assert.Empty(hero.Statuses);
        Assert.Equal(MovementAllowance.Daily(state, hero), hero.MaxMovementPoints); // Monday's allowance without the Stables
    }

    [Fact]
    public void Draw_Relics_DoNotRepeatUntilThePoolIsEmpty_ThenItRefills()
    {
        var state = World();
        var session = Start(state);
        var relics = state.Artifacts.OfTier(ArtifactTier.Relic).Select(a => a.Id).ToHashSet(StringComparer.Ordinal);

        var firstRound = Enumerable.Range(0, relics.Count).Select(_ => session.Artifacts.Draw(ArtifactTier.Relic)!).ToList();
        Assert.Equal(relics.Order(StringComparer.Ordinal), firstRound.Order(StringComparer.Ordinal));
        Assert.Contains("relics drawn", GameStateDigest.Describe(state), StringComparison.Ordinal);

        var next = session.Artifacts.Draw(ArtifactTier.Relic)!; // the pool refilled and the draw starts anew
        Assert.Contains(next, relics);
        Assert.Equal(new[] { next }, state.DrawnRelics);
    }

    [Fact]
    public void Draw_Relics_SkipThoseOnTheMapOrCarried_AndOtherTiersMayRepeat()
    {
        var state = World();
        var relics = state.Artifacts.OfTier(ArtifactTier.Relic).Select(a => a.Id).Order(StringComparer.Ordinal).ToList();
        state.GetHero(A).Artifacts.AddToBackpack(relics[0]);
        var session = Start(state);

        var drawn = Enumerable.Range(0, relics.Count - 1).Select(_ => session.Artifacts.Draw(ArtifactTier.Relic)!).ToList();
        Assert.DoesNotContain(relics[0], drawn);
        Assert.Equal(relics.Count - 1, drawn.Distinct().Count());

        var minors = state.Artifacts.OfTier(ArtifactTier.Minor).Select(a => a.Id).ToHashSet(StringComparer.Ordinal);
        var many = Enumerable.Range(0, minors.Count * 3).Select(_ => session.Artifacts.Draw(ArtifactTier.Minor)!).ToList();
        Assert.All(many, id => Assert.Contains(id, minors));
        Assert.True(many.Distinct().Count() < many.Count); // repeats are allowed
        Assert.Empty(state.DrawnRelics.Intersect(minors));
    }
}
