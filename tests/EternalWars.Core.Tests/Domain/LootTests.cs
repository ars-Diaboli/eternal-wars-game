using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.AI;
using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>One-time loot (ADR-068): campfires, supply crates, wagons, corpses, lean-tos, the warrior's tomb, and the new treasure chest table.</summary>
public class LootTests
{
    private static readonly ResourceType[] NonGold = [ResourceType.Lumber, ResourceType.Ore, ResourceType.Crystal, ResourceType.Mercury, ResourceType.Sulfur];

    private static ResourceType[] Kinds(ResourceSet r) => [.. Enum.GetValues<ResourceType>().Where(t => r[t] != 0)];

    /// <summary>Hero A at (3, 2); the site at (3, 1) for single-tile sites. Other heroes visit through the service directly.</summary>
    private static (GameSession Session, LootSite Site) World(string definition, ulong seed = 1)
    {
        var state = Create(new GameOptions(Seed: seed), ".........", ".........", "...A.....", ".........", "C.......B");
        var single = MapObjectCatalog.Default.Get(definition).Footprint.Width == 1;
        var site = Add<LootSite>(state, definition, single ? 3 : 2, single ? 1 : 0);
        return (Start(state), site);
    }

    private static List<LootFound> Searches(string definition, int games)
    {
        var found = new List<LootFound>();
        for (ulong seed = 1; seed <= (ulong)games; seed++)
        {
            var (session, site) = World(definition, seed);
            session.Events.Subscribe<LootFound>(found.Add);
            session.Interactions.Visit(session.State.GetHero(A), site);
        }

        return found;
    }

    [Fact]
    public void ACampfire_GivesGoldInHundredsAndOneOtherResource_AndVanishes()
    {
        var (session, campfire) = World(BuiltInMapObjects.Campfire);
        var before = session.State.GetPlayer(Blue).Resources;
        var found = new List<LootFound>();
        session.Events.Subscribe<LootFound>(found.Add);

        Assert.Equal(MoveResult.Moved, session.Step(A, Direction.North));

        var loot = Assert.Single(found).Resources;
        Assert.Contains(loot.Gold, new[] { 400, 500, 600 });
        var other = Assert.Single(Kinds(loot with { Gold = 0 }));
        Assert.InRange(loot[other], 4, 6);
        Assert.Equal(before + loot, session.State.GetPlayer(Blue).Resources);
        Assert.Null(session.State.FindObject(campfire.Id));

        Assert.All(Searches(BuiltInMapObjects.Campfire, 30), f => Assert.Contains(f.Resources.Gold, new[] { 400, 500, 600 }));
    }

    [Fact]
    public void SupplyCrates_GiveTwoDifferentResources_FourToSixOfEach()
    {
        var all = Searches(BuiltInMapObjects.SupplyCrates, 40);

        Assert.Equal(40, all.Count);
        Assert.All(all, f =>
        {
            Assert.Equal(0, f.Resources.Gold);
            var kinds = Kinds(f.Resources);
            Assert.Equal(2, kinds.Length);
            Assert.All(kinds, k => Assert.InRange(f.Resources[k], 4, 6));
        });
    }

    [Fact]
    public void ATravelersWagon_GivesNothing_AMinorArtifact_OrTwoToFiveOfOneResource()
    {
        var all = Searches(BuiltInMapObjects.TravelersWagon, 200);

        Assert.All(all, f =>
        {
            if (f.Artifact is { } artifact)
            {
                Assert.Equal(ArtifactTier.Minor, ArtifactCatalog.Default.Get(artifact).Tier);
                Assert.Equal(ResourceSet.Empty, f.Resources);
            }
            else if (f.Resources != ResourceSet.Empty)
            {
                Assert.InRange(f.Resources[Assert.Single(Kinds(f.Resources))], 2, 5);
                Assert.Equal(0, f.Resources.Gold);
            }
        });
        Assert.InRange(all.Count(f => f.Artifact is not null), 35, 85);       // 30 %
        Assert.InRange(all.Count(f => f.Resources != ResourceSet.Empty), 95, 145); // 60 %
        Assert.Contains(all, f => f.Artifact is null && f.Resources == ResourceSet.Empty); // 10 % nothing
    }

    [Fact]
    public void ACorpse_StaysAndRewardsOnlyItsFirstVisitor()
    {
        var all = Searches(BuiltInMapObjects.Corpse, 200);
        Assert.InRange(all.Count(f => f.Artifact is not null), 20, 60); // 20 %
        Assert.All(all, f => Assert.Equal(ResourceSet.Empty, f.Resources));

        var (session, corpse) = World(BuiltInMapObjects.Corpse);
        var empty = new List<MapObjectFoundEmpty>();
        session.Events.Subscribe<MapObjectFoundEmpty>(empty.Add);
        session.Step(A, Direction.North);
        session.Interactions.Visit(session.State.GetHero(C), corpse);

        Assert.Same(corpse, session.State.FindObject(corpse.Id));
        Assert.True(corpse.Visits.IsSpent);
        Assert.Equal(new[] { new MapObjectFoundEmpty(C, corpse.Id) }, empty);
    }

    [Fact]
    public void ALeanTo_GivesOneToSixOfOneResource_Once()
    {
        var all = Searches(BuiltInMapObjects.LeanTo, 40);
        Assert.All(all, f =>
        {
            var kind = Assert.Single(Kinds(f.Resources));
            Assert.Contains(kind, NonGold);
            Assert.InRange(f.Resources[kind], 1, 6);
        });

        var (session, leanTo) = World(BuiltInMapObjects.LeanTo);
        session.Interactions.Visit(session.State.GetHero(A), leanTo);
        var after = session.State.GetPlayer(Blue).Resources;
        session.Interactions.Visit(session.State.GetHero(C), leanTo);
        Assert.Equal(after, session.State.GetPlayer(Blue).Resources);
    }

    [Fact]
    public void TheWarriorsTomb_GivesItsFirstVisitorAnArtifact_AndEveryVisitorMinusThreeMoraleForTheNextBattle()
    {
        var all = Searches(BuiltInMapObjects.WarriorsTomb, 300);
        var tiers = all.Select(f => ArtifactCatalog.Default.Get(f.Artifact!).Tier).ToList();
        Assert.Equal(300, tiers.Count); // always an artifact for the first visitor
        Assert.InRange(tiers.Count(t => t == ArtifactTier.Minor), 210, 270);
        Assert.InRange(tiers.Count(t => t == ArtifactTier.Major), 25, 75);
        Assert.Contains(ArtifactTier.Relic, tiers);

        var (session, tomb) = World(BuiltInMapObjects.WarriorsTomb);
        var first = session.State.GetHero(A);
        var later = session.State.GetHero(C);
        session.Interactions.Visit(first, tomb);
        session.Interactions.Visit(later, tomb);

        Assert.Equal(LootRules.TombMorale, first.TotalMorale);
        Assert.Equal(LootRules.TombMorale, later.TotalMorale);
        Assert.Single(first.Artifacts.Backpack);
        Assert.Empty(later.Artifacts.Backpack);
        Assert.Equal(StatusExpiry.NextBattle, Assert.Single(later.Statuses).Expires);
    }

    [Fact]
    public void BuffsEnd_AfterABattleAgainstNeutralsToo()
    {
        var state = Create(".A.....", ".......", "......B");
        var monster = Add<MonsterGuard>(state, BuiltInMapObjects.Monster, 2, 1);
        monster.Creature = BuiltInCreatures.Wolf;
        monster.Count = 2;
        monster.Disposition = GuardDisposition.Savage;
        monster.NeverFlees = true; // a fight, not a flee offer
        var tomb = Add<LootSite>(state, BuiltInMapObjects.WarriorsTomb, 4, 0);
        var session = Start(state);
        var hero = state.GetHero(A);
        session.Interactions.Visit(hero, tomb);
        Assert.Equal(LootRules.TombMorale, hero.TotalMorale);

        Assert.Equal(MoveResult.GuardEncountered, session.Step(A, Direction.SouthEast));
        session.AutoResolve();

        Assert.Null(state.Battle);
        Assert.Same(hero, state.Heroes.Single(h => h.Id == A)); // won
        Assert.Empty(hero.Statuses);
        Assert.Equal(0, hero.TotalMorale);
    }

    [Fact]
    public void ClassicGuards_LetVanishingLootBeStolen()
    {
        var state = Create(".A.....", ".......", "......B");
        var monster = Add<MonsterGuard>(state, BuiltInMapObjects.Monster, 3, 1);
        monster.Count = 5;
        monster.Disposition = GuardDisposition.Savage;
        Add<LootSite>(state, BuiltInMapObjects.Campfire, 2, 0); // guarded by the monster at (3, 1)
        var session = Start(state);

        Assert.Equal(MoveResult.CollectedFromAdjacent, session.Step(A, Direction.East));
        Assert.Empty(state.ObjectsOf<LootSite>());
    }

    [Fact]
    public void TheTreasureChest_RollsTheNewTable_WithAMinorArtifactNowAndThen()
    {
        Assert.Equal(new[] { 30, 30, 25, 10, 5 }, TreasureChest.Odds.Select(o => o.Weight));
        Assert.Equal(new ChestValues(2500, 2000), TreasureChest.RollTable[^1]);

        var offers = new List<TreasureChestDecision>();
        var artifacts = new List<LootFound>();
        for (ulong seed = 1; seed <= 300; seed++)
        {
            var state = Create(new GameOptions(Seed: seed), ".........", "...A.....", "........B");
            var chest = Add<TreasureChest>(state, BuiltInMapObjects.TreasureChest, 3, 0);
            var session = Start(state);
            session.Events.Subscribe<LootFound>(artifacts.Add);
            session.Interactions.Visit(state.GetHero(A), chest);
            if (state.PendingDecision is TreasureChestDecision decision)
            {
                offers.Add(decision);
            }
            else
            {
                Assert.Null(state.FindObject(chest.Id)); // emptied: the artifact needs no choice
                Assert.Single(state.GetHero(A).Artifacts.Backpack);
            }
        }

        Assert.Equal(300, offers.Count + artifacts.Count);
        Assert.InRange(artifacts.Count, 5, 30); // 5 %
        Assert.All(artifacts, f => Assert.Equal(ArtifactTier.Minor, ArtifactCatalog.Default.Get(f.Artifact!).Tier));
        Assert.Contains(offers, o => o.Gold == 2500 && o.Experience == 2000);
        Assert.All(offers, o => Assert.Contains(new ChestValues(o.Gold, o.Experience), TreasureChest.RollTable));
    }

    [Fact]
    public void AFixedChest_NeverHoldsAnArtifact()
    {
        for (ulong seed = 1; seed <= 60; seed++)
        {
            var state = Create(new GameOptions(Seed: seed), ".........", "...A.....", "........B");
            var chest = Add<TreasureChest>(state, BuiltInMapObjects.TreasureChest, 3, 0);
            chest.FixedValues = new ChestValues(1000, 500);
            var session = Start(state);
            session.Interactions.Visit(state.GetHero(A), chest);
            Assert.IsType<TreasureChestDecision>(state.PendingDecision);
        }
    }

    [Fact]
    public void LootSites_AreInTheMapFormat_AndTheEditorPlacesThem()
    {
        var editor = new MapEditorSession(BlankScenario.Create(14, 10));
        Assert.Equal(EditResult.Ok, editor.PlaceObject(BuiltInMapObjects.Campfire, new(2, 2), out _));
        Assert.Equal(EditResult.Ok, editor.PlaceObject(BuiltInMapObjects.WarriorsTomb, new(6, 2), out _));
        Assert.DoesNotContain(MapValidator.Validate(editor.Scenario), i => i.Severity == IssueSeverity.Error);

        var json = MapSerializer.Serialize(MapConverter.ToDocument(editor.Scenario));
        var restored = MapConverter.ToGameState(MapSerializer.Deserialize(json));

        Assert.True(MapDocument.CurrentFormatVersion >= 17);
        Assert.Contains("\"kind\": \"loot\"", json, StringComparison.Ordinal);
        Assert.Equal(new[] { BuiltInMapObjects.Campfire, BuiltInMapObjects.WarriorsTomb }, restored.ObjectsOf<LootSite>().Select(s => s.Definition.Id));
    }

    [Fact]
    public void TheAi_PicksUpLootNearby_AndLeavesSpentSitesAlone()
    {
        var (session, crates) = World(BuiltInMapObjects.SupplyCrates);
        new AdventureAi(session, controlHumanSlots: true).PlayTurn();
        Assert.Null(session.State.FindObject(crates.Id));

        var (second, corpse) = World(BuiltInMapObjects.Corpse);
        second.Interactions.Visit(second.State.GetHero(C), corpse); // spent before A gets there
        new AdventureAi(second, controlHumanSlots: true).PlayTurn();
        Assert.NotEqual(corpse.VisitPosition, second.State.GetHero(A).Position);
    }
}
