using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>
/// The third object catalogue (ADR-079): cartographer (MAP-31), artifact merchant (MAP-33), refugee camp (MAP-30), sacrificial altar (MAP-29),
/// sphinx (MAP-27) and witch hut (MAP-34).
/// </summary>
public class ThirdCatalogueTests
{
    // Every object here has its entrance at (3, 1) when placed at (2, 0); hero A starts below it, Charlie in the corner.
    private static (GameSession Session, T Object) World<T>(string definition, Action<GameState, T>? setUp = null, bool revealAll = true)
        where T : MapObject
    {
        var state = Create(".............................", ".............................", "...A.........................", ".............................", "C...........................B");
        var mapObject = Add<T>(state, definition, 2, 0);
        setUp?.Invoke(state, mapObject);
        var session = Start(state, revealAll);
        state.GetPlayer(Blue).Resources = new ResourceSet(Gold: 50_000);
        return (session, mapObject);
    }

    private static Hero HeroA(GameSession session) => session.State.GetHero(A);

    [Fact]
    public void TheCartographer_SellsTheWholeLevelFor3000_OncePerPlayer()
    {
        var (session, cartographer) = World<Cartographer>(BuiltInMapObjects.SurfaceCartographer, revealAll: false);
        var state = session.State;
        Assert.False(state.Fog.IsExplored(Blue, new MapPosition(20, 4)));

        session.Step(A, Direction.North);
        Assert.Equal(new CartographerDecision(A, cartographer.Id, 0, 3000), state.PendingDecision);
        session.Execute(new AnswerCartographerCommand(Blue, Buy: true)).ThrowIfRejected();

        Assert.Equal(47_000, state.GetPlayer(Blue).Resources.Gold);
        Assert.True(state.Fog.IsExplored(Blue, new MapPosition(20, 4)));
        var empty = new List<MapObjectFoundEmpty>();
        session.Events.Subscribe<MapObjectFoundEmpty>(empty.Add);
        session.Interactions.Visit(state.GetHero(C), cartographer);
        Assert.Null(state.PendingDecision);
        Assert.Single(empty);
    }

    [Fact]
    public void TheCartographer_RefusesWithoutGold_AndTheUndergroundOneOnASingleLevelMap()
    {
        var (session, _) = World<Cartographer>(BuiltInMapObjects.SurfaceCartographer);
        var refused = new List<MapObjectRefused>();
        session.Events.Subscribe<MapObjectRefused>(refused.Add);
        session.State.GetPlayer(Blue).Resources = new ResourceSet(Gold: 2999);
        session.Step(A, Direction.North);
        Assert.Null(session.State.PendingDecision);
        Assert.Contains("3000 gold", Assert.Single(refused).Reason, StringComparison.Ordinal);

        var (deep, _) = World<Cartographer>(BuiltInMapObjects.UndergroundCartographer);
        var noUnderground = new List<MapObjectRefused>();
        deep.Events.Subscribe<MapObjectRefused>(noUnderground.Add);
        deep.Step(A, Direction.North);
        Assert.Contains("no underground", Assert.Single(noUnderground).Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void TheMerchant_LaysOutSevenMinorOrMajorArtifacts_SellsThemForTwiceTheirWorth_AndRestocksMonthly()
    {
        var (session, merchant) = World<ArtifactMerchant>(BuiltInMapObjects.ArtifactMerchant);
        var state = session.State;
        Assert.Equal(ArtifactMerchant.StockSize, merchant.Stock.Count);
        Assert.All(merchant.Stock, a => Assert.Contains(state.Artifacts.Get(a!).Tier, new[] { ArtifactTier.Minor, ArtifactTier.Major }));
        Assert.False(session.Execute(new BuyArtifactCommand(Blue, merchant.Id, 0)).IsAccepted); // nobody stands there

        session.Step(A, Direction.North);
        var artifact = merchant.Stock[0]!;
        var price = ArtifactMerchantRules.Price(state.Artifacts.Get(artifact));
        Assert.Equal(2 * ArtifactMerchantRules.Value(state.Artifacts.Get(artifact).Tier), price.Gold);
        session.Execute(new BuyArtifactCommand(Blue, merchant.Id, 0)).ThrowIfRejected();

        Assert.Contains(artifact, HeroA(session).Artifacts.Backpack);
        Assert.Equal(50_000 - price.Gold, state.GetPlayer(Blue).Resources.Gold);
        Assert.Null(merchant.Stock[0]);
        Assert.Contains("Nothing is for sale", session.Execute(new BuyArtifactCommand(Blue, merchant.Id, 0)).Error);

        session.Interactions.StartWeek(restock: false);
        Assert.Null(merchant.Stock[0]); // a sold slot waits for the month
        session.Interactions.StartWeek(restock: true);
        Assert.All(merchant.Stock, Assert.NotNull);
    }

    [Fact]
    public void ARefugeeCamp_HasAWeekOfATier3To5BaseCreature_SoldAtTheNormalPrice_AndANewOneEveryWeek()
    {
        var (session, camp) = World<RefugeeCamp>(BuiltInMapObjects.RefugeeCamp);
        var state = session.State;
        var creature = state.Creatures.Get(camp.Creature!);
        Assert.InRange(creature.Tier, 3, 5);
        Assert.Equal(creature.Id, state.Creatures.BaseOf(creature.Id));
        Assert.Contains(creature.Town, BuiltInMapObjects.PlayableTowns);
        Assert.Equal(creature.WeeklyGrowth, camp.Available);

        session.Step(A, Direction.North);
        session.Execute(new RecruitAtRefugeeCampCommand(Blue, camp.Id, 2)).ThrowIfRejected();

        Assert.Equal(creature.WeeklyGrowth - 2, camp.Available);
        Assert.Equal(50_000 - (2 * creature.Cost.Gold), state.GetPlayer(Blue).Resources.Gold);
        Assert.Contains(HeroA(session).Army.Stacks, s => s.Stack == new CreatureStack(creature.Id, 2));
        Assert.False(session.Execute(new RecruitAtRefugeeCampCommand(Blue, camp.Id, camp.Available + 1)).IsAccepted);

        var seen = new HashSet<string>();
        for (var week = 0; week < 12; week++)
        {
            session.Interactions.StartWeek(restock: false);
            seen.Add(camp.Creature!);
            Assert.Equal(state.Creatures.Get(camp.Creature!).WeeklyGrowth, camp.Available);
        }

        Assert.True(seen.Count > 1);
    }

    [Fact]
    public void DarknessHeroes_SacrificeCreatures_ForTwiceTheirHitPoints_KeepingOne()
    {
        var (session, altar) = World<SacrificialAltar>(BuiltInMapObjects.SacrificialAltar, (state, _) =>
        {
            state.GetHero(A).PreferredCreature = "necropolis.skeleton";
            state.GetHero(A).Army.Set(1, new CreatureStack("necropolis.skeleton", 5));
        });
        session.Step(A, Direction.North);
        var hero = HeroA(session);
        var health = session.State.Creatures.Get("necropolis.skeleton").Health;

        session.Execute(new SacrificeCreaturesCommand(Blue, altar.Id, 1, 5)).ThrowIfRejected();

        Assert.Equal(5 * health * 2, hero.Experience);
        Assert.Null(hero.Army[1]);
        Assert.Contains("keep at least one", session.Execute(new SacrificeCreaturesCommand(Blue, altar.Id, 0, hero.Army[0]!.Count)).Error);
        Assert.Contains("cannot sacrifice artifacts", session.Execute(new SacrificeArtifactCommand(Blue, altar.Id, "x")).Error);
    }

    [Fact]
    public void LightHeroes_SacrificeArtifacts_ByTier_AndWildHeroesEither()
    {
        var minor = ArtifactCatalog.Default.OfTier(ArtifactTier.Minor).First().Id;
        var (session, altar) = World<SacrificialAltar>(BuiltInMapObjects.SacrificialAltar, (state, _) =>
        {
            state.GetHero(A).PreferredCreature = "haven.footman";
            state.GetHero(A).Artifacts.AddToBackpack(minor);
        });
        session.Step(A, Direction.North);

        Assert.Contains("cannot sacrifice creatures", session.Execute(new SacrificeCreaturesCommand(Blue, altar.Id, 0, 1)).Error);
        session.Execute(new SacrificeArtifactCommand(Blue, altar.Id, minor)).ThrowIfRejected();

        Assert.Equal(2000, HeroA(session).Experience);
        Assert.DoesNotContain(minor, HeroA(session).Artifacts.Backpack);
        Assert.Equal(SacrificeKind.Creatures | SacrificeKind.Artifacts, SacrificeRules.For(Core.Domain.Towns.TownIds.Stronghold));
        Assert.Equal(SacrificeKind.Creatures, SacrificeRules.For(Core.Domain.Towns.TownIds.Dungeon));
    }

    private static readonly Riddle Author = new("What has keys but opens no lock?", "A piano", "A jailer", "A chest");

    [Fact]
    public void TheSphinx_AsksItsAuthorsRiddle_AWrongAnswerCostsTheDayAndMorale_AndEachHeroTriesOnce()
    {
        var (session, sphinx) = World<Sphinx>(BuiltInMapObjects.Sphinx, (_, s) => s.Riddle = Author);
        session.Step(A, Direction.North);
        var decision = Assert.IsType<SphinxDecision>(session.State.PendingDecision);
        Assert.Equal(Author.Question, decision.Question);
        Assert.Equal("A piano", decision.Options[decision.Correct]);
        Assert.Equal(["A chest", "A jailer", "A piano"], decision.Options.Order(StringComparer.Ordinal));

        session.Execute(new AnswerSphinxCommand(Blue, (decision.Correct + 1) % 3)).ThrowIfRejected();

        var hero = HeroA(session);
        Assert.Equal(0, hero.MovementPoints);
        Assert.Contains(hero.Statuses, s => s.Kind == HeroStatusKind.Morale && s.Value == -3 && s.Expires == StatusExpiry.NextBattle);
        session.Interactions.Visit(hero, sphinx);
        Assert.Null(session.State.PendingDecision); // one try per hero
    }

    [Fact]
    public void TheRightAnswer_BringsAMajorArtifactOr5000Experience_AndRiddlesComeFromTheTable()
    {
        Assert.True(RiddleTable.Default.Count >= 10);
        var rewards = new List<SphinxAnswered>();
        for (var seed = 0; seed < 8; seed++)
        {
            var state = Create(new GameOptions(Seed: (ulong)seed), ".........", ".........", "...A.....", ".........", "C.......B");
            Add<Sphinx>(state, BuiltInMapObjects.Sphinx, 2, 0);
            var session = Start(state);
            session.Events.Subscribe<SphinxAnswered>(rewards.Add);
            session.Step(A, Direction.North);
            var decision = Assert.IsType<SphinxDecision>(state.PendingDecision);
            Assert.Contains(RiddleTable.Default, r => r.Question == decision.Question);
            session.Execute(new AnswerSphinxCommand(Blue, decision.Correct)).ThrowIfRejected();
        }

        Assert.All(rewards, r => Assert.True(r.Right && (r.Experience == 5000) != (r.Artifact is not null)));
        Assert.Contains(rewards, r => r.Artifact is { } a && ArtifactCatalog.Default.Get(a).Tier == ArtifactTier.Major);
        Assert.Contains(rewards, r => r.Experience == 5000);
    }

    [Fact]
    public void TheWitchHut_OffersItsTwoSkills_OrAnotherOfTheirGroup_OncePerLevel()
    {
        var (session, hut) = World<WitchHut>(BuiltInMapObjects.WitchHut, (state, h) =>
        {
            h.Skills = [BuiltInSkills.Pathfinding, BuiltInSkills.Stewardship];
            state.GetHero(A).Skills.Set(BuiltInSkills.Pathfinding, BuiltInSkills.Get(BuiltInSkills.Pathfinding).MaxLevel);
        });
        var hero = HeroA(session);
        session.Step(A, Direction.North);

        var decision = Assert.IsType<WitchHutDecision>(session.State.PendingDecision);
        Assert.Equal(2, decision.Options.Count);
        Assert.Equal("pathfinding", BuiltInSkills.Get(decision.Options[0]).Group);
        Assert.NotEqual(BuiltInSkills.Pathfinding, decision.Options[0]); // maxed: another of the group stands in
        Assert.Equal(BuiltInSkills.Stewardship, decision.Options[1]);
        session.Execute(new ChooseWitchHutSkillCommand(Blue, BuiltInSkills.Stewardship)).ThrowIfRejected();

        Assert.Equal(SkillLevel.Basic, hero.Skills.Level(BuiltInSkills.Stewardship));
        Assert.Equal(1, hero.WitchHutLevel);
        var refused = new List<MapObjectRefused>();
        session.Events.Subscribe<MapObjectRefused>(refused.Add);
        session.Interactions.Visit(hero, hut);
        Assert.Contains("at this level already", Assert.Single(refused).Reason, StringComparison.Ordinal);

        hero.Level = 2;
        session.Interactions.Visit(hero, hut);
        Assert.IsType<WitchHutDecision>(session.State.PendingDecision);
        session.Execute(new ChooseWitchHutSkillCommand(Blue, null)).ThrowIfRejected(); // leaving uses nothing
        Assert.Equal(1, hero.WitchHutLevel);
    }

    [Fact]
    public void TheNewObjects_AreInTheMapFormat_WithTheirRiddleAndSkills_AndAGameWithThemReplays()
    {
        var state = Create(".............", ".............", "...A.........", ".............", "C...........B");
        Add<Sphinx>(state, BuiltInMapObjects.Sphinx, 2, 0).Riddle = Author;
        Add<WitchHut>(state, BuiltInMapObjects.WitchHut, 6, 0).Skills = [BuiltInSkills.Logistics, BuiltInSkills.Archery];
        Add<Cartographer>(state, BuiltInMapObjects.SurfaceCartographer, 9, 0);
        Add<ArtifactMerchant>(state, BuiltInMapObjects.ArtifactMerchant, 6, 3);
        Add<RefugeeCamp>(state, BuiltInMapObjects.RefugeeCamp, 2, 3);
        Add<SacrificialAltar>(state, BuiltInMapObjects.SacrificialAltar, 9, 3);
        var map = MapConverter.ToDocument(state);
        var json = MapSerializer.Serialize(map);
        var restored = MapConverter.ToGameState(MapSerializer.Deserialize(json));

        Assert.Equal(Author, restored.ObjectsOf<Sphinx>().Single().Riddle);
        Assert.Equal([BuiltInSkills.Logistics, BuiltInSkills.Archery], restored.ObjectsOf<WitchHut>().Single().Skills);
        Assert.Equal(6, restored.Objects.Count);

        var session = new GameSession(MapConverter.ToGameState(map, options: new GameOptions(Seed: 4)));
        session.Start();
        session.Step(A, Direction.North);
        session.Execute(new AnswerSphinxCommand(Blue, 0)).ThrowIfRejected();
        var record = GameRecordSerializer.Serialize(GameReplay.Record(map, session));
        var replayed = GameReplay.Replay(GameRecordSerializer.Deserialize(record));

        Assert.Contains("\"sphinx\"", record, StringComparison.Ordinal);
        Assert.Equal(GameStateDigest.Describe(session.State), GameStateDigest.Describe(replayed.State));
    }
}
