using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.AI;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The Necropolis's Undead Transformer (ADR-137, ADR-143) and the Academy's Observatory (ADR-143).</summary>
public class UndeadTransformerTests
{
    private const string Skeleton = "necropolis.skeleton";
    private const string SkeletonWarrior = "necropolis.skeleton-warrior";
    private const string PlagueBearer = "necropolis.plague-bearer";
    private const string Ghoul = "necropolis.ghoul";
    private const string Wight = "necropolis.wight";

    /// <summary>Blue's Necropolis at (0, 0) with the Transformer; hero A on its entrance (3, 6), visiting it.</summary>
    private static (GameState State, Town Town) Necropolis(bool transformer = true)
    {
        var state = Create(new string('.', 12), new string('.', 12), new string('.', 12), new string('.', 12), new string('.', 12),
            new string('.', 12), new string('.', 12), "...A........");
        var town = AddTown(state, TownIds.Necropolis, 0, 0, Blue);
        if (transformer)
        {
            town.SetLevel(UndeadTransformer.Building, 1);
        }

        state.GetHero(A).Position = town.VisitPosition;
        return (state, town);
    }

    private static UndeadTransform? Of(GameState state, Town town, string creature, int count, CreatureCategory line = CreatureCategory.None) =>
        UndeadTransformer.Of(state, town, creature, count, line).Transform;

    [Fact]
    public void TheBuildings_AreRowsOfTheirTowns()
    {
        var catalog = BuildingCatalog.Default;

        var transformer = catalog.For(TownIds.Necropolis).Get(UndeadTransformer.Building, 1);
        Assert.Equal(new ResourceSet(Gold: 1000, Mercury: 5), transformer.Cost);
        Assert.Equal([new BuildingRequirement("dwelling-1", 1)], transformer.Requires);
        var observatory = catalog.For(TownIds.Academy).Get(Town.Observatory, 1);
        Assert.Equal("Observatory", observatory.Name);
        Assert.False(catalog.For(TownIds.Haven).Contains(Town.Observatory));
        Assert.False(catalog.For(TownIds.Haven).Contains(UndeadTransformer.Building));
    }

    [Fact]
    public void LivingCreatures_BecomeTheUndeadOfTheirClass_OneForOne()
    {
        var (state, town) = Necropolis();

        Assert.Equal(new UndeadTransform("haven.footman", 30, Skeleton, 30), Of(state, town, "haven.footman", 30)); // tier 1, A
        Assert.Equal(PlagueBearer, Of(state, town, "haven.pikeman", 5)!.To); // tier 1, B
        Assert.Equal(Skeleton, Of(state, town, "haven.zealot", 5)!.To); // tier 2, A
        Assert.Equal(Ghoul, Of(state, town, "haven.monk", 5)!.To); // tier 3, A
        Assert.Equal(PlagueBearer, Of(state, town, "haven.archer", 5)!.To); // tier 2, B
        Assert.Equal(Wight, Of(state, town, "haven.lancer", 5)!.To); // tier 4, B
        Assert.Null(Of(state, town, "conflux.storm-elemental", 5));
    }

    [Fact]
    public void AnUpgrade_CountsAsItsBase_AndBecomesTheUpgradedUndeadWithTheUpgradedDwelling()
    {
        var (state, town) = Necropolis();

        Assert.Equal(Skeleton, Of(state, town, "haven.crusader", 10)!.To);

        town.SetDwelling(Skeleton, 2);
        Assert.Equal(new UndeadTransform("haven.crusader", 10, SkeletonWarrior, 10), Of(state, town, "haven.crusader", 10));
    }

    [Fact]
    public void TheFirstTwoUndeadTiers_SwapLines_ForThreeQuarters()
    {
        var (state, town) = Necropolis();

        Assert.Equal(new UndeadTransform(Skeleton, 10, PlagueBearer, 7), Of(state, town, Skeleton, 10));
        Assert.Equal(new UndeadTransform(PlagueBearer, 1, Skeleton, 1), Of(state, town, PlagueBearer, 1));
        Assert.Equal(Wight, Of(state, town, Ghoul, 4)!.To);
        Assert.Equal(Skeleton, Of(state, town, "necropolis.flesh-golem", 4)!.To); // an upgraded undead counts as its base too
    }

    [Fact]
    public void ElementalsTierFiveAndHigherUndead_AreRefused_AndAClasslessCreaturePicksItsLine()
    {
        var (state, town) = Necropolis();

        Assert.NotNull(UndeadTransformer.Of(state, town, "conflux.wisp", 5).Refusal);
        Assert.NotNull(UndeadTransformer.Of(state, town, "haven.radiant-spirit", 1).Refusal);
        Assert.NotNull(UndeadTransformer.Of(state, town, "necropolis.vampire", 5).Refusal);

        Assert.True(UndeadTransformer.NeedsLine(state, BuiltInCreatures.Wolf));
        Assert.NotNull(UndeadTransformer.Of(state, town, BuiltInCreatures.Wolf, 5).Refusal);
        Assert.Equal(PlagueBearer, Of(state, town, BuiltInCreatures.Wolf, 5, CreatureCategory.B)!.To);
        Assert.Equal(Skeleton, Of(state, town, BuiltInCreatures.DireWolf, 5, CreatureCategory.A)!.To);
        Assert.False(UndeadTransformer.NeedsLine(state, "haven.footman"));
    }

    [Fact]
    public void SeveralStacks_AreTransformedAtOnce_AndJoinTheSameUndead()
    {
        var (state, town) = Necropolis();
        var hero = state.GetHero(A);
        hero.Army.Set(0, new CreatureStack(Skeleton, 4));
        hero.Army.Set(1, new CreatureStack("haven.footman", 10));
        hero.Army.Set(2, new CreatureStack(BuiltInCreatures.Wolf, 6));
        town.Garrison.Set(3, new CreatureStack("haven.pikeman", 8));
        var session = Start(state);
        var transformed = new List<CreaturesTransformed>();
        session.Events.Subscribe<CreaturesTransformed>(transformed.Add);
        var gold = state.GetPlayer(Blue).Resources;

        session.Execute(new TransformCreaturesCommand(Blue, town.Id,
        [
            new(TownArmy.VisitingHero, 1), new(TownArmy.VisitingHero, 2, CreatureCategory.B), new(TownArmy.Garrison, 3),
        ])).ThrowIfRejected();

        Assert.Equal([new CreatureStack(Skeleton, 14), null, new CreatureStack(PlagueBearer, 6)], hero.Army.Slots.Take(3));
        Assert.Equal(new CreatureStack(PlagueBearer, 8), town.Garrison[3]);
        Assert.Equal(3, transformed.Count);
        Assert.Equal(gold, state.GetPlayer(Blue).Resources); // free
    }

    [Fact]
    public void AnOrderWithARefusedStack_ChangesNothing_AndATownWithoutTheBuildingRefuses()
    {
        var (state, town) = Necropolis();
        var hero = state.GetHero(A);
        hero.Army.Set(1, new CreatureStack("haven.footman", 10));
        hero.Army.Set(2, new CreatureStack(BuiltInCreatures.Wolf, 6));
        var session = Start(state);

        Assert.False(session.Execute(new TransformCreaturesCommand(Blue, town.Id, [new(TownArmy.VisitingHero, 1), new(TownArmy.VisitingHero, 2)])).IsAccepted);
        Assert.Equal("haven.footman", hero.Army[1]!.Creature);

        var (other, plain) = Necropolis(transformer: false);
        other.GetHero(A).Army.Set(1, new CreatureStack("haven.footman", 10));
        Assert.Contains("Undead Transformer", Start(other).Execute(new TransformCreaturesCommand(Blue, plain.Id, [new(TownArmy.VisitingHero, 1)])).Error);
    }

    [Fact]
    public void ATransform_IsReplayed()
    {
        var (state, town) = Necropolis();
        town.Garrison.Set(0, new CreatureStack("haven.footman", 10));
        var map = MapConverter.ToDocument(state);
        var session = new GameSession(MapConverter.ToGameState(map));
        session.Start();

        session.Execute(new TransformCreaturesCommand(Blue, town.Id, [new(TownArmy.Garrison, 0)])).ThrowIfRejected();

        var replayed = GameReplay.Replay(GameRecordSerializer.Deserialize(GameRecordSerializer.Serialize(GameReplay.Record(map, session))));
        Assert.Equal(new CreatureStack(Skeleton, 10), ((Town)replayed.State.GetObject(town.Id)).Garrison[0]);
    }

    [Fact]
    public void TheAi_TurnsTheLivingIntoUndead_InAMostlyUndeadArmy()
    {
        var (state, town) = Necropolis();
        town.Garrison.Set(0, new CreatureStack(Ghoul, 20));
        town.Garrison.Set(1, new CreatureStack("haven.footman", 10));
        state.GetHero(A).Position = new MapPosition(11, 7);
        var session = Start(state);

        new AdventureAi(session, controlHumanSlots: true).PlayTurn();

        Assert.Equal(new CreatureStack(Skeleton, 10), town.Garrison.Stacks.Single(s => s.Stack.Creature != Ghoul).Stack);

        var (living, kept) = Necropolis();
        kept.Garrison.Set(0, new CreatureStack("haven.footman", 10)); // all living, and worth more than 10 Skeletons: kept
        living.GetHero(A).Position = new MapPosition(11, 7);
        new AdventureAi(Start(living), controlHumanSlots: true).PlayTurn();

        Assert.Equal("haven.footman", Assert.Single(kept.Garrison.Stacks).Stack.Creature);
    }

    [Fact]
    public void TheObservatory_LetsAnAcademyTownSeeTwentyFourCells_FromTheDayItIsBuilt()
    {
        var rows = Enumerable.Range(0, 40).Select(y => y == 39 ? new string('.', 39) + "A" : new string('.', 40)).ToArray();
        var state = Create(rows);
        var town = AddTown(state, TownIds.Academy, 0, 0, Blue); // centre (3, 3)
        town.SetLevel(BuiltInBuildings.MageGuild, 1);
        state.GetPlayer(Blue).Resources = new ResourceSet(Gold: 10_000, Ore: 10, Mercury: 10);
        var session = Start(state, revealAll: false);
        var far = new MapPosition(3, 26);

        Assert.Equal(Town.TownScoutingRadius, town.ScoutingRadius);
        Assert.False(state.Fog.IsExplored(Blue, far));

        session.Execute(new BuildCommand(Blue, town.Id, Town.Observatory)).ThrowIfRejected();

        Assert.Equal(Town.ObservatoryScoutingRadius, town.ScoutingRadius);
        Assert.True(state.Fog.IsExplored(Blue, far));
        Assert.False(state.Fog.IsExplored(Blue, new MapPosition(3, 28)));
    }
}
