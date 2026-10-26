using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Towns;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The skill rework (ADR-069, ADR-072): renamed ids, Scouting, Tactics widths, Primal Fury for two towns and their mages' stats.</summary>
public class SkillReworkTests
{
    [Fact]
    public void OldIds_ResolveToTheirSuccessors()
    {
        Assert.All(BuiltInSkills.Renamed, pair =>
        {
            Assert.True(BuiltInSkills.Contains(pair.Key));
            Assert.Equal(pair.Value, BuiltInSkills.Get(pair.Key).Id);
        });
        Assert.Equal(BuiltInSkills.WarTempo, BuiltInSkills.Resolve("tactics.drill"));
        Assert.Equal(BuiltInSkills.Scouting, BuiltInSkills.Resolve("pathfinding.troops"));
        Assert.Equal(BuiltInSkills.BloodRage, BuiltInSkills.Resolve("adrenaline.first"));
        Assert.Equal(BuiltInSkills.Melee, BuiltInSkills.Resolve(BuiltInSkills.Melee));
    }

    [Fact]
    public void Migrate_RenamesOldIds_AndCapsLevelsAtTheNewMaximum()
    {
        var migrated = BuiltInSkills.Migrate(new Dictionary<string, SkillLevel>
        {
            ["tactics.deployment"] = SkillLevel.Master, // Tactics goes up to Expert
            ["offense.third"] = SkillLevel.Advanced,
            ["adrenaline.second"] = SkillLevel.Basic,
            [BuiltInSkills.Melee] = SkillLevel.Basic,
        });

        Assert.Equal(
            new Dictionary<string, SkillLevel>
            {
                [BuiltInSkills.Tactics] = SkillLevel.Expert,
                [BuiltInSkills.Leadership] = SkillLevel.Advanced,
                [BuiltInSkills.Warcry] = SkillLevel.Basic,
                [BuiltInSkills.Melee] = SkillLevel.Basic,
            },
            migrated);
    }

    [Fact]
    public void HeroTable_ReadsOldIds_AsTheNewSkills()
    {
        const string header = "id,name,town,class,preferred,specialty,skills\n";
        var hero = Assert.Single(HeroTable.Parse(header + "x.y,Y,swamp-bestiary,Mage,swamp-bestiary.kappa,skill adrenaline.second,adrenaline.first Basic; tactics.drill Master"));

        Assert.Equal(BuiltInSkills.Warcry, hero.Specialty.Target);
        Assert.Equal(new Dictionary<string, SkillLevel> { [BuiltInSkills.BloodRage] = SkillLevel.Basic, [BuiltInSkills.WarTempo] = SkillLevel.Master }, hero.StartingSkills);
    }

    [Fact]
    public void Roster_UsesOnlyCurrentIds_AndPrimalFuryWarriorsStartWithOffenseOrDefense()
    {
        var roster = HeroCatalog.Default.All.ToList();

        Assert.All(roster.SelectMany(h => h.StartingSkills.Keys), id => Assert.DoesNotContain(id, BuiltInSkills.Renamed.Keys));
        Assert.All(roster.Where(h => h.Town == TownIds.Stronghold && h.Class == HeroClass.Warrior),
            h => Assert.Contains(h.StartingSkills.Keys, id => BuiltInSkills.Get(id).Group == "offense"));
        Assert.All(roster.Where(h => h.Town == TownIds.SwampBestiary && h.Class == HeroClass.Warrior),
            h => Assert.Contains(h.StartingSkills.Keys, id => BuiltInSkills.Get(id).Group == "defense"));
    }

    [Fact]
    public void AMapWithOldSkillIds_StillLoads()
    {
        var json = """
            { "formatVersion": 21, "name": "Old", "width": 2, "height": 1, "terrain": [["gg"]],
              "players": [{ "name": "Blue", "isHuman": true }],
              "heroes": [{ "id": 1, "name": "Old", "owner": 0, "x": 0, "y": 0,
                           "skills": { "pathfinding.troops": "Advanced", "defense.third": "Basic" } }] }
            """;

        var hero = MapConverter.ToGameState(MapSerializer.Deserialize(json)).GetHero(A);

        Assert.Equal(SkillLevel.Advanced, hero.Skills.Level(BuiltInSkills.Scouting));
        Assert.Equal(SkillLevel.Basic, hero.Skills.Level(BuiltInSkills.Alertness));
        Assert.Equal(SkillLevel.Advanced, hero.Skills.Level("pathfinding.troops")); // the old id reads the new skill
    }

    [Theory]
    [InlineData(SkillLevel.None, 0)]
    [InlineData(SkillLevel.Basic, 2)]
    [InlineData(SkillLevel.Advanced, 4)]
    [InlineData(SkillLevel.Expert, 6)]
    public void Scouting_WidensTheSightRadius(SkillLevel level, int extra)
    {
        var hero = new Hero(A, "Alpha", Blue, default);
        hero.Skills.Set(BuiltInSkills.Scouting, level);

        Assert.Equal(Hero.DefaultScoutingRadius + extra, hero.ScoutingRadius);
    }

    [Fact]
    public void LearningScouting_RevealsTheWiderSurroundingsAtOnce()
    {
        var state = Create("A...................", "....................", "...................B");
        var hero = state.GetHero(A);
        // Six groups, every skill at its maximum except Scouting: the only offer.
        foreach (var skill in BuiltInSkills.All.Where(s => s.Group is "offense" or "defense" or "estates" or "tactics" or "machines" or "pathfinding"))
        {
            hero.Skills.Set(skill.Id, skill.Id == BuiltInSkills.Scouting ? SkillLevel.None : skill.MaxLevel);
        }

        var session = Start(state, revealAll: false);
        var far = new MapPosition(Hero.DefaultScoutingRadius + 1, 0);
        Assert.False(state.Fog.IsExplored(Blue, far));

        hero.Experience = ExperienceTable.For(2);
        session.Progression.OfferNext();
        var decision = Assert.IsType<LevelUpDecision>(state.PendingDecision);
        Assert.Equal([BuiltInSkills.Scouting], decision.Options);
        session.Execute(new ChooseSkillCommand(Blue, "pathfinding.troops")).ThrowIfRejected(); // the old id, as an old replay names it

        Assert.True(state.Fog.IsExplored(Blue, far));
        Assert.False(state.Fog.IsExplored(Blue, new MapPosition(hero.ScoutingRadius + 1, 0)));
    }

    [Theory]
    [InlineData(SkillLevel.None, new[] { 30 })]
    [InlineData(SkillLevel.Basic, new[] { 30 })]
    [InlineData(SkillLevel.Advanced, new[] { 30, 32 })]
    [InlineData(SkillLevel.Expert, new[] { 28, 30, 32, 34 })]
    public void Tactics_ChoosesTheWidthOnlyFromAdvanced(SkillLevel effective, int[] widths) =>
        Assert.Equal(widths, BattleSetup.WidthOptions(effective));

    [Fact]
    public void PrimalFuryMages_RaiseTheirStatsWithEvenOdds()
    {
        Assert.Equal([25, 25, 25, 25], StatGrowth.Odds(HeroClass.Mage, 2, TownIds.SwampBestiary));
        Assert.Equal([25, 25, 25, 25], StatGrowth.Odds(HeroClass.Mage, 12, TownIds.Stronghold));
        Assert.Equal([10, 10, 40, 40], StatGrowth.Odds(HeroClass.Mage, 2, TownIds.Haven));
        Assert.Equal([35, 45, 10, 10], StatGrowth.Odds(HeroClass.Warrior, 2, TownIds.Stronghold));
    }

    [Fact]
    public void SwampBestiaryHeroes_AreOfferedPrimalFury_AndNoSchoolOfTheirOwn()
    {
        var warrior = Hero.Create(A, Blue, default, null, HeroCatalog.Default.Get("swamp-bestiary.ssarkh"));
        var havenWarrior = Hero.Create(B, Red, default, null, HeroCatalog.Default.Get("haven.aldric"));
        var random = new GameRandom(3);
        var (rage, light) = (0, 0);
        const int levelUps = 2000;
        for (var i = 0; i < levelUps; i++)
        {
            var offers = SkillOffers.Generate(warrior, TownIds.SwampBestiary, random);
            Assert.DoesNotContain(offers, o => o.StartsWith("magic.swamp-bestiary", StringComparison.Ordinal));
            rage += offers.Any(o => o.StartsWith("rage.", StringComparison.Ordinal)) ? 1 : 0;
            light += SkillOffers.Generate(havenWarrior, TownIds.Haven, random).Any(o => o.StartsWith("magic.haven", StringComparison.Ordinal)) ? 1 : 0;
        }

        // Weight 6 for warriors too (a warrior's own school has 2): about every third level-up offers it, so warriors learn it soon (HERO-7).
        Assert.InRange(100.0 * rage / levelUps, 22, 40);
        Assert.True(rage > 2 * light);
    }
}
