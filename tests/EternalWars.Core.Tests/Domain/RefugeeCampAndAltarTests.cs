using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Objects;
using static EternalWars.Core.Tests.Domain.ObjectVisitWorld;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The refugee camp (MAP-30) and the sacrificial altar (MAP-29) of the third object catalogue (ADR-079).</summary>
public class RefugeeCampAndAltarTests
{
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
            session.ObjectLifecycle.StartWeek(restock: false);
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
}
