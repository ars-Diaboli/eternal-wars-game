using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Magic;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>What a player is shown of the armies it does not own, by its heroes' Scouting (ADR-125).</summary>
public class ScoutingIntelTests
{
    // Blue's A at (0, 1) sees 5 cells; Red's B at (4, 1) is in its sight, Blue's C at (29, 1) far away.
    private static GameState Strip() => Create(new string('.', 30), "A...B" + new string('.', 24) + "C", new string('.', 30));

    private static Hero Scout(GameState state, SkillLevel level, HeroId? id = null)
    {
        var hero = state.GetHero(id ?? A);
        hero.Skills.Set(BuiltInSkills.Scouting, level);
        return hero;
    }

    private static void Veil(Hero hero, int rank) => hero.AddStatus(new HeroStatus(AdventureMagic.Veil, HeroStatusKind.Veil, rank, StatusExpiry.EndOfWeek));

    [Theory]
    [InlineData(1, "a few (1–4)", 2)]
    [InlineData(4, "a few (1–4)", 2)]
    [InlineData(10, "a pack (10–19)", 14)]
    [InlineData(99, "a horde (50–99)", 74)]
    [InlineData(250, "a swarm (250–499)", 374)]
    [InlineData(5000, "a legion (1000+)", 1000)]
    public void AHiddenCount_ShowsItsWordWithItsRange_AndIsTakenForTheMiddle(int count, string amount, int estimate)
    {
        var shown = AdventureIntel.Shown(BuiltInCreatures.Footman, count, hidden: true);

        Assert.Null(shown.Count);
        Assert.Equal(amount, shown.Amount);
        Assert.Equal(estimate, shown.Estimate);
        Assert.Equal(count, AdventureIntel.Shown(BuiltInCreatures.Footman, count, hidden: false).Estimate);
    }

    [Fact]
    public void WithoutScouting_AnEnemyHeroShowsOnlyAmounts_NoLevelNoArtifacts()
    {
        var state = Strip();

        var shown = AdventureIntel.ScoutHero(state, state.GetHero(B), Blue);

        Assert.Null(shown.Stacks.Single().Count);
        Assert.Equal("a pack (10–19)", shown.Stacks.Single().Amount);
        Assert.Null(shown.Details);
        Assert.True(shown.ArtifactsHidden);
        Assert.False(shown.Veiled);
        Assert.Equal(10, AdventureIntel.ScoutHero(state, state.GetHero(B), Red).Stacks.Single().Count); // its owner sees it all
    }

    [Fact]
    public void EachScoutingLevel_ShowsMoreOfAnEnemyHero()
    {
        var state = Strip();
        var enemy = state.GetHero(B);
        enemy.Learn("chaos.firebolt");

        Scout(state, SkillLevel.Basic);
        var basic = AdventureIntel.ScoutHero(state, enemy, Blue);
        Assert.Null(basic.Stacks.Single().Count);
        Assert.Equal(enemy.Level, basic.Details!.Level);
        Assert.Equal(enemy.TotalAttack, basic.Details.Attack);
        Assert.Null(basic.Details.WarMachines);
        Assert.True(basic.ArtifactsHidden);

        Scout(state, SkillLevel.Advanced);
        var advanced = AdventureIntel.ScoutHero(state, enemy, Blue);
        Assert.Equal(10, advanced.Stacks.Single().Count);
        Assert.False(advanced.ArtifactsHidden);
        Assert.NotNull(advanced.Details!.WarMachines);
        Assert.Null(advanced.Details.Skills);

        Scout(state, SkillLevel.Expert);
        var expert = AdventureIntel.ScoutHero(state, enemy, Blue);
        Assert.Equal(["chaos.firebolt"], expert.Details!.Spells!);
        Assert.NotNull(expert.Details.Skills);
    }

    [Fact]
    public void OnlyTheHeroesThatSeeTheArmy_Count()
    {
        var state = Strip();
        Scout(state, SkillLevel.Expert, C); // far from B

        Assert.Equal(SkillLevel.None, AdventureIntel.ScoutingAt(state, Blue, state.GetHero(B).Position));
        Assert.Null(AdventureIntel.ScoutHero(state, state.GetHero(B), Blue).Stacks.Single().Count);

        Scout(state, SkillLevel.Basic);
        Assert.Equal(SkillLevel.Basic, AdventureIntel.ScoutingAt(state, Blue, state.GetHero(B).Position));
    }

    [Fact]
    public void Veil_HidesCountsFromScoutingBelowExpert_AndArtifactsFromEveryoneAtRankII()
    {
        var state = Strip();
        var enemy = state.GetHero(B);
        Veil(enemy, 1);

        Scout(state, SkillLevel.Advanced);
        var advanced = AdventureIntel.ScoutHero(state, enemy, Blue);
        Assert.Null(advanced.Stacks.Single().Count);
        Assert.True(advanced.Veiled);
        Assert.False(advanced.ArtifactsHidden);

        Scout(state, SkillLevel.Expert);
        Assert.Equal(10, AdventureIntel.ScoutHero(state, enemy, Blue).Stacks.Single().Count);

        Veil(enemy, 2);
        var expert = AdventureIntel.ScoutHero(state, enemy, Blue);
        Assert.Equal(10, expert.Stacks.Single().Count);
        Assert.True(expert.ArtifactsHidden);
    }

    [Fact]
    public void NeutralStacksAndGuards_ShowTheirCountsFromBasic_OtherPlayersGuardsFromAdvanced()
    {
        var state = Strip();
        var monster = Add<MonsterGuard>(state, BuiltInMapObjects.Monster, 3, 0);
        (monster.Creature, monster.Count) = (BuiltInCreatures.Wolf, 30);
        CreatureStack[] guard = [new(BuiltInCreatures.Wolf, 30)];

        Assert.Equal("lots (20–49)", AdventureIntel.ScoutMonster(state, monster, Blue).Stacks.Single().Amount);
        Assert.Null(AdventureIntel.ScoutMonster(state, monster, Blue).Stacks.Single().Count);

        Scout(state, SkillLevel.Basic);
        Assert.Equal(30, AdventureIntel.ScoutMonster(state, monster, Blue).Stacks.Single().Count);
        Assert.Equal(30, AdventureIntel.ScoutStacks(state, guard, monster.Position, null, Blue).Single().Count);
        Assert.Null(AdventureIntel.ScoutStacks(state, guard, monster.Position, Red, Blue).Single().Count);
        Assert.Equal(30, AdventureIntel.ScoutStacks(state, guard, monster.Position, Blue, Blue).Single().Count);
    }

    [Fact]
    public void ATown_ShowsItsGarrisonFromAdvanced_AndWhatWaitsInItsDwellingsFromExpert()
    {
        var state = Create(new string('.', 20), new string('.', 20), new string('.', 20), new string('.', 20), new string('.', 20),
            new string('.', 20), new string('.', 20), new string('.', 20), "A..................B");
        var town = AddTown(state, TownIds.Haven, 4, 0, Red); // entrance (7, 6)
        town.Garrison.Set(0, new CreatureStack(BuiltInCreatures.Footman, 60));
        var line = state.Creatures.BaseCreaturesOf(TownIds.Haven).First();
        town.SetAvailable(line.Id, 12);
        state.GetHero(A).Position = new MapPosition(5, 8);

        var none = AdventureIntel.ScoutTown(state, town, Blue);
        Assert.Equal("a horde (50–99)", none.Garrison.Single().Amount);
        Assert.Null(none.Garrison.Single().Count);
        Assert.Null(none.Available);
        Assert.Null(none.Buildings);

        Scout(state, SkillLevel.Advanced);
        var advanced = AdventureIntel.ScoutTown(state, town, Blue);
        Assert.Equal(60, advanced.Garrison.Single().Count);
        Assert.Null(advanced.Available);

        Scout(state, SkillLevel.Expert);
        Assert.Equal(12, AdventureIntel.ScoutTown(state, town, Blue).Available!.Single().Count);
        Assert.Equal(12, AdventureIntel.ScoutTown(state, town, Red).Available!.Single().Count); // its owner sees it all
    }

    [Fact]
    public void TheAi_ReadsHiddenCountsAsTheMiddleOfTheirRange()
    {
        var army = AdventureIntel.Reckoned([AdventureIntel.Shown(BuiltInCreatures.Wolf, 21, hidden: true), AdventureIntel.Shown(BuiltInCreatures.Footman, 7, hidden: false)]);

        Assert.Equal([(BuiltInCreatures.Wolf, 34), (BuiltInCreatures.Footman, 7)], army.Stacks.Select(s => (s.Stack.Creature, s.Stack.Count)));
    }
}
