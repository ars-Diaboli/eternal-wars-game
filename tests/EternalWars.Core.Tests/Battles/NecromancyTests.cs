using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Battles;

/// <summary>Necromancy and the Necromancy Amplifier (ADR-069, ADR-070, ADR-072, MAG-7).</summary>
public class NecromancyTests
{
    private const string Skeleton = "necropolis.skeleton";
    private const string PlagueBearer = "necropolis.plague-bearer";

    [Theory]
    [InlineData(SkillLevel.None, false, 0)]
    [InlineData(SkillLevel.None, true, 0)]
    [InlineData(SkillLevel.Basic, false, 5)]
    [InlineData(SkillLevel.Master, false, 20)]
    [InlineData(SkillLevel.Basic, true, 10)]
    [InlineData(SkillLevel.Master, true, 25)]
    public void Percent_IsFivePerLevel_PlusFiveWithTheAmplifier(SkillLevel level, bool amplified, int percent) =>
        Assert.Equal(percent, NecromancyRules.Percent(level, amplified));

    [Theory]
    [InlineData(SkillLevel.Basic, 5, 1)]
    [InlineData(SkillLevel.Advanced, 3, 1)]
    [InlineData(SkillLevel.Advanced, 4, 2)]
    [InlineData(SkillLevel.Expert, 2, 1)]
    [InlineData(SkillLevel.Expert, 4, 2)]
    [InlineData(SkillLevel.Expert, 5, 3)]
    [InlineData(SkillLevel.Master, 2, 1)]
    [InlineData(SkillLevel.Master, 3, 2)]
    [InlineData(SkillLevel.Master, 5, 4)]
    public void TheLevel_CapsTheUndeadTier(SkillLevel level, int enemyTier, int undeadTier) =>
        Assert.Equal(undeadTier, NecromancyRules.UndeadTier(level, enemyTier));

    [Fact]
    public void ConstructsAndElementals_LeaveNothingToRaise_UndeadDo()
    {
        var creatures = CreatureCatalog.Default;

        Assert.False(NecromancyRules.Counts(creatures.Get("academy.iron-golem")));
        Assert.False(NecromancyRules.Counts(creatures.Get("academy.djinn")));
        Assert.True(NecromancyRules.Counts(creatures.Get(Skeleton)));
        Assert.True(NecromancyRules.Counts(creatures.Get(BuiltInCreatures.Footman)));
        Assert.Empty(Raise((creatures.Get("academy.iron-golem"), 1000)));
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    public void Raise_SplitsTheTiersHitPointsBetweenItsTwoLines(ulong seed)
    {
        var footman = CreatureCatalog.Default.Get(BuiltInCreatures.Footman); // tier 1, 13 hit points
        var raised = Raise(SkillLevel.Master, seed, (footman, 100)); // 1300 × 20 % = 260

        Assert.All(raised, r => Assert.Contains(r.Creature, new[] { Skeleton, PlagueBearer }));
        var health = raised.Sum(r => r.Count * CreatureCatalog.Default.Get(r.Creature).Health);
        Assert.InRange(health, 260 - 11 - 20 + 2, 260); // each part rounded down
    }

    [Fact]
    public void Raise_GivesHigherTiersForStrongerEnemies_AtHigherLevels()
    {
        var seraph = CreatureCatalog.Default.Get(BuiltInCreatures.Seraph); // tier 5

        var basic = Raise(SkillLevel.Basic, 1, (seraph, 10));
        var master = Raise(SkillLevel.Master, 1, (seraph, 10));

        Assert.All(basic, r => Assert.Equal(1, CreatureCatalog.Default.Get(r.Creature).Tier));
        Assert.All(master, r => Assert.Equal(4, CreatureCatalog.Default.Get(r.Creature).Tier));
        Assert.NotEmpty(master);
    }

    [Fact]
    public void AVictory_RaisesUndeadIntoTheArmy_AfterTheBattleEnds_AndBeforeTheExperience()
    {
        var (state, session) = Duel(SkillLevel.Basic);
        var events = new List<IEvent>();
        session.Events.Subscribe<IEvent>(events.Add);

        session.MoveTo(A, new MapPosition(2, 0));
        session.AutoResolve();

        var raised = Assert.Single(events.OfType<UndeadRaised>());
        Assert.Equal(A, raised.Hero);
        Assert.NotEmpty(raised.Raised);
        var army = state.GetHero(A).Army;
        Assert.All(raised.Raised, r => Assert.Equal(r.Count, army.Stacks.Single(s => s.Stack.Creature == r.Creature).Stack.Count));
        Assert.True(events.FindIndex(e => e is BattleEnded) < events.IndexOf(raised));
        Assert.True(events.IndexOf(raised) < events.FindIndex(e => e is HeroExperienceGained));
    }

    [Fact]
    public void WithoutTheSkill_NothingIsRaised()
    {
        var (_, session) = Duel(SkillLevel.None);
        var raised = new List<UndeadRaised>();
        session.Events.Subscribe<UndeadRaised>(raised.Add);

        session.MoveTo(A, new MapPosition(2, 0));
        session.AutoResolve();

        Assert.Empty(raised);
    }

    [Fact]
    public void AnEnemyThatRetreats_LeavesNothingToRaise()
    {
        var state = Create("A.B", "...");
        state.GetHero(B).Skills.Set(NecromancyRules.Skill, SkillLevel.Master);
        var session = Start(state);
        var raised = new List<UndeadRaised>();
        session.Events.Subscribe<UndeadRaised>(raised.Add);
        session.MoveTo(A, new MapPosition(2, 0));
        state.Battle!.Units.First(u => u.Side == BattleSide.Attacker).Count = 1; // nine footmen fell before the retreat

        session.Execute(new BattleRetreatCommand(Blue)).ThrowIfRejected();

        Assert.Empty(raised);
    }

    [Fact]
    public void AFullArmy_GivesThreeQuartersToAStackOfTheUpgrade_OrLosesThem()
    {
        var (state, session) = Duel(SkillLevel.Master);
        var army = state.GetHero(A).Army;
        army.Set(1, new CreatureStack("necropolis.skeleton-warrior", 50));
        army.Set(2, new CreatureStack(BuiltInCreatures.Guardian, 50));
        army.Set(3, new CreatureStack(BuiltInCreatures.Inquisitor, 50));
        army.Set(4, new CreatureStack(BuiltInCreatures.Priest, 50));
        army.Set(5, new CreatureStack(BuiltInCreatures.Marksman, 50));
        army.Set(6, new CreatureStack(BuiltInCreatures.Champion, 50));
        var raised = new List<UndeadRaised>();
        session.Events.Subscribe<UndeadRaised>(raised.Add);

        session.MoveTo(A, new MapPosition(2, 0));
        session.AutoResolve();

        // Skeletons go to the Skeleton Warriors (3/4); Plague Bearers have no Flesh Golems to join and are lost.
        Assert.All(raised.SelectMany(r => r.Raised), r => Assert.Equal("necropolis.skeleton-warrior", r.Creature));
        Assert.DoesNotContain(army.Stacks, s => s.Stack.Creature is Skeleton or PlagueBearer);
    }

    [Fact]
    public void TheAmplifier_HelpsEveryHeroOfItsOwner()
    {
        var state = Create(Enumerable.Repeat("............", 12).ToArray());
        Assert.False(NecromancyRules.IsAmplified(state, Blue));

        var own = AddTown(state, TownIds.Necropolis, 0, 0, Blue);
        Assert.False(NecromancyRules.IsAmplified(state, Blue));
        own.SetLevel(NecromancyRules.Amplifier, 1);
        Assert.True(NecromancyRules.IsAmplified(state, Blue));
        Assert.False(NecromancyRules.IsAmplified(state, Red));
    }

    [Fact]
    public void TheAmplifier_IsANecropolisBuilding()
    {
        var catalog = BuildingCatalog.Default;
        var amplifier = catalog.For(TownIds.Necropolis).Get(NecromancyRules.Amplifier, 1);

        Assert.Equal(("Necromancy Amplifier", 1500, 5), (amplifier.Name, amplifier.Cost.Gold, amplifier.Cost.Mercury));
        Assert.False(catalog.For(TownIds.Haven).Contains(NecromancyRules.Amplifier));
    }

    [Fact]
    public void ComputerPlayers_TakeNecromancyWheneverItIsOffered()
    {
        Assert.Equal(NecromancyRules.Skill, SkillOffers.ComputerChoice([BuiltInSkills.Melee, NecromancyRules.Skill, BuiltInSkills.Armor]));
        Assert.Equal(BuiltInSkills.Melee, SkillOffers.ComputerChoice([BuiltInSkills.Melee, BuiltInSkills.Armor]));
        Assert.Null(SkillOffers.ComputerChoice([]));
    }

    private static (GameState State, GameSession Session) Duel(SkillLevel necromancy)
    {
        var state = Create("A.B", "...");
        state.GetHero(A).Army.Set(0, new CreatureStack(BuiltInCreatures.Seraph, 30));
        state.GetHero(A).Skills.Set(NecromancyRules.Skill, necromancy);
        state.GetHero(B).Army.Set(0, new CreatureStack(BuiltInCreatures.Footman, 100));
        return (state, Start(state));
    }

    private static IReadOnlyList<RaisedUndead> Raise(params (CreatureDefinition Creature, int Slain)[] slain) => Raise(SkillLevel.Master, 1, slain);

    private static IReadOnlyList<RaisedUndead> Raise(SkillLevel level, ulong seed, params (CreatureDefinition Creature, int Slain)[] slain) =>
        NecromancyRules.Raise(slain, level, amplified: false, CreatureCatalog.Default, new GameRandom(seed));
}
