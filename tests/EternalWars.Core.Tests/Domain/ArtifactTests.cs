using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.AI;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>Artifacts (ADR-061): the catalog, wearing and carrying, effects, pickups, loot, map data and the AI.</summary>
public class ArtifactTests
{
    private static void Outfit(GameState state, HeroId hero, string[]? worn = null, string[]? backpack = null)
    {
        var h = state.GetHero(hero);
        h.Artifacts.ReplaceWith(worn ?? [], backpack ?? [], state.Artifacts);
        state.RefreshArtifacts(h);
    }

    // The catalog ------------------------------------------------------------------------------------------

    [Fact]
    public void TheShippedCatalog_HasEveryTierAndSlot_AndCompleteSets()
    {
        var catalog = ArtifactCatalog.Default;

        Assert.True(catalog.All.Count() >= 60);
        Assert.All(Enum.GetValues<ArtifactTier>(), tier => Assert.NotEmpty(catalog.OfTier(tier)));
        Assert.All(ArtifactSlots.All, slot => Assert.Contains(catalog.All, a => a.Slot == slot));
        Assert.All(catalog.All, a => Assert.NotEmpty(a.Effects));
        Assert.All(catalog.Sets, set => Assert.True(catalog.PiecesOf(set.Id).Count() >= 2));
        Assert.Equal(5, catalog.Sets.Count());
        Assert.Equal(catalog.All.Count(), catalog.All.Select(a => a.Id).Distinct().Count());
    }

    [Fact]
    public void Relics_AreFewer_ThanMinorAndMajorArtifacts()
    {
        var catalog = ArtifactCatalog.Default;

        Assert.True(catalog.OfTier(ArtifactTier.Relic).Count() < catalog.OfTier(ArtifactTier.Minor).Count());
        Assert.True(catalog.OfTier(ArtifactTier.Relic).Count() < catalog.OfTier(ArtifactTier.Major).Count());
    }

    [Theory]
    [InlineData("attack 3", ArtifactEffectKind.Attack, 3, null)]
    [InlineData("defense -3", ArtifactEffectKind.Defense, -3, null)]
    [InlineData("income gold 100", ArtifactEffectKind.Income, 100, "gold")]
    [InlineData("native-terrain forest-floor", ArtifactEffectKind.NativeTerrain, 1, "ForestFloor")]
    [InlineData("skill machines.ballistics 1", ArtifactEffectKind.Skill, 1, "machines.ballistics")]
    [InlineData("all-skills 1", ArtifactEffectKind.AllSkills, 1, null)]
    [InlineData("sight 2", ArtifactEffectKind.Sight, 2, null)]
    [InlineData("immune mind", ArtifactEffectKind.Immune, 1, "mind")]
    [InlineData("element-power fire 25", ArtifactEffectKind.ElementPower, 25, "fire")]
    [InlineData("spells 1-2", ArtifactEffectKind.Spells, 2, "1")]
    [InlineData("no-retreat", ArtifactEffectKind.NoRetreat, 1, null)]
    [InlineData("no-spells-above 2", ArtifactEffectKind.NoSpellsAbove, 2, null)]
    public void EffectsParse(string text, ArtifactEffectKind kind, int value, string? target) =>
        Assert.Equal(new ArtifactEffect(kind, value, target), ArtifactEffect.Parse(text));

    [Theory]
    [InlineData("")]
    [InlineData("fly 2")]
    [InlineData("attack")]
    [InlineData("income gold")]
    [InlineData("native-terrain moon")]
    [InlineData("skill no.such 1")]
    [InlineData("element-power all 25")]
    [InlineData("spells 2-1")]
    [InlineData("spells 1-5")]
    [InlineData("no-retreat 1")]
    [InlineData("immune")]
    public void BadEffects_AreRefused(string text) => Assert.Throws<FormatException>(() => ArtifactEffect.Parse(text));

    [Fact]
    public void ABrokenTable_IsRefused()
    {
        Assert.Throws<FormatException>(() => ArtifactTable.ParseArtifacts("id,name,slot,tier,set,effects\nx,X,Head,Minor,,attack many\n"));
        Assert.Throws<FormatException>(() => new ArtifactCatalog(ArtifactTable.ParseArtifacts("id,name,slot,tier,set,effects\nx,X,Head,Minor,nope,attack 1\n")));
        Assert.Throws<FormatException>(() => new ArtifactCatalog(ArtifactTable.ParseArtifacts("id,name,slot,tier,set,effects\nx,X,Head,Minor,,attack 1\nx,Y,Head,Minor,,attack 1\n")));
    }

    // What a hero wears ------------------------------------------------------------------------------------

    [Fact]
    public void WornArtifacts_AddUp_AndCompleteSetsAddTheirBonus()
    {
        var state = Create("A.B");
        Outfit(state, A, worn: ["rusted-blade", "squires-shield", "badge-of-courage"]);
        var hero = state.GetHero(A);

        Assert.Equal(hero.Attack + 1, hero.TotalAttack);
        Assert.Equal(hero.Defense + 1, hero.TotalDefense);
        Assert.Equal(1, hero.Bonus.Morale);
        Assert.Equal(hero.Morale + 1, hero.TotalMorale);

        Outfit(state, A, worn: ["pilgrims-boots", "weathered-cloak"]); // two of the three pieces
        Assert.Equal(10, hero.Bonus.MovementPercent);
        Assert.Equal(25, hero.Bonus.TerrainPenaltyReductionPercent);

        Outfit(state, A, worn: ["pilgrims-boots", "weathered-cloak", "hearth-charm"]); // the whole set of Pilgrim's Road
        Assert.Equal(30, hero.Bonus.MovementPercent);
        Assert.Equal(100, hero.Bonus.TerrainPenaltyReductionPercent);
        Assert.Equal(100, hero.Bonus.Income.Gold);
    }

    [Fact]
    public void ASlotHoldsItsCapacity_AndTheBackpackItsSize()
    {
        var state = Create("A.B");
        var artifacts = state.GetHero(A).Artifacts;

        Assert.Throws<InvalidOperationException>(() => artifacts.ReplaceWith(["rusted-blade", "centaurs-ax"], [], state.Artifacts));
        artifacts.ReplaceWith(["iron-band", "ring-of-pale-fire"], [], state.Artifacts); // two rings fit
        Assert.Throws<InvalidOperationException>(() => artifacts.ReplaceWith(["iron-band", "ring-of-pale-fire", "ring-of-the-wayfarer"], [], state.Artifacts));
        Assert.Throws<InvalidOperationException>(() => artifacts.ReplaceWith([], Enumerable.Repeat("rusted-blade", ArtifactSlots.BackpackSize + 1), state.Artifacts));
        Assert.Throws<KeyNotFoundException>(() => artifacts.ReplaceWith(["no-such-artifact"], [], state.Artifacts));
        Assert.Equal(2, artifacts.Worn(ArtifactSlot.Ring).Count); // the failed replaces left it alone
    }

    [Fact]
    public void TheSkillOfAnArtifact_IsCappedAtTheSkillsMaximum_AndAllSkillsOnlyRaiseLearnedOnes()
    {
        var state = Create("A.B");
        var hero = state.GetHero(A);
        hero.Skills.Set(BuiltInSkills.Armor, SkillLevel.Expert);
        Outfit(state, A, worn: ["gilded-hourglass"]);

        Assert.Equal(SkillLevel.Master, hero.SkillLevelOf(BuiltInSkills.Armor));
        Assert.Equal(SkillLevel.None, hero.SkillLevelOf(BuiltInSkills.Melee)); // not learned: all-skills does not start it
        hero.Skills.Set(BuiltInSkills.Archery, SkillLevel.Expert);
        Assert.Equal(SkillLevel.Expert, hero.SkillLevelOf(BuiltInSkills.Archery)); // Archery stops at Expert
    }

    // Effects in the rules ---------------------------------------------------------------------------------

    [Fact]
    public void ArtifactsReachBattle_MeleeRangedSpeedMoraleLuckAndDamageTaken()
    {
        var state = Create("A.B");
        Outfit(state, A, worn: ["wardens-edge", "boots-of-the-wheel-runner", "ring-of-reckoning", "plate-of-the-faithful"]);

        var bonuses = HeroBattleBonuses.For(state.GetHero(A), state.Creatures);

        Assert.Equal(10, bonuses.MeleePercent);
        Assert.Equal(1, bonuses.SpeedBonus);
        Assert.Equal(1, bonuses.Morale);
        Assert.Equal(2, bonuses.Luck);
        Assert.Equal(10, bonuses.RangedDamageTakenReductionPercent);
    }

    [Fact]
    public void MovementArtifacts_RaiseTheDailyMovementPoints()
    {
        var state = Create("A.B");
        var hero = state.GetHero(A);
        var plain = MovementAllowance.Daily(hero, state.Creatures);

        Outfit(state, A, worn: ["boots-of-speed", "cape-of-velocity"]);

        Assert.Equal((plain * 110 / 100) + 600, MovementAllowance.Daily(hero, state.Creatures));
    }

    [Fact]
    public void ANativeTerrainArtifact_RemovesTheTerrainPenalty()
    {
        var state = Create("AsB");
        var hero = state.GetHero(A);
        var from = new MapPosition(1, 0);
        var to = new MapPosition(2, 0);
        var swamp = MovementRules.StepCost(state, hero, from, to, Direction.East);
        Assert.True(swamp > TerrainCatalog.NativeCost);

        Outfit(state, A, worn: ["marsh-talisman"]);

        Assert.Equal(TerrainCatalog.NativeCost, MovementRules.StepCost(state, hero, from, to, Direction.East));
        Assert.Equal(TerrainCatalog.NativeCost + ((swamp - TerrainCatalog.NativeCost) * 75 / 100), TerrainCostWith(state, "weathered-cloak", from, to));

        static int TerrainCostWith(GameState s, string artifact, MapPosition f, MapPosition t)
        {
            Outfit(s, A, worn: [artifact]);
            return MovementRules.StepCost(s, s.GetHero(A), f, t, Direction.East);
        }
    }

    [Fact]
    public void IncomeArtifacts_PayEveryDay()
    {
        var state = Create("A.B");
        Outfit(state, A, worn: ["ledger-of-debts", "alchemists-satchel", "woodcutters-axe"]);
        var session = Start(state);

        var income = session.Economy.DailyIncome(Blue);

        Assert.Equal(300, income.Gold);
        Assert.Equal(1, income.Lumber);
        Assert.Equal((1, 1, 1), (income.Crystal, income.Mercury, income.Sulfur));
    }

    // Wearing, taking off, giving ----------------------------------------------------------------------------

    [Fact]
    public void Equipping_PutsTheArtifactOnAndASecondOneInTheSameSlotSwapsOut()
    {
        var state = Create("A.B");
        Outfit(state, A, backpack: ["rusted-blade", "centaurs-ax"]);
        var session = Start(state);
        var events = new List<IEvent>();
        session.Events.Subscribe<ArtifactEquipped>(events.Add);
        session.Events.Subscribe<ArtifactUnequipped>(events.Add);
        var hero = state.GetHero(A);

        Assert.True(session.Execute(new EquipArtifactCommand(Blue, A, "rusted-blade")).IsAccepted);
        Assert.Equal(["rusted-blade"], hero.Artifacts.Worn(ArtifactSlot.Weapon));
        Assert.Equal(hero.Attack + 1, hero.TotalAttack);

        Assert.True(session.Execute(new EquipArtifactCommand(Blue, A, "centaurs-ax")).IsAccepted);
        Assert.Equal(["centaurs-ax"], hero.Artifacts.Worn(ArtifactSlot.Weapon));
        Assert.Equal(["rusted-blade"], hero.Artifacts.Backpack);
        Assert.Equal(hero.Attack + 2, hero.TotalAttack);
        Assert.Equal(3, events.Count);
    }

    [Fact]
    public void Unequipping_NeedsRoomInTheBackpack_AndTheArtifactToBeWorn()
    {
        var state = Create("A.B");
        Outfit(state, A, worn: ["rusted-blade"], backpack: Enumerable.Repeat("iron-band", ArtifactSlots.BackpackSize).ToArray());
        var session = Start(state);

        Assert.False(session.Execute(new UnequipArtifactCommand(Blue, A, "rusted-blade")).IsAccepted);
        Assert.False(session.Execute(new UnequipArtifactCommand(Blue, A, "centaurs-ax")).IsAccepted);

        state.GetHero(A).Artifacts.RemoveFromBackpack("iron-band");
        Assert.True(session.Execute(new UnequipArtifactCommand(Blue, A, "rusted-blade")).IsAccepted);
        Assert.Equal(state.GetHero(A).Attack, state.GetHero(A).TotalAttack);
    }

    [Fact]
    public void YouCannotEquipWhatYouDoNotCarry_OrTouchAnotherPlayersHero()
    {
        var state = Create("A.B");
        Outfit(state, B, backpack: ["rusted-blade"]);
        var session = Start(state);

        Assert.False(session.Execute(new EquipArtifactCommand(Blue, A, "rusted-blade")).IsAccepted);
        Assert.False(session.Execute(new EquipArtifactCommand(Blue, B, "rusted-blade")).IsAccepted);
        Assert.False(session.Execute(new EquipArtifactCommand(Red, B, "rusted-blade")).IsAccepted); // not Red's turn
    }

    [Fact]
    public void Giving_WorksOnlyWhileTheExchangeIsOpen_AndTakesFromTheBackpackFirst()
    {
        var state = Create("AC.B");
        Outfit(state, A, worn: ["rusted-blade"], backpack: ["rusted-blade", "iron-band"]);
        var session = Start(state);
        var given = new List<ArtifactGiven>();
        session.Events.Subscribe<ArtifactGiven>(given.Add);

        Assert.False(session.Execute(new GiveArtifactCommand(Blue, A, "iron-band", C)).IsAccepted); // no exchange yet
        session.Step(A, Direction.East); // meets C
        Assert.IsType<EternalWars.Core.Domain.Decisions.HeroExchangeDecision>(state.PendingDecision);

        Assert.True(session.Execute(new GiveArtifactCommand(Blue, A, "rusted-blade", C)).IsAccepted);
        Assert.Equal(["iron-band"], state.GetHero(A).Artifacts.Backpack); // the carried copy went, the worn one stayed
        Assert.Equal(["rusted-blade"], state.GetHero(A).Artifacts.Worn(ArtifactSlot.Weapon));
        Assert.Equal(["rusted-blade"], state.GetHero(C).Artifacts.Backpack);

        Assert.True(session.Execute(new GiveArtifactCommand(Blue, A, "rusted-blade", C)).IsAccepted); // now the worn one
        Assert.Equal(state.GetHero(A).Attack, state.GetHero(A).TotalAttack);
        Assert.Equal(2, given.Count);
    }

    // The map -------------------------------------------------------------------------------------------------

    [Fact]
    public void SteppingOnAPickup_PutsItInTheBackpack()
    {
        var state = Create("A.B");
        Add<ArtifactPickup>(state, BuiltInMapObjects.ArtifactId(ArtifactTier.Minor), 1, 0).Artifact = "rusted-blade";
        var session = Start(state);
        var gained = new List<ArtifactGained>();
        session.Events.Subscribe<ArtifactGained>(gained.Add);

        session.MoveTo(A, new MapPosition(1, 0));

        Assert.Equal(["rusted-blade"], state.GetHero(A).Artifacts.Backpack);
        Assert.DoesNotContain(state.Objects, o => o is ArtifactPickup);
        Assert.Equal([new ArtifactGained(A, "rusted-blade", ArtifactSource.Pickup)], gained);
    }

    [Fact]
    public void AFullBackpack_LeavesThePickupWhereItIs()
    {
        var state = Create("A.B");
        Add<ArtifactPickup>(state, BuiltInMapObjects.ArtifactId(ArtifactTier.Minor), 1, 0).Artifact = "rusted-blade";
        Outfit(state, A, backpack: Enumerable.Repeat("iron-band", ArtifactSlots.BackpackSize).ToArray());
        var session = Start(state);
        var full = new List<ArtifactBackpackFull>();
        session.Events.Subscribe<ArtifactBackpackFull>(full.Add);

        session.MoveTo(A, new MapPosition(1, 0));

        Assert.Contains(state.Objects, o => o is ArtifactPickup);
        Assert.Single(full);
    }

    [Fact]
    public void OpenPickups_RollAtGameStart_FromTheirTier_WithoutRepeats()
    {
        var state = Create("A..........B");
        for (var i = 0; i < 6; i++)
        {
            Add<ArtifactPickup>(state, BuiltInMapObjects.ArtifactId(ArtifactTier.Relic), i + 1, 0);
        }

        Start(state);

        var rolled = state.ObjectsOf<ArtifactPickup>().Select(p => p.Artifact).ToList();
        Assert.All(rolled, id => Assert.Equal(ArtifactTier.Relic, state.Artifacts.Get(id!).Tier));
        Assert.Equal(6, rolled.Distinct().Count());
    }

    [Fact]
    public void RollingIsDeterministic_AndSkipsWhatHeroesCarry()
    {
        static List<string?> Roll(string[]? carried = null)
        {
            var state = Create("A..........B", "............");
            Add<ArtifactPickup>(state, BuiltInMapObjects.ArtifactId(ArtifactTier.Minor), 3, 0);
            Add<ArtifactPickup>(state, BuiltInMapObjects.ArtifactId(ArtifactTier.Major), 5, 0);
            if (carried is not null)
            {
                Outfit(state, A, backpack: carried);
            }

            Start(state);
            return state.ObjectsOf<ArtifactPickup>().Select(p => p.Artifact).ToList();
        }

        Assert.Equal(Roll(), Roll());
        var all = ArtifactCatalog.Default.OfTier(ArtifactTier.Minor).Select(a => a.Id).Take(12).ToArray();
        Assert.DoesNotContain(Roll(all)[0], all);
    }

    [Fact]
    public void ADefeatedHeros_ArtifactsGoToTheWinner()
    {
        var state = Create("A.B");
        state.GetHero(A).Army.Set(0, new CreatureStack(BuiltInCreatures.Champion, 10));
        Outfit(state, B, worn: ["centaurs-ax"], backpack: ["iron-band"]);
        var session = Start(state);

        session.MoveTo(A, new MapPosition(2, 0));
        session.AutoResolve();

        Assert.Equal(["centaurs-ax", "iron-band"], state.GetHero(A).Artifacts.Backpack.Order());
    }

    [Fact]
    public void ALootThatDoesNotFit_KeepsTheMostValuableFirst()
    {
        var state = Create("A.B");
        state.GetHero(A).Army.Set(0, new CreatureStack(BuiltInCreatures.Champion, 10));
        Outfit(state, A, backpack: Enumerable.Repeat("iron-band", ArtifactSlots.BackpackSize - 2).ToArray()); // room for two
        Outfit(state, B, worn: ["squires-shield", "ogres-club-of-havoc"], backpack: ["iron-band", "endless-sack-of-gold"]);
        var session = Start(state);
        var lost = new List<ArtifactLost>();
        session.Events.Subscribe<ArtifactLost>(lost.Add);

        session.MoveTo(A, new MapPosition(2, 0));
        session.AutoResolve();

        var carried = state.GetHero(A).Artifacts.Backpack;
        Assert.Equal(ArtifactSlots.BackpackSize, carried.Count);
        Assert.Contains("endless-sack-of-gold", carried);
        Assert.Contains("ogres-club-of-havoc", carried);
        Assert.Equal(["iron-band", "squires-shield"], lost.Select(l => l.Artifact).Order());
    }

    [Fact]
    public void TheBackpack_Holds50_AndValueOrdersByEffectsThenTier()
    {
        var catalog = ArtifactCatalog.Default;

        Assert.Equal(50, ArtifactSlots.BackpackSize);
        Assert.Equal(
            ["endless-sack-of-gold", "titans-gladius", "ogres-club-of-havoc", "rusted-blade"],
            ArtifactValue.BestFirst(catalog, ["rusted-blade", "ogres-club-of-havoc", "endless-sack-of-gold", "titans-gladius"]));
        Assert.Equal(["no-such-artifact"], ArtifactValue.BestFirst(catalog, ["no-such-artifact"]));
        Assert.Equal("no-such-artifact", ArtifactValue.BestFirst(catalog, ["no-such-artifact", "iron-band"]).Last());
    }

    [Fact]
    public void ALootThatDoesNotFit_IsLost()
    {
        var state = Create("A.B");
        state.GetHero(A).Army.Set(0, new CreatureStack(BuiltInCreatures.Champion, 10));
        Outfit(state, A, backpack: Enumerable.Repeat("iron-band", ArtifactSlots.BackpackSize).ToArray());
        Outfit(state, B, backpack: ["rusted-blade"]);
        var session = Start(state);
        var lost = new List<ArtifactLost>();
        session.Events.Subscribe<ArtifactLost>(lost.Add);

        session.MoveTo(A, new MapPosition(2, 0));
        session.AutoResolve();

        Assert.Equal([new ArtifactLost(A, "rusted-blade")], lost);
    }

    // Map data and the editor -----------------------------------------------------------------------------------

    [Fact]
    public void HeroesAndPickups_RoundTripThroughTheMapFile_AndOlderMapsLoad()
    {
        var state = Create("A.B");
        Outfit(state, A, worn: ["rusted-blade", "iron-band"], backpack: ["scholars-cap"]);
        Add<ArtifactPickup>(state, BuiltInMapObjects.ArtifactId(ArtifactTier.Major), 1, 0).Artifact = "hunters-mark";
        Add<ArtifactPickup>(state, BuiltInMapObjects.ArtifactId(ArtifactTier.Relic), 2, 0);

        var json = MapSerializer.Serialize(MapConverter.ToDocument(state));
        var loaded = MapConverter.ToGameState(MapSerializer.Deserialize(json));

        Assert.Equal(["rusted-blade", "iron-band"], loaded.GetHero(A).Artifacts.AllWorn.Select(w => w.Id).Order(StringComparer.Ordinal).Reverse());
        Assert.Equal(["scholars-cap"], loaded.GetHero(A).Artifacts.Backpack);
        Assert.Equal(loaded.GetHero(A).Attack + 1, loaded.GetHero(A).TotalAttack);
        Assert.Equal(["hunters-mark", null], loaded.ObjectsOf<ArtifactPickup>().OrderBy(p => p.Id.Value).Select(p => p.Artifact));

        var plain = MapConverter.ToDocument(Create("A.B"));
        Assert.Null(plain.Heroes[0].Artifacts);
        Assert.NotNull(MapConverter.ToGameState(plain with { FormatVersion = 10 }));
    }

    [Fact]
    public void ADamagedMap_WithAnUnknownOrOverfullArtifact_IsRefused()
    {
        var document = MapConverter.ToDocument(Create("A.B"));
        var heroes = document.Heroes.ToList();

        heroes[0] = heroes[0] with { Artifacts = ["no-such-artifact"] };
        Assert.Throws<FormatException>(() => MapConverter.ToGameState(document with { Heroes = heroes }));

        heroes[0] = heroes[0] with { Artifacts = ["rusted-blade", "centaurs-ax"] };
        Assert.Throws<FormatException>(() => MapConverter.ToGameState(document with { Heroes = heroes }));
    }

    [Fact]
    public void TheEditor_SetsHeroArtifacts_AndPickups_Undoably()
    {
        var scenario = Create("A.B");
        var pickup = Add<ArtifactPickup>(scenario, BuiltInMapObjects.ArtifactId(ArtifactTier.Minor), 1, 0);
        var editor = new MapEditorSession(scenario);

        Assert.Equal(EditResult.Ok, editor.SetHeroArtifacts(A, ["rusted-blade"], ["iron-band"]));
        Assert.Equal(scenario.GetHero(A).Attack + 1, scenario.GetHero(A).TotalAttack);
        Assert.Equal(EditResult.InvalidValue, editor.SetHeroArtifacts(A, ["rusted-blade", "centaurs-ax"], []));
        Assert.Equal(EditResult.InvalidValue, editor.SetHeroArtifacts(A, ["nope"], []));
        Assert.Equal(EditResult.InvalidTarget, editor.SetHeroArtifacts(new HeroId(99), [], []));

        Assert.Equal(EditResult.Ok, editor.SetArtifactPickup(pickup.Id, "iron-band"));
        Assert.Equal(EditResult.InvalidValue, editor.SetArtifactPickup(pickup.Id, "nope"));
        Assert.Equal(EditResult.InvalidTarget, editor.SetArtifactPickup(new MapObjectId(99), "iron-band"));
        Assert.Equal("iron-band", pickup.Artifact);

        Assert.True(editor.Undo());
        Assert.Null(pickup.Artifact);
        Assert.True(editor.Undo());
        Assert.True(scenario.GetHero(A).Artifacts.IsEmpty);
        Assert.Equal(scenario.GetHero(A).Attack, scenario.GetHero(A).TotalAttack);
    }

    [Fact]
    public void TheValidator_FlagsUnknownArtifacts()
    {
        var state = Create("A.B");
        state.GetHero(A).Artifacts.AddToBackpack("no-such-artifact");
        Add<ArtifactPickup>(state, BuiltInMapObjects.ArtifactId(ArtifactTier.Minor), 1, 0).Artifact = "also-missing";

        var errors = MapValidator.Validate(state).Where(i => i.Severity == IssueSeverity.Error).Select(i => i.Message).ToList();

        Assert.Contains(errors, e => e.Contains("no-such-artifact", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Contains("also-missing", StringComparison.Ordinal));
    }

    [Fact]
    public void AGameWithArtifacts_ReplaysFromItsCommandLog()
    {
        var state = Create("A.B");
        Add<ArtifactPickup>(state, BuiltInMapObjects.ArtifactId(ArtifactTier.Minor), 1, 0);
        var document = MapConverter.ToDocument(state);
        var session = Start(state, revealAll: false);
        session.MoveTo(A, new MapPosition(1, 0));
        var found = Assert.Single(state.GetHero(A).Artifacts.Backpack);
        session.Execute(new EquipArtifactCommand(Blue, A, found)).ThrowIfRejected();

        var json = GameRecordSerializer.Serialize(GameReplay.Record(document, session));
        var replayed = GameReplay.Replay(GameRecordSerializer.Deserialize(json));

        Assert.Equal(GameStateDigest.Compute(session.State), GameStateDigest.Compute(replayed.State));
        Assert.Equal(found, replayed.State.GetHero(A).Artifacts.AllWorn.Single().Id);
    }

    // The AI ---------------------------------------------------------------------------------------------------

    [Fact]
    public void TheAi_PicksUpArtifacts_AndPutsOnTheBetterOnes()
    {
        var state = Create("A.......B");
        Add<ArtifactPickup>(state, BuiltInMapObjects.ArtifactId(ArtifactTier.Major), 3, 0).Artifact = "ogres-club-of-havoc";
        Outfit(state, A, worn: ["rusted-blade"], backpack: ["centaurs-ax"]);
        var session = Start(state);
        var ai = new AdventureAi(session, controlHumanSlots: true);

        Assert.True(ai.PlayTurn());

        var weapon = Assert.Single(state.GetHero(A).Artifacts.Worn(ArtifactSlot.Weapon));
        Assert.Equal("ogres-club-of-havoc", weapon); // the +5 club beat the +2 ax and the +1 blade
        Assert.Equal(0, ai.Refused);
    }

    [Fact]
    public void TheAi_KeepsAnEqualArtifact_InsteadOfSwappingBackAndForth()
    {
        var state = Create("A.B");
        Outfit(state, A, worn: ["centaurs-ax"], backpack: ["centaurs-ax"]);
        var session = Start(state);
        var ai = new AdventureAi(session, controlHumanSlots: true);

        ai.PlayTurn();

        Assert.DoesNotContain(session.CommandLog, c => c is EquipArtifactCommand);
    }

    [Fact]
    public void ScoresFollowTheEffects()
    {
        var catalog = ArtifactCatalog.Default;

        Assert.True(AdventureAi.Score(catalog.Get("ogres-club-of-havoc")) > AdventureAi.Score(catalog.Get("centaurs-ax")));
        Assert.True(AdventureAi.Score(catalog.Get("endless-sack-of-gold")) > AdventureAi.Score(catalog.Get("hearth-charm")));
        Assert.True(AdventureAi.Score(catalog.Get("titans-gladius")) > 0); // +12 attack outweighs −3 defense
    }

    [Fact]
    public void TwoAisPlayingWithArtifactsOnTheMap_NeverIssueARefusedCommand()
    {
        var state = TownsScenario.Create();
        foreach (var tier in Enum.GetValues<ArtifactTier>())
        {
            Add<ArtifactPickup>(state, BuiltInMapObjects.ArtifactId(tier), 12 + (int)tier, 8);
        }

        var session = new GameSession(state);
        var ai = new AdventureAi(session, controlHumanSlots: true);
        session.Start();
        while (!state.IsOver && state.Date.AbsoluteDay <= 25)
        {
            Assert.True(ai.PlayTurn());
        }

        Assert.Equal(0, ai.Refused);
    }
}
