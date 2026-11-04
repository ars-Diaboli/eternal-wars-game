using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Magic;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>
/// The adventure spells that show something (Distant Vision, Detect Magic, Grave Whisper, Ledger of Ages, Veil, Eavesdrop) and the adventure
/// spells' ranks II and III.
/// </summary>
public class AdventureInfoSpellTests
{
    private static Hero Mage(GameState state, string spell, int rank = 1, HeroId? id = null)
    {
        var hero = state.GetHero(id ?? A);
        hero.HasSpellBook = true;
        hero.Knowledge = 20;
        hero.Learn(spell);
        hero.RaiseSpell(spell, rank);
        return hero;
    }

    private static CommandResult Cast(GameSession session, string spell, MapPosition? target = null) =>
        session.Execute(new CastAdventureSpellCommand(Blue, A, spell, target));

    private static List<T> Record<T>(GameSession session)
        where T : IEvent
    {
        var list = new List<T>();
        session.Events.Subscribe<T>(list.Add);
        return list;
    }

    private static string[] Field(int width, int height) => [.. Enumerable.Repeat(new string('.', width), height)];

    private static GameState WithHeroes(int width, int height, (int X, int Y) a, (int X, int Y) b, GameOptions? options = null)
    {
        var rows = Field(width, height).Select(r => r.ToCharArray()).ToArray();
        rows[a.Y][a.X] = 'A';
        rows[b.Y][b.X] = 'B';
        return Create(options ?? GameOptions.Default, [.. rows.Select(r => new string(r))]);
    }

    private static MonsterGuard Monster(GameState state, int x, int y, int count)
    {
        var monster = Add<MonsterGuard>(state, BuiltInMapObjects.Monster, x, y);
        (monster.Creature, monster.Count) = (BuiltInCreatures.Wolf, count);
        return monster;
    }

    [Fact]
    public void DistantVision_ListsTheHeroesAndStacksAroundACellInSight_WithTheirArmies()
    {
        var state = WithHeroes(30, 3, (0, 0), (12, 0));
        var monster = Monster(state, 8, 2, 7);
        Mage(state, AdventureMagic.DistantVision);
        var session = Start(state);
        var sighted = Record<ArmiesSighted>(session);

        Assert.True(Cast(session, AdventureMagic.DistantVision, new MapPosition(4, 0)).IsAccepted);

        var report = Assert.Single(sighted);
        Assert.Equal(10, report.Radius);
        var bravo = report.Armies.Single(a => a.Hero == B);
        Assert.Equal(10, bravo.Stacks.Single().Count);
        Assert.Equal(7, report.Armies.Single(a => a.Monster == monster.Id).Stacks.Single().Count);
        Assert.Contains("cell in sight", Cast(session, AdventureMagic.DistantVision, null).Error);
    }

    [Fact]
    public void DistantVision_SeesUnderTheFogOnlyAtRankIII_AndReachesFourteenAtRankII()
    {
        var state = WithHeroes(30, 3, (0, 0), (16, 0));
        var hero = Mage(state, AdventureMagic.DistantVision);
        var session = Start(state, revealAll: false);
        var sighted = Record<ArmiesSighted>(session);
        Assert.False(state.Fog.IsExplored(Blue, new MapPosition(16, 0)));

        Assert.True(Cast(session, AdventureMagic.DistantVision, new MapPosition(3, 0)).IsAccepted);
        Assert.DoesNotContain(sighted[^1].Armies, a => a.Hero == B); // 13 away

        hero.RaiseSpell(AdventureMagic.DistantVision, 2);
        Assert.True(Cast(session, AdventureMagic.DistantVision, new MapPosition(3, 0)).IsAccepted);
        Assert.Equal(14, sighted[^1].Radius);
        Assert.DoesNotContain(sighted[^1].Armies, a => a.Hero == B); // in reach, but under the fog

        hero.RaiseSpell(AdventureMagic.DistantVision, 3);
        Assert.True(Cast(session, AdventureMagic.DistantVision, new MapPosition(3, 0)).IsAccepted);
        Assert.Equal(14, sighted[^1].Radius);
        Assert.Contains(sighted[^1].Armies, a => a.Hero == B);
    }

    [Fact]
    public void DetectMagic_ShowsWhatTheObjectsAroundHold_BanksRewardsAtRankIII()
    {
        var state = Create(Field(30, 8).Select((r, i) => i == 0 ? "A" + r[1..] : i == 7 ? r[..^1] + "B" : r).ToArray());
        var chest = Add<TreasureChest>(state, BuiltInMapObjects.TreasureChest, 5, 0);
        chest.FixedValues = new ChestValues(1500, 1000);
        var far = Add<TreasureChest>(state, BuiltInMapObjects.TreasureChest, 13, 0);
        var bank = Add<CreatureBank>(state, BuiltInMapObjects.DragonCity, 2, 3);
        var hero = Mage(state, AdventureMagic.DetectMagic);
        var session = Start(state);
        var detected = Record<MagicDetected>(session);

        Assert.True(Cast(session, AdventureMagic.DetectMagic).IsAccepted);
        var objects = detected[^1].Objects;
        Assert.Contains("1500 gold or 1000 experience", objects.Single(o => o.Object == chest.Id).Contents);
        Assert.DoesNotContain(objects, o => o.Object == far.Id); // 13 away, radius 10
        var guarded = objects.Single(o => o.Object == bank.Id).Contents;
        Assert.StartsWith("guard:", guarded.Single());

        hero.RaiseSpell(AdventureMagic.DetectMagic, 3);
        Assert.True(Cast(session, AdventureMagic.DetectMagic).IsAccepted);
        Assert.Equal(15, detected[^1].Radius);
        Assert.Contains(detected[^1].Objects, o => o.Object == far.Id);
        Assert.Contains(detected[^1].Objects.Single(o => o.Object == bank.Id).Contents, line => line.StartsWith("reward:", StringComparison.Ordinal));
    }

    [Fact]
    public void GraveWhisper_AddsTenPercentUntilTheHeroRaisesUndead()
    {
        var state = Create("A.B", "...");
        var hero = Mage(state, AdventureMagic.GraveWhisper);
        hero.Army.Set(0, new CreatureStack(BuiltInCreatures.Seraph, 30));
        hero.Skills.Set(NecromancyRules.Skill, SkillLevel.Basic);
        state.GetHero(B).Army.Set(0, new CreatureStack(BuiltInCreatures.Footman, 100));
        var session = Start(state);
        var marked = Record<GravesMarked>(session);

        Assert.True(Cast(session, AdventureMagic.GraveWhisper).IsAccepted);
        Assert.Equal(10, AdventureMagic.NecromancyPercent(hero));
        Assert.Equal(10, NecromancyRules.HeroBonus(hero)); // it reaches the Necromancy raise
        Assert.Equal(10, Assert.Single(marked).Percent);
        Assert.Empty(marked[0].Guards); // the guards' numbers come at rank III

        session.MoveTo(A, new MapPosition(2, 0));
        session.AutoResolve();

        Assert.Equal(0, AdventureMagic.NecromancyPercent(hero)); // used up by the victory that raised undead
    }

    [Fact]
    public void GraveWhisper_RankII_Fifteen_RankIII_ShowsTheGuardsNumbers_AndItEndsWithTheWeek()
    {
        var state = WithHeroes(20, 3, (0, 0), (19, 2));
        var monster = Monster(state, 6, 2, 12);
        var hero = Mage(state, AdventureMagic.GraveWhisper, rank: 3);
        var session = Start(state);
        var marked = Record<GravesMarked>(session);

        Assert.True(Cast(session, AdventureMagic.GraveWhisper).IsAccepted);
        Assert.Equal(15, AdventureMagic.NecromancyPercent(hero));
        Assert.Equal(12, marked[0].Guards.Single(g => g.Monster == monster.Id).Stacks.Single().Count);

        for (var day = 0; day < GameDate.DaysPerWeek; day++)
        {
            session.EndTurn();
            session.EndTurn();
        }

        Assert.Equal(0, AdventureMagic.NecromancyPercent(hero));
    }

    [Fact]
    public void LedgerOfAges_TellsNextWeek_AndTheEnemyHeroesInSight_ForAWeekAtRankIII()
    {
        var state = WithHeroes(20, 3, (0, 0), (8, 0));
        var hero = Mage(state, AdventureMagic.LedgerOfAges);
        var session = Start(state);
        var read = Record<LedgerRead>(session);

        Assert.True(Cast(session, AdventureMagic.LedgerOfAges).IsAccepted);
        Assert.Equal(state.NextWeekEvent, read[0].Report.NextWeek);
        Assert.Equal(B, Assert.Single(read[0].Report.Heroes).Hero);
        Assert.False(AdventureMagic.ReadsLedger(hero));

        hero.RaiseSpell(AdventureMagic.LedgerOfAges, 3);
        Assert.True(Cast(session, AdventureMagic.LedgerOfAges).IsAccepted);
        Assert.True(AdventureMagic.ReadsLedger(hero));
    }

    [Fact]
    public void Veil_ShowsTheArmyOnlyAsAmountsToOtherPlayers_ArtifactsHiddenAtRankII()
    {
        var state = WithHeroes(20, 3, (0, 0), (8, 0));
        var hero = Mage(state, AdventureMagic.Veil, rank: 2);
        var session = Start(state);

        Assert.Equal(10, AdventureIntel.ShowHero(state, hero, Red).Stacks.Single().Count);
        Assert.True(Cast(session, AdventureMagic.Veil).IsAccepted);

        var shown = AdventureIntel.ShowHero(state, hero, Red);
        Assert.True(shown.Veiled);
        Assert.Null(shown.Stacks.Single().Count);
        Assert.Equal("a pack", shown.Stacks.Single().Amount); // 10 footmen
        Assert.True(shown.ArtifactsHidden);
        Assert.Equal(10, AdventureIntel.ShowHero(state, hero, Blue).Stacks.Single().Count); // its owner sees it all
        Assert.True(state.CanSee(Red, hero)); // hidden on the map only at rank III

        for (var day = 0; day < GameDate.DaysPerWeek; day++)
        {
            session.EndTurn();
            session.EndTurn();
        }

        Assert.False(AdventureIntel.ShowHero(state, hero, Red).Veiled); // a week (until the week ends)
    }

    [Fact]
    public void VeilIII_HidesTheHeroOnTheMap_FromPlayersWithoutAHeroWithinThreeCells()
    {
        var state = WithHeroes(20, 3, (0, 0), (8, 0));
        var hero = Mage(state, AdventureMagic.Veil, rank: 3);
        var session = Start(state);
        Assert.True(state.CanSee(Red, hero));

        Assert.True(Cast(session, AdventureMagic.Veil).IsAccepted);
        Assert.False(state.CanSee(Red, hero));

        state.GetHero(B).Position = new MapPosition(3, 1);
        Assert.True(state.CanSee(Red, hero));
    }

    [Fact]
    public void Eavesdrop_ShowsAnEnemyTownInSight_ReadOnly()
    {
        var state = WithHeroes(20, 10, (0, 9), (19, 0));
        var town = AddTown(state, TownIds.Haven, 4, 1, Red);
        town.Garrison.Set(0, new CreatureStack(BuiltInCreatures.Footman, 25));
        AddTown(state, TownIds.Haven, 12, 1, Blue);
        Mage(state, AdventureMagic.Eavesdrop);
        var session = Start(state);
        var overheard = Record<TownOverheard>(session);

        Assert.Contains("enemy towns", Cast(session, AdventureMagic.Eavesdrop, new MapPosition(15, 7)).Error); // own
        Assert.Contains("needs a town", Cast(session, AdventureMagic.Eavesdrop, new MapPosition(0, 0)).Error);
        Assert.True(Cast(session, AdventureMagic.Eavesdrop, town.VisitPosition).IsAccepted);

        var intel = Assert.Single(overheard).Town;
        Assert.Equal(town.Id, intel.Town);
        Assert.Equal(25, intel.Garrison.Single().Count);
        Assert.Equal(town.Buildings.Count(b => b.Value > 0), intel.Buildings.Count);
    }

    [Fact]
    public void Eavesdrop_NeedsSightAtRankI_AnExploredTownAtRankII_AndListensAWeekAtRankIII()
    {
        var state = WithHeroes(30, 10, (0, 9), (29, 0), new GameOptions(FogMode.ShroudWithRefog));
        var town = AddTown(state, TownIds.Haven, 20, 1, Red);
        var hero = Mage(state, AdventureMagic.Eavesdrop);
        var session = Start(state, revealAll: false);
        state.Fog.Reveal(Blue, town.VisitPosition, 1); // explored, not in sight

        Assert.Contains("in sight", Cast(session, AdventureMagic.Eavesdrop, town.VisitPosition).Error);
        hero.RaiseSpell(AdventureMagic.Eavesdrop, 3);
        Assert.True(Cast(session, AdventureMagic.Eavesdrop, town.VisitPosition).IsAccepted);
        Assert.Equal(town.Id, AdventureMagic.ListensTo(hero));
    }

    [Fact]
    public void DimensionDoor_GoesNineCellsAtRankII_AndTwiceADayAtRankIII()
    {
        var state = WithHeroes(30, 3, (0, 0), (29, 2));
        var hero = Mage(state, AdventureMagic.DimensionDoor, rank: 2);
        hero.Knowledge = 60;
        var session = Start(state);
        hero.Mana = 200;

        Assert.True(Cast(session, AdventureMagic.DimensionDoor, new MapPosition(9, 0)).IsAccepted);
        Assert.Contains("once a day", Cast(session, AdventureMagic.DimensionDoor, new MapPosition(18, 0)).Error);

        hero.RaiseSpell(AdventureMagic.DimensionDoor, 3);
        hero.MovementPoints = 2000;
        Assert.True(Cast(session, AdventureMagic.DimensionDoor, new MapPosition(18, 0)).IsAccepted);
        Assert.Contains("2 times a day", Cast(session, AdventureMagic.DimensionDoor, new MapPosition(27, 0)).Error);
    }

    [Fact]
    public void TownPortal_PicksAnyOwnTownFromRankII_AndKeepsTheRestOfTheMovementAtRankIII()
    {
        var state = WithHeroes(40, 10, (20, 9), (39, 0));
        var near = AddTown(state, TownIds.Haven, 12, 0, Blue);
        var far = AddTown(state, TownIds.Haven, 0, 0, Blue);
        var hero = Mage(state, AdventureMagic.TownPortal);
        hero.Knowledge = 60;
        var session = Start(state);
        hero.Mana = 200;

        Assert.Contains("nearest", Cast(session, AdventureMagic.TownPortal, far.VisitPosition).Error);
        Assert.True(Cast(session, AdventureMagic.TownPortal).IsAccepted);
        Assert.Equal(near.VisitPosition, hero.Position);
        Assert.Equal(0, hero.MovementPoints); // rank I ends the day's movement

        hero.RaiseSpell(AdventureMagic.TownPortal, 3);
        hero.MovementPoints = 1500;
        Assert.True(AdventureMagic.NeedsTarget(state, hero, AdventureMagic.TownPortal));
        Assert.True(Cast(session, AdventureMagic.TownPortal, new MapPosition(1, 0)).IsAccepted); // any cell of the town
        Assert.Equal(far.VisitPosition, hero.Position);
        Assert.Equal(1000, hero.MovementPoints);
    }

    [Fact]
    public void PathfindersCall_EveryTerrainAtRankII_AndTenPercentMoreMovementAtRankIII()
    {
        var state = Create("Allllllll", "........B");
        var hero = Mage(state, AdventureMagic.PathfindersCall, rank: 2);
        var session = Start(state);
        var lava = MovementRules.StepCost(state, hero, new MapPosition(1, 0), new MapPosition(2, 0), Direction.East);
        Assert.True(lava > TerrainCatalog.NativeCost);

        Assert.True(Cast(session, AdventureMagic.PathfindersCall).IsAccepted);
        Assert.Equal(TerrainCatalog.NativeCost, MovementRules.StepCost(state, hero, new MapPosition(1, 0), new MapPosition(2, 0), Direction.East));

        hero.RaiseSpell(AdventureMagic.PathfindersCall, 3);
        var before = hero.MovementPoints;
        Assert.True(Cast(session, AdventureMagic.PathfindersCall).IsAccepted);
        Assert.Equal(before - AdventureMagic.MovementCost + (hero.MaxMovementPoints / 10), hero.MovementPoints);
    }

    [Fact]
    public void ViewTerrain_RevealsEighteenAtRankII()
    {
        var state = WithHeroes(40, 3, (0, 0), (39, 2));
        Mage(state, AdventureMagic.ViewTerrain, rank: 2);
        var session = Start(state, revealAll: false);
        Assert.False(state.Fog.IsExplored(Blue, new MapPosition(18, 0)));

        Assert.True(Cast(session, AdventureMagic.ViewTerrain).IsAccepted);

        Assert.True(state.Fog.IsExplored(Blue, new MapPosition(18, 0)));
    }

    [Fact]
    public void TheAmountWords_FollowTheSizes()
    {
        Assert.Equal("a few", AdventureIntel.AmountWord(4));
        Assert.Equal("several", AdventureIntel.AmountWord(5));
        Assert.Equal("a horde", AdventureIntel.AmountWord(99));
        Assert.Equal("a legion", AdventureIntel.AmountWord(1000));
    }
}
